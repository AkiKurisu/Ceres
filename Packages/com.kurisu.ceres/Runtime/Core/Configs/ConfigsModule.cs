using System.IO;
using Ceres.Serialization;
using UnityEngine;
using System;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

namespace Ceres.Configs
{
    public static class ConfigsModule
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public static readonly string ActualStreamingDirectory = Path.Combine(Application.streamingAssetsPath, "Configs");
#else
        public static readonly string ActualStreamingDirectory = Path.Combine(Application.persistentDataPath, "Configs");
#endif

        public static readonly string StreamingDirectory = Path.Combine(Application.streamingAssetsPath, "Configs");

        public static readonly string PersistentDirectory = Path.Combine(SaveUtility.SavePath, "Configs");

#if UNITY_EDITOR
        private static string _editorBaseDirectory;
        
        private static string _editorDirectory;
        
        internal static string EditorBaseDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_editorBaseDirectory))
                {
                    _editorBaseDirectory = Path.Combine(Application.dataPath, "../Configs");
                    Directory.CreateDirectory(_editorBaseDirectory);
                }
                return _editorBaseDirectory;
            }
        }

        internal static string EditorPlatformDirectory
        {
            get
            {
                if (string.IsNullOrEmpty(_editorDirectory))
                {
                    _editorDirectory = Path.Combine(EditorBaseDirectory, UnityEditor.EditorUserBuildSettings.activeBuildTarget.ToString());
                    Directory.CreateDirectory(_editorDirectory);
                }
                return _editorDirectory;
            }
        }
#endif

        public const string Extension = "cfg";

        private static SaveLoadSerializer _persistentSerializer;

        /// <summary>
        /// Get runtime config serializer
        /// </summary>
        public static SaveLoadSerializer PersistentSerializer
        {
            get
            {
                return _persistentSerializer ??= new SaveLoadSerializer(
                    PersistentDirectory,
                    Extension,
                    ConfigSerializer);
            }
        }

        private static ISerializeFormatter _serializeFormatter = TextSerializeFormatter.Instance;
        
        public static ISerializeFormatter ConfigSerializer
        {
            get => _serializeFormatter;
            set
            {
                // Update serializer to sync formatter change
                _serializeFormatter = value;
                _persistentSerializer = null;
                ConfigSystem.ClearCache();
            }
        }

        /// <summary>
        /// Initialize config system, should always be called before runtime module loaded.
        /// </summary>
        internal static void InitializeInternal()
        {
#if !UNITY_STANDALONE_WIN || UNITY_EDITOR
            // Transfer streaming configs archive to actual streaming configs directory
            ExtractStreamingConfigs();
#endif
        }

        // ReSharper disable once UnusedMember.Local
        private static void ExtractStreamingConfigs()
        {
            var downloadZipPath = $"{SaveUtility.SavePath}/Configs.zip";
            using var request = UnityWebRequest.Get(new Uri(StreamingDirectory + ".zip").AbsoluteUri);
            request.downloadHandler = new DownloadHandlerFile(downloadZipPath);
            try
            {
                request.SendWebRequest();
                while (!request.isDone)
                {
                    // Block main thread
                }
            }
            // Do not exist, skip
            catch
            {
                // ignored
            }

            var result = request.result;
            bool succeed = result == UnityWebRequest.Result.Success;

            // Remove invalid file when zip is nil
            if (File.Exists(PersistentDirectory))
            {
                File.Delete(PersistentDirectory);
            }

            if (File.Exists(downloadZipPath))
            {
                // Prevent unzipping when downloading failed
                if (result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        ZipWrapper.UnzipFile(downloadZipPath, ActualStreamingDirectory);
                    }
                    catch
                    {
                        succeed = false;
                    }
                }
                File.Delete(downloadZipPath);
            }

            if (succeed)
            {
                ConfigSystem.ClearCache();
                Debug.Log("[Ceres] Extract streaming configs succeed.");
            }
#if !UNITY_EDITOR
            else
            {
                Debug.LogWarning("[Ceres] No streaming configs need to be extracted.");
            }
#endif
        }
    }
}