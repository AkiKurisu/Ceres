using System.Linq;
using UnityEditor;

namespace Ceres.Editor.UIElements
{
    internal sealed class UIToolkitPreviewAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            UIToolkitPreviewWindow window = UIToolkitPreviewWindow.Instance;
            if (window == null)
            {
                return;
            }
            string[] changed = imported.Concat(deleted).Concat(moved).Concat(movedFrom).ToArray();
            if (window.WatchesAny(changed))
            {
                window.QueueRebuild();
            }
        }
    }
}
