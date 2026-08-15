using System;
using Ceres.Collections;
using R3;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ceres.Gameplay
{
    /// <summary>
    /// World container in Gameplay level.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameWorld : MonoBehaviour
    {
        internal struct PinScope : IDisposable
        {
            public void Dispose()
            {
                UnPin();
            }
        }
        
        private ulong _serialNum = 1;
        
        private const int InitialCapacity = 100;
        
        internal readonly SparseArray<Actor> ActorsInWorld = new(InitialCapacity, ActorHandle.MaxIndex);

        private readonly Subject<Unit> _onActorsUpdate = new();
        
        private WorldSubsystemCollection _subsystemCollection;
        
        private static GameWorld _current;

        private static bool _isPinned;

        private static bool _isInPendingDestroyed;

        private static int _inPendingDestroyedFrame;
        
        private bool _isSystemDirty;
        
        internal bool IsDestroyed { get; private set; }
        
        private void Awake()
        {
            if (_current != null && _current != this)
            {
                Destroy(gameObject);
                return;
            }
            _current = this;
            _subsystemCollection = new WorldSubsystemCollection((WorldContext)this);
            _onActorsUpdate.Subscribe(_subsystemCollection.SetDirtyOnActorsUpdate);
        }
        
        private void Start()
        {
            _subsystemCollection.Init();
        }
        
        private void Update()
        {
            _subsystemCollection.Tick();
            if (!_isSystemDirty) return;
            _subsystemCollection.Rebuild();
            _isSystemDirty = false;
        }
        
        private void FixedUpdate()
        {
            _subsystemCollection.FixedTick();
            if (!_isSystemDirty) return;
            _subsystemCollection.Rebuild();
            _isSystemDirty = false;
        }
        
        private void OnDestroy()
        {
            IsDestroyed = true;
            _isInPendingDestroyed = true;
            _inPendingDestroyedFrame = Time.frameCount;
            _subsystemCollection?.Dispose();
            _onActorsUpdate.Dispose();
        }

        /// <summary>
        /// Mark GameWorld should be valid in pending destroy stage.
        /// </summary>
        internal static PinScope Pin()
        {
            _isPinned = true;
            return new PinScope();
        }
        
        /// <summary>
        /// Reverse pin
        /// </summary>
        private static void UnPin()
        {
            _isPinned = false;
#if UNITY_EDITOR
            if (_current)
            {
                _current.gameObject.hideFlags &= ~HideFlags.DontSave;
            }
#endif
        }

        /// <summary>
        /// Whether access to world is safe which will return null if world is being destroyed
        /// </summary>
        /// <returns></returns>
        public static bool IsValid()
        {
            if (_current) return !_current.IsDestroyed;
            
            if (_isInPendingDestroyed)
            {
                if (Time.frameCount > _inPendingDestroyedFrame)
                {
                    _isInPendingDestroyed = false;
                }
            }
            
            if (_isPinned)
            {
                return true;
            }
            
            return !_isInPendingDestroyed;
        }
        
        public static WorldContext Get()
        {
            /* Access to world in destroy stage is not allowed */
            if (!IsValid()) return default;

            if (!_current)
            {
                _current = new GameObject(nameof(GameWorld))
                {
                    hideFlags = HideFlags.NotEditable
                }.AddComponent<GameWorld>();
#if UNITY_EDITOR
                // Mark world as an editor only object to ignore editor validation error
                if (_isPinned)
                {
                    _current.gameObject.hideFlags |= HideFlags.DontSave;
                }
#endif
            }
            return (WorldContext)_current;
        }
        
        internal void RegisterActor(Actor actor, ref ActorHandle handle)
        {
            if ((bool)GetActor(handle))
            {
                Debug.LogError($"{actor.gameObject.name} is already registered to world.");
                return;
            }
            int index = ActorsInWorld.Add(actor);
            handle = new ActorHandle(_serialNum, index);
            _onActorsUpdate.OnNext(Unit.Default);
        }
        
        internal void UnregisterActor(Actor actor)
        {
            var handle = actor.GetActorHandle();
            int index = handle.GetIndex();
            if (!ActorsInWorld.IsAllocated(index)) return;
            
            var current = ActorsInWorld[index];
            if (current.GetActorHandle().GetSerialNumber() != handle.GetSerialNumber())
            {
                Debug.LogWarning($"Actor {handle.Handle} has already been removed from world.");
                return;
            }
            // increase serial num as version update
            ++_serialNum;
            ActorsInWorld.RemoveAt(index);
            _onActorsUpdate.OnNext(Unit.Default);
        }
        
        internal Actor GetActor(ActorHandle handle)
        {
            int index = handle.GetIndex();
            if (!handle.IsValid() || !ActorsInWorld.IsAllocated(index)) return null;
            
            var actor = ActorsInWorld[index];
            return actor.GetActorHandle().GetSerialNumber() != handle.GetSerialNumber() ? null : actor;
        }
        
        internal T GetSubsystem<T>() where T : SubsystemBase
        {
            return _subsystemCollection.GetSubsystem<T>();
        }
        
        internal SubsystemBase GetSubsystem(Type type)
        {
            return _subsystemCollection.GetSubsystem(type);
        }
        
        internal void RegisterSubsystem<T>(T subsystem) where T : SubsystemBase
        {
            Assert.IsFalse(IsDestroyed);
            _subsystemCollection.RegisterSubsystem(subsystem);
            _isSystemDirty = true;
        }

        internal void RegisterSubsystem(Type type, SubsystemBase subsystem)
        {
            Assert.IsFalse(IsDestroyed);
            _subsystemCollection.RegisterSubsystem(type, subsystem);
            _isSystemDirty = true;
        }
    }
}
