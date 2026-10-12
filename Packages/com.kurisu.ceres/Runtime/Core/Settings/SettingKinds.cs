using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace Ceres.Settings
{
    public sealed class ToggleSetting : ValueSetting<bool>
    {
        public ToggleSetting(string id, string label) : base(id, label)
        {
        }
    }

    public sealed class SliderSetting : ValueSetting<float>
    {
        public float Min { get; }

        public float Max { get; }

        public float Step { get; }

        public Func<float, string> Format { get; internal set; }

        public ToggleSetting Toggle { get; internal set; }

        public override IEnumerable<Setting> Companions => Toggle != null ? new Setting[] { Toggle } : Array.Empty<Setting>();

        public SliderSetting(string id, string label, float min, float max, float step = 0) : base(id, label)
        {
            Min = min;
            Max = max;
            Step = step;
            Format = value => value.ToString("0.##");
        }

        protected override float Coerce(float value)
        {
            value = Mathf.Clamp(value, Min, Max);
            if (Step > 0) value = Min + Mathf.Round((value - Min) / Step) * Step;
            return Mathf.Clamp(value, Min, Max);
        }
    }

    public sealed class ChoiceOption
    {
        private readonly Func<SettingEditState> _condition;

        public string Label { get; }

        /// <summary>
        /// Key of the image the window shows for this option, or null to show the setting's
        /// </summary>
        public string Preview { get; }

        public ChoiceOption(string label, Func<SettingEditState> condition = null, string preview = null)
        {
            Label = label;
            _condition = condition;
            Preview = preview;
        }

        public SettingEditState EvaluateEditState()
        {
            return _condition?.Invoke() ?? SettingEditState.Enabled;
        }
    }

    public interface IChoiceSetting
    {
        IReadOnlyList<ChoiceOption> Options { get; }

        /// <summary>
        /// Index of the current option, or -1 when the current value matches no option
        /// </summary>
        int SelectedIndex { get; set; }

        /// <summary>
        /// Label shown when <see cref="SelectedIndex"/> is -1
        /// </summary>
        string UnmatchedLabel { get; }

        SettingApplyMode ApplyMode { get; }
    }

    internal interface IStageableChoice
    {
        void Stage(SettingsSession session, int index);

        bool TryGetStagedIndex(SettingsSession session, out int index);
    }

    public sealed class ChoiceSetting<T> : ValueSetting<T>, IChoiceSetting, IStageableChoice
    {
        private readonly List<ChoiceOption> _options = new();

        private readonly List<T> _values = new();

        public IReadOnlyList<ChoiceOption> Options => _options;

        public IReadOnlyList<T> Values => _values;

        public string UnmatchedLabel { get; internal set; } = string.Empty;

        public ChoiceSetting(string id, string label) : base(id, label)
        {
        }

        public ChoiceSetting<T> Option(T value, string label, Func<SettingEditState> condition = null, string preview = null)
        {
            _values.Add(value);
            _options.Add(new ChoiceOption(label, condition, preview));
            return this;
        }

        public void ClearOptions()
        {
            _values.Clear();
            _options.Clear();
        }

        public int SelectedIndex
        {
            get
            {
                var value = Value;
                for (int i = 0; i < _values.Count; i++)
                {
                    if (Comparer.Equals(_values[i], value)) return i;
                }
                return -1;
            }
            set
            {
                if (value < 0 || value >= _values.Count) return;
                Value = _values[value];
            }
        }

        void IStageableChoice.Stage(SettingsSession session, int index)
        {
            if (index >= 0 && index < _values.Count) session.Stage(this, _values[index]);
        }

        bool IStageableChoice.TryGetStagedIndex(SettingsSession session, out int index)
        {
            index = session.TryGetStaged(this, out var staged) ? _values.FindIndex(value => Comparer.Equals(value, staged)) : -1;
            return index >= 0;
        }
    }

    public sealed class ActionSetting : Setting
    {
        private readonly Action _action;

        public string ActionLabel { get; }

        public override Observable<Unit> Changed => Observable.Empty<Unit>();

        public ActionSetting(string id, string label, string actionLabel, Action action) : base(id, label)
        {
            ActionLabel = actionLabel;
            _action = action;
        }

        public void Invoke()
        {
            if (EvaluateEditState().IsEnabled) _action();
        }
    }
}
