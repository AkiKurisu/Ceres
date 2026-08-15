using System.Collections.Generic;
using Ceres.Collections;
using Ceres.Gameplay;
using Ceres.Schedulers;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unity.Burst;
using Unity.Profiling;
using UnityEngine.Assertions;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// Command for schedule post query job
    /// </summary>
    public struct PostQueryCommand
    {
        public ActorHandle Self;
        
        public ActorHandle Target;
        
        public float3 Offset;
        
        public int LayerMask;
        
        public PostQueryParameters Parameters;
    }
    
    public class PostQuerySystem : WorldSubsystem
    {
        [BurstCompile]
        public struct PrepareCommandJob : IJobParallelFor
        {
            [ReadOnly]
            public PostQueryCommand Command;
            
            [ReadOnly]
            public ActorData Source;
            
            [ReadOnly]
            public ActorData Target;
            
            [ReadOnly]
            public int Length;
            
            [WriteOnly, NativeDisableParallelForRestriction]
            public NativeArray<RaycastCommand> RaycastCommands;
            
            public void Execute(int index)
            {
                var direction = math.normalize(Source.Position - Target.Position);

                float angle = Command.Parameters.angle / 2;

                quaternion rot = quaternion.RotateY(math.radians(math.lerp(-angle, angle, (float)index / Length)));

                RaycastCommands[index] = new RaycastCommand()
                {
                    from = Target.Position + Command.Offset,
                    direction = math.rotate(rot, direction),
                    distance = Command.Parameters.distance,
                    queryParameters = new QueryParameters { layerMask = Command.LayerMask }
                };
            }
        }
        
        /// <summary>
        /// Worker per actor
        /// </summary>
        private class PostQueryWorker
        {
            private NativeList<float3> _posts = new(Allocator.Persistent);
            
            private NativeArray<RaycastHit> _hits;
            
            private NativeArray<RaycastCommand> _raycastCommands;
            
            private JobHandle _jobHandle;
            
            public bool IsRunning { get; private set; }
            
            public bool HasPendingCommand { get; private set; }

            public NativeArray<float3>.ReadOnly GetPosts()
            {
                return _posts.AsReadOnly();
            }
            
            public void SetPending()
            {
                HasPendingCommand = true;
            }
            
            public void ExecuteCommand(ref PostQueryCommand command, ref NativeArray<ActorData> actorDatas)
            {
                HasPendingCommand = false;
                IsRunning = true;
                int length = command.Parameters.step * command.Parameters.depth;
                _raycastCommands.DisposeSafe();
                _raycastCommands = new NativeArray<RaycastCommand>(length, Allocator.TempJob);
                _hits.DisposeSafe();
                _hits = new(length, Allocator.TempJob);
                var job = new PrepareCommandJob()
                {
                    Command = command,
                    RaycastCommands = _raycastCommands,
                    Length = length,
                    Source = actorDatas[command.Self.GetIndex()],
                    Target = actorDatas[command.Target.GetIndex()]
                };
                _jobHandle = job.Schedule(length, 32, default);
                _jobHandle = RaycastCommand.ScheduleBatch(_raycastCommands, _hits, _raycastCommands.Length, _jobHandle);
            }
            
            public void CompleteCommand()
            {
                IsRunning = false;
                _jobHandle.Complete();
                _posts.Clear();
                bool hasHit = false;
                foreach (var hit in _hits)
                {
                    bool isHit = hit.point != default;
                    if (!hasHit && isHit)
                    {
                        _posts.Add(hit.point);
                    }
                    hasHit = isHit;
                }
                _raycastCommands.Dispose();
                _hits.Dispose();
            }
            public void Dispose()
            {
                _posts.Dispose();
                _hits.DisposeSafe();
                _raycastCommands.DisposeSafe();
            }
        }
        
        private readonly Queue<PostQueryCommand> _commandBuffer = new();
        
        private SchedulerHandle _updateTickHandle;
        
        private SchedulerHandle _lateUpdateTickHandle;
        
        private NativeArray<ActorHandle> _batchHandles;
        
        private int _batchLength;
        
        private readonly Dictionary<ActorHandle, PostQueryWorker> _workerDic = new();
        
        /// <summary>
        /// Set system parallel workers count
        /// </summary>
        /// <value></value>
        public static int MaxWorkerCount { get; set; } = DefaultWorkerCount;
        
        /// <summary>
        /// Default parallel workers count: 5
        /// </summary>
        public const int DefaultWorkerCount = 5;
        
        /// <summary>
        /// Set sysytem tick frame
        /// </summary>
        /// <value></value>
        public static int FramePerTick { get; set; } = DefaultFramePerTick;
        
        /// <summary>
        /// Default tick frame: 2 fps
        /// </summary>
        public const int DefaultFramePerTick = 25;
        
        private static readonly ProfilerMarker ConsumeCommandsProfilerMarker = new("PostQuerySystem.ConsumeCommands");
        
        private static readonly ProfilerMarker CompleteCommandsProfilerMarker = new("PostQuerySystem.CompleteCommands");
        
        protected override void Initialize()
        {
            Assert.IsFalse(FramePerTick <= 3);
            Scheduler.WaitFrame(ref _updateTickHandle, FramePerTick, ConsumeCommands, TickFrame.FixedUpdate, isLooped: true);
            // Allow job scheduled in 3 frames
            Scheduler.WaitFrame(ref _lateUpdateTickHandle, 3, CompleteCommands, TickFrame.FixedUpdate, isLooped: true);
            _lateUpdateTickHandle.Pause();
            _batchHandles = new NativeArray<ActorHandle>(MaxWorkerCount, Allocator.Persistent);
        }
        
        private void ConsumeCommands(int _)
        {
            using (ConsumeCommandsProfilerMarker.Auto())
            {
                _batchLength = 0;
                var actorDatas = GetOrCreate<ActorQuerySystem>().GetAllActors(Allocator.Temp);
                while (_batchLength < MaxWorkerCount)
                {
                    if (!_commandBuffer.TryDequeue(out var command))
                    {
                        break;
                    }

                    var worker = _workerDic[command.Self];

                    if (worker.IsRunning)
                    {
                        Debug.LogWarning($"[PostQuerySystem] Should not enquene new command [ActorId: {command.Self.Handle}] before last command completed!");
                        continue;
                    }

                    worker.ExecuteCommand(ref command, ref actorDatas);
                    _batchHandles[_batchLength++] = command.Self;
                }
                actorDatas.Dispose();
            }
            _lateUpdateTickHandle.Resume();
        }
        
        private void CompleteCommands(int _)
        {
            using (CompleteCommandsProfilerMarker.Auto())
            {
                for (int i = 0; i < _batchLength; ++i)
                {
                    _workerDic[_batchHandles[i]].CompleteCommand();
                }
            }
            _lateUpdateTickHandle.Pause();
        }

        protected override void Release()
        {
            _batchHandles.Dispose();
            _updateTickHandle.Dispose();
            _lateUpdateTickHandle.Dispose();
            foreach (var worker in _workerDic.Values)
            {
                worker.Dispose();
            }
            _workerDic.Clear();
        }
        
        /// <summary>
        /// Enqueue a new <see cref="PostQueryCommand"/> to the system
        /// </summary>
        /// <param name="command"></param>
        public void EnqueueCommand(PostQueryCommand command)
        {
            if (!_workerDic.TryGetValue(command.Self, out var worker))
            {
                worker = _workerDic[command.Self] = new();
            }
            worker.SetPending();
            _commandBuffer.Enqueue(command);
        }
        
        /// <summary>
        /// Get cached posts has found for target actor use latest command
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public NativeArray<float3>.ReadOnly GetPosts(ActorHandle handle)
        {
            if (_workerDic.TryGetValue(handle, out var worker))
                return worker.GetPosts();
            return default;
        }
        
        /// <summary>
        /// Whether the worker for target actor is free to execute new command
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public bool IsFree(ActorHandle handle)
        {
            if (_workerDic.TryGetValue(handle, out var worker))
                return !worker.IsRunning && !worker.HasPendingCommand;
            return true;
        }
    }
}
