using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Procedure;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// A subway train pulling into the platform: it runs in from the tunnel along its track, eases to a stop with
    /// its doors in line with the platform screen doors, then both sets of doors slide open together.
    /// </summary>
    public class StationTrain : SetPiece
    {
        [Tooltip("The whole train (cars, doors, lights); moved along its local X.")]
        public Transform body;
        [Tooltip("Where the train stops, in its parent's space.")]
        public Vector3 stopPosition;
        [Tooltip("How far up the tunnel it starts (metres along -X of the parent).")]
        public float runIn = 70f;
        public float arriveSeconds = 7f;
        [Tooltip("Door leaves that slide along X when they open: the train's and the platform screen doors.")]
        public Transform[] leavesLeft, leavesRight;
        public float doorTravel = 0.62f;
        public float doorSeconds = 1.6f;

        Vector3[] closedLeft, closedRight;
        bool arrived;

        /// <summary>The train has stopped and its doors are open (people get off and on).</summary>
        public bool DoorsOpen => arrived;

        void Awake()
        {
            closedLeft = Positions(leavesLeft);
            closedRight = Positions(leavesRight);
            if (body) body.localPosition = stopPosition - new Vector3(runIn, 0, 0);
            if (body) body.gameObject.SetActive(false);
        }

        static Vector3[] Positions(Transform[] t)
        {
            var p = new Vector3[t?.Length ?? 0];
            for (int i = 0; i < p.Length; i++) p[i] = t[i] ? t[i].localPosition : Vector3.zero;
            return p;
        }

        public override async Task PlayAsync(CancellationToken ct)
        {
            if (arrived || !body) return;
            body.gameObject.SetActive(true);
            Sfx.Play(ProceduralAudio.Rumble, 0.8f);
            var from = stopPosition - new Vector3(runIn, 0, 0);
            float t = 0;
            while (t < arriveSeconds)
            {
                t += Time.deltaTime;
                // decelerating all the way in, as a train braking into a station
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / arriveSeconds), 2.4f);
                body.localPosition = Vector3.Lerp(from, stopPosition, k);
                await Awaitable.NextFrameAsync(ct);
            }
            body.localPosition = stopPosition;
            await Awaitable.WaitForSecondsAsync(0.6f, ct);
            Sfx.Play(ProceduralAudio.Chime, 0.35f);
            t = 0;
            while (t < doorSeconds)
            {
                t += Time.deltaTime;
                SetDoors(Mathf.SmoothStep(0, 1, t / doorSeconds));
                await Awaitable.NextFrameAsync(ct);
            }
            SetDoors(1);
            arrived = true;
        }

        public override void Finish()
        {
            if (!body) return;
            body.gameObject.SetActive(true);
            body.localPosition = stopPosition;
            SetDoors(1);
            arrived = true;
        }

        void SetDoors(float open)
        {
            for (int i = 0; i < closedLeft.Length; i++)
                if (leavesLeft[i]) leavesLeft[i].localPosition = closedLeft[i] - new Vector3(doorTravel * open, 0, 0);
            for (int i = 0; i < closedRight.Length; i++)
                if (leavesRight[i]) leavesRight[i].localPosition = closedRight[i] + new Vector3(doorTravel * open, 0, 0);
        }
    }
}
