using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SeoulFirstWeek.Procedure;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>One answer to a question on a personalised visit's story card: its title, its story, its flags.</summary>
    public class SituationOption
    {
        public string id;
        public LocText title;
        public LocText tag;   // the case in a few words where the question isn't beside it (the results screen)
        public LocText story;
        public List<string> grants = new();
        public List<string> carry = new(); // documents the player has with them in this case (no "received" toast)
    }

    /// <summary>
    /// A question the story card asks before a personalised visit ("What brings you here today?", "How long have you
    /// been in Korea?"). The option picked sets flags (`sit:…`) that show or hide the steps, answers, facts and notes
    /// that fit it; the first option is the default.
    /// </summary>
    public class SituationQuestion
    {
        public string id;
        public LocText question;
        public List<SituationOption> options = new();
    }

    /// <summary>A "Know before you go" fact (hours, what to bring, the cost, a tip); it may fit one situation only.</summary>
    public class FactDef
    {
        public string kind;
        public LocText text;
        public string needs;
        public string unless;
    }

    public class LevelDef
    {
        public string id;
        public int level;
        public string procedure;
        public string scene;
        public string entrance;
        public string start;      // where in the scene the player starts ("Start_<start>" anchor); empty: the scene's own spawn
        public bool available;
        public string needs;
        public LocText title;
        public LocText story; // the background shown full-screen before the level starts
        public List<SituationQuestion> situations; // a personalised visit: the story card asks these first
        public List<FactDef> before;                // Know before you go: shown on the story card and in the Logbook

        [JsonIgnore] public PlaceDef Place;
        [JsonIgnore] public string DoneFlag => "done:" + id;

        /// <summary>No entrance on the Myeongdong street: the level starts in its own scene (the airport).</summary>
        [JsonIgnore] public bool StartsInside => string.IsNullOrEmpty(entrance);

        /// <summary>"Part 1 · See a doctor".</summary>
        [JsonIgnore] public string FullTitle => $"{Loc.T("part_n", level)} · {title?.Get()}";
    }

    public class PlaceDef
    {
        public string id;
        public string nameKo;
        public string color = "#FF5A5F";
        public LocText name;
        public List<LevelDef> levels = new();

        public Color Color => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.white;
    }

    /// <summary>Places and their levels, loaded from Resources/levels (Data/levels.json).</summary>
    public static class Levels
    {
        class Catalog
        {
            public List<PlaceDef> places = new();
        }

        static List<PlaceDef> places;

        public static IReadOnlyList<PlaceDef> Places
        {
            get
            {
                if (places == null) Load();
                return places;
            }
        }

        public static IEnumerable<LevelDef> All => Places.SelectMany(p => p.levels);

        public static LevelDef Find(string id) => All.FirstOrDefault(l => l.id == id);

        public static LevelDef ForProcedure(string procedure) => All.FirstOrDefault(l => l.procedure == procedure);

        static void Load()
        {
            var asset = Resources.Load<TextAsset>("levels");
            var catalog = asset ? JsonConvert.DeserializeObject<Catalog>(asset.text) : new Catalog();
            places = catalog.places;
            foreach (var p in places)
            foreach (var l in p.levels)
                l.Place = p;
        }
    }
}
