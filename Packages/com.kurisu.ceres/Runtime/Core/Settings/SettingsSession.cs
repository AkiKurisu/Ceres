using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ceres.Settings
{
    /// <summary>
    /// One editing pass over a registry: records values on open, saves changed configs on close
    /// </summary>
    public sealed class SettingsSession : IDisposable
    {
        private interface IStagedValue
        {
            IValueSetting Setting { get; }

            void Apply();
        }

        private sealed class StagedValue<T> : IStagedValue
        {
            public readonly ValueSetting<T> Target;

            public readonly T Value;

            public StagedValue(ValueSetting<T> target, T value)
            {
                Target = target;
                Value = value;
            }

            public IValueSetting Setting => Target;

            public void Apply() => Target.Value = Value;
        }

        private readonly List<ISettingSnapshot> _snapshots;

        private readonly List<IStagedValue> _staged = new();

        private readonly List<ISettingSnapshot> _confirming = new();

        private float _confirmDeadline;

        private bool _closed;

        public SettingsRegistry Registry { get; }

        public float ConfirmSeconds { get; }

        public SettingsSession(SettingsRegistry registry, float confirmSeconds = 15f)
        {
            Registry = registry;
            ConfirmSeconds = confirmSeconds;
            _snapshots = registry.Settings.OfType<IValueSetting>().Select(setting => setting.Capture()).ToList();
        }

        public bool HasChanges => _snapshots.Any(snapshot => snapshot.IsModified);

        public bool HasStaged => _staged.Count > 0;

        public bool IsConfirming => _confirming.Count > 0;

        public float ConfirmRemaining => IsConfirming ? Mathf.Max(0, _confirmDeadline - Time.realtimeSinceStartup) : 0;

        /// <summary>
        /// Hold a value of a confirmed setting until <see cref="ApplyStaged"/>
        /// </summary>
        public void Stage<T>(ValueSetting<T> setting, T value)
        {
            _staged.RemoveAll(staged => staged.Setting == setting);
            value = setting.CoerceValue(value);
            if (!EqualityComparer<T>.Default.Equals(setting.Value, value))
            {
                _staged.Add(new StagedValue<T>(setting, value));
            }
        }

        public bool TryGetStaged<T>(ValueSetting<T> setting, out T value)
        {
            if (_staged.FirstOrDefault(staged => staged.Setting == setting) is StagedValue<T> typed)
            {
                value = typed.Value;
                return true;
            }
            value = default;
            return false;
        }

        public void StageChoice(IChoiceSetting setting, int index)
        {
            if (setting is IStageableChoice stageable) stageable.Stage(this, index);
        }

        public bool TryGetStagedChoice(IChoiceSetting setting, out int index)
        {
            index = -1;
            return setting is IStageableChoice stageable && stageable.TryGetStagedIndex(this, out index);
        }

        /// <summary>
        /// Apply staged values and start the confirmation countdown
        /// </summary>
        public void ApplyStaged()
        {
            if (!HasStaged) return;
            RevertConfirmation();
            foreach (var staged in _staged)
            {
                _confirming.Add(staged.Setting.Capture());
                staged.Apply();
            }
            _staged.Clear();
            _confirmDeadline = Time.realtimeSinceStartup + ConfirmSeconds;
        }

        public void Keep()
        {
            _confirming.Clear();
        }

        public void RevertConfirmation()
        {
            for (int i = _confirming.Count - 1; i >= 0; i--)
            {
                _confirming[i].Restore();
            }
            _confirming.Clear();
        }

        /// <summary>
        /// Revert a confirmation the player did not keep in time
        /// </summary>
        public void Update()
        {
            if (IsConfirming && Time.realtimeSinceStartup >= _confirmDeadline)
            {
                RevertConfirmation();
            }
        }

        public void RevertAll()
        {
            _staged.Clear();
            _confirming.Clear();
            foreach (var snapshot in _snapshots.Where(snapshot => snapshot.IsModified))
            {
                snapshot.Restore();
            }
        }

        /// <summary>
        /// Restore defaults of the page's editable immediate settings
        /// </summary>
        public void ResetPage(SettingsPage page)
        {
            foreach (var setting in page.Settings)
            {
                if (setting is not IValueSetting { Resettable: true, ApplyMode: SettingApplyMode.Immediate } value) continue;
                if (!setting.EvaluateEditState().IsEnabled) continue;
                value.ResetToDefault();
            }
        }

        /// <summary>
        /// Drop staged values, revert an unconfirmed change and save every config whose settings changed
        /// </summary>
        public void Close()
        {
            if (_closed) return;
            _closed = true;
            _staged.Clear();
            RevertConfirmation();
            foreach (var owner in _snapshots.Where(snapshot => snapshot.IsModified)
                         .Select(snapshot => snapshot.Setting.Owner).Where(owner => owner != null).Distinct())
            {
                owner.Save();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
