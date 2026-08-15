using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Ceres
{
    /// <summary>
    /// Utils for Mathematics
    /// </summary>
    /// <remarks>Methods should be annotated with <see cref="MethodImplOptions.AggressiveInlining"/>
    /// which allows the burst compiler to optimize the method while optimizing the method's caller.</remarks>
    public static class MathUtils
    {
        /// <summary>
        /// <see cref="Matrix4x4.MultiplyVector"/> for <see cref="float4x4"/>
        /// </summary>
        /// <param name="worldMatrix"></param>
        /// <param name="point"></param>
        /// <param name="result"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyVector(ref this float4x4 worldMatrix, in float3 point, ref float3 result)
        {
            result = math.mul(worldMatrix, new float4(point, 0.0f)).xyz;
        }

        /// <summary>
        /// <see cref="Matrix4x4.MultiplyPoint3x4"/> for <see cref="float4x4"/>
        /// </summary>
        /// <param name="worldMatrix"></param>
        /// <param name="point"></param>
        /// <param name="result"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyPoint3x4(ref this float4x4 worldMatrix, in float3 point, ref float3 result)
        {
            result = math.mul(worldMatrix, new float4(point, 1.0f)).xyz;
        }

        // thanks to https://discussions.unity.com/t/rotate-towards-c-jobs/778453/5
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static quaternion RotateTowards(in quaternion from, in quaternion to, in float maxDegreesDelta)
        {
            float num = Angle(from, to);
            return num < float.Epsilon ? to : math.slerp(from, to, math.min(1f, maxDegreesDelta / num));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Angle(in quaternion q1, in quaternion q2)
        {
            var dot = math.dot(q1, q2);
            return !(dot > 0.999998986721039) ? (float)(math.acos(math.min(math.abs(dot), 1f)) * 2.0) : 0.0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InViewAngle(in float3 center, in float3 position, in float3 forward, in float angle)
        {
            float3 targetPosition = position;
            targetPosition.y = center.y;

            float3 directionToTarget = math.normalize(targetPosition - center);

            return Angle(forward, directionToTarget) <= angle / 2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Angle(in float3 from, in float3 to)
        {
            float num = math.sqrt(math.sqrt(math.dot(from, from)) * math.sqrt(math.dot(to, to)));
            if (num < 1E-15f)
            {
                return 0f;
            }
            float num2 = math.clamp(math.dot(from, to) / num, -1f, 1f);
            return (float)math.acos(num2) * 57.29578f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInLayerMask(in int layer, in LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPointInPolygon(in NativeArray<float3> polygonCorners, in float3 p)
        {
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
            return inside;
        }

        /// <summary>
        /// Quadratic Bézier curve, dynamically draw a curve based on three points
        /// </summary>
        /// <param name="t">The arrival coefficient is 0 for the start and 1 for the arrival</param>
        /// <param name="p0">Starting point</param>
        /// <param name="p1">Middle point</param>
        /// <param name="p2">End point</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float3 GetQuadraticCurvePoint(in float t, in float3 p0, in float3 p1, in float3 p2)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            return uu * p0 + 2 * u * t * p1 + tt * p2;
        }
    }
}
