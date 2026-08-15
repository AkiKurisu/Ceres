using System.Collections.Generic;
using System.IO;
using Ceres.Resource;
using Cysharp.Threading.Tasks;

namespace Ceres.Gameplay.Mod
{
    public interface IModLoader
    {
        UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos);
    }
    
    public class ModLoader : IModLoader
    {
        private readonly ModConfig _modConfigData;
        
        private readonly IModValidator _validator;
        
        public ModLoader(ModConfig modConfigData, IModValidator validator)
        {
            _modConfigData = modConfigData;
            _validator = validator;
        }
        
        public async UniTask<bool> LoadAllModsAsync(List<ModInfo> modInfos)
        {
            string modPath = _modConfigData.LoadingPath;
            if (!Directory.Exists(modPath))
            {
                Directory.CreateDirectory(modPath);
                return true;
            }
            
            await ModAPI.UnZipAllAsync(modPath, true);
            var directories = Directory.GetDirectories(modPath, "*", SearchOption.AllDirectories);
            if (directories.Length == 0)
            {
                return true;
            }
            
            List<string> configPaths = new();
            List<string> directoryPaths = new();
            foreach (var directory in directories)
            {
                string[] files = Directory.GetFiles(directory, "*.cfg");
                if (files.Length != 0)
                {
                    configPaths.AddRange(files);
                    directoryPaths.Add(directory);
                }
            }
            if (configPaths.Count == 0)
            {
                return true;
            }
            for (int i = configPaths.Count - 1; i >= 0; i--)
            {
                var modInfo = await ModAPI.LoadModInfo(configPaths[i]);
                var state = _modConfigData.GetModState(modInfo);
                if (state == ModStatus.Enabled)
                {
                    modInfos.Add(modInfo);
                }
                else if (state == ModStatus.Disabled)
                {
                    directoryPaths.RemoveAt(i);
                    modInfos.Add(modInfo);
                }
                else
                {
                    ModAPI.DeleteModFromDisk(modInfo);
                    directoryPaths.RemoveAt(i);
                }

            }
            foreach (var directory in directoryPaths)
            {
                await ResourceSystem.LoadCatalogAsync(directory);
            }
            return true;
        }
        
        public async UniTask<ModInfo> LoadModAsync(ModConfig configData, string path)
        {
            var configs = Directory.GetFiles(path, "*.cfg");
            if (configs.Length == 0) return null;
            string config = configs[0];
            var modInfo = await ModAPI.LoadModInfo(config);
            var state = configData.GetModState(modInfo);
            if (state == ModStatus.Enabled)
            {
                if (!_validator.ValidateMod(modInfo))
                {
                    return modInfo;
                }
            }
            else if (state == ModStatus.Disabled)
            {
                return modInfo;
            }
            else
            {
                ModAPI.DeleteModFromDisk(modInfo);
                return null;
            }
            
            await ResourceSystem.LoadCatalogAsync(path);
            return modInfo;
        }
    }
}