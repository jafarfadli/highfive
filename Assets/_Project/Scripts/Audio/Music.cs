using SeoulFirstWeek.Core;
using SeoulFirstWeek.Procedure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SeoulFirstWeek.Audio
{
    /// <summary>
    /// Background music that survives scene loads. Each scene maps to one track in
    /// Resources/Audio/Music (menu, outdoor, indoor); moving between scenes with the same track keeps
    /// it playing, otherwise the tracks crossfade. A missing file just means silence.
    /// Volume follows <see cref="Settings.Music"/> and ducks while staff speak.
    /// </summary>
    public class Music : MonoBehaviour
    {
        const string Folder = "Audio/Music/";
        static Music instance;
        LoopPlayer player;
        float duck = 1f;
        static float duckUntil;

        public static void Ensure()
        {
            if (instance) return;
            var go = new GameObject("[Music]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Music>();
        }

        /// <summary>Ducks the music under a line played outside a conversation (the Logbook's listen buttons).</summary>
        public static void DuckFor(float seconds) => duckUntil = Time.unscaledTime + seconds;

        /// <summary>The track for a scene: menus share one, the street has its own, every place you go into shares one.</summary>
        public static string TrackFor(string scene) => scene switch
        {
            SceneFlow.Hub or SceneFlow.Street119 => "outdoor",
            SceneFlow.Home or SceneFlow.Select or SceneFlow.Results => "menu",
            _ => "indoor", // the clinic, the immigration office, the bank and every place added after them
        };

        void Awake()
        {
            player = new LoopPlayer(gameObject, 2.5f, true);
            SceneManager.activeSceneChanged += OnSceneChanged;
            Pick(SceneManager.GetActiveScene().name);
        }

        void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

        void OnSceneChanged(Scene from, Scene to) => Pick(to.name);

        // interiors sit under Korean dialogue, so their track plays lower
        static float TrackGain(string track) => track switch { "indoor" => 0.7f, "outdoor" => 0.9f, _ => 1f };

        string track;

        void Pick(string scene)
        {
            track = TrackFor(scene);
            player.Play(Resources.Load<AudioClip>(Folder + track));
        }

        void Update()
        {
            duck = Mathf.MoveTowards(duck, NpcActor.Speaking > 0 || Time.unscaledTime < duckUntil ? 0.25f : 1f, Time.unscaledDeltaTime * 2f);
            player.Volume = Settings.Music * 0.6f * TrackGain(track) * duck;
            player.Tick(Time.unscaledDeltaTime);
        }
    }
}
