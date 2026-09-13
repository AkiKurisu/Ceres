using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.UIElements
{
    public sealed class UIToolkitPreviewWindow : EditorWindow
    {
        private const string Title = "UI Toolkit Preview";

        private IReadOnlyList<IUIToolkitPreviewProvider> _providers = Array.Empty<IUIToolkitPreviewProvider>();
        private IUIToolkitPreviewProvider _provider;
        private UIToolkitPreviewFixture _fixture;
        private UIToolkitPreviewViewport _viewport;
        private VisualElement _stage;
        private VisualElement _viewportFrame;
        private VisualElement _previewRoot;
        private UIToolkitRuntimePreview _runtime;
        private IMGUIContainer _canvas;
        private VisualElement _safeArea;
        private VisualElement _boundsOverlay;
        private PopupField<string> _providerField;
        private PopupField<string> _fixtureField;
        private PopupField<string> _viewportField;
        private Toggle _lightBackground;
        private Toggle _showBounds;
        private bool _ready;
        private bool _rebuildQueued;
        private double _nextDependencyCheck;
        private string _dependencyStamp;
        [SerializeField] private string _selectedProvider;
        [SerializeField] private string _selectedFixture;
        [SerializeField] private string _selectedViewport;
        [SerializeField] private bool _fit = true;
        private double _nextRepaint;

        internal static UIToolkitPreviewWindow Instance { get; private set; }
        public bool Ready => _ready && _runtime?.Ready == true && _previewRoot?.panel != null &&
                             _previewRoot.layout.width > 0 && _previewRoot.layout.height > 0;
        public VisualElement PreviewRoot => _previewRoot;
        public PanelSettings RuntimeSettings => _runtime?.Settings;
        public string PanelSettingsPath => AssetDatabase.GetAssetPath(_provider?.PanelSettings);
        public string ProviderId => _provider?.Id ?? _selectedProvider;
        public string FixtureId => _fixture?.Id ?? _selectedFixture;
        public string ViewportId => _viewport?.Id ?? _selectedViewport;
        internal IReadOnlyList<string> SnapshotElementNames => _runtime?.SnapshotElementNames ?? Array.Empty<string>();

        [MenuItem("Tools/Ceres/UI Toolkit Preview", false, 50)]
        public static void OpenFromMenu()
        {
            Open(null, null, null);
        }

        public static UIToolkitPreviewWindow Open(string providerId, string fixtureId, string viewportId)
        {
            UIToolkitPreviewWindow window = Instance;
            if (window == null)
            {
                window = CreateInstance<UIToolkitPreviewWindow>();
                window.titleContent = new GUIContent(Title);
                window.minSize = new Vector2(640, 420);
                window.position = new Rect(140, 80, 1280, 800);
                window.ShowUtility();
            }
            window.Configure(providerId, fixtureId, viewportId);
            return window;
        }

        private void OnEnable()
        {
            Instance = this;
            EditorApplication.update += CheckImportedAssets;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseRuntime;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= CheckImportedAssets;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseRuntime;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ReleaseRuntime();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void CreateGUI()
        {
            BuildChrome();
            Configure(ProviderId, FixtureId, ViewportId);
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            ReleaseRuntime();
            if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode) QueueRebuild();
        }

        private void OnLostFocus() => _runtime?.Blur();

        private void ReleaseRuntime()
        {
            _ready = false;
            _runtime?.Dispose();
            _runtime = null;
            _previewRoot = null;
            _canvas = null;
            _safeArea = null;
            _boundsOverlay = null;
            _viewportFrame?.Clear();
        }

        internal void Configure(string providerId, string fixtureId, string viewportId)
        {
            _providers = UIToolkitPreviewRegistry.GetProviders();
            if (_providers.Count == 0 || _stage == null)
            {
                ReleaseRuntime();
                return;
            }

            _provider = _providers.FirstOrDefault(candidate => candidate.Id == providerId) ?? _providers[0];
            _fixture = _provider.Fixtures.FirstOrDefault(candidate => candidate.Id == fixtureId) ?? _provider.Fixtures.FirstOrDefault();
            _viewport = _provider.Viewports.FirstOrDefault(candidate => candidate.Id == viewportId) ?? _provider.Viewports.FirstOrDefault();
            if (_fixture == null || _viewport == null)
            {
                ReleaseRuntime();
                return;
            }
            _selectedProvider = _provider.Id;
            _selectedFixture = _fixture.Id;
            _selectedViewport = _viewport.Id;

            RebindChoices();
            Rebuild();
        }

        internal void Rebuild()
        {
            _rebuildQueued = false;
            ReleaseRuntime();
            _dependencyStamp = GetDependencyStamp();
            if (_provider?.VisualTreeAsset == null || _fixture == null || _viewport == null || _viewportFrame == null)
            {
                return;
            }

            _viewportFrame.style.width = _viewport.Width;
            _viewportFrame.style.height = _viewport.Height;
            _viewportFrame.EnableInClassList("ceres-preview-light", _lightBackground?.value == true);

            try { _runtime = new UIToolkitRuntimePreview(_provider, _fixture, _viewport); }
            catch (Exception error)
            {
                _viewportFrame.Add(new HelpBox(error.Message, HelpBoxMessageType.Error));
                Debug.LogException(error);
                return;
            }
            _previewRoot = _runtime.Root;
            _canvas = new IMGUIContainer(DrawRuntimeCanvas) { focusable = true };
            _canvas.style.position = Position.Absolute;
            _canvas.style.left = _canvas.style.top = _canvas.style.right = _canvas.style.bottom = 0;
            _viewportFrame.Add(_canvas);

            Rect safe = _viewport.SafeArea;
            _safeArea = new VisualElement { name = "preview-safe-area", pickingMode = PickingMode.Ignore };
            _safeArea.AddToClassList("ceres-preview-safe-area");
            _safeArea.style.left = safe.x;
            _safeArea.style.bottom = safe.y;
            _safeArea.style.width = safe.width;
            _safeArea.style.height = safe.height;
            _viewportFrame.Add(_safeArea);

            _boundsOverlay = new VisualElement { pickingMode = PickingMode.Ignore };
            _boundsOverlay.AddToClassList("ceres-preview-bounds-overlay");
            _viewportFrame.Add(_boundsOverlay);

            ApplyBounds();
            VisualElement builtRoot = _previewRoot;
            rootVisualElement.schedule.Execute(() =>
            {
                if (_previewRoot != builtRoot) return;
                UpdateScale();
                _ready = true;
                ApplyBounds();
            });
        }

        private void DrawRuntimeCanvas()
        {
            if (_runtime == null) return;
            Event evt = Event.current;
            if (evt.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0, 0, _viewport.Width, _viewport.Height), _runtime.Texture, ScaleMode.StretchToFill, true);
                return;
            }
            if (evt.isMouse || evt.isKey || evt.type == EventType.ScrollWheel ||
                evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand)
            {
                if (evt.type == EventType.MouseDown) { _canvas.Focus(); _canvas.CaptureMouse(); }
                _runtime.Send(evt);
                if (evt.type == EventType.MouseUp) _canvas.ReleaseMouse();
                evt.Use();
                Repaint();
            }
        }

        internal bool WatchesAny(IEnumerable<string> paths)
        {
            if (_provider == null)
            {
                return false;
            }

            var watched = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                AssetDatabase.GetAssetPath(_provider.VisualTreeAsset),
                AssetDatabase.GetAssetPath(_provider.PanelSettings)
            };
            foreach (StyleSheet styleSheet in _provider.StyleSheets ?? Array.Empty<StyleSheet>())
            {
                watched.Add(AssetDatabase.GetAssetPath(styleSheet));
            }
            return paths.Any(watched.Contains);
        }

        internal void QueueRebuild()
        {
            _rebuildQueued = true;
        }

        private void CheckImportedAssets()
        {
            if (_provider == null || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (_runtime != null && EditorApplication.timeSinceStartup >= _nextRepaint)
            {
                _nextRepaint = EditorApplication.timeSinceStartup + 1.0 / 30;
                _runtime.Draw();
                Repaint();
            }
            if (!_rebuildQueued && EditorApplication.timeSinceStartup < _nextDependencyCheck) return;
            _nextDependencyCheck = EditorApplication.timeSinceStartup + 0.25;
            if (_rebuildQueued || GetDependencyStamp() != _dependencyStamp) Rebuild();
        }

        private string GetDependencyStamp()
        {
            if (_provider == null) return string.Empty;
            // Background imports can complete before the Editor dispatches its postprocess notification.
            var paths = new[] { AssetDatabase.GetAssetPath(_provider.VisualTreeAsset), AssetDatabase.GetAssetPath(_provider.PanelSettings) }
                .Concat((_provider.StyleSheets ?? Array.Empty<StyleSheet>()).Select(AssetDatabase.GetAssetPath))
                .Where(path => !string.IsNullOrEmpty(path));
            return string.Join("|", paths.Select(path => path + ":" + AssetDatabase.GetAssetDependencyHash(path)));
        }

        internal VisualElement FindPreviewElement(string elementName)
        {
            return _previewRoot?.Q(elementName);
        }

        internal Rect WindowBounds(VisualElement element)
        {
            Rect rect = element.worldBound;
            float scale = _runtime.Settings.scale;
            return new Rect(_viewportFrame.LocalToWorld(rect.position * scale), rect.size * scale * _viewportFrame.resolvedStyle.scale.value.x);
        }

        public Vector2 PanelToWindow(Vector2 point) => _viewportFrame.LocalToWorld(point * _runtime.Settings.scale);

        private void BuildChrome()
        {
            rootVisualElement.Clear();
            StyleSheet chromeStyle = Resources.Load<StyleSheet>("CeresUIToolkitPreview");
            if (chromeStyle != null)
            {
                rootVisualElement.styleSheets.Add(chromeStyle);
            }

            var toolbar = new Toolbar { name = "preview-toolbar" };
            toolbar.AddToClassList("ceres-preview-toolbar");
            var selectors = new VisualElement { name = "preview-selectors" };
            selectors.AddToClassList("ceres-preview-selectors");
            _providerField = new PopupField<string>("Surface", new List<string> { "None" }, 0);
            _fixtureField = new PopupField<string>("State", new List<string> { "None" }, 0);
            _viewportField = new PopupField<string>("Viewport", new List<string> { "None" }, 0);
            _providerField.AddToClassList("ceres-preview-selector");
            _providerField.AddToClassList("ceres-preview-selector--surface");
            _fixtureField.AddToClassList("ceres-preview-selector");
            _fixtureField.AddToClassList("ceres-preview-selector--state");
            _viewportField.AddToClassList("ceres-preview-selector");
            _viewportField.AddToClassList("ceres-preview-selector--viewport");
            _providerField.RegisterValueChangedCallback(_ => Configure(ProviderIdForLabel(_providerField.value), null, null));
            _fixtureField.RegisterValueChangedCallback(_ => Configure(_provider?.Id, FixtureIdForLabel(_fixtureField.value), _viewport?.Id));
            _viewportField.RegisterValueChangedCallback(_ => Configure(_provider?.Id, _fixture?.Id, ViewportIdForLabel(_viewportField.value)));
            selectors.Add(_providerField);
            selectors.Add(_fixtureField);
            selectors.Add(_viewportField);
            toolbar.Add(selectors);

            var viewOptions = new VisualElement { name = "preview-view-options" };
            viewOptions.AddToClassList("ceres-preview-view-options");
            var viewLabel = new Label("View");
            viewLabel.AddToClassList("ceres-preview-view-label");
            viewOptions.Add(viewLabel);
            var fit = new ToolbarToggle { text = "Fit", value = _fit, tooltip = "Fit the final texture; disable for a 1:1 pixel review." };
            fit.RegisterValueChangedCallback(evt => { _fit = evt.newValue; UpdateScale(); });
            viewOptions.Add(fit);

            _lightBackground = new ToolbarToggle { text = "Light", tooltip = "Use a light preview surround." };
            _lightBackground.RegisterValueChangedCallback(_ => Rebuild());
            viewOptions.Add(_lightBackground);
            _showBounds = new ToolbarToggle { text = "Bounds", tooltip = "Outline the safe area and named elements." };
            _showBounds.RegisterValueChangedCallback(_ => ApplyBounds());
            viewOptions.Add(_showBounds);
            var refresh = new ToolbarButton(Rebuild) { text = "Refresh", tooltip = "Rebuild the active preview." };
            refresh.AddToClassList("ceres-preview-refresh");
            viewOptions.Add(refresh);
            toolbar.Add(viewOptions);
            rootVisualElement.Add(toolbar);

            _stage = new VisualElement { name = "preview-stage" };
            _stage.AddToClassList("ceres-preview-stage");
            _stage.RegisterCallback<GeometryChangedEvent>(_ => UpdateScale());
            rootVisualElement.Add(_stage);
            _viewportFrame = new VisualElement { name = "preview-viewport" };
            _viewportFrame.AddToClassList("ceres-preview-viewport");
            _stage.Add(_viewportFrame);
        }

        private void RebindChoices()
        {
            SetChoices(_providerField, _providers.Select(candidate => candidate.DisplayName), _provider.DisplayName);
            SetChoices(_fixtureField, _provider.Fixtures.Select(candidate => candidate.DisplayName), _fixture.DisplayName);
            SetChoices(_viewportField, _provider.Viewports.Select(candidate => candidate.DisplayName), _viewport.DisplayName);
        }

        private static void SetChoices(PopupField<string> field, IEnumerable<string> choices, string value)
        {
            field.choices = choices.ToList();
            field.SetValueWithoutNotify(value);
        }

        private string ProviderIdForLabel(string label) => _providers.FirstOrDefault(candidate => candidate.DisplayName == label)?.Id;
        private string FixtureIdForLabel(string label) => _provider?.Fixtures.FirstOrDefault(candidate => candidate.DisplayName == label)?.Id;
        private string ViewportIdForLabel(string label) => _provider?.Viewports.FirstOrDefault(candidate => candidate.DisplayName == label)?.Id;

        private void UpdateScale()
        {
            if (_viewport == null || _viewportFrame == null || _stage == null)
            {
                return;
            }
            float availableWidth = Mathf.Max(1, _stage.resolvedStyle.width - 32);
            float availableHeight = Mathf.Max(1, _stage.resolvedStyle.height - 32);
            float scale = _fit ? Mathf.Min(availableWidth / _viewport.Width, availableHeight / _viewport.Height, 1f) : 1;
            _viewportFrame.style.scale = new Scale(new Vector3(scale, scale, 1));
            _viewportFrame.style.left = (_stage.resolvedStyle.width - _viewport.Width * scale) * 0.5f;
            _viewportFrame.style.top = (_stage.resolvedStyle.height - _viewport.Height * scale) * 0.5f;
        }

        private void ApplyBounds()
        {
            if (_previewRoot == null || _boundsOverlay == null)
            {
                return;
            }
            bool enabled = _showBounds?.value == true;
            _safeArea.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
            _boundsOverlay.Clear();
            if (!enabled) return;
            _previewRoot.Query<VisualElement>().ForEach(element =>
            {
                if (string.IsNullOrEmpty(element.name) || element.resolvedStyle.display == DisplayStyle.None) return;
                Rect rect = element.worldBound;
                rect = new Rect(rect.position * _runtime.Settings.scale, rect.size * _runtime.Settings.scale);
                if (float.IsNaN(rect.width) || rect.width <= 0 || rect.height <= 0) return;
                var outline = new VisualElement { pickingMode = PickingMode.Ignore };
                outline.AddToClassList("ceres-preview-element-bound");
                outline.style.left = rect.x;
                outline.style.top = rect.y;
                outline.style.width = rect.width;
                outline.style.height = rect.height;
                _boundsOverlay.Add(outline);
            });
        }
    }
}
