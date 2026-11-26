using System;
using System.Globalization;
using System.IO;
using SeoulFirstWeek.Core;
using UnityEngine;

namespace SeoulFirstWeek.Telemetry
{
    /// <summary>Appends one CSV row per finished scenario, for the user test at the venue.</summary>
    public static class TelemetryLogger
    {
        const string Header =
            "timestamp,session_id,hint_language,level,outcome,progress_done,progress_total,mistakes,seconds,procedure_verified,difficulty";

        public static string Folder => Path.Combine(Application.persistentDataPath, "telemetry");
        public static string FilePath => Path.Combine(Folder, "levels.csv");

        public static void Log(ScenarioResult r)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                bool fresh = !File.Exists(FilePath);
                // a log started before the difficulty column: its header gains the column (old rows leave it empty)
                if (!fresh)
                {
                    var lines = File.ReadAllLines(FilePath);
                    if (lines.Length > 0 && lines[0] != Header && Header.StartsWith(lines[0]))
                    {
                        lines[0] = Header;
                        File.WriteAllLines(FilePath, lines);
                    }
                }
                using var w = new StreamWriter(FilePath, true);
                if (fresh) w.WriteLine(Header);
                var s = GameSession.I;
                var inv = CultureInfo.InvariantCulture;
                w.WriteLine(string.Join(",",
                    DateTime.Now.ToString("s", inv),
                    s.SessionId,
                    s.LangCode,
                    r.ScenarioId,
                    r.Outcome.ToString().ToLowerInvariant(),
                    r.StepsCompleted,
                    r.StepsTotal,
                    r.Mistakes,
                    r.Seconds.ToString("0", inv),
                    r.Verified ? "true" : "false",
                    Settings.Difficulty.ToString().ToLowerInvariant()));
                Debug.Log($"[Telemetry] logged {r.ScenarioId} to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Telemetry] " + e.Message);
            }
        }
    }
}
