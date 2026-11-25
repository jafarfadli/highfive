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
        public const string Results = "Results";

        static bool loading;

        public static void Reload() => Load(SceneManager.GetActiveScene().name);

        /// <summary>Starts a level: the walk to the building on the Myeongdong street comes first.</summary>
        public static void Play(LevelDef level)
        {
            GameSession.I.CurrentLevel = level;
            Load(Hub);
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
