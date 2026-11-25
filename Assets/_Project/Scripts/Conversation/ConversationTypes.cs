using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SeoulFirstWeek.Conversation
{
    public class VoiceSpec
    {
        public string Id;                     // baked voice (staff id, "player" or "pa"); see BakedVoice
        public string OpenAiVoice = "coral";
        public string MacVoice = "Yuna";
        public int MacRate = 185;
    }

    public interface ITextToSpeech
    {
        string Label { get; }
        Task<AudioClip> SynthesizeAsync(string text, VoiceSpec voice, bool slow, CancellationToken ct);
    }
}
