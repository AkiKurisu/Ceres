using System;
using Unity.Collections;
using UnityEngine;

namespace Ceres.Gameplay.AI.EQS
{
    /// <summary>
    /// Represents an advanced field of view for AI mentioned in "Naughty Dog: Human Enemy AI In Last of The Us". 
    /// </summary>
    [Serializable]
    public struct FieldView
    {
        [Range(0, 500), Tooltip("Field of view radius, ai can only sensor new target within this radius in far distance")]
        public float radius;
        
        [Range(0, 360), Tooltip("Field of view angle, ai can only see target within angle in far distance")]
        public float angle;
        
        [Range(3, 20), Tooltip("Field of view frustum sides, ai can only see target within frustum in close distance")]
        public int sides;
        
        [Range(0.1f, 1f), Tooltip("Field of view blend weight")]
        public float blend;
        
        public readonly float PolygonRadius => radius * blend * 0.5f;
        
        public FieldView(float radius, float angle, int sides, float blend)
        {
            this.radius = radius;
            this.angle = angle;
            this.sides = sides;
            this.blend = blend;
        }
        
        /// <summary>
        /// Detect whether it can see the target
        /// </summary>
        /// <param name="target"></param>
        /// <param name="fromPosition"></param>
        /// <param name="fromRotation"></param>
        /// <param name="layerMask"></param>
        /// <param name="filterTags"></param>
        /// <returns></returns>
        public readonly bool Detect(Vector3 target, Vector3 fromPosition, Quaternion fromRotation, LayerMask layerMask, string[] filterTags = null)
        {
            // Primary detect
            bool isVisible = true;
            Vector3 forward = fromRotation * Vector3.forward;
            Vector3 directionToTarget = (target - fromPosition).normalized;
            float centerDistance = Vector3.Distance(fromPosition + forward * PolygonRadius, target);
            if (centerDistance < PolygonRadius)
            {
                if (!IsPointInPolygon(fromPosition, fromRotation, target))
                {
                    // When target is nearly on edge, detect whether target is in fov now
                    const float threshold = 0.9f;
                    if (centerDistance >= threshold * PolygonRadius && Vector3.Angle(forward, directionToTarget) <= angle / 2)
                    {
                        goto raycast;
                    }
                    return false;
                }
            }
            else
            {
                if (Vector3.Angle(forward, directionToTarget) > angle / 2)
                {
                    return false;
                }
            }
            
            raycast:
            // Raycast detect, ignore height
            float normalDistance = Vector3.Distance(new Vector3(fromPosition.x, 0, fromPosition.z), new Vector3(target.x, 0, target.z));
            if (normalDistance > this.radius)
            {
                return false;
            }
            Physics.Linecast(fromPosition, target, out RaycastHit hit, layerMask);
            if (hit.collider != null)
            {
                if (CompareTags(hit.collider, filterTags) == false)
                {
                    Debug.DrawLine(hit.point, fromPosition, Color.cyan);
                    isVisible = false;
                }
            }
            return isVisible;
        }
        
        private static bool CompareTags(Component target, string[] allowedTags)
        {
            if (target == null || allowedTags == null) return false;

            bool match = false;
            foreach (string tag in allowedTags)
            {
                if (target.CompareTag(tag)) match = true;
            }
            return match;
        }
        
        public readonly NativeArray<Vector3> AllocatePolygonCorners(Vector3 position, Quaternion rotation, Allocator allocator)
        {
            Vector3 forward = rotation * Vector3.forward;
            var frustumCorners = new NativeArray<Vector3>(sides, allocator);
            float angleStep = 360f / sides;

            for (int i = 0; i < sides; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                frustumCorners[i] = new Vector3(Mathf.Cos(angle) * PolygonRadius, 0, Mathf.Sin(angle) * PolygonRadius);
            }
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                frustumCorners[i] = position + forward * PolygonRadius + rotation * frustumCorners[i];
            }
            return frustumCorners;
        }
        
        public readonly bool IsPointInPolygon(Vector3 position, Quaternion rotation, Vector3 p)
        {
            var polygonCorners = AllocatePolygonCorners(position, rotation, Allocator.Temp);
            var j = polygonCorners.Length - 1;
            var inside = false;
            for (int i = 0; i < polygonCorners.Length; j = i++)
            {
                var pi = polygonCorners[i];
                var pj = polygonCorners[j];
                if (((pi.z <= p.z && p.z < pj.z) || (pj.z <= p.z && p.z < pi.z)) &&
                    (p.x < (pj.x - pi.x) * (p.z - pi.z) / (pj.z - pi.z) + pi.x))
                    inside = !inside;
            }
            polygonCorners.Dispose();
            return inside;
        }
        
        public readonly void DrawGizmos(Vector3 position, Quaternion rotation)
        {
#if UNITY_EDITOR
            // Draw fov
            Vector3 forward = rotation * Vector3.forward;
            UnityEditor.Handles.color = Color.green;
            UnityEditor.Handles.DrawWireDisc(position, Vector3.up, radius);

            UnityEditor.Handles.color = Color.red;
            UnityEditor.Handles.DrawWireArc(position, Vector3.up, forward, angle / 2, radius - 0.1f);
            UnityEditor.Handles.DrawWireArc(position, Vector3.up, forward, -angle / 2, radius - 0.1f);

            UnityEditor.Handles.color = new Color(1, 0, 0, 0.1f);
            UnityEditor.Handles.DrawSolidArc(position, Vector3.up, forward, angle / 2, radius - 0.2f);
            UnityEditor.Handles.DrawSolidArc(position, Vector3.up, forward, -angle / 2, radius - 0.2f);

            // Draw polygon
            var frustumCorners = AllocatePolygonCorners(position, rotation, Allocator.Temp);
            Gizmos.color = Color.red;
            for (int i = 0; i < frustumCorners.Length; i++)
            {
                int nextIndex = (i + 1) % frustumCorners.Length;
                Gizmos.DrawLine(frustumCorners[i], frustumCorners[nextIndex]);
            }
            frustumCorners.Dispose();
#endif
        }
    }
}