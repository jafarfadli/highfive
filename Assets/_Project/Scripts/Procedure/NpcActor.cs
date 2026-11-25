using System;
using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.UI;
using SeoulFirstWeek.World;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>A staff member: body, voice, and a thinking bubble.</summary>
    public class NpcActor : MonoBehaviour
    {
        public string staffId;
        public HumanRig rig;
        public Transform bubble;
        public Transform face;
        AudioSource source;
        bool stopped;

        public VoiceSpec Voice { get; set; } = new();

        /// <summary>How many staff members are speaking right now (the ambience ducks under them).</summary>
        public static int Speaking { get; private set; }

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // mostly 2D so the line stays loud and clear at the counter; a little 3D keeps it coming from the staff member
            source.spatialBlend = 0.12f;
            source.minDistance = 6f;
            if (bubble) bubble.gameObject.SetActive(false);
        }

        public async Task PlayAsync(AudioClip clip, string text, CancellationToken ct)
        {
            rig.talking = true;
            Speaking++;
            try
            {
                if (clip)
                {
                    source.clip = clip;
                    source.Play();
                    AudioTap.Note(clip, source.volume);
                    stopped = false;
                    // wait the line out in game time, so a recording stepped frame by frame keeps the pacing
                    float end = Time.time + clip.length;
                    while (source && !stopped && Time.time < end) await Awaitable.NextFrameAsync(ct);
                    await Awaitable.WaitForSecondsAsync(0.2f, ct);
                }
                else
                {
                    await Awaitable.WaitForSecondsAsync(Mathf.Clamp(text.Length * 0.09f, 1.2f, 6f), ct);
                }
            }
            finally
            {
                Speaking = Mathf.Max(0, Speaking - 1);
                if (rig) rig.talking = false;
            }
        }

        public void StopVoice()
        {
            stopped = true;
            if (!source) return;
            if (source.isPlaying) AudioTap.NoteStop(source.clip);
            source.Stop();
        }

        public void SetThinking(bool on)
        {
            rig.thinking = on;
            if (bubble) bubble.gameObject.SetActive(on);
        }
    }
}
