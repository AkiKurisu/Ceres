using System;
using System.Collections.Generic;
using System.Linq;
using Ceres;
using Ceres.Flow;
using Ceres.DataDriven;
using Ceres.Pool;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Gameplay
{
    [Serializable, AddressableDataTable(address: ActorFlowGraphDataTableManager.TableKey)]
    public class ActorFlowGraphRow : IDataTableRow
    {
        [Tooltip("Streaming flow graph asset reference")]
        public SoftAssetReference<FlowGraphAsset> reference;

        [Tooltip("Preload flow graph asset on game load")]
        public bool preload;

        [Tooltip("Enable load flow graph asset from remote path for runtime update")]
        public bool remoteUpdate;
        
        [Tooltip("Overwrite remote updating path, default using actor address")]
        public Optional<string> remotePath;
    }
    
    public sealed class ActorFlowGraphDataTableManager : DataTableManager<ActorFlowGraphDataTableManager>
    {
        public const string TableKey = "ActorFlowGraphDataTable";
        
        private struct ActorFlowGraphData
        {
            public FlowGraphAsset Asset;

            public ResourceHandle Handle;
            
            public bool RemoteUpdate;
        
            public string RemotePath;
        }
        
        private readonly Dictionary<string, ActorFlowGraphData> _graphData = new();
        
        public ActorFlowGraphDataTableManager(object _) : base(_)
        {
        }

        protected override async UniTask Initialize(bool sync)
        {
            if (!GameplayConfig.Get().enableRemoteUpdate)
            {
                return;
            }
            
            await InitializeSingleTable(TableKey, sync);
            var parallel = UniParallel.Get();
            foreach (var pair in DataTables.SelectMany(pair => pair.Value.GetRowMap()))
            {
                if (pair.Value is not ActorFlowGraphRow row) continue;
                var data = new ActorFlowGraphData
                {
                    RemoteUpdate = row.remoteUpdate,
                    RemotePath = row.remotePath.Enabled ? row.remotePath.Value : pair.Key
                };
                _graphData.Add(pair.Key, data);
                if (row.preload && row.reference.IsValid())
                {
                    parallel.Add(PreloadFlowGraphAsset(pair.Key, row, sync));
                }
            }

            await parallel;
        }

        public bool TryGetFlowGraphAsset(string address, out FlowGraphAsset asset)
        {
            if (_graphData.TryGetValue(address, out var data))
            {
                if (data.Asset && data.RemoteUpdate)
                {
                    asset = data.Asset;
                    return true;
                }
            }

            asset = null;
            return false;
        }
        
        public bool TryGetRemoteUpdatePath(string address, out string path)
        {
            if (_graphData.TryGetValue(address, out var data))
            {
                if (data.RemoteUpdate)
                {
                    path = data.RemotePath;
                    return true;
                }
            }

            path = null;
            return false;
        }

        private async UniTask PreloadFlowGraphAsset(string key, ActorFlowGraphRow row, bool sync)
        {
            FlowGraphAsset asset;
            var handle = row.reference.LoadAsync();
            if (sync)
            {
                asset = handle.WaitForCompletion();
            }
            else
            {
                asset = await handle.ToUniTask();
            }
            if (asset)
            {
                var data = _graphData[key];
                data.Asset = asset;
                data.Handle = handle;
                _graphData[key] = data;
            }
        }
    }
}
