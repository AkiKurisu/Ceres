using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Ceres.Graph;
using Ceres.Flow;
using Ceres.Serialization;
using UnityEditor;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace Ceres.Gameplay.Editor
{
    public static class ActorFlowGraphEditorUtils
    {
        public static void ExportRemoteAsset(IFlowGraphContainer container, string address)
        {
            var flowPath = Path.Combine(SaveUtility.SavePath, "Flow");
            if (!ActorFlowGraphDataTableManager.Get().TryGetRemoteUpdatePath(address, out var path))
            {
                path = address;
            }
            var data = container.GetFlowGraphData();
            var referencedAssets = data.subGraphData.SelectMany(x => x.graphData.nodeData)
                .Concat(data.nodeData)
                .SelectMany(node => node.GetReferencedObjects()).ToArray();
            var assetPaths = referencedAssets.Where(x => (bool)x)
                .Select(AssetDatabase.GetAssetPath)
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .ToList();
            bool canSerializeAsText = CeresGameplaySettings.Instance.remoteUpdateSerializeMode == RemoteUpdateSerializeMode.ForceText
                || !assetPaths.Any() && CeresGameplaySettings.Instance.remoteUpdateSerializeMode == RemoteUpdateSerializeMode.PreferText;
            if (canSerializeAsText)
            {
                BuildRemoteAssetText(container, flowPath, path, data);
            }
            else
            {
                BuildRemoteAssetBundle(container, path, data, assetPaths, flowPath);
            }
            Process.Start(flowPath);
        }

        private static void BuildRemoteAssetText(IFlowGraphContainer container, string flowPath, string path,
            FlowGraphData data)
        {
            var serializer = new SaveLoadSerializer(flowPath, "json", TextSerializeFormatter.Instance);
            serializer.Serialize(path, data);
            UDebug.Log($"Export {container.GetIdentifier()} remote asset to {Path.Combine(flowPath, $"{path}.json")}");
        }

        private static void BuildRemoteAssetBundle(IFlowGraphContainer container, string path, FlowGraphData data,
            List<string> assetPaths, string flowPath)
        {
            // Create temporary container asset
            var tempPath = $"Assets/{path}.asset";
            var temporaryAsset = ScriptableObject.CreateInstance<FlowGraphAsset>();
            AssetDatabase.CreateAsset(temporaryAsset, tempPath);
            AssetDatabase.Refresh();
            temporaryAsset.SetGraphData(data.CloneT<FlowGraphData>());
            EditorUtility.SetDirty(temporaryAsset);
            AssetDatabase.SaveAssetIfDirty(temporaryAsset);
            
            assetPaths.Insert(0, AssetDatabase.GetAssetPath(temporaryAsset));
            var addressableNames = assetPaths.ToArray();
            /* Use path as main asset's address */
            addressableNames[0] = path;
            var buildMap = new AssetBundleBuild
            {
                assetBundleName = $"{path}.bundle",
                assetNames = assetPaths.ToArray(),
                addressableNames = addressableNames
            };
            var manifest = BuildPipeline.BuildAssetBundles(
                flowPath,
                new[] { buildMap },
                BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows);
            var ab =  manifest.GetAllAssetBundles()[0];
            var abName = Path.Combine(flowPath, ab);
            var destName = Path.Combine(flowPath, $"{path}.bundle");
            File.Delete(abName + ".manifest");
            File.Delete(Path.Combine(flowPath, "Flow"));
            File.Delete(Path.Combine(flowPath, "Flow.manifest"));
            AssetDatabase.DeleteAsset(tempPath);
            AssetDatabase.Refresh();
            UDebug.Log($"Export {container.GetIdentifier()} remote asset to {destName}");
        }
    }
}
