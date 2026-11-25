using System.Collections.Generic;
using System.Linq;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>
    /// Survives scene loads: the selected level, inventory flags, results history and voice service.
    /// Progress (flags) is saved to PlayerPrefs so the level select remembers finished levels.
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        const string ProgressKey = "sfw.progress";

        static GameSession instance;

        public static bool Exists => instance != null;

        public static GameSession I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[GameSession]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameSession>();
                }
                return instance;
            }
        }

        public string PlayerName = "Minh";
        public string PlayerNameKo = "민"; // how staff say it (PA calls)
        public LevelDef CurrentLevel;
        public ScenarioResult LastResult;
        public readonly List<ScenarioResult> History = new();
        public readonly HashSet<string> Flags = new();
        public readonly List<string> Inventory = new();

        public HintLanguage Language
        {
            get => Settings.Language;
            set => Settings.Language = value;
        }

        public GameConfig Config { get; private set; }
        public VoiceService Voice { get; private set; }
        public string SessionId { get; private set; }
        public string LangCode => Loc.Code(Language);

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            SessionId = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Config = GameConfig.Load();
            Voice = new VoiceService(Config);
            LoadProgress();
            Music.Ensure();
        }

        void LoadProgress()
        {
            Flags.Clear();
            Inventory.Clear();
            foreach (var doc in Documents.StartingInventory) GiveDocument(doc, false, false);
            var saved = PlayerPrefs.GetString(ProgressKey, "");
            foreach (var flag in saved.Split('|').Where(f => f.Length > 0))
            {
                if (flag.StartsWith("doc:")) GiveDocument(flag.Substring(4), false, false);
                else Flags.Add(flag);
            }
        }

        void SaveProgress()
        {
            PlayerPrefs.SetString(ProgressKey, string.Join("|", Flags));
            PlayerPrefs.Save();
        }

        public void ResetProgress()
        {
            PlayerPrefs.DeleteKey(ProgressKey);
            History.Clear();
            LastResult = null;
            LoadProgress();
        }

        public bool Has(string flag) => Flags.Contains(flag);

        public bool HasDocument(string id) => Flags.Contains(Documents.Flag(id));

        public bool IsDone(LevelDef level) => level != null && Flags.Contains(level.DoneFlag);

        public void Grant(string flag)
        {
            if (flag.StartsWith("doc:")) GiveDocument(flag.Substring(4), true, true);
            else
            {
                Flags.Add(flag);
                SaveProgress();
            }
        }

        public void GiveDocument(string id, bool notify, bool save = true)
        {
            if (!Flags.Add(Documents.Flag(id))) return;
            if (!Inventory.Contains(id)) Inventory.Add(id);
            if (notify) UI.Toasts.Show(Loc.T("received", Documents.LocalName(id)), Documents.Korean(id));
            if (save) SaveProgress();
        }

        public void TakeDocument(string id)
        {
            Flags.Remove(Documents.Flag(id));
            Inventory.Remove(id);
            SaveProgress();
        }

        public ScenarioResult BestResult(string levelId) =>
            History.Where(r => r.ScenarioId == levelId).OrderByDescending(r => r.StepsCompleted).FirstOrDefault();

        /// <summary>Picks a default level when a scene is played directly from the editor.</summary>
        public LevelDef EnsureLevel(string procedure = null)
        {
            if (CurrentLevel != null && (procedure == null || CurrentLevel.procedure == procedure)) return CurrentLevel;
            CurrentLevel = procedure != null ? Levels.ForProcedure(procedure) : Levels.All.FirstOrDefault(l => l.available);
            return CurrentLevel;
        }
    }
}
