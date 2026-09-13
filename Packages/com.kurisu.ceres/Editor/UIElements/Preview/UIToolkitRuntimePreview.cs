using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Ceres.Editor.UIElements
{
    internal sealed class UIToolkitRuntimePreview : IDisposable
    {
        private Scene _scene;
        private GameObject _host;
        private UIDocument _document;
        private MethodInfo _update;
        private MethodInfo _repaint;
        private MethodInfo _render;
        private IUIToolkitPreviewSession _session;
        private readonly Event _repaintEvent = new Event { type = EventType.Repaint };
        private static readonly MethodInfo CreateEvent = typeof(VisualElement).Assembly
            .GetType("UnityEngine.UIElements.UIElementsRuntimeUtility")?.GetMethod("CreateEvent", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly MethodInfo LeafFocus = typeof(FocusController)
            .GetMethod("GetLeafFocusedElement", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        public PanelSettings Settings { get; private set; }
        public RenderTexture Texture { get; private set; }
        public VisualElement Root => _document ? _document.rootVisualElement : null;
        public IReadOnlyList<string> SnapshotElementNames => _session?.SnapshotElementNames ?? Array.Empty<string>();
        public bool Ready { get; private set; }

        public UIToolkitRuntimePreview(IUIToolkitPreviewProvider provider, UIToolkitPreviewFixture fixture, UIToolkitPreviewViewport viewport)
        {
            if (!provider.PanelSettings) throw new InvalidOperationException("A runtime preview requires production PanelSettings.");
            try
            {
                _scene = EditorSceneManager.NewPreviewScene();
                _host = new GameObject("Ceres Runtime UI Preview") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(_host, _scene);
                Settings = Object.Instantiate(provider.PanelSettings);
                Settings.hideFlags = HideFlags.HideAndDontSave;
                Settings.scale = provider.GetPanelScale(viewport);
                Texture = new RenderTexture(viewport.Width, viewport.Height, 24, RenderTextureFormat.ARGB32)
                    { name = "Ceres UI Preview", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                Texture.Create();
                Settings.targetTexture = Texture;
                Settings.clearColor = true;
                Settings.colorClearValue = Color.clear;
                Settings.SetScreenToPanelSpaceFunction(_ => new Vector2(float.NaN, float.NaN));
                _document = _host.AddComponent<UIDocument>();
                _document.panelSettings = Settings;
                // Clone explicitly so live reload cannot silently replace a tree whose fixture owns callbacks.
                provider.VisualTreeAsset.CloneTree(Root);
                Root.name = "preview-root";
                Root.style.position = Position.Absolute;
                Root.style.left = Root.style.top = Root.style.right = Root.style.bottom = 0;
                foreach (var sheet in provider.StyleSheets)
                    if (sheet && !Root.styleSheets.Contains(sheet)) Root.styleSheets.Add(sheet);
                _session = provider.CreateSession(Root, new UIToolkitPreviewContext(fixture, viewport, Settings.scale))
                    ?? throw new InvalidOperationException($"Preview provider '{provider.Id}' returned no session.");
                var type = Root.panel.GetType();
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                _update = type.GetMethod("Update", flags, null, Type.EmptyTypes, null);
                _repaint = type.GetMethod("Repaint", flags, null, new[] { typeof(Event) }, null);
                _render = type.GetMethod("Render", flags, null, Type.EmptyTypes, null);
                if (_update == null || _repaint == null || CreateEvent == null || LeafFocus == null || Root.panel.contextType != ContextType.Player)
                    throw new NotSupportedException("This Unity version cannot host a runtime UI preview.");
                Draw();
            }
            catch { Dispose(); throw; }
        }

        public void Draw()
        {
            if (Root?.panel == null) return;
            var previousTexture = RenderTexture.active;
            var previousCamera = Camera.current;
            try
            {
                // Unity exposes the runtime panel through IPanel, but its standalone render entry points are internal.
                _update.Invoke(Root.panel, null);
                _repaint.Invoke(Root.panel, new object[] { _repaintEvent });
                _render?.Invoke(Root.panel, null);
                Ready = Root.layout.width > 0 && Root.layout.height > 0;
            }
            finally
            {
                Camera.SetupCurrent(previousCamera);
                RenderTexture.active = previousTexture;
            }
        }

        public void Send(Event source)
        {
            if (!Ready) return;
            var mapped = new Event(source) { mousePosition = source.mousePosition / Settings.scale };
            var focused = LeafFocus.Invoke(Root.panel.focusController, null) as VisualElement;
            if (source.type == EventType.KeyDown && focused is Button button &&
                (source.keyCode == KeyCode.Return || source.keyCode == KeyCode.KeypadEnter || source.keyCode == KeyCode.Space))
            {
                using var submit = NavigationSubmitEvent.GetPooled();
                button.SendEvent(submit);
                return;
            }
            using var evt = (EventBase)CreateEvent.Invoke(null, new object[] { mapped });
            if (source.isKey || source.type == EventType.ValidateCommand || source.type == EventType.ExecuteCommand)
                evt.target = focused ?? Root;
            else
                evt.target = Root.panel.GetCapturingElement(PointerId.mousePointerId) as VisualElement
                    ?? Root.panel.Pick(mapped.mousePosition) ?? Root;
            ((VisualElement)evt.target).SendEvent(evt);
        }

        public void Blur()
        {
            Root?.panel?.focusController.focusedElement?.Blur();
            if (Root?.panel?.GetCapturingElement(PointerId.mousePointerId) is VisualElement element)
                element.ReleasePointer(PointerId.mousePointerId);
        }

        public void Dispose()
        {
            Ready = false;
            Blur();
            _session?.Dispose();
            _session = null;
            if (_host) Object.DestroyImmediate(_host);
            if (Settings) Object.DestroyImmediate(Settings);
            if (Texture) { Texture.Release(); Object.DestroyImmediate(Texture); }
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _host = null;
            _document = null;
            Settings = null;
            Texture = null;
        }
    }
}
