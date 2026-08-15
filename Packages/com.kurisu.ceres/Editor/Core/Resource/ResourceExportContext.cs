using System;
using UnityEditor.AddressableAssets.Settings;

namespace Ceres.Resource.Editor
{
    /// <summary>
    /// Resource exporter context object
    /// </summary>
    public class ResourceExportContext
    {
        /// <summary>
        /// Context name used for naming asset bundles
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Export destination directory.
        /// </summary>
        public string BuildPath { get; set; }

        /// <summary>
        /// Filter func for get <see cref="AddressableAssetGroup"/>s to export
        /// </summary>
        public Func<AddressableAssetGroup, bool> AssetGroupFilter { get; set; }

        internal bool SkipCatalogPostprocess { get; set; }
    }
}
