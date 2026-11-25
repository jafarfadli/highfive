using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SeoulFirstWeek.Procedure;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    public class LevelDef
    {
        public string id;
        public int level;
        public string procedure;
        public string scene;
        public string entrance;
        public bool available;
        public string needs;
        public LocText title;
        public LocText story; // the background shown full-screen before the level starts

        [JsonIgnore] public PlaceDef Place;
        [JsonIgnore] public string DoneFlag => "done:" + id;

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
