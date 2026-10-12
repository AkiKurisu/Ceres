using System;

namespace Ceres.Settings
{
    public static class SettingExtensions
    {
        public static TSetting Describe<TSetting>(this TSetting setting, string description) where TSetting : Setting
        {
            setting.Description = description;
            return setting;
        }

        public static TSetting Cost<TSetting>(this TSetting setting, SettingImpactLevel gpu,
            SettingImpactLevel vram = SettingImpactLevel.None, SettingImpactLevel cpu = SettingImpactLevel.None)
            where TSetting : Setting
        {
            setting.Impact = new SettingImpact(gpu, vram, cpu);
            return setting;
        }

        public static TSetting WithPreview<TSetting>(this TSetting setting, string key) where TSetting : Setting
        {
            setting.Preview = key;
            return setting;
        }

        public static TSetting RequireRestart<TSetting>(this TSetting setting) where TSetting : Setting
        {
            setting.RequiresRestart = true;
            return setting;
        }

        public static TSetting When<TSetting>(this TSetting setting, Func<SettingEditState> condition) where TSetting : Setting
        {
            setting.AddCondition(condition);
            return setting;
        }

        /// <summary>
        /// Hide the setting unless the device has the trait
        /// </summary>
        public static TSetting WhenTrait<TSetting>(this TSetting setting, string trait) where TSetting : Setting
        {
            setting.AddCondition(() => SettingEditState.VisibleWhen(SettingsTraits.Has(trait)));
            return setting;
        }

        /// <summary>
        /// Stage changes until applied and revert them unless kept in time
        /// </summary>
        public static TSetting Confirmed<TSetting>(this TSetting setting) where TSetting : Setting
        {
            var value = AsValueSetting(setting);
            value.ApplyMode = SettingApplyMode.Confirm;
            value.Resettable = false;
            return setting;
        }

        public static TSetting NotResettable<TSetting>(this TSetting setting) where TSetting : Setting
        {
            AsValueSetting(setting).Resettable = false;
            return setting;
        }

        private static IValueSetting AsValueSetting(Setting setting)
        {
            return setting as IValueSetting
                   ?? throw new ArgumentException($"[Ceres] Setting '{setting.Id}' has no value.");
        }

        public static SliderSetting Formatted(this SliderSetting setting, Func<float, string> format)
        {
            setting.Format = format;
            return setting;
        }

        /// <summary>
        /// Show a toggle inside the slider's row; add the slider to a section after attaching it
        /// </summary>
        public static SliderSetting WithToggle(this SliderSetting setting, ToggleSetting toggle)
        {
            setting.Toggle = toggle;
            return setting;
        }

        public static ChoiceSetting<T> Unmatched<T>(this ChoiceSetting<T> setting, string label)
        {
            setting.UnmatchedLabel = label;
            return setting;
        }
    }
}
