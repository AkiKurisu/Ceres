using System;
using System.Collections.Generic;
using Ceres.Flow;
using Ceres.Flow.Annotations;
using Ceres.Serialization;
using UnityEngine;
using UnityEngine.Assertions;

namespace Ceres.Gameplay
{
    /// <summary>
    /// Actor is a MonoBehaviour to place GameObject in Gameplay level.
    /// </summary>
    public class Actor :
        FlowGraphObject
    {
        /// <summary>
        /// Settings contains multi-types of flow graph dependencies for <see cref="Actor"/>
        /// </summary>
        [Serializable]
        public class FlowGraphSettings
        {
            [Tooltip("Set to let actor create flow graph instance from asset")]
            public FlowGraphAsset graphAsset;
        
            [Tooltip("Address for sharing flow graph dependencies from DataTable")]
            public string actorAddress;
        }
        
        private WorldContext _worldContext;
        
        private PlayerController _controller;
        
        private ActorHandle _handle;
        
        private readonly HashSet<ActorComponent> _actorComponents = new();

        [Tooltip("Advanced settings for loading and updating flow graph")]
        public FlowGraphSettings advancedSettings = new();

        [ImplementableEvent]
        protected virtual void Awake()
        {
            RegisterActor(this);
        }
        
        [ImplementableEvent]
        protected virtual void OnEnable()
        {

        }

        [ImplementableEvent]
        protected virtual void Start()
        {

        }
        
        [ImplementableEvent]
        protected virtual void OnDisable()
        {

        }
        
        [ImplementableEvent]
        protected virtual void Update()
        {

        }
        
        [ImplementableEvent]
        protected virtual void FixedUpdate()
        {

        }
        
        [ImplementableEvent]
        protected virtual void LateUpdate()
        {

        }

        [ImplementableEvent]
        protected virtual void OnDestroy()
        {
            UnregisterActor(this);
            ReleaseGraph();
            _actorComponents.Clear();
        }
        
        public override FlowGraph GetFlowGraph()
        {
            if (Application.isPlaying)
            {
                if (!string.IsNullOrEmpty(advancedSettings.actorAddress))
                {
                    var asset = ActorFlowGraphSubsystem.Get().GetFlowGraphAsset(advancedSettings.actorAddress);
                    if (asset)
                    {
                        return asset.GetFlowGraph();
                    }
                }
                if (advancedSettings.graphAsset) return advancedSettings.graphAsset.GetFlowGraph();
            }

            return base.GetFlowGraph();
        }
        
        /// <summary>
        /// Get actor's world
        /// </summary>
        /// <returns></returns>
        [ExecutableFunction]
        public GameWorld GetWorld()
        {
            return _worldContext.Cast();
        }

        /// <summary>
        /// Get actor's id according to actor's world
        /// </summary>
        /// <returns></returns>
        [ExecutableFunction]
        public ActorHandle GetActorHandle() => _handle;
        
        /// <summary>
        /// Register an actor to world
        /// </summary>
        /// <param name="actor"></param>
        protected static void RegisterActor(Actor actor)
        {
            actor._worldContext = GameWorld.Get();
            actor._worldContext.RegisterActor(actor, ref actor._handle);
        }
        
        /// <summary>
        /// Unregister an actor from world
        /// </summary>
        /// <param name="actor"></param>
        protected static void UnregisterActor(Actor actor)
        {
            if (actor._worldContext != GameWorld.Get()) return;
            actor._worldContext.UnregisterActor(actor);
            actor._worldContext = default;
            actor._handle = default;
        }
        
        public TController GetTController<TController>() where TController : PlayerController
        {
            return _controller as TController;
        }
        
        [ExecutableFunction]
        public PlayerController GetController()
        {
            return _controller;
        }
        
        /// <summary>
        /// Register an actor component to actor
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="component"></param>
        internal static void RegisterActorComponent(Actor actor, ActorComponent component)
        {
            Assert.IsNotNull(actor);
            Assert.IsNotNull(component);
            actor._actorComponents.Add(component);
        }
        
        /// <summary>
        /// Unregister an actor component from actor
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="component"></param>
        internal static void UnregisterActor(Actor actor, ActorComponent component)
        {
            Assert.IsNotNull(actor);
            Assert.IsNotNull(component);
            actor._actorComponents.Remove(component);
        }
        
        internal void BindController(PlayerController controller)
        {
            if (_controller != null)
            {
                Debug.LogError("[Actor] Actor already bound to a controller!");
                return;
            }
            _controller = controller;
        }
        
        internal void UnbindController(PlayerController controller)
        {
            if (_controller == controller)
            {
                _controller = null;
            }
        }
        
        public TComponent GetActorComponent<TComponent>() where TComponent : ActorComponent
        {
            foreach (var component in _actorComponents)
            {
                if (component is TComponent tComponent) return tComponent;
            }
            return null;
        }
        
        [ExecutableFunction]
        public ActorComponent GetActorComponent(
            [ResolveReturn] SerializedType<ActorComponent> type)
        {
            foreach (var component in _actorComponents)
            {
                if (component.GetComponentType()== type) return component;
            }
            return null;
        }
        
        public void GetActorComponents<TComponent>(List<TComponent> components) where TComponent : ActorComponent
        {
            foreach (var component in _actorComponents)
            {
                if (component is TComponent tComponent) components.Add(tComponent);
            }
        }
    }
}
