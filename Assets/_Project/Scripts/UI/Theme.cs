using UnityEngine;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// What a colour is for. Every UI colour comes from a role: ink (lines, text, hard shadows), ink soft
    /// (secondary marks), paper (cards, keycaps), paper deep (tags, chips), brush (the one active thing),
    /// brush deep (pressed, the block under extruded lettering), glow (highlights, lesson notes, stars),
    /// night (answer strips, dark labels), balloon (speech balloons), on brush (text on a lit strip) and
    /// ground (plain backdrops).
    /// </summary>
    public enum Role { Ink, InkSoft, Paper, PaperDeep, Brush, BrushDeep, Glow, Night, Balloon, OnBrush, Ground }

    /// <summary>
    /// The one palette, Signboard, taken from the 3D street so the UI and the world read as one game: the teal
    /// of Myeongdong's shop fascias for the active thing, the late-afternoon sun for highlights, the street's
    /// blue-violet shade for ink and dark plates, and warm paper and pavement beige for cards. Text on a
    /// brush fill is light (on brush); text on a glow fill stays ink.
    /// </summary>
    public static class Theme
    {
        static readonly Color[] Colors =
        {
            Hex("#1A1D26"), // ink
            Hex("#4B5263"), // ink soft
            Hex("#F2EADB"), // paper
            Hex("#D9C6A5"), // paper deep
            Hex("#1B6F68"), // brush
            Hex("#0F4A45"), // brush deep
            Hex("#E9B445"), // glow
            Hex("#1E2636"), // night
            Hex("#FBF6EC"), // balloon
            Hex("#F7F1E6"), // on brush
            Hex("#D9C6A5"), // ground
        };

        public static Color Get(Role role) => Colors[(int)role];

        public static Color Get(Role role, float alpha)
        {
            var c = Get(role);
            c.a = alpha;
            return c;
        }

        /// <summary>Hex for rich text, e.g. &lt;color=#…&gt;.</summary>
        public static string Html(Role role) => "#" + ColorUtility.ToHtmlStringRGB(Get(role));

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }

    /// <summary>
    /// Legacy names kept for code outside the UI folder: they now resolve to theme roles
    /// (a "panel" is paper with ink text, a "card" is balloon paper, warnings are glow notes).
    /// </summary>
    public static class Palette
    {
        public static Color Panel => Theme.Get(Role.Paper);
        public static Color PanelSoft => Theme.Get(Role.PaperDeep);
        public static Color Card => Theme.Get(Role.Balloon);
        public static Color Ink => Theme.Get(Role.Ink);
        public static Color Accent => Theme.Get(Role.Brush);
        public static Color Blue => Theme.Get(Role.BrushDeep);
        public static Color Good => Theme.Get(Role.Brush);
        public static Color Warn => Theme.Get(Role.Glow);
        public static Color Text => Theme.Get(Role.Ink);
        public static Color TextDim => Theme.Get(Role.InkSoft);
        public static Color Scrim => Theme.Get(Role.Ink, 0.55f);

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(this Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
