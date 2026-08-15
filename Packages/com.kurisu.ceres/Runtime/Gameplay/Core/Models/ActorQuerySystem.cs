using System.Collections.Generic;
using Ceres.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace Ceres.Gameplay
{
    /// <summary>
    /// Actor world basic data
    /// </summary>
    public struct ActorData
    {
        public ActorHandle Handle;
        
        public int Layer;
        
        public bool Active;
        
        public quaternion Rotation;
        
        public float3 Position;
    }
    
    /// <summary>
    /// Represent a DOP actor query system.
    /// </summary>
    public class ActorQuerySystem : WorldSubsystem
    {
        private NativeArray<ActorData> _actorData;
        
        private readonly List<Actor> _actors = new();
        
        private static readonly ProfilerMarker TickProfilerMarker = new("ActorQuerySystem.Tick");
        
        protected override void Initialize()
        {
            RebuildArray();
        }
        
        public override void Tick()
        {
            using (TickProfilerMarker.Auto())
            {
                if (IsActorsDirty)
                {
                    RebuildArray();
                    IsActorsDirty = false;
                }
                unsafe
                {
                    void* arrayPtr = _actorData.GetUnsafePtr();
                    for (int i = 0; i < _actorData.Length; ++i)
                    {
                        _actors[i].transform.GetPositionAndRotation(out var pos, out var rot);
                        fixed (ActorData* ptr = &UnsafeUtility.ArrayElementAsRef<ActorData>(arrayPtr, i))
                        {
                            ptr->Position = pos;
                            ptr->Rotation = rot;
                            ptr->Active = _actors[i].isActiveAndEnabled;
                        }
                    }
                }
            }
        }
        
        private void RebuildArray()
        {
            _actors.Clear();
            GetActorsInWorld(_actors);
            _actorData.Resize(_actors.Count);
            for (int i = 0; i < _actorData.Length; ++i)
            {
                _actorData[i] = new ActorData
                {
                    Handle = _actors[i].GetActorHandle(),
                    // not update layer in tick to prevent allocation
                    Layer = _actors[i].gameObject.layer
                };
            }
        }
        
        /// <summary>
        /// Allocate an actor data array for query
        /// </summary>
        /// <param name="allocator"></param>
        /// <returns></returns>
        public NativeArray<ActorData> GetAllActors(Allocator allocator)
        {
            if (!_actorData.IsCreated)
            {
                return new NativeArray<ActorData>(0, allocator);
            }
            return new NativeArray<ActorData>(_actorData, allocator);
        }
        
        protected override void Release()
        {
            _actorData.DisposeSafe();
        }
    }
}
