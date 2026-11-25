using UnityEngine;

namespace SeoulFirstWeek.World
{
    /// <summary>Slow back-and-forth camera move for menu backdrops.</summary>
    public class CameraDrift : MonoBehaviour
    {
        public Vector3 from, to;
        public Vector3 lookFrom, lookTo;
        public float period = 50f;

        void Update()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 2f * Mathf.PI / period);
            var pos = Vector3.Lerp(from, to, k);
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(Vector3.Lerp(lookFrom, lookTo, k) - pos));
        }
    }
}
