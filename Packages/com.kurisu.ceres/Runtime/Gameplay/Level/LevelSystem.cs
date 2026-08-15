using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.DataDriven;
using Ceres.Pool;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Ceres.Gameplay.Level
{
    /// <summary>
    /// Semantic role of an additively loaded scene in the stack (for selective unload).
    /// </summary>
    public enum AdditiveSceneRole
    {
        /// <summary>Additive load that does not become the active scene.</summary>
        Additive = 0,

        /// <summary>Additive load that became the active scene after load.</summary>
        Override = 1,

        /// <summary>Runtime additive content that does not become the active scene.</summary>
        RuntimeAdditive = 2,

        /// <summary>Runtime additive content that became the active scene after load.</summary>
        RuntimeOverride = 3,
    }

    /// <summary>
    /// Addressables handle plus <see cref="AdditiveSceneRole"/> for each additive scene entry.
    /// </summary>
    public readonly struct AdditiveSceneEntry
    {
        public readonly AsyncOperationHandle<SceneInstance> Handle;
        public readonly AdditiveSceneRole Role;

        public AdditiveSceneEntry(AsyncOperationHandle<SceneInstance> handle, AdditiveSceneRole role)
        {
            Handle = handle;
            Role = role;
        }
    }

    /// <summary>
    /// Reference to gameplay level structure
    /// </summary>
    public class LevelReference
    {
        private string[] _tags;
        
        public string Name => Scenes.Length > 0 ? Scenes[0].levelName : string.Empty;
        
        /// <summary>
        /// All scenes contained in level
        /// </summary>
        public LevelSceneRow[] Scenes { get; }
        
        /// <summary>
        /// Get level tags
        /// </summary>
        public string[] Tags => _tags ??= Scenes.SelectMany(row => row.tags).Distinct().ToArray();

        /// <summary>
        /// Is level contains main scene
        /// </summary>
        public bool IsMain => Scenes.Any(sceneRow => sceneRow.loadMode == LoadLevelMode.Single);

        /// <summary>
        /// Get a empty level reference
        /// </summary>
        public static readonly LevelReference Empty = new();

        private LevelReference()
        {
            Scenes = Array.Empty<LevelSceneRow>();
        }
        
        public LevelReference(IEnumerable<LevelSceneRow> levelSceneRows)
        {
            Scenes = levelSceneRows.ToArray();
        }
    }

    public sealed class LevelSceneDataTableManager : DataTableManager<LevelSceneDataTableManager>
    {
        public const string TableLabel = "LevelSceneTable";
        
        public LevelSceneDataTableManager(object _) : base(_)
        {
        }

        protected override async UniTask Initialize(bool sync)
        {
            IList<DataTable> tables;
            if (sync)
            {
                tables = ResourceSystem.LoadAssetsAsync<DataTable>(TableLabel).WaitForCompletion();
            }
            else
            {
                tables = await ResourceSystem.LoadAssetsAsync<DataTable>(TableLabel);
            }

            if (tables == null) return;
            foreach (var table in tables)
            {
                if (table != null)
                {
                    RegisterDataTable(table);
                }
            }
        }
        
        private LevelReference[] _references;
        
        public LevelReference[] GetLevelReferences()
        {
            if (_references != null) return _references;
            
            var dict = new Dictionary<string, List<LevelSceneRow>>();
            foreach (var scene in DataTables.SelectMany(pair => pair.Value.GetAllRows<LevelSceneRow>()))
            {
                // Whether it can load in current platform
                if (!scene.ValidateLoadPolicy()) continue;

                if (!dict.TryGetValue(scene.levelName, out var rows))
                {
                    rows = dict[scene.levelName] = new List<LevelSceneRow>();
                }
                rows.Add(scene);
            }
            return _references = dict.Select(keyValuePair => new LevelReference(keyValuePair.Value)).ToArray();
        }
        
        public LevelReference FindLevel(string levelName)
        {
            foreach (var level in GetLevelReferences())
            {
                if (level.Name == levelName)
                {
                    return level;
                }
            }
            return LevelReference.Empty;
        }
        
        public LevelReference FindLevelFromTag(string tag)
        {
            foreach (var level in GetLevelReferences())
            {
                if (level.Tags.Contains(tag))
                {
                    return level;
                }
            }
            return LevelReference.Empty;
        }
    }
    
    public static class LevelSystem
    {
        /// <summary>
        /// Last loaded level that contains main scene
        /// </summary>
        public static LevelReference LastLevel { get; private set; } = LevelReference.Empty;
        
        /// <summary>
        /// Current loaded level that contains main scene
        /// </summary>
        public static LevelReference CurrentLevel { get; private set; } = LevelReference.Empty;
        
        private static readonly Subject<LevelReference> LevelPreloadSubject = new();
                
        private static readonly Subject<LevelReference> LevelPostLoadSubject = new();

        /// <summary>
        /// Event when level start loading
        /// </summary>
        public static Observable<LevelReference> LevelPreload => LevelPreloadSubject;

        /// <summary>
        /// Event when level end loading
        /// </summary>
        public static Observable<LevelReference> LevelPostLoad => LevelPostLoadSubject;

        private static readonly ReactiveProperty<float> LoadingProgressProperty = new(0f);

        /// <summary>
        /// Normalized [0, 1] loading progress for the current LoadAsync operation.
        /// Resets to 0 when loading begins and reaches 1 when all scenes are loaded.
        /// </summary>
        public static ReadOnlyReactiveProperty<float> LoadingProgress => LoadingProgressProperty;

        // Additive scenes loaded on top of the current level, ordered by load time (last = most recent).
        // Cleared and released whenever a new Single-mode scene takes over.
        private static readonly List<AdditiveSceneEntry> AdditiveSceneEntries = new();

        /// <summary>
        /// Scene to set active when unloading runtime additively loaded content, if <see cref="UnloadLastAdditiveAsync"/> is used with base restore.
        /// Set via <see cref="RegisterBaseScene"/> before loading content not from the level table.
        /// </summary>
        private static Scene _registeredBaseScene;

        /// <summary>
        /// Number of additive scenes currently loaded on top of the current level's base scene.
        /// </summary>
        public static int AdditiveSceneCount => AdditiveSceneEntries.Count;

        /// <summary>
        /// Registers the scene that should become active again when using <see cref="UnloadLastAdditiveAsync"/> with base restore (typically the Single-mode base scene).
        /// </summary>
        public static void RegisterBaseScene(Scene baseScene)
        {
            if (baseScene.IsValid())
                _registeredBaseScene = baseScene;
        }

        /// <summary>
        /// Loads one additive scene by Addressables address (not from <see cref="LevelSceneDataTableManager"/>). Does not change <see cref="CurrentLevel"/> or fire level load events.
        /// </summary>
        /// <param name="sceneAddress">Addressables scene address.</param>
        /// <param name="setActiveAfterLoad">When true, sets this scene as the active scene after load.</param>
        public static async UniTask LoadAdditiveByAddressAsync(
            string sceneAddress,
            bool setActiveAfterLoad,
            AdditiveSceneRole? roleOverride = null)
        {
            if (string.IsNullOrEmpty(sceneAddress))
                return;

            LoadingProgressProperty.Value = 0f;
            var handle = Addressables.LoadSceneAsync(sceneAddress, LoadSceneMode.Additive);
            var role = roleOverride ?? (setActiveAfterLoad ? AdditiveSceneRole.Override : AdditiveSceneRole.Additive);
            AdditiveSceneEntries.Add(new AdditiveSceneEntry(handle, role));

            try
            {
                await handle.ToUniTask(progress: new Progress<float>(p => LoadingProgressProperty.Value = p));

                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    PopLastFailedAdditiveHandleAndRelease();
                    LoadingProgressProperty.Value = 0f;
                    return;
                }

                LoadingProgressProperty.Value = 1f;

                if (setActiveAfterLoad)
                    SceneManager.SetActiveScene(handle.Result.Scene);
            }
            catch (Exception)
            {
                PopLastFailedAdditiveHandleAndRelease();
                LoadingProgressProperty.Value = 0f;
                throw;
            }
        }

        private static void PopLastFailedAdditiveHandleAndRelease()
        {
            if (AdditiveSceneEntries.Count == 0) return;
            var handle = AdditiveSceneEntries[^1].Handle;
            AdditiveSceneEntries.RemoveAt(AdditiveSceneEntries.Count - 1);
            if (handle.IsValid())
                Addressables.Release(handle);
        }

        /// <summary>
        /// Load level by name
        /// </summary>
        /// <param name="levelName"></param>
        public static async UniTask LoadAsync(string levelName)
        {
            var reference = LevelSceneDataTableManager.Get().FindLevel(levelName);
            if (reference != null)
            {
                await LoadAsync(reference);
            }
        }

        /// <summary>
        /// Load level from <see cref="LevelReference"/>
        /// </summary>
        /// <param name="reference"></param>
        public static async UniTask LoadAsync(LevelReference reference)
        {
            LoadingProgressProperty.Value = 0f;
            LevelPreloadSubject.OnNext(reference);
            
            // First check has single load scene
            var mainScene = reference.Scenes.FirstOrDefault(row => row.loadMode == LoadLevelMode.Single);
            var additiveScenes = reference.Scenes.Where(s => s.loadMode >= LoadLevelMode.Additive).ToArray();
            int totalScenes = (mainScene != null ? 1 : 0) + additiveScenes.Length;
            var aggregate = totalScenes > 0 ? new AggregateProgress(LoadingProgressProperty, totalScenes) : null;
            int slotIndex = 0;

            if (mainScene != null)
            {
                LastLevel = CurrentLevel;
                CurrentLevel = reference;

                // The incoming Single load will implicitly unload all previous scenes via Unity.
                // Release the Addressables handles for old additive scenes to avoid reference leaks.
                ReleaseAdditiveHandles();
                
                /* Since Unity destroy and awake MonoBehaviour in same frame, need notify world still valid */
                using (GameWorld.Pin())
                {
                    await Addressables.LoadSceneAsync(mainScene.reference.Address)
                        .ToUniTask(progress: aggregate?.GetSlotProgress(slotIndex));
                }
                slotIndex++;
            }
            
            // Parallel for the others, tracking each handle for later explicit unload
            using var parallel = UniParallel.Get();
            foreach (var scene in additiveScenes)
            {
                var handle = Addressables.LoadSceneAsync(scene.reference.Address, LoadSceneMode.Additive);
                AdditiveSceneEntries.Add(new AdditiveSceneEntry(handle, AdditiveSceneRole.Additive));
                parallel.Add(handle.ToUniTask(progress: aggregate?.GetSlotProgress(slotIndex++)));
            }
            await parallel;

            LoadingProgressProperty.Value = 1f;
            LevelPostLoadSubject.OnNext(reference);
        }

        /// <summary>
        /// Unloads one additive scene. By default removes the stack top (LIFO).
        /// When <paramref name="unloadOnlyRole"/> is set, removes the topmost entry whose role matches (scan from most recent downward).
        /// </summary>
        /// <param name="restoreRegisteredBaseBeforeUnload">When true, sets <see cref="RegisterBaseScene"/> as active before unloading (if registered). For role-filtered unload, applies when the removed entry is an override role.</param>
        /// <param name="unloadOnlyRole">When null, unloads the top of the stack only. When set, unloads the first matching role from the top.</param>
        /// <returns>True if a scene was unloaded, false if none matched or the stack is empty.</returns>
        public static async UniTask<bool> UnloadLastAdditiveAsync(
            bool restoreRegisteredBaseBeforeUnload = false,
            AdditiveSceneRole? unloadOnlyRole = null)
        {
            if (AdditiveSceneEntries.Count == 0) return false;

            int indexToRemove;
            if (unloadOnlyRole == null)
            {
                indexToRemove = AdditiveSceneEntries.Count - 1;
            }
            else
            {
                indexToRemove = -1;
                for (var i = AdditiveSceneEntries.Count - 1; i >= 0; i--)
                {
                    if (AdditiveSceneEntries[i].Role == unloadOnlyRole.Value)
                    {
                        indexToRemove = i;
                        break;
                    }
                }

                if (indexToRemove < 0) return false;
            }

            var entry = AdditiveSceneEntries[indexToRemove];
            if (unloadOnlyRole != null)
            {
                if (restoreRegisteredBaseBeforeUnload && IsOverrideRole(entry.Role) && _registeredBaseScene.IsValid())
                    SceneManager.SetActiveScene(_registeredBaseScene);
            }
            else
            {
                if (restoreRegisteredBaseBeforeUnload && _registeredBaseScene.IsValid())
                    SceneManager.SetActiveScene(_registeredBaseScene);
            }

            AdditiveSceneEntries.RemoveAt(indexToRemove);
            var handle = entry.Handle;

            if (handle.IsValid())
            {
                await Addressables.UnloadSceneAsync(handle).ToUniTask();
            }

            await Resources.UnloadUnusedAssets().ToUniTask();
            return true;
        }

        private static bool IsOverrideRole(AdditiveSceneRole role)
        {
            return role == AdditiveSceneRole.Override || role == AdditiveSceneRole.RuntimeOverride;
        }

        /// <summary>
        /// Unloads runtime map stack: optional secondary additive first, then primary with base-scene restore.
        /// </summary>
        public static async UniTask UnloadRuntimeAdditiveStackAsync(bool hasSecondary)
        {
            if (hasSecondary)
                await UnloadLastAdditiveAsync(false, AdditiveSceneRole.RuntimeAdditive);
            await UnloadLastAdditiveAsync(true, AdditiveSceneRole.RuntimeOverride);
        }

        /// <summary>
        /// Explicitly unload all additive scenes belonging to the current level.
        /// </summary>
        public static async UniTask UnloadAdditiveAsync()
        {
            var entries = AdditiveSceneEntries.ToArray();
            AdditiveSceneEntries.Clear();

            foreach (var entry in entries)
            {
                if (entry.Handle.IsValid())
                {
                    await Addressables.UnloadSceneAsync(entry.Handle).ToUniTask();
                }
            }

            await Resources.UnloadUnusedAssets().ToUniTask();
        }

        // Release all stored additive handles without triggering scene unload
        // (used when Unity is already unloading those scenes via a Single-mode load).
        private static void ReleaseAdditiveHandles()
        {
            foreach (var entry in AdditiveSceneEntries)
            {
                if (entry.Handle.IsValid())
                {
                    Addressables.Release(entry.Handle);
                }
            }
            AdditiveSceneEntries.Clear();
        }

        // Aggregates progress from N independent scene-loading slots into a single ReactiveProperty.
        // Each slot contributes equally (1/N weight) to the total progress value.
        private sealed class AggregateProgress
        {
            private readonly ReactiveProperty<float> _target;
            private readonly float[] _slots;

            public AggregateProgress(ReactiveProperty<float> target, int slotCount)
            {
                _target = target;
                _slots = new float[slotCount];
            }

            public IProgress<float> GetSlotProgress(int index)
            {
                return new SlotProgress(this, index);
            }

            private void UpdateSlot(int index, float value)
            {
                _slots[index] = value;
                float sum = 0f;
                for (int i = 0; i < _slots.Length; i++) sum += _slots[i];
                _target.Value = sum / _slots.Length;
            }

            private sealed class SlotProgress : IProgress<float>
            {
                private readonly AggregateProgress _parent;
                private readonly int _index;

                public SlotProgress(AggregateProgress parent, int index)
                {
                    _parent = parent;
                    _index = index;
                }

                public void Report(float value) => _parent.UpdateSlot(_index, value);
            }
        }
    }
}
