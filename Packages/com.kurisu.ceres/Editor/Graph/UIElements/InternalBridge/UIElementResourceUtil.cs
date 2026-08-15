#if UNITY_EDITOR
using System;
using UnityEngine;
using UObject = UnityEngine.Object;
using UnityEditor.UIElements.StyleSheets;
namespace UnityEditor.UIElements
{
    internal static class UIElementResourceUtil
    {
        public static UObject LoadResource(string pathName, Type type)
        {
            return StyleSheetResourceUtil.LoadResource(pathName, type, GUIUtility.pixelsPerPoint);
        }
        
        public static TObject LoadResource<TObject>(string pathName) where TObject: UObject
        {
            return LoadResource(pathName, typeof(TObject)) as TObject;
        }

        public static void ClearConsole()
        {
            LogEntries.Clear();
        }
    }
}
#endif