using System;
using UnityEngine;

namespace SeoulFirstWeek.Audio
{
    /// <summary>
    /// Reports every voice, announcement and sound effect the game starts or stops, so an editor tool can
    /// rebuild the sound track of recorded footage (Editor/ClipRecorder). Costs nothing when nobody listens.
    /// </summary>
    public static class AudioTap
    {
        public static event Action<AudioClip, float> Played;
        public static event Action<AudioClip> Stopped;

        public static void Note(AudioClip clip, float volume)
        {
            if (clip) Played?.Invoke(clip, volume);
        }

        public static void NoteStop(AudioClip clip)
        {
            if (clip) Stopped?.Invoke(clip);
        }
    }
}
