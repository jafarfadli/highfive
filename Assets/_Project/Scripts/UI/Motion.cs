using System;
using System.Threading.Tasks;
using SeoulFirstWeek.Core;
using UnityEngine;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// The motion grammar of the inked UI. Shape changes (drawing on, wiping in, printing) step at 12 fps like
    /// hand-drawn animation; transforms (slap, stamp, peel) snap with a hard overshoot at full frame rate.
    /// Nothing cross-fades. All of it runs on unscaled time (it plays while the game is paused) and stops
    /// quietly when its target is destroyed. Reduced motion jumps to the end states.
    /// </summary>
    public static class Motion
    {
        public const float Fps = 12f;

        public static bool Reduced => Settings.ReducedMotion;

        /// <summary>Time quantised to animation frames ("on twos" at 12 fps).</summary>
        public static float Stepped(float t) => Mathf.Floor(t * Fps) / Fps;

        /// <summary>Runs <paramref name="step"/> with progress 0..1 over <paramref name="duration"/> unscaled seconds.</summary>
        public static async Task Tween(UnityEngine.Object owner, float duration, Action<float> step, bool stepped = false, float delay = 0f)
        {
            if (delay > 0f && !Reduced)
            {
                float until = Time.unscaledTime + delay;
                while (Time.unscaledTime < until)
                {
                    await Awaitable.NextFrameAsync();
                    if (!owner) return;
                }
            }
            if (Reduced || duration <= 0f)
            {
                if (owner) step(1f);
                return;
            }
            float start = Time.unscaledTime;
            while (true)
            {
                if (!owner) return;
                float t = Time.unscaledTime - start;
                if (stepped) t = Stepped(t);
                float k = Mathf.Clamp01(t / duration);
                step(k);
                if (k >= 1f) return;
                await Awaitable.NextFrameAsync();
            }
        }

        static float BackOut(float t, float s = 2.2f)
        {
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        /// <summary>
        /// Paper arrives: from 1.18× and a few degrees off, it lands at 0.95× and settles at its resting tilt.
        /// </summary>
        public static Task Slap(RectTransform rt, float rest = -1.5f, float delay = 0f, float from = -7f, float duration = 0.26f)
        {
            if (!rt) return Task.CompletedTask;
            var group = rt.GetComponent<CanvasGroup>();
            if (!Reduced)
            {
                rt.localScale = Vector3.one * 1.18f;
                rt.localRotation = Quaternion.Euler(0, 0, rest + from);
                if (group) group.alpha = 0f;
            }
            return Tween(rt, duration, k =>
            {
                float s = k < 0.55f ? Mathf.Lerp(1.18f, 0.95f, BackOut(k / 0.55f, 0.6f)) : Mathf.Lerp(0.95f, 1f, (k - 0.55f) / 0.45f);
                float a = k < 0.55f ? Mathf.Lerp(rest + from, rest + 0.8f, k / 0.55f) : Mathf.Lerp(rest + 0.8f, rest, (k - 0.55f) / 0.45f);
                rt.localScale = Vector3.one * s;
                rt.localRotation = Quaternion.Euler(0, 0, a);
                if (group) group.alpha = Mathf.Clamp01(k / 0.25f);
            }, false, delay);
        }

        /// <summary>A stamp slams down: 2.3× to 0.88× to 1 at the given angle, then the screen shakes.</summary>
        public static async Task Stamp(RectTransform rt, float angle, RectTransform shake = null, float delay = 0f)
        {
            if (!rt) return;
            var group = rt.GetComponent<CanvasGroup>();
            if (!Reduced)
            {
                rt.localScale = Vector3.one * 2.3f;
                if (group) group.alpha = 0f;
            }
            await Tween(rt, 0.3f, k =>
            {
                float s = k < 0.3f ? Mathf.Lerp(2.3f, 0.88f, k / 0.3f) : Mathf.Lerp(0.88f, 1f, BackOut((k - 0.3f) / 0.7f, 1.4f));
                rt.localScale = Vector3.one * s;
                rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(angle + 10f, angle, Mathf.Clamp01(k / 0.3f)));
                if (group) group.alpha = Mathf.Clamp01(k / 0.12f);
            }, false, delay);
            if (shake) await Shake(shake);
        }

        /// <summary>Four hard jolts, stepped, for stamps and slams.</summary>
        public static async Task Shake(RectTransform rt, float amplitude = 7f, float duration = 0.2f)
        {
            if (!rt || Reduced) return;
            var home = rt.anchoredPosition;
            int last = -1;
            await Tween(rt, duration, k =>
            {
                int f = Mathf.Min(3, Mathf.FloorToInt(k * 4f));
                if (f == last) return;
                last = f;
                float a = amplitude * (1f - k);
                rt.anchoredPosition = home + (k >= 1f ? Vector2.zero : new Vector2((f % 2 == 0 ? -1 : 1) * a, (f % 3 == 0 ? 1 : -1) * a * 0.6f));
            });
            if (rt) rt.anchoredPosition = home;
        }

        /// <summary>Draws a ring, stroke or strip on (reveal 0 to 1), stepped at 12 fps.</summary>
        public static Task Draw(InkShape shape, float duration = 0.32f, float delay = 0f)
        {
            if (!shape) return Task.CompletedTask;
            if (!Reduced) shape.SetReveal(0f);
            return Tween(shape, duration, k => shape.SetReveal(k), true, delay);
        }

        /// <summary>Peels a piece of paper off: lifts, turns and drops out of frame, then removes it.</summary>
        public static async Task Peel(RectTransform rt, bool destroy = true)
        {
            if (!rt) return;
            var group = rt.Group();
            var home = rt.anchoredPosition;
            var rot = rt.localEulerAngles.z;
            await Tween(rt, 0.42f, k =>
            {
                float e = k * k;
                rt.anchoredPosition = home + new Vector2(30f * e, -6f * Mathf.Sin(k * Mathf.PI) * 2f - 160f * e);
                rt.localRotation = Quaternion.Euler(0, 0, rot - 4f * Mathf.Sin(k * Mathf.PI) + 16f * e);
                group.alpha = 1f - Mathf.Clamp01((k - 0.35f) / 0.65f);
            });
            if (rt && destroy) UnityEngine.Object.Destroy(rt.gameObject);
        }

        /// <summary>A ticket feeding out of a slot: slides down from above in six steps.</summary>
        public static Task Print(RectTransform rt, float duration = 0.5f)
        {
            if (!rt) return Task.CompletedTask;
            var home = rt.anchoredPosition;
            float h = rt.rect.height;
            if (!Reduced) rt.anchoredPosition = home + new Vector2(0, h);
            return Tween(rt, duration, k => rt.anchoredPosition = home + new Vector2(0, h * (1f - k)), true);
        }

        /// <summary>A quick overshoot pop for numbers and marks.</summary>
        public static Task Pop(RectTransform rt, float peak = 1.25f, float duration = 0.22f)
        {
            if (!rt) return Task.CompletedTask;
            return Tween(rt, duration, k =>
            {
                float s = k < 0.4f ? Mathf.Lerp(1f, peak, k / 0.4f) : Mathf.Lerp(peak, 1f, BackOut((k - 0.4f) / 0.6f, 1.2f));
                rt.localScale = Vector3.one * s;
            });
        }

        /// <summary>Slides a rect in from an offset with a hard overshoot (banners, strips).</summary>
        public static Task SlideIn(RectTransform rt, Vector2 offset, float duration = 0.24f, float delay = 0f)
        {
            if (!rt) return Task.CompletedTask;
            var home = rt.anchoredPosition;
            if (!Reduced) rt.anchoredPosition = home + offset;
            return Tween(rt, duration, k => rt.anchoredPosition = home + offset * (1f - BackOut(k, 1.6f)), false, delay);
        }
    }
}
