using SeoulFirstWeek.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.World
{
    /// <summary>Keeps a world label turned toward the main camera (yaw only).</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam) return;
            var d = transform.position - cam.transform.position;
            d.y = 0;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
