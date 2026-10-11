using System;

namespace Ceres.Settings
{
    public enum SettingEditKind
    {
        Enabled,
        Disabled,
        Hidden
    }

    /// <summary>
    /// Whether a setting or option can be edited, with the player-facing reason when it cannot
    /// </summary>
    public readonly struct SettingEditState
    {
        public SettingEditKind Kind { get; }

        public string Reason { get; }

        private SettingEditState(SettingEditKind kind, string reason)
        {
            Kind = kind;
            Reason = reason;
        }

        public bool IsEnabled => Kind == SettingEditKind.Enabled;

        public bool IsVisible => Kind != SettingEditKind.Hidden;

        public static SettingEditState Enabled => default;

        public static SettingEditState Hidden => new(SettingEditKind.Hidden, null);

        public static SettingEditState Disabled(string reason) => new(SettingEditKind.Disabled, reason);

        public static SettingEditState When(bool enabled, string disabledReason)
        {
            return enabled ? Enabled : Disabled(disabledReason);
        }

        public static SettingEditState VisibleWhen(bool visible)
        {
            return visible ? Enabled : Hidden;
        }

        internal static SettingEditState Combine(SettingEditState current, SettingEditState next)
        {
            if (current.Kind == SettingEditKind.Hidden || next.Kind == SettingEditKind.Enabled) return current;
            if (next.Kind == SettingEditKind.Hidden || current.Kind == SettingEditKind.Enabled) return next;
            return Disabled(string.Concat(current.Reason, Environment.NewLine, next.Reason));
        }
    }

    public enum SettingImpactLevel
    {
        None,
        Low,
        Medium,
        High
    }

    public readonly struct SettingImpact
    {
        public SettingImpactLevel Gpu { get; }

        public SettingImpactLevel Vram { get; }

        public SettingImpactLevel Cpu { get; }

        public SettingImpact(SettingImpactLevel gpu, SettingImpactLevel vram = SettingImpactLevel.None,
            SettingImpactLevel cpu = SettingImpactLevel.None)
        {
            Gpu = gpu;
            Vram = vram;
            Cpu = cpu;
        }

        public bool IsDeclared => Gpu != SettingImpactLevel.None || Vram != SettingImpactLevel.None || Cpu != SettingImpactLevel.None;
    }
}
