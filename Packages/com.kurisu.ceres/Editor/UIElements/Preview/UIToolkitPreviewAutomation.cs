using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.UIElements
{
    public static class UIToolkitPreviewAutomation
    {
        [Serializable]
        private sealed class Snapshot
        {
            public string providerId;
            public string fixtureId;
            public string viewportId;
            public Rect window;
            public string renderContext;
            public string colorSpace;
            public string panelSettings;
            public string theme;
            public string textSettings;
            public string defaultFont;
            public float panelScale;
            public Vector2 viewportPixels;
            public ElementSnapshot[] elements;
        }

        [Serializable]
        private sealed class ElementSnapshot
        {
            public string name;
            public bool found;
            public Rect worldBound;
            public Rect layout;
            public Rect panelBounds;
            public bool enabled;
            public string type;
            public string display;
            public string visibility;
            public float opacity;
            public string[] classes;
            public float fontSize;
            public string textAlign;
            public Color color;
            public Color backgroundColor;
            public float radius;
            public Vector4 padding;
            public string font;
            public string fontAsset;
            public string image;
            public string imageType;
            public Color imageTint;
        }

        public static void Open(string providerId, string fixtureId, string viewportId)
        {
            UIToolkitPreviewWindow.Open(providerId, fixtureId, viewportId);
        }

        public static void Refresh()
        {
            UIToolkitPreviewWindow.Instance?.Rebuild();
        }

        public static bool IsReady => UIToolkitPreviewWindow.Instance?.Ready == true;

        public static string GetSnapshot()
        {
            UIToolkitPreviewWindow window = UIToolkitPreviewWindow.Instance;
            return GetSnapshot(window?.SnapshotElementNames?.ToArray() ?? Array.Empty<string>());
        }

        public static string GetSnapshot(params string[] elementNames)
        {
            UIToolkitPreviewWindow window = UIToolkitPreviewWindow.Instance;
            if (window == null)
            {
                return JsonUtility.ToJson(new Snapshot { elements = Array.Empty<ElementSnapshot>() });
            }

            var snapshot = new Snapshot
            {
                providerId = window.ProviderId,
                fixtureId = window.FixtureId,
                viewportId = window.ViewportId,
                window = window.position,
                elements = (elementNames ?? Array.Empty<string>()).Select(name => Capture(window, name)).ToArray()
            };
            RenderSettings(snapshot, window.PreviewRoot, window.RuntimeSettings, window.PanelSettingsPath);
            return JsonUtility.ToJson(snapshot, true);
        }

        public static string GetRuntimeSnapshot(UIDocument document, params string[] elementNames)
        {
            if (!document) throw new ArgumentNullException(nameof(document));
            var root = document.rootVisualElement;
            var snapshot = new Snapshot
            {
                elements = (elementNames ?? Array.Empty<string>()).Select(name => Capture(root?.Q(name), name)).ToArray()
            };
            RenderSettings(snapshot, root, document.panelSettings, AssetDatabase.GetAssetPath(document.panelSettings));
            return JsonUtility.ToJson(snapshot, true);
        }

        private static void RenderSettings(Snapshot snapshot, VisualElement root, PanelSettings settings, string assetPath)
        {
            snapshot.renderContext = root?.panel?.contextType.ToString();
            snapshot.colorSpace = QualitySettings.activeColorSpace.ToString();
            if (!settings) return;
            snapshot.panelSettings = assetPath;
            snapshot.panelScale = settings.scale;
            snapshot.theme = AssetDatabase.GetAssetPath(settings.themeStyleSheet);
            snapshot.textSettings = settings.textSettings ? AssetDatabase.GetAssetPath(settings.textSettings) : "<runtime-default>";
            snapshot.defaultFont = settings.textSettings?.defaultFontAsset?.name ?? "<runtime-default>";
            snapshot.viewportPixels = root?.panel == null ? Vector2.zero : root.panel.visualTree.layout.size * settings.scale;
        }

        private static ElementSnapshot Capture(UIToolkitPreviewWindow window, string name)
        {
            VisualElement element = window.FindPreviewElement(name);
            var snapshot = Capture(element, name);
            if (element != null) snapshot.worldBound = window.WindowBounds(element);
            return snapshot;
        }

        private static ElementSnapshot Capture(VisualElement element, string name)
        {
            if (element == null)
            {
                return new ElementSnapshot { name = name, found = false, classes = Array.Empty<string>() };
            }
            return new ElementSnapshot
            {
                name = name,
                found = true,
                worldBound = element.worldBound,
                panelBounds = element.worldBound,
                layout = element.layout,
                enabled = element.enabledInHierarchy,
                type = element.GetType().FullName,
                display = element.resolvedStyle.display.ToString(),
                visibility = element.resolvedStyle.visibility.ToString(),
                opacity = element.resolvedStyle.opacity,
                classes = element.GetClasses().ToArray(),
                fontSize = element.resolvedStyle.fontSize,
                textAlign = element.resolvedStyle.unityTextAlign.ToString(),
                color = element.resolvedStyle.color,
                backgroundColor = element.resolvedStyle.backgroundColor,
                radius = element.resolvedStyle.borderTopLeftRadius,
                padding = new Vector4(element.resolvedStyle.paddingLeft, element.resolvedStyle.paddingTop,
                    element.resolvedStyle.paddingRight, element.resolvedStyle.paddingBottom),
                font = element.resolvedStyle.unityFontDefinition.font?.name ?? element.resolvedStyle.unityFont?.name,
                fontAsset = element.resolvedStyle.unityFontDefinition.fontAsset?.name,
                image = ImageName(element.resolvedStyle.backgroundImage),
                imageType = element.resolvedStyle.backgroundImage.vectorImage ? "VectorImage" : element.resolvedStyle.backgroundImage.texture ? "Texture2D" : element.resolvedStyle.backgroundImage.sprite ? "Sprite" : "None",
                imageTint = element.resolvedStyle.unityBackgroundImageTintColor
            };
        }

        private static string ImageName(Background image)
        {
            UnityEngine.Object asset = image.vectorImage ? (UnityEngine.Object)image.vectorImage : image.texture ? image.texture : image.sprite;
            if (!asset) return null;
            string path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? asset.name : path;
        }
    }
}
