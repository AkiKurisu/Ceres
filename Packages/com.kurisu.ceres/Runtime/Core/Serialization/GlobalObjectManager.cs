using System;
using Ceres.Collections;
using UnityEngine.Assertions;
namespace Ceres.Serialization
{
    /// <summary>
    /// Class for managing dynamically load/created <see cref="object"/> 
    /// </summary>
    public static class GlobalObjectManager
    {
        /// <summary>
        /// Cleanup global objects
        /// </summary>
        public static void Cleanup()
        {
            // Should not reset, since some modules may still keep old reference, so only increase serialNum.
            _serialNum += 1;
            GlobalObjects.Clear();
            OnGlobalObjectCleanup?.Invoke();
            _isDirty = true;
        }
        
        public delegate void GlobalObjectCleanupDelegate();
        
        /// <summary>
        /// Fired when GlobalObjectManager cleanup, subscribe this event to clean up all references to SoftObjectHandle.
        /// </summary>
        public static event GlobalObjectCleanupDelegate OnGlobalObjectCleanup;
        
        /// <summary>
        /// Container for Object
        /// </summary>
        internal struct ObjectStructure
        {
            public object Object;
            
            public SoftObjectHandle Handle;
        }
        
        private static ulong _serialNum = 1;
        
        private static readonly SparseArray<ObjectStructure> GlobalObjects = new(10, SoftObjectHandle.MaxIndex);
        
        private static bool _isDirty;
        
        internal static void ForEach(Action<ObjectStructure> func)
        {
            Assert.IsNotNull(func);
            foreach (var gObject in GlobalObjects)
            {
                func(gObject);
            }
        }
        
        internal static bool CheckAndResetDirty()
        {
            if (_isDirty)
            {
                _isDirty = false;
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get managed global objects count
        /// </summary>
        /// <returns></returns>
        public static int GetObjectNum()
        {
            return GlobalObjects.Count;
        }
        
        /// <summary>
        /// Get object by soft reference if exists
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static object GetObject(SoftObjectHandle handle)
        {
            int index = handle.GetIndex();
            if (handle.IsValid() && GlobalObjects.IsAllocated(index))
            {
                var structure = GlobalObjects[index];
                if (structure.Handle.GetSerialNumber() != handle.GetSerialNumber()) return null;
                return structure.Object;
            }
            return null;
        }
        
        /// <summary>
        /// Register object to global object manager
        /// </summary>
        /// <param name="object"></param>
        /// <param name="handle"></param>
        public static void RegisterObject(object @object, ref SoftObjectHandle handle)
        {
            if (GetObject(handle) != null)
            {
                return;
            }
            var structure = new ObjectStructure { Object = @object };
            int index = GlobalObjects.AddUninitialized();
            handle = new SoftObjectHandle(_serialNum, index);
            structure.Handle = handle;
            GlobalObjects[index] = structure;
            _isDirty = true;
        }
        
        /// <summary>
        /// Unregister object from global object manager
        /// </summary>
        /// <param name="handle"></param>
        public static void UnregisterObject(SoftObjectHandle handle)
        {
            int index = handle.GetIndex();
            if (GlobalObjects.IsAllocated(index))
            {
                var current = GlobalObjects[index];
                if (current.Handle.GetSerialNumber() != handle.GetSerialNumber())
                {
                    return;
                }
                // increase serial num as version update
                ++_serialNum;
                GlobalObjects.RemoveAt(index);
                _isDirty = true;
            }
        }
    }
}