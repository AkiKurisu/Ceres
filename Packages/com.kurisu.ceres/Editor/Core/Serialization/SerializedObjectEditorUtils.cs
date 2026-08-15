namespace Ceres.Serialization.Editor
{
    public static class SerializedObjectEditorUtils
    {
        /// <summary>
        /// Cleanup a serializedObjectBase object reference
        /// </summary>
        /// <param name="serializedObjectBase"></param>
        public static void Cleanup(SerializedObjectBase serializedObjectBase)
        {
            serializedObjectBase.objectHandle = 0;
        }

        /// <summary>
        /// Compare internal editing object is equal
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        /// <returns></returns>
        public static bool InternalEqual(SerializedObjectBase object1, SerializedObjectBase object2)
        {
            return object1.objectHandle == object2.objectHandle;
        }
        /// <summary>
        /// Get internal object handle
        /// </summary>
        /// <param name="serializedObjectBase"></param>
        /// <returns></returns>
        public static ulong GetObjectHandle(SerializedObjectBase serializedObjectBase)
        {
            return serializedObjectBase.objectHandle;
        }
    }
}
