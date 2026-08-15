using System.Collections.Generic;
using System.IO;
using Ceres.Flow;
using Ceres.Serialization;
using UnityEngine;

namespace Ceres.Gameplay
{
    [InitializeOnWorldCreate]
    public class ActorFlowGraphSubsystem: WorldSubsystem
    {
        private SaveLoadSerializer _serializer;

        private readonly Dictionary<string, FlowGraphAsset> _remoteFlowGraph = new();

        private readonly List<AssetBundle> _assetBundles = new();

        protected override void Initialize()
        {
            base.Initialize();
            _serializer = new SaveLoadSerializer(
                Path.Combine(SaveUtility.SavePath, "Flow"), 
                "json", 
                TextSerializeFormatter.Instance);
        }

        public static ActorFlowGraphSubsystem Get()
        {
            return GameWorld.Get().GetSubsystem<ActorFlowGraphSubsystem>();
        }

        public FlowGraphAsset GetFlowGraphAsset(string address)
        {
            // Not enabled
            if (!GameplayConfig.Get().enableRemoteUpdate)
            {
                return null;
            }
            
            // Already loaded
            if (ActorFlowGraphDataTableManager.Get().TryGetFlowGraphAsset(address, out var flowGraphAsset))
            {
                return flowGraphAsset;
            }
            
            // Remote update flow graph
            if (ActorFlowGraphDataTableManager.Get().TryGetRemoteUpdatePath(address, out var remotePath))
            {
                if (_remoteFlowGraph.TryGetValue(remotePath, out var asset))
                {
                    return asset;
                }
                
                if (_serializer.Exists(remotePath))
                {
                    asset = ScriptableObject.CreateInstance<FlowGraphAsset>();
                    var data = _serializer.Deserialize<FlowGraphData>(remotePath);
                    asset.SetGraphData(data);
                    _remoteFlowGraph.Add(remotePath, asset);
                    return asset;
                }

                var bundlePath = Path.Combine(Path.Combine(SaveUtility.SavePath, "Flow"), $"{remotePath}.bundle");
                if (File.Exists(bundlePath))
                {
                    var assetBundle = AssetBundle.LoadFromFile(bundlePath);
                    asset = assetBundle.LoadAsset<FlowGraphAsset>(remotePath);
                    _remoteFlowGraph.Add(remotePath, asset);
                    _assetBundles.Add(assetBundle);
                    return asset;
                }
            }

            return null;
        }

        protected override void Release()
        {
            _remoteFlowGraph.Clear();
            foreach (var assetBundle in _assetBundles)
            {
                assetBundle.UnloadAsync(true);
            }
            _assetBundles.Clear();
            base.Release();
        }
    }
}
