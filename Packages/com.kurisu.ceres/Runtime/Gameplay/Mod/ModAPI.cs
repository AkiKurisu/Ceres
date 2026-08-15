using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ceres;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using R3;
using UnityEngine;

namespace Ceres.Gameplay.Mod
{
    public static class ModAPI
    {
        internal const string DefaultAPIVersion = "0.1.0";

        /// <summary>
        /// Default mod loading directory path.
        /// </summary>
#if !UNITY_EDITOR && UNITY_ANDROID
        public static readonly string LoadingPath = Path.Combine(Application.persistentDataPath, "Mods");
#else
        public static readonly string LoadingPath = Path.Combine(Path.GetDirectoryName(Application.dataPath)!, "Mods");
#endif

        private static readonly ReactiveProperty<bool> InitializedProperty = new(false);

        private static readonly Subject<Unit> RefreshSubject = new();

        private static readonly List<ModInfo> ModInfos = new();

        private static ModConfig _config;

        /// <summary>
        /// Event when mod api is initialized.
        /// </summary>
        public static ReadOnlyReactiveProperty<bool> Initialized => InitializedProperty;

        /// <summary>
        /// Event when any mod info state is changed.
        /// </summary>
        public static Observable<Unit> Refresh => RefreshSubject;

        /// <summary>
        /// Initialize api and load all mods
        /// </summary>
        /// <param name="modConfig"></param>
        /// <param name="modLoader"></param>
        /// <returns></returns>
        public static async UniTask Initialize(ModConfig modConfig, IModLoader modLoader = null)
        {
            if (InitializedProperty.Value)
            {
                Debug.LogError("[Mod API] Mod api is already initialized");
                return;
            }
            Debug.Log("[Mod API] Initialize mod api...");
            modLoader ??= new ModLoader(modConfig, new APIValidator(modConfig.ApiVersion));
            _config = modConfig;
            if (await modLoader.LoadAllModsAsync(ModInfos))
            {
                for (int i = _config.States.Count - 1; i >= 0; i--)
                {
                    var state = _config.States[i];
                    if (ModInfos.All(y => y.FullName != state.fullName))
                    {
                        Debug.LogWarning($"[Mod API] Missing mod {state.fullName}");
                        _config.States.RemoveAt(i);
                    }
                }
                InitializedProperty.Value = true;
            }
        }

        /// <summary>
        /// Delete mod on next launch
        /// </summary>
        /// <param name="modInfo"></param>
        public static void DeleteMod(ModInfo modInfo)
        {
            if (!InitializedProperty.Value)
            {
                Debug.LogError("[Mod API] Mod api is not initialized");
                return;
            }
            if (_config.GetModState(modInfo) == ModStatus.Delete) return;
            _config.DeleteMod(modInfo);
            ModInfos.Remove(modInfo);
            RefreshSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Enable mod on next launch
        /// </summary>
        /// <param name="modInfo"></param>
        /// <param name="isEnabled"></param>
        public static void EnabledMod(ModInfo modInfo, bool isEnabled)
        {
            if (!InitializedProperty.Value)
            {
                Debug.LogError("[Mod API] Mod api is not initialized");
                return;
            }
            if (_config.GetModState(modInfo) == (isEnabled ? ModStatus.Enabled : ModStatus.Disabled)) return;
            _config.SetModEnabled(modInfo, isEnabled);
            RefreshSubject.OnNext(Unit.Default);
        }

        /// <summary>
        /// Get mod state
        /// </summary>
        /// <param name="modInfo"></param>
        /// <returns></returns>
        public static ModStatus GetModState(ModInfo modInfo)
        {
            if (!InitializedProperty.Value)
            {
                Debug.LogError("[Mod API] Mod api is not initialized");
                return ModStatus.Disabled;
            }
            return _config.GetModState(modInfo);
        }

        /// <summary>
        /// Get all enabled mod infos
        /// </summary>
        /// <returns></returns>
        public static List<ModInfo> GetAllInfos()
        {
            if (!InitializedProperty.Value)
            {
                Debug.LogError("[Mod API] Mod api is not initialized");
                return new List<ModInfo>();
            }
            return ModInfos.ToList();
        }

        /// <summary>
        /// Unzip all zip files from <see cref="path"/> 
        /// </summary>
        /// <param name="path">The root directory path</param>
        /// <param name="allDirectories">Whether to search all directories</param>
        public static void UnZipAll(string path, bool allDirectories)
        {
            var zips = Directory.GetFiles(path, "*.zip", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();
            foreach (var zip in zips)
            {
                ZipWrapper.UnzipFile(zip, Path.GetDirectoryName(zip));
                File.Delete(zip);
            }
        }

        /// <summary>
        /// Unzip all zip files from <see cref="path"/> asynchronously using multithreading
        /// </summary>
        /// <param name="path">The root directory path</param>
        /// <param name="allDirectories">Whether to search all directories</param>
        public static async UniTask UnZipAllAsync(string path, bool allDirectories)
        {
            var zips = Directory.GetFiles(path, "*.zip", allDirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).ToList();

            // Create parallel unzip tasks
            var tasks = zips.Select(zip => UniTask.RunOnThreadPool(() =>
            {
                ZipWrapper.UnzipFile(zip, Path.GetDirectoryName(zip));
                File.Delete(zip);
            })).ToArray();

            // Wait for all tasks to complete
            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// Delete mod files from disk, is not safe when is already initialized,
        /// recommend to use <see cref="DeleteMod"/> to delete mod on next launcher
        /// </summary>
        /// <param name="modInfo"></param>
        public static void DeleteModFromDisk(ModInfo modInfo)
        {
            Directory.Delete(modInfo.FilePath, true);
        }

        /// <summary>
        /// Load <see cref="ModInfo"/> from path
        /// </summary>
        /// <param name="modInfoPath"></param>
        /// <returns></returns>
        public static async UniTask<ModInfo> LoadModInfo(string modInfoPath)
        {
            var modInfo = JsonConvert.DeserializeObject<ModInfo>(await File.ReadAllTextAsync(modInfoPath));
            modInfo.FilePath = Path.GetDirectoryName(modInfoPath)!.Replace(@"\", "/");
            return modInfo;
        }
    }
}
