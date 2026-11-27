using System.Collections.Generic;
using System.IO;
using SeoulFirstWeek.Audio;
using UnityEditor;
using UnityEngine;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Records play-mode footage for the pitch video. The editor is stepped one frame at a time at a fixed
    /// frame rate (<see cref="Time.captureFramerate"/>), and every recorded frame is written as a JPEG (the 3D
    /// view plus the screen-space UI, post-processing on). Audio can't be captured live while the editor steps,
    /// so the sound track is rebuilt instead: <see cref="AudioTap"/> reports every voice, announcement and
    /// sound effect the game starts, and <see cref="End"/> mixes those clips at the frames they started into
    /// audio.wav. Game logic waits on game time, so picture and sound stay in sync however slow a frame is.
    /// ffmpeg turns the folder (f_00000.jpg … + audio.wav) into a clip. Play mode only; pause the editor first.
    /// </summary>
    public static class ClipRecorder
    {
        public const int Fps = 30;
        const int Rate = 48000;
        public static int Width = 1920, Height = 1080;

        /// <summary>When set, the overlay UI is laid over the finished frame the way the game draws it: after
        /// post-processing, so its text gets no anti-aliasing blur, grading or vignette. Costs two more renders a frame.</summary>
        public static bool UiAfterPost;

        struct Shot
        {
            public int frame, stopFrame, rate, channels;
            public float volume;
            public float[] data;
            public AudioClip clip;
        }

        static string folder;
        static int frame;
        static bool recording;
        static readonly List<Shot> shots = new();
        static readonly Dictionary<AudioClip, (float[] data, int rate, int channels)> cache = new();
        static RenderTexture rt;
        static Texture2D tex;

        public static int FrameCount => frame;

        /// <summary>Starts a clip in <paramref name="path"/> (old frames there are deleted).</summary>
        public static void Begin(string path)
        {
            folder = path;
            Directory.CreateDirectory(folder);
            foreach (var f in Directory.GetFiles(folder, "f_*.jpg")) File.Delete(f);
            frame = 0;
            shots.Clear();
            cache.Clear();
            Time.captureFramerate = Fps;
            rt = new RenderTexture(Width, Height, 24);
            tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            emptyBlack = emptyWhite = null;
            AudioTap.Played -= OnPlayed;
            AudioTap.Stopped -= OnStopped;
            AudioTap.Played += OnPlayed;
            AudioTap.Stopped += OnStopped;
            recording = true;
        }

        /// <summary>Stops taking frames (a cut); the game keeps running at the fixed frame rate.</summary>
        public static void Pause() => recording = false;

        public static void Resume() => recording = true;

        /// <summary>Steps <paramref name="frames"/> frames, recording each one unless paused.</summary>
        public static void Run(int frames)
        {
            for (int i = 0; i < frames; i++) Step();
        }

        /// <summary>Steps until <paramref name="done"/> holds (or <paramref name="max"/> frames).</summary>
        public static bool RunUntil(System.Func<bool> done, int max = 900)
        {
            for (int i = 0; i < max; i++)
            {
                if (done()) return true;
                Step();
            }
            return done();
        }

        public static void Step()
        {
            EditorApplication.Step();
            System.Threading.Thread.Sleep(2);
            if (recording) Capture();
        }

        /// <summary>Ends the clip: mixes and writes audio.wav next to the frames, returns the frame count.</summary>
        public static int End()
        {
            recording = false;
            AudioTap.Played -= OnPlayed;
            AudioTap.Stopped -= OnStopped;
            Time.captureFramerate = 0;
            WriteWav(Path.Combine(folder, "audio.wav"), Mix());
            File.WriteAllLines(Path.Combine(folder, "sounds.txt"),
                shots.ConvertAll(s => $"{s.frame}\t{(s.stopFrame >= 0 ? s.stopFrame.ToString() : "-")}\t{s.volume:F2}\t{(s.clip ? s.clip.name : "?")}"));
            if (rt) Object.DestroyImmediate(rt);
            if (tex) Object.DestroyImmediate(tex);
            return frame;
        }

        static void OnPlayed(AudioClip clip, float volume)
        {
            if (!recording || !clip) return;
            if (!cache.TryGetValue(clip, out var c))
            {
                var data = new float[clip.samples * clip.channels];
                c = clip.GetData(data, 0) ? (data, clip.frequency, clip.channels) : (null, 0, 0);
                cache[clip] = c;
            }
            if (c.data == null) return;
            shots.Add(new Shot { frame = frame, stopFrame = -1, rate = c.rate, channels = c.channels, volume = volume, data = c.data, clip = clip });
        }

        static void OnStopped(AudioClip clip)
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (shots[i].clip != clip || shots[i].stopFrame >= 0) continue;
                var s = shots[i];
                s.stopFrame = frame;
                shots[i] = s;
                return;
            }
        }

        static float[] Mix()
        {
            long total = (long)frame * Rate / Fps;
            var mix = new float[total * 2];
            foreach (var s in shots)
            {
                long start = (long)s.frame * Rate / Fps;
                long stop = s.stopFrame >= 0 ? (long)s.stopFrame * Rate / Fps : total;
                int length = s.data.Length / s.channels;
                double ratio = (double)s.rate / Rate;
                for (long j = 0; start + j < stop; j++)
                {
                    double at = j * ratio;
                    int i0 = (int)at;
                    if (i0 >= length - 1) break;
                    float t = (float)(at - i0);
                    for (int ch = 0; ch < 2; ch++)
                    {
                        int sc = Mathf.Min(ch, s.channels - 1);
                        float v = s.data[i0 * s.channels + sc] * (1 - t) + s.data[(i0 + 1) * s.channels + sc] * t;
                        mix[(start + j) * 2 + ch] += v * s.volume;
                    }
                }
            }
            return mix;
        }

        // The UI is recorded by turning the overlay canvases into camera canvases for one render, so it gets the
        // same post-processing as the world. Depth of field (the menu look) would then blur the UI wherever the
        // scene behind it is far away, because the UI writes no depth. When depth of field is on, the frame is
        // put together instead: the world is rendered with depth of field, the world with the UI without it, and
        // the UI's coverage is measured by rendering it alone over black and over white (each next to the same
        // empty render, so vignette and grading cancel out). Covered pixels come from the sharp render.
        static void Capture()
        {
            var cam = Camera.main;
            if (!cam) return;
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            var dofs = ActiveDepthOfField();
            Color32[] frameColors;
            if (Core.VRMode.Simulated)
            {
                // a headset's panels are in the world and drawn after post-processing by the renderer's VR UI
                // pass, so one plain render is the eye's view
                frameColors = null;
                Render(cam, true);
            }
            else if (UiAfterPost) frameColors = UiOverFrame(cam);
            else if (dofs.Count == 0)
            {
                frameColors = null;
                Render(cam, true);
            }
            else
            {
                var blurred = Render(cam, false);
                foreach (var d in dofs) d.active = false;
                try
                {
                    var sharpUi = Render(cam, true);
                    var onBlack = RenderAlone(cam, true, Color.black);
                    var onWhite = RenderAlone(cam, true, Color.white);
                    var black = RenderAlone(cam, false, Color.black);
                    var white = RenderAlone(cam, false, Color.white);
                    for (int i = 0; i < blurred.Length; i++)
                    {
                        int through = onWhite[i].r - onBlack[i].r + onWhite[i].g - onBlack[i].g + onWhite[i].b - onBlack[i].b;
                        int open = white[i].r - black[i].r + white[i].g - black[i].g + white[i].b - black[i].b;
                        float covered = open > 30 ? Mathf.Clamp01(1f - through / (float)open) : 0f;
                        blurred[i] = Color32.Lerp(blurred[i], sharpUi[i], covered);
                    }
                    frameColors = blurred;
                }
                finally
                {
                    foreach (var d in dofs) d.active = true;
                }
            }
            if (frameColors != null)
            {
                tex.SetPixels32(frameColors);
                tex.Apply();
            }
            File.WriteAllBytes(Path.Combine(folder, $"f_{frame:D5}.jpg"), tex.EncodeToJPG(96));
            frame++;
            cam.targetTexture = prev;
            RenderTexture.active = null;
        }

        /// <summary>Renders the camera into <see cref="rt"/>, with or without the screen-space UI, and reads it into <see cref="tex"/>.</summary>
        static Color32[] Render(Camera cam, bool withUi)
        {
            var changed = new List<Canvas>();
            if (withUi)
                foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                {
                    if (c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isRootCanvas) continue;
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.02f + (1000 - c.sortingOrder) * 0.0004f;
                    changed.Add(c);
                }
            // The canvas scalers worked out their scale for the screen (in batch mode a small virtual one), so the
            // text would be drawn at that size and stretched to the frame: let them scale for the frame instead.
            Rescale(changed);
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            foreach (var c in changed) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Rescale(changed);
            return tex.GetPixels32();
        }

        static readonly System.Reflection.MethodInfo ScalerHandle =
            typeof(UnityEngine.UI.CanvasScaler).GetMethod("Handle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        static void Rescale(List<Canvas> canvases)
        {
            foreach (var c in canvases)
            {
                var scaler = c ? c.GetComponent<UnityEngine.UI.CanvasScaler>() : null;
                if (scaler && scaler.isActiveAndEnabled) ScalerHandle?.Invoke(scaler, null);
            }
        }

        /// <summary>Renders only the screen-space UI (or nothing) over a solid background, post-processing on. The
        /// canvases share a layer with the world (Default), so they are moved to an empty layer for this render.</summary>
        static Color32[] RenderAlone(Camera cam, bool withUi, Color background)
        {
            const int SpareLayer = 31;
            var moved = new List<(Canvas canvas, int layer)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    moved.Add((c, c.gameObject.layer));
                    c.gameObject.layer = SpareLayer;
                }
            var (cull, clear, color) = (cam.cullingMask, cam.clearFlags, cam.backgroundColor);
            cam.cullingMask = 1 << SpareLayer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            try { return Render(cam, withUi); }
            finally
            {
                (cam.cullingMask, cam.clearFlags, cam.backgroundColor) = (cull, clear, color);
                foreach (var (c, layer) in moved) c.gameObject.layer = layer;
            }
        }

        static Color32[] emptyBlack, emptyWhite;
        static float[] toLinear;
        static byte[] toGamma;

        /// <summary>The world as the camera finishes it (post-processing and depth of field included) with the overlay
        /// UI blended over it in linear light, as the game composites its overlay canvases. The UI's colour and coverage
        /// come from rendering it alone without post-processing, over black and over white, each measured against the
        /// same render with no UI (kept for the clip), so whatever the ink pass leaves on an empty frame cancels out.</summary>
        static Color32[] UiOverFrame(Camera cam)
        {
            var world = Render(cam, false);
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = data && data.renderPostProcessing;
            var aa = data ? data.antialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
            if (data)
            {
                data.renderPostProcessing = false;
                data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            }
            Color32[] onBlack, onWhite;
            try
            {
                onBlack = RenderAlone(cam, true, Color.black);
                onWhite = RenderAlone(cam, true, Color.white);
                emptyBlack ??= RenderAlone(cam, false, Color.black);
                emptyWhite ??= RenderAlone(cam, false, Color.white);
            }
            finally
            {
                if (data)
                {
                    data.renderPostProcessing = post;
                    data.antialiasing = aa;
                }
            }
            if (toLinear == null)
            {
                toLinear = new float[256];
                for (int i = 0; i < 256; i++) toLinear[i] = Mathf.GammaToLinearSpace(i / 255f);
                toGamma = new byte[4096];
                for (int i = 0; i < 4096; i++) toGamma[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(i / 4095f) * 255f), 0, 255);
            }
            var lin = toLinear;
            for (int i = 0; i < world.Length; i++)
            {
                Color32 b1 = onBlack[i], w1 = onWhite[i], b0 = emptyBlack[i], w0 = emptyWhite[i];
                float open = lin[w0.r] - lin[b0.r] + lin[w0.g] - lin[b0.g] + lin[w0.b] - lin[b0.b];
                float through = lin[w1.r] - lin[b1.r] + lin[w1.g] - lin[b1.g] + lin[w1.b] - lin[b1.b];
                // how much of the world shows through the UI here (1 where there is no UI)
                float t = open > 0.3f ? Mathf.Clamp01(through / open) : 1f;
                if (t > 0.999f) continue;
                var w = world[i];
                world[i] = new Color32(
                    Gamma(lin[b1.r] + t * (lin[w.r] - lin[b0.r])),
                    Gamma(lin[b1.g] + t * (lin[w.g] - lin[b0.g])),
                    Gamma(lin[b1.b] + t * (lin[w.b] - lin[b0.b])), 255);
            }
            return world;
        }

        static byte Gamma(float linear) => toGamma[Mathf.Clamp((int)(linear * 4095f + 0.5f), 0, 4095)];

        static List<UnityEngine.Rendering.Universal.DepthOfField> ActiveDepthOfField()
        {
            var list = new List<UnityEngine.Rendering.Universal.DepthOfField>();
            foreach (var v in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Exclude))
            {
                if (!v.enabled || v.weight <= 0f || !v.HasInstantiatedProfile() && v.sharedProfile == null) continue;
                if (v.profile.TryGet(out UnityEngine.Rendering.Universal.DepthOfField d) && d.active
                    && d.mode.value != UnityEngine.Rendering.Universal.DepthOfFieldMode.Off)
                    list.Add(d);
            }
            return list;
        }

        static void WriteWav(string path, float[] samples)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)2);
            w.Write(Rate);
            w.Write(Rate * 4);
            w.Write((short)4);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
