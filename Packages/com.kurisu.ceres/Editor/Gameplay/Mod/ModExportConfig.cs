using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ceres.Flow;
using Ceres.Flow.Annotations;
using Ceres.Resource.Editor;
using Ceres.Serialization;
using Newtonsoft.Json;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ceres.Gameplay.Mod.Editor
{
    /// <summary>
    /// Scriptable mod export configuration
    /// </summary>
    [CreateAssetMenu(fileName = "ModExportConfig", menuName = "Ceres/Mod/Export Config")]
    public class ModExportConfig : 
        FlowGraphScriptableObject
    {
        private class InternalBuilder : IResourceBuilder
        {
            private readonly ModExportConfig _config;
            
            public InternalBuilder(ModExportConfig config)
            {
                _config = config;
            }
            
            public void Build(ResourceExportContext context)
            {
                _config.Flow_OnBuild(context.BuildPath);
                var info = new ModInfo
                {
                    authorName = _config.authorName,
                    description = _config.description,
                    modName = context.Name,
                    version = _config.version,
                    modIconBytes = _config.iconData ?? Array.Empty<byte>(),
                    apiVersion = ModConfig.Get().ApiVersion
                };
                var stream = JsonConvert.SerializeObject(info);
                File.WriteAllText(context.BuildPath + "/ModConfig.cfg", stream);
            }

            public void Cleanup(ResourceExportContext context)
            {
                _config.Flow_OnCleanup();
            }
        }
        
        public string authorName = "Default";

        public string modName = "Mod";

        public string version = "1.0.0";

        [Multiline]
        public string description;

        [HideInInspector]
        public byte[] iconData;

        public SerializedType<CustomBuilder>[] customBuilders;

        internal bool Validate()
        {
            if (string.IsNullOrEmpty(authorName)) return false;
            if (string.IsNullOrEmpty(modName)) return false;
            if (string.IsNullOrEmpty(version)) return false;
            return true;
        }

        internal AddressableAssetGroup CreateAssetGroup()
        {
            return ResourceEditorUtils.GetOrCreateAssetGroup($"Mod_{modName}");
        }

        internal IResourceBuilder CreateInternalBuilder()
        {
            return new InternalBuilder(this);
        }
        
        [ImplementableEvent]
        public void Flow_OnBuild(string buildPath)
        {

        }

        [ImplementableEvent]
        public void Flow_OnCleanup()
        {

        }
        
        /// <summary>
        /// Create resource exporter
        /// </summary>
        /// <returns></returns>
        public ResourceExporter CreateResourceExporter()
        {
            var builders = new List<IResourceBuilder> { new AddressableAssetBuilder() };
            builders.AddRange(customBuilders
                .Select(serializedType => serializedType.GetObject())
                .Where(builder => builder != null)
                .ToArray());
            builders.Add(CreateInternalBuilder());
            
            var mainAssetGroup = CreateAssetGroup();
            var context = new ResourceExportContext
            {
                Name = modName,
                AssetGroupFilter = MatchAssetGroupName
            };

            var exporter = ResourceExporter.CreateFromContext(context, builders.ToArray());
            return exporter;

            bool MatchAssetGroupName(AddressableAssetGroup group)
            {
                return group.Name.StartsWith(mainAssetGroup.Name);
            }
        }
    }
}
