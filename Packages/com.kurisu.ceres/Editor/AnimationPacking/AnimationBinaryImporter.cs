using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ceres.AnimationPacking
{
    [ScriptedImporter(AnimationBinaryFormat.CurrentVersion, AnimationBinaryFormat.Extension, AllowCaching = true)]
    public sealed class AnimationBinaryImporter : ScriptedImporter
    {
        private static readonly Regex GuidPattern = new(
            @"\bguid:\s*(?<guid>[0-9a-fA-F]{32})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public override void OnImportAsset(AssetImportContext context)
        {
            string temporaryPath = Path.Combine(
                Path.GetTempPath(),
                "ceres-animation-" + Guid.NewGuid().ToString("N") + ".anim");
            AnimationClip source = null;
            try
            {
                AnimationBinaryUtility.Decompress(context.assetPath, temporaryPath);
                RegisterDependencies(context, temporaryPath);
                source = AnimationBinaryUtility.LoadSingleClip(temporaryPath);
                AnimationClip clip = Rebuild(source);
                context.AddObjectToAsset(AnimationBinaryFormat.ClipIdentifier, clip);
                context.SetMainObject(clip);
            }
            finally
            {
                if (source)
                    DestroyImmediate(source);
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static AnimationClip Rebuild(AnimationClip source)
        {
            _ = AnimationUtility.GetCurveBindings(source);
            _ = AnimationUtility.GetObjectReferenceCurveBindings(source);
            AnimationClip clip = new();
            EditorUtility.CopySerialized(source, clip);
            CopyProperty(source, clip, "m_ClipBindingConstant");
            clip.name = source.name;
            return clip;
        }

        private static void CopyProperty(AnimationClip source, AnimationClip target, string propertyName)
        {
            SerializedProperty sourceProperty = new SerializedObject(source).FindProperty(propertyName);
            SerializedObject targetObject = new(target);
            if (sourceProperty == null || targetObject.FindProperty(propertyName) == null)
                throw new InvalidDataException("Missing AnimationClip property: " + propertyName);
            targetObject.CopyFromSerializedProperty(sourceProperty);
            targetObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterDependencies(AssetImportContext context, string path)
        {
            HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadLines(path))
            {
                foreach (Match match in GuidPattern.Matches(line))
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(match.Groups["guid"].Value);
                    if (!string.IsNullOrEmpty(assetPath) &&
                        !string.Equals(assetPath, context.assetPath, StringComparison.OrdinalIgnoreCase))
                        paths.Add(assetPath);
                }
            }
            foreach (string assetPath in paths)
                context.DependsOnSourceAsset(assetPath);
        }
    }
}
