using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Ceres.Resource.Editor
{
    public class ExcludeDefaultBundleBuilder : CustomBuilder
    {
        public override string Description => "Exclude default group bundle from build.";
        
        private AddressableAssetGroup _defaultGroup;
        
        public override void Build(ResourceExportContext context)
        {
            _defaultGroup = AddressableAssetSettingsDefaultObject.Settings.groups.FirstOrDefault(group => !group.HasSchema<BundledAssetGroupSchema>());
            if (_defaultGroup)
            {
                AddressableAssetSettingsDefaultObject.Settings.groups.Remove(_defaultGroup);
            }
        }
        
        public override void Cleanup(ResourceExportContext context)
        {
            if (_defaultGroup)
            {
                AddressableAssetSettingsDefaultObject.Settings.groups.Insert(0, _defaultGroup);
            }
        }
    }
}