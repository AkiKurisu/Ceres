using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// Reference article: "Naughty Dog: Human Enemy AI In Last of The Us" with only runtime raycast detect.
    /// </summary>
    [Serializable]
    public struct PostQueryParameters
    {
       [Range(0, 360), Tooltip("Post query angle, can only see target within angle")]
        public float angle;
        
        [Range(0, 500), Tooltip("Post query max distance")]
        public float distance;
        
        [Range(2, 36), Tooltip("Post query iterate step, decrease step to increase performance but loss diversity")]
        public int step;
        
        [Range(1, 6), Tooltip("Post query sampling depth, decrease depth to increase performance but loss precision")]
        public int depth;
        
        private struct RaycastPair
        {
            public bool isHitL;
            
            public bool isHitR;
            
            public RaycastHit hitL;
            
            public RaycastHit hitR;
            
            public Vector3 left;
            
            public Vector3 right;
            
            public readonly Vector3 Half => ((left + right) / 2).normalized;
        }
        
        private static readonly ProfilerMarker m_ProfilerMarker = new("PostQuerier.QueryPosts");
        
        /// <summary>
        /// Query posts in source view using a Fan-Shaped Binary Search immediately.
        /// </summary>
        /// <param name="posts">Store results</param>
        /// <param name="source">View source transform</param>
        /// <param name="direction">View direction</param>
        /// <param name="layerMask"></param>
        /// <returns>Has post</returns>
        public readonly bool QueryPosts(List<Vector3> posts, Transform source, Vector3 direction, LayerMask layerMask)
        {
            using (m_ProfilerMarker.Auto())
            {
                float angleInRadians = angle * Mathf.Deg2Rad;
                Vector3 left = Vector3.RotateTowards(direction, -source.right, angleInRadians / 2, float.MaxValue);
                Vector3 right = Vector3.RotateTowards(direction, source.right, angleInRadians / 2, float.MaxValue);
                float anglePerStep = angleInRadians / step;
                for (int i = 0; i < step - 1; ++i)
                {
                    RaycastPair pair = new()
                    {
                        left = Vector3.RotateTowards(left, right, i * anglePerStep, float.MaxValue).normalized,
                        right = Vector3.RotateTowards(left, right, (i + 1) * anglePerStep, float.MaxValue).normalized
                    };
                    int d = 0;
                    RaycastHit hit = default;
                    while (d < depth)
                    {
                        int count = DoubleRaycast(source.position, layerMask, ref pair, out var newHit);
                        if (count == 1)
                        {
                            d++;
                            hit = newHit;
                            // use the most closest hit
                            if (d == depth)
                            {
                                posts.Add(hit.point);
                                break;
                            }
                            continue;
                        }
                        // use last hit if possible
                        if (d != 0)
                            posts.Add(hit.point);
                        break;
                    }
                }
            }
            return posts.Count > 0;
        }
        
        private readonly int DoubleRaycast(Vector3 from, LayerMask layer, ref RaycastPair rayPair, out RaycastHit hit)
        {
            if (!rayPair.isHitL)
                rayPair.isHitL = Raycast(from, rayPair.left, layer, out rayPair.hitL);
            if (!rayPair.isHitR)
                rayPair.isHitR = Raycast(from, rayPair.right, layer, out rayPair.hitR);

            hit = default;
            if (rayPair.isHitL && rayPair.isHitR)
            {
                return 2;
            }
            if (rayPair.isHitL && !rayPair.isHitR)
            {
                hit = rayPair.hitL;
                rayPair.left = rayPair.Half;
                rayPair.isHitL = false;
                return 1;
            }
            if (!rayPair.isHitL && rayPair.isHitR)
            {
                hit = rayPair.hitR;
                rayPair.right = rayPair.Half;
                rayPair.isHitR = false;
                return 1;
            }
            return 0;
        }
        
        private readonly bool Raycast(Vector3 from, Vector3 direction, LayerMask layer, out RaycastHit hit)
        {
            return Physics.Raycast(from, direction, out hit, distance, layer);
        }
    }
}