using System;

namespace Ceres.Gameplay.Mod
{
    public interface IModValidator
    {
        bool ValidateMod(ModInfo modInfo);
    }

    public class APIValidator : IModValidator
    {
        private readonly Version _apiVersion;
        
        public APIValidator(string apiVersion)
        {
            if (!Version.TryParse(apiVersion, out _apiVersion))
            {
                _apiVersion = new Version(0, 1, 0);
            }
        }
        
        public bool ValidateMod(ModInfo modInfo)
        {
            if (Version.TryParse(modInfo.apiVersion, out var modVersion))
            {
                return modVersion.CompareTo(_apiVersion) == 0;
            }
            return false;
        }
    }
}