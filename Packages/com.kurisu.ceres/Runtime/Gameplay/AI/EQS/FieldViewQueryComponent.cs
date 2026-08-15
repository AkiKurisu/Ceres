using System.Collections.Generic;
using Ceres.Gameplay;
using UnityEngine;
using UnityEngine.Pool;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// Field view prime query data provider associated with an Actor as component
    /// </summary>
    public class FieldViewQueryComponent : ActorComponent
    {
        [Header("Data")]
        public FieldView fieldView = new()
        {
            radius = 20,
            angle = 120,
            sides = 8,
            blend = 0.5f
        };
        
        public LayerMask queryLayerMask;
        
        private FieldViewQuerySystem _system;
        
        [Header("Gizmos")]
        public Vector3 offset;
        
        private void Start()
        {
            _system = WorldSubsystem.GetOrCreate<FieldViewQuerySystem>();
            if (_system == null)
            {
                Debug.LogError($"[FieldViewPrimeQueryComponent] Can not get FieldViewPrimeQuerySystem dynamically.");
            }
        }
        
        /// <summary>
        /// Request a new query from target's field view
        /// </summary>
        /// <returns></returns>
        public bool RequestFieldViewQuery()
        {
            if (_system == null)
            {
                return false;
            }
            _system.EnqueueCommand(new FieldViewQueryCommand()
            {
                Self = GetActor().GetActorHandle(),
                FieldView = fieldView,
                LayerMask = queryLayerMask
            });
            return true;
        }
        
        /// <summary>
        /// Query actors overlap in field of view from cache
        /// </summary>
        /// <param name="actors"></param>
        public void CollectViewActors(List<Actor> actors)
        {
            _system.GetActorsInFieldView(GetActor().GetActorHandle(), actors);
        }
        
        /// <summary>
        /// Query actors overlap in field of view from cache with specific type
        /// </summary>
        /// <param name="actors"></param>
        /// <typeparam name="T"></typeparam>
        public void CollectViewActors<T>(List<T> actors) where T : Actor
        {
            var list = ListPool<Actor>.Get();
            CollectViewActors(list);
            foreach (var actor in list)
            {
                if (actor is T tActor) actors.Add(tActor);
            }
            ListPool<Actor>.Release(list);
        }
        
        /// <summary>
        /// Detect whether it can see the target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fromPosition"></param>
        /// <param name="fromRotation"></param>
        /// <param name="filterTags"></param>
        /// <returns></returns>
        public bool Detect(Vector3 target, Vector3 fromPosition, Quaternion fromRotation, string[] filterTags = null)
        {
            return fieldView.Detect(target, fromPosition, fromRotation, queryLayerMask, filterTags);
        }
        
        private void OnDrawGizmos()
        {
            fieldView.DrawGizmos(transform.position + offset, transform.rotation);
        }
    }
}
