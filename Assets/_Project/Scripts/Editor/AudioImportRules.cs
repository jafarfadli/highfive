using UnityEditor;
using UnityEngine;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Import settings for game audio: music streams from disk (full songs would otherwise sit
    /// decompressed in memory), ambience and baked voice lines load into memory as Vorbis.
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_Project/Resources/Audio/") && !assetPath.StartsWith("Assets/_Project/Resources/Voice/")) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            if (assetPath.Contains("/Audio/Music/"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.quality = 0.7f;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.quality = 0.6f;
                importer.forceToMono = true;
            }
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = assetPath.Contains("/Audio/Music/");
        }
    }
}
