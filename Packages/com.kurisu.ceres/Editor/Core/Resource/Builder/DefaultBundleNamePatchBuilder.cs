using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;

namespace Ceres.Resource.Editor
{
    public class DefaultBundleNamePatchBuilder : CustomBuilder
    {
        public override string Description => "Use context name for naming shader bundle and monoScript bundle." +
                " If you build addressables in source project and use project hash as name, use this builder for preventing bundle conflict.";
        
        public override void Build(ResourceExportContext context)
        {
#if UNITY_6000_0_OR_NEWER
            AddressableAssetSettingsDefaultObject.Settings.BuiltInBundleNaming = BuiltInBundleNaming.Custom;
            AddressableAssetSettingsDefaultObject.Settings.BuiltInBundleCustomNaming = $"Resource_{context.Name}_BuiltIn";
#else
            AddressableAssetSettingsDefaultObject.Settings.ShaderBundleNaming = ShaderBundleNaming.Custom;
            AddressableAssetSettingsDefaultObject.Settings.ShaderBundleCustomNaming = $"Resource_{context.Name}_Shader";
#endif
            AddressableAssetSettingsDefaultObject.Settings.MonoScriptBundleNaming = MonoScriptBundleNaming.Custom;
            AddressableAssetSettingsDefaultObject.Settings.MonoScriptBundleCustomNaming = $"Resource_{context.Name}_MonoScript";
        }

        public override void Cleanup(ResourceExportContext _)
        {
#if UNITY_6000_0_OR_NEWER
            AddressableAssetSettingsDefaultObject.Settings.BuiltInBundleNaming = BuiltInBundleNaming.ProjectName;
#else
            AddressableAssetSettingsDefaultObject.Settings.ShaderBundleNaming = ShaderBundleNaming.ProjectName;
#endif
            AddressableAssetSettingsDefaultObject.Settings.MonoScriptBundleNaming = MonoScriptBundleNaming.ProjectName;
        }
    }
}