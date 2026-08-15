using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Flow.Annotations;
using R3;

namespace Ceres.Gameplay
{
    internal class WorldSubsystemCollection : IDisposable
    {
        private readonly Dictionary<Type, SubsystemBase> _systems;
        
        private SubsystemBase[] _subsystems;

        private const string AssemblyName = "Ceres.Gameplay";
        
        public WorldSubsystemCollection(WorldContext worldContext)
        {
            var typeList = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(assembly =>
                    {
#if UNITY_EDITOR
                        if (assembly.GetName().Name.Contains(".Editor"))
                        {
                            return false;
                        }
#endif
                        return assembly.GetName().Name == AssemblyName ||
                               assembly.GetReferencedAssemblies().Any(name => name.Name == AssemblyName);
                    })
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(type => type.IsSubclassOf(typeof(WorldSubsystem)) && !type.IsAbstract)
                    .Where(type => type.GetCustomAttribute<InitializeOnWorldCreateAttribute>() != null)
                    .ToList();
            _systems = typeList.ToDictionary(type => type, type => Activator.CreateInstance(type) as SubsystemBase);
            foreach (var type in typeList)
            {
                if (!((WorldSubsystem)_systems[type]).CanCreate(worldContext)) _systems.Remove(type);
                else _systems[type].SetWorld(worldContext);
            }
            _subsystems = _systems.Values.ToArray();
        }
        
        internal void RegisterSubsystem<T>(T subsystem) where T : SubsystemBase
        {
            _systems.Add(typeof(T), subsystem);
            subsystem.InternalInit();
        }

        internal void RegisterSubsystem(Type type, SubsystemBase subsystem)
        {
            _systems.Add(type, subsystem);
            subsystem.InternalInit();
        }
        
        internal void Rebuild()
        {
            _subsystems = _systems.Values.ToArray();
            Init();
        }
        
        public T GetSubsystem<T>() where T : SubsystemBase
        {
            if (_systems.TryGetValue(typeof(T), out var subsystem))
            {
                if (GameplayConfig.Get().subsystemForceInitializeBeforeGet)
                {
                    subsystem.InternalInit();
                }
                return (T)subsystem;
            }
            return null;
        }
        
        public SubsystemBase GetSubsystem(Type type)
        {
            if (_systems.TryGetValue(type, out var subsystem))
            {
                if (GameplayConfig.Get().subsystemForceInitializeBeforeGet)
                {
                    subsystem.InternalInit();
                }
                return subsystem;
            }
            return null;
        }
        
        public void Init()
        {
            for (int i = 0; i < _subsystems.Length; ++i)
            {
                _subsystems[i].InternalInit();
            }
        }
        
        public void Tick()
        {
            for (int i = 0; i < _subsystems.Length; ++i)
            {
                _subsystems[i].Tick();
            }
        }
        
        public void FixedTick()
        {
            for (int i = 0; i < _subsystems.Length; ++i)
            {
                _subsystems[i].FixedTick();
            }
        }
        
        internal void SetDirtyOnActorsUpdate(Unit _)
        {
            for (int i = 0; i < _subsystems.Length; ++i)
            {
                _subsystems[i].IsActorsDirty = true;
            }
        }
        
        public void Dispose()
        {
            for (int i = 0; i < _subsystems.Length; ++i)
            {
                _subsystems[i].InternalRelease();
            }
            _subsystems = null;
        }
    }
    /// <summary>
    /// Base class for subsystem, gameplay subsystem should not implement from this directly.
    /// See <see cref="WorldSubsystem"/>.
    /// </summary>
    public abstract class SubsystemBase
    {
        /// <summary>
        /// Buffered dirty flag when world actors changed
        /// </summary>
        protected internal bool IsActorsDirty { get; set; }
        
        /// <summary>
        /// Is system initialized
        /// </summary>
        /// <value></value>
        protected bool IsInitialized { get; private set; }
        
        /// <summary>
        /// Is system destroyed
        /// </summary>
        /// <value></value>
        protected bool IsDestroyed { get; private set; }

        private WorldContext _worldContext;

        /// <summary>
        /// Subsystem initialize phase, should bind callbacks and collect references in this phase
        /// </summary>
        protected virtual void Initialize()
        {

        }

        internal virtual void InternalInit()
        {
            if (IsInitialized) return;
            IsInitialized = true;
            Initialize();
        }

        public virtual void Tick()
        {

        }

        public virtual void FixedTick()
        {

        }

        /// <summary>
        /// Subsystem release phase, should unbind callbacks and release references in this phase
        /// </summary>
        protected virtual void Release()
        {

        }

        internal virtual void InternalRelease()
        {
            if (IsDestroyed) return;
            _worldContext = default;
            IsDestroyed = true;
            Release();
        }

        internal void SetWorld(WorldContext worldContext)
        {
            _worldContext = worldContext;
        }

        /// <summary>
        /// Get attached world
        /// </summary>
        /// <returns></returns>
        [ExecutableFunction]
        public GameWorld GetWorld()
        {
            return _worldContext.Cast();
        }

        /// <summary>
        /// Get all actors in world, readonly
        /// </summary>
        /// <returns></returns>
        protected void GetActorsInWorld(List<Actor> actors)
        {
            foreach (var actor in _worldContext.Cast().ActorsInWorld)
            {
                actors.Add(actor);
            }
        }

        /// <summary>
        /// Get actor num in world, readonly
        /// </summary>
        /// <returns></returns>
        protected int GetActorsNum()
        {
            return _worldContext.Cast().ActorsInWorld.Count;
        }
    }
    
    /// <summary>
    /// Subsystem bound to an actor world.
    /// </summary>
    public abstract class WorldSubsystem : SubsystemBase
    {
        /// <summary>
        /// Whether <see cref="WorldSubsystem"/> can create
        /// </summary>
        /// <param name="worldContext"></param>
        /// <returns></returns>
        public virtual bool CanCreate(WorldContext worldContext)
        {
            return worldContext.IsValid();
        }

        private static T GetOrCreate_Internal<T>(WorldContext worldContext) where T : WorldSubsystem, new()
        {
            var system = worldContext.GetSubsystem<T>();
            if (system != null) return system;
            
            system = new T();
            if (!system.CanCreate(worldContext)) return null;
            
            system.SetWorld(worldContext);
            worldContext.RegisterSubsystem(system);
            return system;
        }

        private static WorldSubsystem GetOrCreate_Internal(WorldContext worldContext, Type type)
        {
            var system = (WorldSubsystem)worldContext.GetSubsystem(type);
            if (system != null) return system;
            
            system = (WorldSubsystem)Activator.CreateInstance(type);
            if (!system.CanCreate(worldContext)) return null;
            
            system.SetWorld(worldContext);
            worldContext.RegisterSubsystem(type, system);
            return system;
        }

        /// <summary>
        /// Get or create system if not registered
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T GetOrCreate<T>() where T : WorldSubsystem, new()
        {
            return !GameWorld.IsValid() ? null : GetOrCreate_Internal<T>(GameWorld.Get());
        }

        /// <summary>
        /// Get or create system if not registered
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public static WorldSubsystem GetOrCreate(Type type)
        {
            return !GameWorld.IsValid() ? null : GetOrCreate_Internal(GameWorld.Get(), type);
        }
    }
}
