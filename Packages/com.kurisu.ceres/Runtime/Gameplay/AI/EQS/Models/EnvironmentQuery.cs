using System.Collections.Generic;
using Ceres;
using Ceres.Gameplay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// API for query actors
    /// </summary>
    public static class EnvironmentQuery
    {
        [BurstCompile]
        public struct OverlapFieldViewJob : IJobParallelFor
        {
            [ReadOnly]
            public float3 Center;
            
            [ReadOnly]
            public float3 Forward;
            
            [ReadOnly]
            public LayerMask LayerMask;
            
            [ReadOnly]
            public float Radius;
            
            [ReadOnly]
            public float Angle;
            
            [ReadOnly]
            public ActorHandle Ignored;
            
            [ReadOnly]
            public NativeArray<ActorData> Actors;
            
            [NativeDisableParallelForRestriction]
            public NativeList<ActorHandle> ResultActors;
            
            [BurstCompile]
            public void Execute(int index)
            {
                var actor = Actors[index];
                if (MathUtils.IsInLayerMask(actor.Layer, LayerMask)
                && actor.Handle != Ignored
                && math.distance(Center, actor.Position) <= Radius
                && MathUtils.InViewAngle(Center, actor.Position, Forward, Angle))
                {
                    ResultActors.Add(actor.Handle);
                }
            }
        }
        
        /// <summary>
        /// Query actors overlap in field of view immediately
        /// </summary>
        /// <param name="actors"></param>
        /// <param name="position"></param>
        /// <param name="forward"></param>
        /// <param name="radius"></param>
        /// <param name="angle"></param>
        /// <param name="targetMask"></param>
        /// <param name="ignoredActor"></param>
        public static void OverlapFieldView(List<Actor> actors, Vector3 position, Vector3 forward, float radius, float angle, LayerMask targetMask, Actor ignoredActor = null)
        {
            var resultActors = new NativeList<ActorHandle>(Allocator.TempJob);
            var actorData = WorldSubsystem.GetOrCreate<ActorQuerySystem>().GetAllActors(Allocator.TempJob);
            var job = new OverlapFieldViewJob()
            {
                Center = position,
                Forward = forward,
                Radius = radius,
                Angle = angle,
                LayerMask = targetMask,
                Ignored = ignoredActor == null ? default : ignoredActor.GetActorHandle(),
                Actors = actorData,
                ResultActors = resultActors
            };
            job.Schedule(actorData.Length, 32).Complete();
            foreach (var id in resultActors)
            {
                actors.Add(GameWorld.Get().GetActor(id));
            }
            actorData.Dispose();
            resultActors.Dispose();
        }
    }
}
