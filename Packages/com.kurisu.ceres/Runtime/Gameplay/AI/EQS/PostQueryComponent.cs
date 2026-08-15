using System;
using Ceres.Gameplay;
using Unity.Mathematics;
using UnityEngine;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// Post query data provider associated with an Actor as component
    /// </summary>
    public class PostQueryComponent : ActorComponent
    {
        [Header("Data")]
        public PostQueryParameters postQuery = new()
        {
            angle = 120,
            distance = 30,
            step = 6,
            depth = 3
        };
        
        public LayerMask raycastLayerMask;
        
        public Vector3 raycastOffset;
        
        private PostQuerySystem _system;
        
        private void Start()
        {
            _system = WorldSubsystem.GetOrCreate<PostQuerySystem>();
            if (_system == null)
            {
                Debug.LogError("[PostQueryComponent] Can not get PostQuerySystem dynamically.");
            }
        }
        
        /// <summary>
        /// Request a new post query from target's view
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        public bool RequestPostQuery(Actor target)
        {
            if (_system == null)
            {
                return false;
            }
            if (target == GetActor())
            {
                Debug.LogWarning($"[PostQueryComponent] Can not request post query from self view.");
                return false;
            }
            if (!_system.IsFree(GetActor().GetActorHandle()))
            {
                return false;
            }
            PostQueryCommand command = new()
            {
                Self = GetActor().GetActorHandle(),
                Target = target.GetActorHandle(),
                Parameters = postQuery,
                Offset = raycastOffset,
                LayerMask = raycastLayerMask
            };
            _system.EnqueueCommand(command);
            return true;
        }
        
        /// <summary>
        /// Get current posts
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<float3> GetPosts()
        {
            var readonlyPosts = _system.GetPosts(GetActor().GetActorHandle());
            return !readonlyPosts.IsCreated ? default(ReadOnlySpan<float3>) : readonlyPosts;
        }
    }
}
