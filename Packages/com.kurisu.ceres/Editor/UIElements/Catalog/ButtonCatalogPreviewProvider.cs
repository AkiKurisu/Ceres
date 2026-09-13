using System.Collections.Generic;
using System.Linq;
using Ceres.UIElements;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ceres.Editor.UIElements
{
    public sealed class ButtonCatalogPreviewProvider : IUIToolkitPreviewProvider
    {
        private const string ResourcePath = "Packages/com.kurisu.ceres/Editor/UIElements/Catalog/Resources/";
        private const string ThemePath = "Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresNeutral.uss";

        public string Id => "ceres-neutral-buttons";
        public string DisplayName => "Ceres Neutral · Buttons";
        public VisualTreeAsset VisualTreeAsset => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ResourcePath + "ButtonCatalog.uxml");
        public PanelSettings PanelSettings => AssetDatabase.LoadAssetAtPath<PanelSettings>("Packages/com.kurisu.ceres/Runtime/UIElements/Themes/CeresPanelSettings.asset");
        public float GetPanelScale(UIToolkitPreviewViewport viewport) => 1;
        public IReadOnlyList<StyleSheet> StyleSheets => new[]
        {
            AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath),
            AssetDatabase.LoadAssetAtPath<StyleSheet>(ResourcePath + "ButtonCatalog.uss")
        };
        public IReadOnlyList<UIToolkitPreviewFixture> Fixtures { get; } = new[]
        {
            new UIToolkitPreviewFixture("overview", "Overview"),
            new UIToolkitPreviewFixture("disabled", "Disabled"),
            new UIToolkitPreviewFixture("long-labels", "Long labels")
        };
        public IReadOnlyList<UIToolkitPreviewViewport> Viewports => UIToolkitPreviewViewports.Standard;

        public IUIToolkitPreviewSession CreateSession(VisualElement root, UIToolkitPreviewContext context)
        {
            bool mobile = context.Viewport.Id.StartsWith("mobile-");
            root.EnableInClassList("ceres-touch", mobile);
            var catalog = root.Q("button-catalog");
            var safe = context.LogicalSafeArea;
            catalog.style.left = safe.x + 24;
            catalog.style.right = context.LogicalSize.x - safe.xMax + 24;
            catalog.style.top = context.LogicalSize.y - safe.yMax + 24;
            catalog.style.bottom = safe.y + 24;
            root.Q<Label>("catalog-standard-metric").text = mobile ? "44h / 8r" : "32h / 8r";
            root.Q<Label>("catalog-touch-note").text = mobile
                ? "Touch · Standard 44h / 8r · Compact and Prominent retain a 44px hit target"
                : "Desktop · visual surface is the hit target";

            if (context.Fixture.Id == "long-labels")
            {
                var longButton = root.Q<UIToolkitButton>("catalog-long-label");
                longButton.text = "Export selected configuration entries — Workspace / Final Revision";
                longButton.tooltip = longButton.text;
            }

            int clicks = 0;
            var scope = new UIToolkitBindingScope();
            root.Query<UIToolkitButton>().ForEach(button =>
            {
                if (context.Fixture.Id == "disabled") button.SetEnabled(false);
                scope.Click(button, () => root.Q<Label>("catalog-feedback").text = $"{button.text} · {button.Variant} / {button.Size} · Click {++clicks}");
            });
            return new UIToolkitPreviewSession(scope, root.Query<VisualElement>().ToList().Where(element => !string.IsNullOrEmpty(element.name)).Select(element => element.name));
        }
    }
}
