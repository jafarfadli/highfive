using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// An LED strip that scrolls one line of text right to left (exchange rates, notices). The text sits
    /// in a world-space canvas clipped by a RectMask2D on its parent.
    /// </summary>
    public class Ticker : MonoBehaviour
    {
        public Text text;
        [Tooltip("Scroll speed in canvas pixels per second (100 pixels = 1 m).")]
        public float speed = 60f;

        RectTransform rt, viewport;
        float width;

        void Start()
        {
            if (!text) return;
            rt = text.rectTransform;
            viewport = (RectTransform)rt.parent;
            width = text.preferredWidth;
            rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(viewport.rect.width, rt.anchoredPosition.y);
        }

        void Update()
        {
            if (!rt) return;
            var p = rt.anchoredPosition;
            p.x -= speed * Time.deltaTime;
            if (p.x < -width) p.x = viewport.rect.width;
            rt.anchoredPosition = p;
        }
    }
}
