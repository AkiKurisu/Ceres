using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.UIElements
{
    [Serializable]
    public sealed class UIToolkitPreviewFixture
    {
        public string Id { get; }
        public string DisplayName { get; }

        public UIToolkitPreviewFixture(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }
    }

    [Serializable]
    public sealed class UIToolkitPreviewViewport
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Width { get; }
        public int Height { get; }
        public Rect SafeArea { get; }

        public UIToolkitPreviewViewport(string id, string displayName, int width, int height, Rect safeArea)
        {
            Id = id;
            DisplayName = displayName;
            Width = width;
            Height = height;
            SafeArea = safeArea;
        }
    }

    public readonly struct UIToolkitPreviewContext
    {
        public UIToolkitPreviewFixture Fixture { get; }
        public UIToolkitPreviewViewport Viewport { get; }
        public float PanelScale { get; }
        public Vector2 LogicalSize => new Vector2(Viewport.Width, Viewport.Height) / PanelScale;
        public Rect LogicalSafeArea => new Rect(Viewport.SafeArea.position / PanelScale, Viewport.SafeArea.size / PanelScale);

        public UIToolkitPreviewContext(UIToolkitPreviewFixture fixture, UIToolkitPreviewViewport viewport, float panelScale)
        {
            Fixture = fixture;
            Viewport = viewport;
            PanelScale = panelScale;
        }
    }

    public interface IUIToolkitPreviewProvider
    {
        string Id { get; }
        string DisplayName { get; }
        VisualTreeAsset VisualTreeAsset { get; }
        PanelSettings PanelSettings { get; }
        float GetPanelScale(UIToolkitPreviewViewport viewport);
        IReadOnlyList<StyleSheet> StyleSheets { get; }
        IReadOnlyList<UIToolkitPreviewFixture> Fixtures { get; }
        IReadOnlyList<UIToolkitPreviewViewport> Viewports { get; }
        IUIToolkitPreviewSession CreateSession(VisualElement root, UIToolkitPreviewContext context);
    }

    public interface IUIToolkitPreviewSession : IDisposable
    {
        IReadOnlyList<string> SnapshotElementNames { get; }
    }

    public sealed class UIToolkitPreviewSession : IUIToolkitPreviewSession
    {
        private IDisposable _lifetime;

        public UIToolkitPreviewSession(IDisposable lifetime, IEnumerable<string> snapshotElementNames)
        {
            _lifetime = lifetime;
            SnapshotElementNames = (snapshotElementNames ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).ToArray();
        }

        public UIToolkitPreviewSession(IDisposable lifetime, IEnumerable<UIToolkitElementDescriptor> elements)
            : this(lifetime, elements?.Select(element => element.ElementName))
        {
        }

        public IReadOnlyList<string> SnapshotElementNames { get; }

        public void Dispose()
        {
            _lifetime?.Dispose();
            _lifetime = null;
        }
    }

    public sealed class UIToolkitPreviewCase<TFixture>
    {
        public UIToolkitPreviewCase(string id, string displayName, TFixture fixture)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Fixture id must be non-empty.", nameof(id));
            Id = id;
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            Fixture = fixture;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public TFixture Fixture { get; }
    }

    public abstract class UIToolkitPreviewProvider<TFixture> : IUIToolkitPreviewProvider
    {
        private IReadOnlyList<UIToolkitPreviewFixture> _fixtures;

        public abstract string Id { get; }
        public abstract string DisplayName { get; }
        public abstract VisualTreeAsset VisualTreeAsset { get; }
        public abstract PanelSettings PanelSettings { get; }
        public abstract float GetPanelScale(UIToolkitPreviewViewport viewport);
        public abstract IReadOnlyList<StyleSheet> StyleSheets { get; }
        public abstract IReadOnlyList<UIToolkitPreviewViewport> Viewports { get; }
        protected abstract IReadOnlyList<UIToolkitPreviewCase<TFixture>> Cases { get; }

        public IReadOnlyList<UIToolkitPreviewFixture> Fixtures
        {
            get
            {
                if (_fixtures != null) return _fixtures;
                var duplicate = Cases.GroupBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
                if (duplicate != null) throw new InvalidOperationException($"Preview fixture id must be unique: '{duplicate.Key}'.");
                return _fixtures = Cases.Select(item => new UIToolkitPreviewFixture(item.Id, item.DisplayName)).ToArray();
            }
        }

        public IUIToolkitPreviewSession CreateSession(VisualElement root, UIToolkitPreviewContext context)
        {
            UIToolkitPreviewCase<TFixture> item = Cases.FirstOrDefault(candidate => candidate.Id == context.Fixture.Id);
            if (item == null) throw new InvalidOperationException($"Unknown fixture '{context.Fixture.Id}' for preview provider '{Id}'.");
            return CreateSession(root, context, item.Fixture);
        }

        protected abstract IUIToolkitPreviewSession CreateSession(VisualElement root, UIToolkitPreviewContext context, TFixture fixture);
    }

    public static class UIToolkitPreviewViewports
    {
        public static IReadOnlyList<UIToolkitPreviewViewport> Standard { get; } = new[]
        {
            new UIToolkitPreviewViewport("desktop-1920x1080", "1920 x 1080", 1920, 1080, new Rect(0, 0, 1920, 1080)),
            new UIToolkitPreviewViewport("desktop-1280x720", "1280 x 720", 1280, 720, new Rect(0, 0, 1280, 720)),
            new UIToolkitPreviewViewport("desktop-1366x768", "1366 x 768", 1366, 768, new Rect(0, 0, 1366, 768)),
            new UIToolkitPreviewViewport("desktop-1440x900", "1440 x 900", 1440, 900, new Rect(0, 0, 1440, 900)),
            new UIToolkitPreviewViewport("desktop-1600x900", "1600 x 900", 1600, 900, new Rect(0, 0, 1600, 900)),
            new UIToolkitPreviewViewport("desktop-1920x1200", "1920 x 1200", 1920, 1200, new Rect(0, 0, 1920, 1200)),
            new UIToolkitPreviewViewport("desktop-2560x1080", "2560 x 1080", 2560, 1080, new Rect(0, 0, 2560, 1080)),
            new UIToolkitPreviewViewport("desktop-2560x1440", "2560 x 1440", 2560, 1440, new Rect(0, 0, 2560, 1440)),
            new UIToolkitPreviewViewport("desktop-3440x1440", "3440 x 1440", 3440, 1440, new Rect(0, 0, 3440, 1440)),
            new UIToolkitPreviewViewport("desktop-3840x2160", "3840 x 2160", 3840, 2160, new Rect(0, 0, 3840, 2160)),
            new UIToolkitPreviewViewport("mobile-2340x1080", "2340 x 1080", 2340, 1080, new Rect(90, 0, 2160, 1080)),
            new UIToolkitPreviewViewport("mobile-2400x1080", "2400 x 1080", 2400, 1080, new Rect(96, 0, 2208, 1032)),
            new UIToolkitPreviewViewport("mobile-2532x1170", "2532 x 1170", 2532, 1170, new Rect(141, 63, 2250, 1107)),
            new UIToolkitPreviewViewport("mobile-2778x1284", "2778 x 1284", 2778, 1284, new Rect(141, 63, 2496, 1221))
        };
    }
}
