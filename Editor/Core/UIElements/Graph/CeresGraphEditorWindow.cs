using System;
using System.Collections.Generic;
using UnityEditor;
using Ceres.Graph;
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public abstract class CeresGraphEditorWindow : EditorWindow
    {
        /// <summary>
        /// Unique object key per window
        /// </summary>
        [field: SerializeField]
        public CeresGraphIdentifier Identifier { get; protected set; }
        
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
            Identifier = container.GetIdentifier();
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
    }
    
    public abstract class CeresGraphEditorWindow<TContainer, TKWindow> : CeresGraphEditorWindow 
            where TContainer: class, ICeresGraphContainer
            where TKWindow: CeresGraphEditorWindow<TContainer, TKWindow>
    {
        public static class EditorWindowRegistry
        {
            private static readonly Dictionary<CeresGraphIdentifier, TKWindow> EditorWindows = new();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static TKWindow GetOrCreateEditorWindow(TContainer container)
            {
                var identifier = container.GetIdentifier();
                if (EditorWindows.TryGetValue(identifier, out var window) && window)
                {
                    return window;
                }
                window = CreateInstance<TKWindow>();
                window.Initialize(container);
                EditorWindows[identifier] = window;
                return window;
            }

            public static void Register(TContainer container, TKWindow window)
            {
                var identifier = container.GetIdentifier();
                EditorWindows[identifier] = window;
            }
            
            public static void Unregister(TContainer container, TKWindow window)
            {
                if(container == null) return;
                var identifier = container.GetIdentifier();
                
                if (EditorWindows.TryGetValue(identifier, out var existedWindow) && existedWindow == window)
                {
                    EditorWindows.Remove(identifier);
                }
            }

            /// <summary>
            /// Find existed <see cref="CeresGraphEditorWindow"/> by <see cref="CeresGraphIdentifier"/>
            /// </summary>
            /// <param name="identifier"></param>
            /// <returns></returns>
            public static CeresGraphEditorWindow FindWindow(CeresGraphIdentifier identifier)
            {
                return EditorWindows.GetValueOrDefault(identifier);
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
            return Identifier.GetContainer<TContainer>();
        }
    }
}