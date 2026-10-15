using Ceres.Configs;
using Ceres.Serialization;
using R3;

namespace Ceres.Localization
{
    [PreferJsonConvert]
    [ConfigPath("Ceres.Localization")]
    public class LocalizationConfig : Config<LocalizationConfig>
    {
        /// <summary>
        /// Language whose tables define every key, set by the project
        /// </summary>
        public string SourceLanguage { get; set; } = "en";

        /// <summary>
        /// Language code chosen by the player, or empty to follow the system language
        /// </summary>
        public ReactiveProperty<string> Language { get; set; } = new(string.Empty);
    }
}
