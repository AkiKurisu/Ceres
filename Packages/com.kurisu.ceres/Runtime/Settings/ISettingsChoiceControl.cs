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
}
