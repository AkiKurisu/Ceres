using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ceres.AnimationPacking
{
    internal static class AnimationBinaryMenu
    {
        private const string Root = "Tools/Ceres/Animation/";

        [MenuItem(Root + "Pack", true)]
        private static bool CanPack() => GetSelectedPath(".anim") != null;

        [MenuItem(Root + "Pack")]
        private static void Pack()
        {
            string source = GetSelectedPath(".anim");
            string destination = EditorUtility.SaveFilePanelInProject(
                "Pack Animation",
                Path.GetFileNameWithoutExtension(source),
                AnimationBinaryFormat.Extension,
                "Choose the Animation Binary destination.");
            if (string.IsNullOrEmpty(destination))
                return;
            AnimationBinaryUtility.Pack(source, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(destination);
        }

        [MenuItem(Root + "Extract", true)]
        private static bool CanExtract() => GetSelectedPath("." + AnimationBinaryFormat.Extension) != null;

        [MenuItem(Root + "Extract")]
        private static void Extract()
        {
            string source = GetSelectedPath("." + AnimationBinaryFormat.Extension);
            string destination = EditorUtility.SaveFilePanelInProject(
                "Extract Animation",
                Path.GetFileNameWithoutExtension(source),
                "anim",
                "Choose the editable AnimationClip destination.");
            if (string.IsNullOrEmpty(destination))
                return;
            AnimationBinaryUtility.Extract(source, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(destination);
        }

        private static string GetSelectedPath(string extension)
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase)
                ? path
                : null;
        }
    }
}
