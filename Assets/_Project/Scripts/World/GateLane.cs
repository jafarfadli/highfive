using System.Collections;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// One lane of a ticket gate: two flaps across it that fold back into the cabinets when a card is tapped and
    /// close again behind the person who walked through. The player's tap holds the lane open for good.
    /// </summary>
    public class GateLane : MonoBehaviour
    {
        [Tooltip("The flaps across the lane, left and right; they slide sideways into their cabinets.")]
        public Transform flapLeft, flapRight;
        [Tooltip("The reader on the cabinet, lit a moment brighter when a card is tapped.")]
        public Renderer reader;
        public float travel = 0.3f;
        public float openSeconds = 0.22f;

        Vector3 leftClosed, rightClosed;
        float open, target, holdUntil;
        bool held;
        Coroutine run;

        public Vector3 Position => transform.position;

        void Awake()
        {
            if (flapLeft) leftClosed = flapLeft.localPosition;
            if (flapRight) rightClosed = flapRight.localPosition;
        }

        /// <summary>A tap: the flaps open for <paramref name="seconds"/> (someone walking through), or for good.</summary>
        public void Open(float seconds = 2.2f, bool hold = false)
        {
            held |= hold;
            holdUntil = Mathf.Max(holdUntil, Time.time + seconds);
            target = 1f;
            if (reader) StartCoroutine(Blink());
            if (run == null) run = StartCoroutine(Run());
        }

        IEnumerator Blink()
        {
            var block = new MaterialPropertyBlock();
            reader.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", new Color(0.5f, 3.2f, 1.2f));
            reader.SetPropertyBlock(block);
            yield return new WaitForSeconds(0.35f);
            if (reader) reader.SetPropertyBlock(null);
        }

        IEnumerator Run()
        {
            while (true)
            {
                if (!held && Time.time > holdUntil) target = 0f;
                open = Mathf.MoveTowards(open, target, Time.deltaTime / openSeconds);
                float k = Mathf.SmoothStep(0, 1, open);
                if (flapLeft) flapLeft.localPosition = leftClosed - Vector3.right * travel * k;
                if (flapRight) flapRight.localPosition = rightClosed + Vector3.right * travel * k;
                if (open <= 0f && target <= 0f) break;
                yield return null;
            }
            run = null;
        }
    }
}
