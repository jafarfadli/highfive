using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace SeoulFirstWeek.Conversation
{
    /// <summary>
    /// Korean voice for staff lines, PA announcements and the player's chosen reply.
    /// Baked OmniVoice clips first (Resources/Voice); for anything not baked, OpenAI TTS when a key is
    /// configured, otherwise the macOS Korean voices, otherwise subtitles only.
    /// Clips are cached so repeated lines play instantly.
    /// </summary>
    public class VoiceService
    {
        readonly ITextToSpeech tts;
        readonly ITextToSpeech fallback;
        readonly Dictionary<string, AudioClip> cache = new();
        readonly Dictionary<string, Task<AudioClip>> inflight = new();

        public string Label => tts.Label;

        public VoiceService(GameConfig config)
        {
            fallback = MacSayTextToSpeech.Supported ? new MacSayTextToSpeech() : new SilentTextToSpeech();
            tts = config.ttsProvider switch
            {
                "openai" when config.HasOpenAi => new OpenAiTextToSpeech(config),
                "auto" when config.HasOpenAi => new OpenAiTextToSpeech(config),
                "none" => new SilentTextToSpeech(),
                _ => fallback,
            };
            Debug.Log($"[Voice] {tts.Label}");
        }

        public Task<AudioClip> SpeakAsync(string text, VoiceSpec voice, bool slow, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(text)) return Task.FromResult<AudioClip>(null);
            // pre-rendered lines ship with the build; without any TTS engine a slow replay uses the normal take
            var baked = BakedVoice.Load(voice.Id, text, slow);
            if (!baked && slow && tts is SilentTextToSpeech) baked = BakedVoice.Load(voice.Id, text, false);
            if (baked) return Task.FromResult(baked);
            var key = $"{voice.OpenAiVoice}|{voice.MacVoice}|{slow}|{text}";
            if (cache.TryGetValue(key, out var cached) && cached) return Task.FromResult(cached);
            if (inflight.TryGetValue(key, out var running)) return running;
            var task = Synthesize(key, text, voice, slow, ct);
            inflight[key] = task;
            return task;
        }

        public void Prefetch(string text, VoiceSpec voice, CancellationToken ct) => _ = SpeakAsync(text, voice, false, ct);

        async Task<AudioClip> Synthesize(string key, string text, VoiceSpec voice, bool slow, CancellationToken ct)
        {
            try
            {
                AudioClip clip = null;
                try
                {
                    clip = await tts.SynthesizeAsync(text, voice, slow, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception e)
                {
                    Debug.LogWarning("[Voice] " + e.Message);
                }
                if (clip == null && tts != fallback)
                {
                    try { clip = await fallback.SynthesizeAsync(text, voice, slow, ct); }
                    catch (Exception e) when (e is not OperationCanceledException) { Debug.LogWarning("[Voice fallback] " + e.Message); }
                }
                if (clip)
                {
                    WavUtility.Normalize(clip);
                    cache[key] = clip;
                }
                return clip;
            }
            finally
            {
                inflight.Remove(key);
            }
        }
    }

    /// <summary>Korean TTS via OpenAI, returned as raw 24 kHz PCM.</summary>
    public class OpenAiTextToSpeech : ITextToSpeech
    {
        readonly GameConfig config;
        public OpenAiTextToSpeech(GameConfig config) => this.config = config;
        public string Label => $"OpenAI ({config.ttsModel})";

        public async Task<AudioClip> SynthesizeAsync(string text, VoiceSpec voice, bool slow, CancellationToken ct)
        {
            var body = new JObject
            {
                ["model"] = config.ttsModel,
                ["voice"] = voice.OpenAiVoice,
                ["input"] = text,
                ["instructions"] = "Speak natural, polite Seoul Korean like real front-desk staff." + (slow ? " Speak slowly and clearly." : ""),
                ["response_format"] = "pcm",
                ["speed"] = slow ? 0.8 : 1.0,
            };
            using var req = new UnityWebRequest("https://api.openai.com/v1/audio/speech", "POST")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None))),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = Math.Max(5, (int)config.requestTimeoutSeconds),
            };
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + config.openaiApiKey);
            await req.SendAsync(ct);
            if (!req.Succeeded()) throw new Exception($"OpenAI speech {req.responseCode}: {req.error}");
            return WavUtility.FromPcm16(req.downloadHandler.data, 24000, 1, "voice");
        }
    }
}
