using Chris.Schedulers;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
namespace Ceres.Graph
{
    internal static class CeresPortSetup
    {
        [RuntimeInitializeOnLoadMethod]
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        private static void InitializeOnLoad()
        {
            /* Implicit conversation */
            // ======================== Value type =========================== //
            CeresPort<float>.MakeCompatibleTo<int>(f => (int)f);
            CeresPort<int>.MakeCompatibleTo<float>(i => i);
            CeresPort<Vector3>.MakeCompatibleTo<Vector2>(vector3 => vector3);
            // ======================== Value type =========================== //
            // ========================= Scheduler =========================== //
            unsafe
            {
                CeresPort<SchedulerHandle>.MakeCompatibleTo<double>(handle =>
                {
                    double value = default;
                    UnsafeUtility.CopyStructureToPtr(ref handle, &value);
                    return value;
                });
                CeresPort<double>.MakeCompatibleTo<SchedulerHandle>(d =>
                {
                    SchedulerHandle handle = default;
                    UnsafeUtility.CopyStructureToPtr(ref d, &handle);
                    return handle;
                });
            }
            // ========================= Scheduler =========================== //
        }
    }
}