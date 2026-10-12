using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Settings.UI
{
    /// <summary>
    /// Process-wide owner of the settings window; survives scene loads and keeps the scene rendering behind it
    /// </summary>
    public sealed class SettingsWindowHost : MonoBehaviour
    {
        [SerializeField, Tooltip("Document whose source asset is the product settings window UXML.")]
        private UIDocument document;

        [SerializeField]
        private SettingsWindowTemplates templates = new();

        [SerializeField, Tooltip("Panel sorting order; keep it above every scene UI.")]
        private int sortingOrder = 100;

        private SettingsWindowView _view;

        private PanelSettings _panelSettings;

        private Vector2 _screen;

        private Rect _safeArea;

        /// <summary>
        /// Runtime-composed wording; assign before the first <see cref="Open"/>
        /// </summary>
        public SettingsWindowText Text { get; set; } = new();

        /// <summary>
        /// Panel scale for a screen size in pixels
        /// </summary>
        public Func<Vector2, float> Scale { get; set; } = screen => Mathf.Clamp(Mathf.Min(screen.x / 1920f, screen.y / 1080f), 1f, 2f);

        /// <summary>
        /// Touch layout; defaults to the device's Touch trait
        /// </summary>
        public bool Touch { get; set; }

        /// <summary>
        /// Images for setting preview keys; without one the window shows no previews
        /// </summary>
        public ISettingsPreviewSource PreviewSource { get; set; }

        public bool IsOpen => _view is { IsOpen: true };

        public SettingsWindowView View => _view;

        public event Action<bool> OpenChanged;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            _panelSettings = Instantiate(document.panelSettings);
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.sortingOrder = sortingOrder;
            document.panelSettings = _panelSettings;
            Touch = SettingsTraits.Has(SettingsTraits.Touch);
        }

        // The document builds its tree when enabled; binding hides it until the first Open.
        private void Start()
        {
            _view ??= new SettingsWindowView(document.rootVisualElement, templates, Text, Close);
        }

        public void Open(string pageId = null)
        {
            Open(SettingsRegistry.Get(), pageId);
        }

        public void Open(SettingsRegistry registry, string pageId = null)
        {
            _view ??= new SettingsWindowView(document.rootVisualElement, templates, Text, Close);
            bool wasOpen = IsOpen;
            _view.SetTouch(Touch);
            _view.PreviewSource = PreviewSource;
            UpdateLayout(true);
            _view.Open(registry, pageId);
            if (!wasOpen) OpenChanged?.Invoke(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            _view.Close();
            OpenChanged?.Invoke(false);
        }

        /// <summary>
        /// Revert an unconfirmed change, otherwise close
        /// </summary>
        public void Cancel()
        {
            _view?.Cancel();
        }

        /// <summary>
        /// Whether a screen point in pixels, origin bottom-left, lies over the open window
        /// </summary>
        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            if (!IsOpen || document.rootVisualElement.panel == null) return false;
            var panelPoint = RuntimePanelUtils.ScreenToPanel(document.rootVisualElement.panel,
                new Vector2(screenPoint.x, Screen.height - screenPoint.y));
            return document.rootVisualElement.panel.Pick(panelPoint) != null;
        }

        private void Update()
        {
            if (!IsOpen) return;
            UpdateLayout(false);
            _view.Update();
        }

        private void UpdateLayout(bool force)
        {
            var screen = new Vector2(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (!force && screen == _screen && safeArea == _safeArea) return;
            _screen = screen;
            _safeArea = safeArea;
            float scale = Scale(screen);
            _panelSettings.scale = scale;
            var panelSafeArea = new Rect(safeArea.x / scale, (screen.y - safeArea.yMax) / scale, safeArea.width / scale, safeArea.height / scale);
            _view.SetSafeArea(screen / scale, panelSafeArea);
        }

        private void OnApplicationQuit()
        {
            Close();
        }

        private void OnDestroy()
        {
            Close();
            _view?.Dispose();
        }
    }
}
