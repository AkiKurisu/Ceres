using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ceres.Settings.UI
{
    /// <summary>
    /// A product control that selects one of a list of labeled options; the value is the selected index or -1
    /// </summary>
    public interface ISettingsChoiceControl : INotifyValueChanged<int>
    {
        /// <summary>
        /// Text shown while the value matches no option
        /// </summary>
        string UnmatchedText { get; set; }

        void SetChoices(IReadOnlyList<string> labels);

        void SetOptionState(int index, bool visible, bool enabled);
    }

    /// <summary>
    /// A choice control that reports the option under the pointer, so the window can preview it before it is chosen
    /// </summary>
    public interface ISettingsChoiceHighlight
    {
        /// <summary>
        /// Raised with the highlighted option index, or -1 when no option is highlighted
        /// </summary>
        event Action<int> Highlighted;
    }
}
