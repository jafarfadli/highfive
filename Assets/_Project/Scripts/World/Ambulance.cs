using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Procedure;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// The ambulance arriving: it drives up the street with its lights flashing and stops by the patient, and the
    /// paramedics step out beside him. Nothing medical happens on screen; the scene ends as help arrives.
    /// </summary>
    public class Ambulance : SetPiece
    {
        public Transform body;
        [Tooltip("Where it starts and where it stops (world space).")]
        public Vector3 from, to;
        public float driveSeconds = 6f;
        [Tooltip("The light bar's two halves, flashed in turn.")]
        public Renderer lightA, lightB;
        [Tooltip("The paramedics, shown once it has stopped.")]
        public GameObject[] crew;

        bool flashing, arrived;
        float flashT;

        void Awake()
        {
            if (body) body.gameObject.SetActive(false);
            foreach (var c in crew ?? new GameObject[0]) if (c) c.SetActive(false);
        }

        void Update()
        {
            if (!flashing) return;
            flashT += Time.deltaTime;
            bool a = Mathf.Repeat(flashT, 0.5f) < 0.25f;
            if (lightA) lightA.enabled = a;
            if (lightB) lightB.enabled = !a;
        }

        public override async Task PlayAsync(CancellationToken ct)
        {
            if (arrived || !body) return;
            body.gameObject.SetActive(true);
            flashing = true;
            Sfx.Play(ProceduralAudio.Siren, 0.6f);
            var dir = to - from;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f) body.rotation = Quaternion.LookRotation(dir);
            float t = 0;
            while (t < driveSeconds)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / driveSeconds), 2f);
                body.position = Vector3.Lerp(from, to, k);
                await Awaitable.NextFrameAsync(ct);
            }
            body.position = to;
            await Awaitable.WaitForSecondsAsync(0.8f, ct);
            foreach (var c in crew ?? new GameObject[0]) if (c) c.SetActive(true);
            await Awaitable.WaitForSecondsAsync(1.6f, ct);
            arrived = true;
        }

        public override void Finish()
        {
            if (!body) return;
            body.gameObject.SetActive(true);
            body.position = to;
            flashing = true;
            foreach (var c in crew ?? new GameObject[0]) if (c) c.SetActive(true);
            arrived = true;
        }
    }
}
