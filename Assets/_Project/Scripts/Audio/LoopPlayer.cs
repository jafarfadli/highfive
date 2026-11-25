using UnityEngine;

namespace SeoulFirstWeek.Audio
{
    /// <summary>
    /// Loops a clip on two alternating sources with a crossfade at the seam, which hides MP3 padding
    /// and song endings, and crossfades to another clip when asked. Owned by a MonoBehaviour that calls
    /// <see cref="Tick"/> every frame.
    /// </summary>
    public class LoopPlayer
    {
        readonly AudioSource[] sources = new AudioSource[2];
        readonly float fade;
        AudioClip clip;
        int active;
        float mix = 1f;

        public float Volume { get; set; } = 1f;
        public AudioClip Clip => clip;

        public LoopPlayer(GameObject host, float fadeSeconds, bool playDuringPause)
        {
            fade = Mathf.Max(0.1f, fadeSeconds);
            for (int i = 0; i < 2; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.loop = false;
                s.spatialBlend = 0;
                s.volume = 0;
                s.ignoreListenerPause = playDuringPause;
                sources[i] = s;
            }
        }

        /// <summary>Crossfades to <paramref name="next"/> (null fades to silence). The same clip keeps playing.</summary>
        public void Play(AudioClip next)
        {
            if (next == clip) return;
            clip = next;
            Start();
        }

        void Start()
        {
            active = 1 - active;
            var s = sources[active];
            s.Stop();
            s.clip = clip;
            if (clip)
            {
                s.time = 0;
                s.Play();
            }
            mix = 0;
        }

        public void Tick(float dt)
        {
            var current = sources[active];
            // start the next pass just before this one ends
            if (clip && mix >= 1f && current.isPlaying && clip.length > fade * 2f && clip.length - current.time <= fade)
                Start();
            mix = Mathf.MoveTowards(mix, 1f, dt / fade);
            // equal-power curve so the seam doesn't dip
            sources[active].volume = Volume * Mathf.Sin(mix * Mathf.PI * 0.5f);
            var other = sources[1 - active];
            other.volume = Volume * Mathf.Cos(mix * Mathf.PI * 0.5f);
            if (mix >= 1f && other.isPlaying) other.Stop();
            // a clip that ended without a seam (too short, or the app lost focus): restart it
            if (clip && mix >= 1f && !current.isPlaying && current.clip == clip && !AudioListener.pause) current.Play();
        }
    }
}
