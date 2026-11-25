using SeoulFirstWeek.Core;
using UnityEngine;

namespace SeoulFirstWeek.Audio
{
    /// <summary>
    /// Looping street ambience (Resources/Audio/crowd). Interiors have none: the background music
    /// carries the mood there. Volume follows <see cref="Settings.Sfx"/>.
    /// </summary>
    public class AmbientAudio : MonoBehaviour
    {
        const string Clip = "Audio/crowd";

        public bool street = true;
        [Range(0, 1)] public float volume = 0.35f;
        LoopPlayer player;

        void Start()
        {
            if (!street) return;
            player = new LoopPlayer(gameObject, 1.5f, false);
            player.Play(Resources.Load<AudioClip>(Clip));
        }

        void Update()
        {
            if (player == null) return;
            player.Volume = volume * Settings.Sfx;
            player.Tick(Time.unscaledDeltaTime);
        }

        public void SetVolume(float v) => volume = v;
    }
}
