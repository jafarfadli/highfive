using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>
    /// How close what the player said is to each answer on screen. Both sides are written out the way they sound
    /// (numbers in Hangul, as the voice bake reads them), reduced to their letters, and split into jamo, so a near
    /// miss costs one letter and not a whole syllable; the score is one minus the edit distance over the longer
    /// length. It measures whether the line came across, not how native it sounded.
    /// </summary>
    public static class KoreanMatch
    {
        const string Digits = "영일이삼사오육칠팔구";

        public struct Result
        {
            public int Index;      // the answer that was said, or -1
            public float Score;    // 0..1 for that answer
            public float Runner;   // the next best answer's score
        }

        /// <summary>
        /// The answer the transcript matches best, if it matches well enough and clearly better than the next one.
        /// Answers that can't be said (bracketed actions, empty) are skipped.
        /// </summary>
        public static Result Pick(string heard, IList<string> answers, float threshold = 0.55f, float margin = 0.05f)
        {
            var said = Letters(heard);
            var best = new Result { Index = -1, Score = 0f, Runner = 0f };
            if (said.Length == 0) return best;
            for (int i = 0; i < answers.Count; i++)
            {
                if (!Speakable(answers[i])) continue;
                float s = Similarity(said, Letters(answers[i]));
                if (s > best.Score)
                {
                    best.Runner = best.Score;
                    best.Score = s;
                    best.Index = i;
                }
                else if (s > best.Runner) best.Runner = s;
            }
            if (best.Score < threshold || best.Score - best.Runner < margin) best.Index = -1;
            return best;
        }

        public static bool Speakable(string korean) =>
            !string.IsNullOrWhiteSpace(korean) && !korean.TrimStart().StartsWith("[");

        /// <summary>Similarity of two lines, 0..1.</summary>
        public static float Score(string a, string b) => Similarity(Letters(a), Letters(b));

        static float Similarity(string a, string b)
        {
            if (a.Length == 0 && b.Length == 0) return 1f;
            int n = Math.Max(a.Length, b.Length);
            return 1f - Levenshtein(a, b) / (float)n;
        }

        /// <summary>The line as jamo (and Latin letters), numbers read out, everything else dropped.</summary>
        public static string Letters(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var t = ReadOut(text);
            var sb = new StringBuilder(t.Length * 3);
            foreach (var c in t)
            {
                if (c >= 0xAC00 && c <= 0xD7A3)
                {
                    int i = c - 0xAC00;
                    sb.Append((char)(0x1100 + i / 588));
                    sb.Append((char)(0x1161 + i % 588 / 28));
                    if (i % 28 != 0) sb.Append((char)(0x11A7 + i % 28));
                }
                else if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Numbers spelled out in Hangul the way the voice bake reads them (Tools/voice/bake_voices.py, speakable):
        /// phone numbers and emergency numbers digit by digit, D-4 as 디포, a car–door number 4-2 as 사다시이, and
        /// other numbers in Sino-Korean.
        /// </summary>
        static string ReadOut(string t)
        {
            // thousands separators first, so 17,000 is one number and not 17 and 000
            t = Regex.Replace(t, @"\d{1,3}(?:,\d{3})+", m => m.Value.Replace(",", ""));
            t = Regex.Replace(t, @"(?<![A-Za-z])D-(\d)(?!\d)", m => "디" + (m.Groups[1].Value == "2" ? "투" : m.Groups[1].Value == "4" ? "포" : Sino(int.Parse(m.Groups[1].Value))));
            // a car–door number (4-2) is read 사다시이
            t = Regex.Replace(t, @"(?<![\d-])(\d{1,2})-(\d)(?![\d-])", m => Sino(int.Parse(m.Groups[1].Value)) + "다시" + Sino(int.Parse(m.Groups[2].Value)));
            // any other hyphenated number, or one with a leading zero, is a phone number read digit by digit (0 as 공),
            // however Whisper groups the digits (010-1234-5678, 0110-1234-5678, 01034-5678)
            t = Regex.Replace(t, @"\d+(?:-\d+)+|(?<!\d)0\d+", m => DigitByDigit(m.Value, true));
            // emergency and service numbers digit by digit: 119 is 일일구
            t = Regex.Replace(t, @"(?<!\d)(119|112|1330)(?!\d)", m => DigitByDigit(m.Value, false));
            t = Regex.Replace(t, @"\d+", m => long.TryParse(m.Value, out var v) && v < 100000000 ? Sino((int)v) : DigitByDigit(m.Value, false));
            return t;
        }

        static string DigitByDigit(string s, bool zeroIsGong)
        {
            var sb = new StringBuilder();
            foreach (var c in s)
                if (char.IsDigit(c)) sb.Append(c == '0' && zeroIsGong ? '공' : Digits[c - '0']);
            return sb.ToString();
        }

        /// <summary>Sino-Korean reading: 5300 → 오천삼백, 17000 → 만칠천.</summary>
        static string Sino(int n)
        {
            if (n == 0) return "영";
            var sb = new StringBuilder();
            if (n >= 10000)
            {
                int head = n / 10000;
                if (head > 1) sb.Append(Sino(head));
                sb.Append('만');
                n %= 10000;
            }
            foreach (var (value, unit) in new[] { (1000, '천'), (100, '백'), (10, '십') })
            {
                int d = n / value;
                if (d > 0)
                {
                    if (d > 1) sb.Append(Digits[d]);
                    sb.Append(unit);
                }
                n %= value;
            }
            if (n > 0) sb.Append(Digits[n]);
            return sb.ToString();
        }

        static int Levenshtein(string a, string b)
        {
            if (a.Length == 0) return b.Length;
            if (b.Length == 0) return a.Length;
            var prev = new int[b.Length + 1];
            var cur = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                cur[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                }
                (prev, cur) = (cur, prev);
            }
            return prev[b.Length];
        }

        /// <summary>
        /// The syllables of the answer that the transcript did not contain (a rough "what didn't come across"),
        /// found by aligning the two lines syllable by syllable.
        /// </summary>
        public static List<string> Missed(string heard, string answer)
        {
            var a = Syllables(answer);
            var h = Syllables(heard);
            var missed = new List<string>();
            int[,] d = new int[a.Count + 1, h.Count + 1];
            for (int i = 0; i <= a.Count; i++) d[i, 0] = i;
            for (int j = 0; j <= h.Count; j++) d[0, j] = j;
            for (int i = 1; i <= a.Count; i++)
            for (int j = 1; j <= h.Count; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == h[j - 1] ? 0 : 1));
            int x = a.Count, y = h.Count;
            while (x > 0)
            {
                if (y > 0 && d[x, y] == d[x - 1, y - 1] + (a[x - 1] == h[y - 1] ? 0 : 1))
                {
                    if (a[x - 1] != h[y - 1]) missed.Insert(0, a[x - 1]);
                    x--;
                    y--;
                }
                else if (d[x, y] == d[x - 1, y] + 1)
                {
                    missed.Insert(0, a[x - 1]);
                    x--;
                }
                else y--;
            }
            return missed;
        }

        static List<string> Syllables(string text)
        {
            var list = new List<string>();
            foreach (var c in ReadOut(text ?? ""))
                if (c >= 0xAC00 && c <= 0xD7A3) list.Add(c.ToString());
            return list;
        }
    }
}
