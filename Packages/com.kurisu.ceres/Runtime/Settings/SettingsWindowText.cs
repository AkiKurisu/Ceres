namespace Ceres.Settings.UI
{
    /// <summary>
    /// Wording the window composes at runtime; static text belongs to the product UXML and setting text to the registry
    /// </summary>
    public sealed class SettingsWindowText
    {
        /// <summary>
        /// Confirmation prompt; {0} is the remaining whole seconds
        /// </summary>
        public string ConfirmMessage { get; set; } = "Keep these display settings? They revert in {0} seconds.";

        public string RestartRequired { get; set; } = "Takes effect after restart.";

        public string Gpu { get; set; } = "GPU";

        public string Vram { get; set; } = "VRAM";

        public string Cpu { get; set; } = "CPU";

        public string ImpactLow { get; set; } = "Low";

        public string ImpactMedium { get; set; } = "Medium";

        public string ImpactHigh { get; set; } = "High";

        public string ImpactLevel(SettingImpactLevel level)
        {
            return level switch
            {
                SettingImpactLevel.Low => ImpactLow,
                SettingImpactLevel.Medium => ImpactMedium,
                SettingImpactLevel.High => ImpactHigh,
                _ => string.Empty
            };
        }
    }
}
