using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Settings.UI
{
    /// <summary>
    /// Product-owned UXML templates the window instantiates for pages, sections and setting rows
    /// </summary>
    [Serializable]
    public sealed class SettingsWindowTemplates
    {
        public const string TabName = "settings-tab";
        public const string SectionName = "settings-section";
        public const string ImpactNameName = "settings-impact-name";
        public const string ImpactLevelName = "settings-impact-level";
        public const string RowName = "settings-row";
        public const string RowLabelName = "settings-row-label";
        public const string RowDetailName = "settings-row-detail";
        public const string RowToggleName = "settings-row-toggle";
        public const string RowSliderName = "settings-row-slider";
        public const string RowValueName = "settings-row-value";
        public const string RowChoiceName = "settings-row-choice";
        public const string RowButtonName = "settings-row-button";

        [Tooltip("A Button named settings-tab.")]
        public VisualTreeAsset tab;

        [Tooltip("A Label named settings-section.")]
        public VisualTreeAsset section;

        [Tooltip("Labels named settings-impact-name and settings-impact-level.")]
        public VisualTreeAsset impact;

        [Tooltip("Row with a Toggle named settings-row-toggle.")]
        public VisualTreeAsset toggle;

        [Tooltip("Row with a Slider named settings-row-slider and an optional Label named settings-row-value.")]
        public VisualTreeAsset slider;

        [Tooltip("Row with a choice control named settings-row-choice, for a few short options.")]
        public VisualTreeAsset choice;

        [Tooltip("Row with a choice control named settings-row-choice, for many or long options.")]
        public VisualTreeAsset choiceList;

        [Tooltip("Row with a Button named settings-row-button.")]
        public VisualTreeAsset action;

        /// <summary>
        /// Option count and label length up to which a choice uses <see cref="choice"/> rather than <see cref="choiceList"/>
        /// </summary>
        [NonSerialized]
        public int ShortChoiceCount = 4;

        [NonSerialized]
        public int ShortChoiceLength = 8;

        internal static T Require<T>(VisualElement root, string name, VisualTreeAsset template) where T : class
        {
            return root.Q(name) as T
                   ?? throw new InvalidOperationException($"[Ceres] Settings template '{template.name}' requires {typeof(T).Name} '{name}'.");
        }
    }
}
