using System;
using System.IO;
using UnityEngine;

namespace SeoulFirstWeek.Core
{
    /// <summary>
    /// Voice settings. Read from the git-ignored StreamingAssets/secrets.json, then overridden
    /// by environment variables. Without an OpenAI key the game uses the macOS Korean voices.
    /// </summary>
    [Serializable]
    public class GameConfig
    {
        public string openaiApiKey = "";
        public string ttsProvider = "auto";           // auto | openai | macos | none
        public string ttsModel = "gpt-4o-mini-tts";
        public float requestTimeoutSeconds = 15f;

        public bool HasOpenAi => !string.IsNullOrWhiteSpace(openaiApiKey);

        public static GameConfig Load()
        {
            var config = new GameConfig();
            var path = Path.Combine(Application.streamingAssetsPath, "secrets.json");
            try
            {
                if (File.Exists(path))
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(path), config);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Config] Could not read {path}: {e.Message}");
            }
            var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(key)) config.openaiApiKey = key;
            return config;
        }
    }
}
