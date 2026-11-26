using System;
using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SeoulFirstWeek.UI
{
    /// <summary>
    /// "Know before you go": the few facts a student wants before going somewhere (hours, what to bring, the cost, a
    /// tip), from the level's `before` list, as glow notes with their kind lettered above the sentence. Shown on the
    /// story card and in the Logbook's Before you go tab.
    /// </summary>
    public static class KnowBefore
    {
        /// <summary>The facts that fit the situation (flags tested with <paramref name="has"/>).</summary>
        public static List<FactDef> For(LevelDef level, Func<string, bool> has) =>
            level?.before == null
                ? new List<FactDef>()
                : level.before.Where(f => f.text != null && (string.IsNullOrEmpty(f.needs) || has(f.needs)) &&
                                          (string.IsNullOrEmpty(f.unless) || !has(f.unless))).ToList();

        public static string Label(string kind) => Loc.T("fact_" + (string.IsNullOrEmpty(kind) ? "tip" : kind));

        /// <summary>One fact: a glow note, the kind in small lettering, the sentence in bold ink.</summary>
        public static RectTransform Card(Transform parent, FactDef f, float width)
        {
            var card = UIKit.Rect("Fact", parent);
            UIKit.VStack(card, 4, new RectOffset(22, 22, 14, 18), TextAnchor.UpperLeft, false);
            var note = UIKit.StickyNote(card);
            note.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            note.rectTransform.Stretch();
            UIKit.Words(card, Label(f.kind).ToUpperInvariant(), 20, Role.BrushDeep, Face.Display);
            UIKit.Words(card, f.text.Get(), 22, Role.Ink, Face.ReadBold).lineSpacing = 1.08f;
            if (width > 0) card.Layout(width);
            return card;
        }
    }
}
