#if UNITY_EDITOR
namespace UnityEditor
{
    internal static class EditorInternalUtil
    {
        public static void ClearConsole()
        {
            LogEntries.Clear();
        }
    }
}
#endif