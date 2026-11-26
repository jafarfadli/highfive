using System.Threading.Tasks;
using SeoulFirstWeek.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeoulFirstWeek.Core
{
    public static class SceneFlow
    {
        public const string Home = "Home";
        public const string Select = "Select";
        public const string Hub = "Hub";
        public const string Clinic = "Clinic";
        public const string Immigration = "Immigration";
        public const string Bank = "Bank";
        public const string Airport = "Airport";
        public const string Subway = "Subway";
        public const string Street119 = "Street119"; // 명동길 again, for the 119 call
        public const string ER = "ER";
        public const string Results = "Results";

        static bool loading;

        public static void Reload() => Load(SceneManager.GetActiveScene().name);

        /// <summary>
        /// Starts a level: the walk to the building on the Myeongdong street comes first. A level without an
        /// entrance on the street (the airport, where the student arrives off the plane) starts in its own scene,
        /// which shows the story card itself.
        /// </summary>
        public static void Play(LevelDef level)
        {
            GameSession.I.CurrentLevel = level;
            // a new start of the part: the situation is picked again on its story card
            GameSession.I.ClearSituation();
            Load(level.StartsInside ? level.scene : Hub);
        }

        public static async void Load(string scene)
        {
            if (loading) return;
            loading = true;
            try
            {
                await Overlay.I.FadeTo(1f, 0.45f);
                Overlay.I.ClearToasts();
                var op = SceneManager.LoadSceneAsync(scene);
                while (op != null && !op.isDone) await Task.Yield();
                await Awaitable.NextFrameAsync();
                await Overlay.I.FadeTo(0f, 0.6f);
            }
            finally
            {
                loading = false;
            }
        }
    }
}
