using System;
using UnityEngine.Assertions;

namespace Ceres.Gameplay
{
    /// <summary>
    /// The Context object ensures that you have safe access to the GameWorld.
    /// </summary>
    public readonly struct WorldContext : IEquatable<WorldContext>
    {
        private readonly GameWorld _world;

        private WorldContext(GameWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// Cast world context to <see cref="GameWorld"/> with check. Always ensure not access to cast world in destroy stage.
        /// </summary>
        /// <returns></returns>
        public GameWorld Cast()
        {
            Assert.IsTrue(IsValid(), "Cannot access a destroyed GameWorld.");
            return _world;
        }

        public override int GetHashCode()
        {
            return IsValid() ? _world.GetHashCode() : 0;
        }
        
        /// <summary>
        /// Get actor from <see cref="ActorHandle"/>
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public Actor GetActor(ActorHandle handle)
        {
            return IsValid() ? _world.GetActor(handle) : null;
        }
        
        /// <summary>
        /// Get <see cref="SubsystemBase"/> from type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetSubsystem<T>() where T : SubsystemBase
        {
            return IsValid() ? _world.GetSubsystem<T>() : null;
        }

        /// <summary>
        /// Get <see cref="SubsystemBase"/> from type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public SubsystemBase GetSubsystem(Type type)
        {
            return IsValid() ? _world.GetSubsystem(type) : null;
        }

        public void RegisterActor(Actor actor, ref ActorHandle handle)
        {
            if (!IsValid()) return;
            _world.RegisterActor(actor, ref handle);
        }

        public void UnregisterActor(Actor actor)
        {
            if (!IsValid()) return;
            _world.UnregisterActor(actor);
        }

        /// <summary>
        /// Register a <see cref="SubsystemBase"/> with type T
        /// </summary>
        /// <param name="subsystem"></param>
        /// <typeparam name="T"></typeparam>
        public void RegisterSubsystem<T>(T subsystem) where T : SubsystemBase
        {
            if (!IsValid()) return;
            _world.RegisterSubsystem(subsystem);
        }

        /// <summary>
        /// Register a <see cref="SubsystemBase"/> with runtime type.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="subsystem"></param>
        public void RegisterSubsystem(Type type, SubsystemBase subsystem)
        {
            if (!IsValid()) return;
            _world.RegisterSubsystem(type, subsystem);
        }
        
        public bool IsValid()
        {
            return (bool)_world && !_world.IsDestroyed;
        }

        #region Operators
                
        public static explicit operator WorldContext(GameWorld gameWorld)
        {
            if (gameWorld && gameWorld.IsDestroyed)
            {
                return default;
            }
            
            return new WorldContext(gameWorld);
        }
        
        public static bool operator ==(WorldContext context1, WorldContext context2)
        {
            return context1._world == context2._world;
        }

        public static bool operator !=(WorldContext context1, WorldContext context2)
        {
            return context1._world != context2._world;
        }
        
        public bool Equals(WorldContext other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldContext other && other == this;
        }

        #endregion // Operators
    }
}