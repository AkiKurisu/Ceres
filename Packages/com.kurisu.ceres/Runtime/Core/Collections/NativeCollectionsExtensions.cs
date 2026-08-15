using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
namespace Ceres.Collections
{
    /// <summary>
    /// Extensions for Native Collections
    /// </summary>
    public static class NativeCollectionsExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafe<T>(ref this NativeArray<T> array) where T : unmanaged
        {
            if (array.IsCreated)
                array.Dispose();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafe<T>(ref this NativeList<T> array) where T : unmanaged
        {
            if (array.IsCreated)
                array.Dispose();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisposeSafe<T, K>(ref this NativeParallelMultiHashMap<T, K> map) where T : unmanaged, IEquatable<T> where K : unmanaged
        {
            if (map.IsCreated)
                map.Dispose();
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Resize<T>(ref this NativeArray<T> array, int size, Allocator allocator = Allocator.Persistent, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : unmanaged
        {
            if (array.IsCreated == false || array.Length < size)
            {
                array.DisposeSafe();
                array = new NativeArray<T>(size, allocator, options);
            }
        }
    }
}