using System;
using System.Collections.Generic;
using Ceres.Configs;
using R3;

namespace Ceres.Settings
{
    public enum SettingApplyMode
    {
        Immediate,
        Confirm
    }

    public abstract class Setting
    {
        private readonly List<Func<SettingEditState>> _conditions = new();

        public string Id { get; }

        public string Label { get; }

        public string Description { get; internal set; }

        public SettingImpact Impact { get; internal set; }

        public bool RequiresRestart { get; internal set; }

        public SettingsSection Section { get; internal set; }

        protected Setting(string id, string label)
        {
            Id = id;
            Label = label;
        }

        /// <summary>
        /// Notifies any value or availability change the view should re-read
        /// </summary>
        public abstract Observable<Unit> Changed { get; }

        /// <summary>
        /// Settings shown inside this setting's row rather than as rows of their own
        /// </summary>
        public virtual IEnumerable<Setting> Companions => Array.Empty<Setting>();

        internal void AddCondition(Func<SettingEditState> condition)
        {
            _conditions.Add(condition);
        }

        public SettingEditState EvaluateEditState()
        {
            var state = SettingEditState.Enabled;
            foreach (var condition in _conditions)
            {
                state = SettingEditState.Combine(state, condition());
            }
            return state;
        }
    }

    internal interface IValueSetting
    {
        SettingApplyMode ApplyMode { get; set; }

        bool Resettable { get; set; }

        ConfigBase Owner { get; }

        ISettingSnapshot Capture();

        void ResetToDefault();
    }

    internal interface ISettingSnapshot
    {
        IValueSetting Setting { get; }

        bool IsModified { get; }

        void Restore();
    }

    public abstract class ValueSetting<T> : Setting, IValueSetting
    {
        private sealed class Snapshot : ISettingSnapshot
        {
            private readonly ValueSetting<T> _setting;

            private readonly T _value;

            public Snapshot(ValueSetting<T> setting)
            {
                _setting = setting;
                _value = setting.Value;
            }

            public IValueSetting Setting => _setting;

            public bool IsModified => !Comparer.Equals(_setting.Value, _value);

            public void Restore()
            {
                if (!Comparer.Equals(_setting.Value, _value)) _setting._setter(_value);
            }
        }

        protected static readonly EqualityComparer<T> Comparer = EqualityComparer<T>.Default;

        private Func<T> _getter;

        private Action<T> _setter;

        private Observable<Unit> _changed = Observable.Empty<Unit>();

        public T Default { get; private set; }

        public ConfigBase Owner { get; private set; }

        public SettingApplyMode ApplyMode { get; internal set; }

        public bool Resettable { get; internal set; } = true;

        public override Observable<Unit> Changed => _changed;

        protected ValueSetting(string id, string label) : base(id, label)
        {
        }

        public T Value
        {
            get => _getter();
            set
            {
                value = Coerce(value);
                if (Comparer.Equals(_getter(), value)) return;
                _setter(value);
            }
        }

        protected virtual T Coerce(T value)
        {
            return value;
        }

        internal T CoerceValue(T value)
        {
            return Coerce(value);
        }

        /// <summary>
        /// Bind to a reactive property of a config; the default is read from a newly constructed config
        /// </summary>
        public void Bind<TConfig>(Func<TConfig, ReactiveProperty<T>> selector) where TConfig : Config<TConfig>, new()
        {
            var property = selector(Config<TConfig>.Get());
            Bind(() => property.Value, value => property.Value = value, property.AsUnitObservable(),
                selector(new TConfig()).Value, Config<TConfig>.Get());
        }

        public void Bind(Func<T> getter, Action<T> setter, Observable<Unit> changed, T defaultValue, ConfigBase owner)
        {
            _getter = getter;
            _setter = setter;
            _changed = changed;
            Default = defaultValue;
            Owner = owner;
        }

        public void ResetToDefault()
        {
            Value = Default;
        }

        SettingApplyMode IValueSetting.ApplyMode
        {
            get => ApplyMode;
            set => ApplyMode = value;
        }

        bool IValueSetting.Resettable
        {
            get => Resettable;
            set => Resettable = value;
        }

        ISettingSnapshot IValueSetting.Capture()
        {
            return new Snapshot(this);
        }
    }
}
