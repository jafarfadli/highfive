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
    /// The one palette of Ready Set Seoul: Persimmon, a retro print kept inside one warm band of the colour
    /// wheel (brick to marigold) with near-black ink and cream. It colours the UI only; the 3D world keeps
    /// its own colours.
    /// </summary>
    public static class Theme
    {
        static readonly Color[] Colors =
        {
            Hex("#1E0F0A"), // ink
            Hex("#4A2618"), // ink soft
            Hex("#FCEBCB"), // paper
            Hex("#F2C47E"), // paper deep
            Hex("#EE5A24"), // brush
            Hex("#B0381A"), // brush deep
            Hex("#FFC23A"), // glow
            Hex("#2B1610"), // night
            Hex("#FFF8EA"), // balloon
            Hex("#1E0F0A"), // on brush
            Hex("#F2C47E"), // ground
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
