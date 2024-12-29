using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Ceres.Graph;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph
{
    public abstract class CeresGraphEditorWindow : EditorWindow
    {
        /// <summary>
        /// Unique object key per window
        /// </summary>
        public UObject Key { get; protected set; }
        
        /// <summary>
        /// Actual graph container of this window
        /// </summary>
        public ICeresGraphContainer Container { get; protected set; }
        
        /// <summary>
        /// Setup EditorWindow
        /// </summary>
        /// <param name="container"></param>
        protected void Initialize(ICeresGraphContainer container)
        {
            Container = container;
            Key = Container.Object;
            OnInitialize();
        }
        
        /// <summary>
        /// Initialize graph view in this stage
        /// </summary>
        protected virtual void OnInitialize()
        {
            
        }
        
        protected virtual void OnEnable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Reload();
        }

        protected virtual void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            switch (playModeStateChange)
            {
                case PlayModeStateChange.EnteredEditMode:
                    Reload();
                    break;
                case PlayModeStateChange.ExitingEditMode:
                    PreReload();
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    Reload();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    PreReload();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playModeStateChange), playModeStateChange, null);
            }
        }

        /// <summary>
        /// Reload editor window
        /// </summary>
        protected virtual void Reload()
        {
            
        }
        
        /// <summary>
        /// Called before reload
        /// </summary>
        protected virtual void PreReload()
        {
            
        }
        
        /// <summary>
        /// Get instance id of current <see cref="Key"/>
        /// </summary>
        /// <returns></returns>
        public int GetWindowId()
        {
            return !Key ? 0 : Key.GetInstanceID();
        }
    }
    
    public abstract class CeresGraphEditorWindow<TContainer, TKWindow> : CeresGraphEditorWindow 
            where TContainer: class, ICeresGraphContainer
            where TKWindow: CeresGraphEditorWindow<TContainer, TKWindow>
    {
        public static class EditorWindowRegistry
        {
            // ReSharper disable once InconsistentNaming
            private static readonly Dictionary<int, TKWindow> _editorWindows = new();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static TKWindow GetOrCreateEditorWindow(TContainer container)
            {
                if (_editorWindows.TryGetValue(container.Object.GetInstanceID(), out var window) && window)
                {
                    return window;
                }
                window = CreateInstance<TKWindow>();
                window.Initialize(container);
                _editorWindows[container.Object.GetInstanceID()] = window;
                return window;
            }

            public static void Register(TContainer container, TKWindow window)
            {
                _editorWindows[container.Object.GetInstanceID()] = window;
            }
            
            public static void Unregister(TContainer container, TKWindow window)
            {
                if(container == null) return;
                
                if (_editorWindows.TryGetValue(container.Object.GetInstanceID(), out var existedWindow) && existedWindow == window)
                {
                    _editorWindows.Remove(container.Object.GetInstanceID());
                }
            }

            /// <summary>
            /// Find existed <see cref="CeresGraphEditorWindow"/> by instance id
            /// </summary>
            /// <param name="instanceId"></param>
            /// <returns></returns>
            public static CeresGraphEditorWindow FindWindow(int instanceId)
            {
                return _editorWindows.GetValueOrDefault(instanceId);
            }
        }
        
        /// <summary>
        /// Actual graph container of this window
        /// </summary>
        public TContainer ContainerT => (TContainer)Container;

        /// <summary>
        /// Create <see cref="CeresGraphEditorWindow"/> instance, return cached window if existed
        /// </summary>
        /// <param name="container"></param>
        /// <typeparam name="TKWindow"></typeparam>
        /// <returns></returns>
        public static TKWindow GetOrCreateEditorWindow(TContainer container)
        {
            return EditorWindowRegistry.GetOrCreateEditorWindow(container);
        }
        
        protected override void OnDisable()
        {
           EditorWindowRegistry.Unregister(ContainerT, (TKWindow)this);
           base.OnDisable();
        }
        
        /// <summary>
        /// Get <see cref="ICeresGraphContainer"/> from editor window key object
        /// </summary>
        /// <typeparam name="TContainer"></typeparam>
        /// <returns></returns>
        public TContainer GetContainer()
        {
            if (Key is GameObject gameObject) return gameObject.GetComponent<TContainer>();
            return Key as TContainer;
        }
    }
}