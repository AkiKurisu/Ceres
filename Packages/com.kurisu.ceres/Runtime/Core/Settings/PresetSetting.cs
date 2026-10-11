using System;
using System.Collections.Generic;
using System.Linq;
using R3;

namespace Ceres.Settings
{
    /// <summary>
    /// A choice whose options assign values to other settings; its value is the first preset that matches
    /// </summary>
    public sealed class PresetSetting : Setting, IChoiceSetting
    {
        internal interface IAssignment
        {
            Setting Target { get; }

            bool Matches();

            void Apply();
        }

        private sealed class Assignment<T> : IAssignment
        {
            private readonly ValueSetting<T> _target;

            private readonly T _value;

            public Assignment(ValueSetting<T> target, T value)
            {
                _target = target;
                _value = value;
            }

            public Setting Target => _target;

            public bool Matches() => EqualityComparer<T>.Default.Equals(_target.Value, _value);

            public void Apply() => _target.Value = _value;
        }

        public sealed class Preset
        {
            internal readonly List<IAssignment> Assignments = new();

            public Preset Set<T>(ValueSetting<T> setting, T value)
            {
                Assignments.Add(new Assignment<T>(setting, value));
                return this;
            }
        }

        private readonly List<ChoiceOption> _options = new();

        private readonly List<Preset> _presets = new();

        private Observable<Unit> _changed = Observable.Empty<Unit>();

        public IReadOnlyList<ChoiceOption> Options => _options;

        public string UnmatchedLabel { get; }

        public SettingApplyMode ApplyMode => SettingApplyMode.Immediate;

        public override Observable<Unit> Changed => _changed;

        public PresetSetting(string id, string label, string customLabel) : base(id, label)
        {
            UnmatchedLabel = customLabel;
        }

        public PresetSetting Option(string label, Action<Preset> build, Func<SettingEditState> condition = null)
        {
            var preset = new Preset();
            build(preset);
            _presets.Add(preset);
            _options.Add(new ChoiceOption(label, condition));
            _changed = Observable.Merge(_presets.SelectMany(p => p.Assignments).Select(a => a.Target).Distinct()
                .Select(target => target.Changed).ToArray());
            return this;
        }

        public int SelectedIndex
        {
            get
            {
                for (int i = 0; i < _presets.Count; i++)
                {
                    if (_presets[i].Assignments.Where(IsEditable).All(a => a.Matches())) return i;
                }
                return -1;
            }
            set
            {
                if (value < 0 || value >= _presets.Count) return;
                foreach (var assignment in _presets[value].Assignments.Where(IsEditable))
                {
                    assignment.Apply();
                }
            }
        }

        // A setting the player cannot edit must not keep a preset from matching.
        private static bool IsEditable(IAssignment assignment)
        {
            return assignment.Target.EvaluateEditState().IsEnabled;
        }
    }
}
