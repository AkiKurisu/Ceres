using System;
using System.IO;
using System.Reflection;
using Ceres.Configs;
using Ceres.Configs.Editor;
using Ceres.Serialization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ceres.Editor
{
    /// <summary>
    /// Mark config singleton should save as project wide config, else will save as platform specific config.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BaseConfigAttribute : Attribute
    {
        
    }
    
    /// <summary>
    ///   <para>Generic class for storing Editor Config.</para>
    /// </summary>
    public class ConfigSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (!_instance)
                    CreateAndLoad();
                return _instance;
            }
        }

        protected ConfigSingleton()
        {
            if (_instance)
            {
                Debug.LogError("ConfigSingleton already exists. Did you query the singleton in a constructor?");
            }
            else
            {
                _instance = (object)this as T;
            }
        }

        private static void CreateAndLoad()
        {
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
                InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            if (_instance)
                return;
            CreateInstance<T>().hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
        }

        protected virtual void Save(bool saveAsText)
        {
            if (!_instance)
            {
                Debug.LogError("Cannot save ScriptableSingleton: no instance!");
            }
            else
            {
                string filePath = GetFilePath();
                if (!string.IsNullOrEmpty(filePath))
                {
                    string directoryName = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(directoryName))
                        Directory.CreateDirectory(directoryName!);
                    InternalEditorUtility.SaveToSerializedFileAndForget(new Object[]
                    {
                        _instance
                    }, filePath, saveAsText);
                }
                else
                {
                    Debug.LogWarning($"Saving has no effect. Your class '{GetType()}' is missing the ConfigPathAttribute. " +
                                     $"Use this attribute to specify where to save your ConfigSingleton.\n" +
                                     $"Only call Save() and use this attribute if you want your state to survive between sessions of Unity.");
                }
            }
        }

        private static bool _isPlatformConfig;
        
        private static string _filePath;
        
        private static string GetFilePath()
        {
            if (string.IsNullOrEmpty(_filePath))
            {
                _isPlatformConfig = typeof(T).GetCustomAttribute<BaseConfigAttribute>() == null;
                _filePath = _isPlatformConfig
                    ? $"ProjectSettings/{EditorUserBuildSettings.activeBuildTarget.ToString()}/{typeof(T).Name}.asset"
                    : $"ProjectSettings/{typeof(T).Name}.asset";
            }
            
            return _filePath;
        }
        
        /// <summary>
        /// Get config serializer
        /// </summary>
        /// <returns></returns>
        private static SaveLoadSerializer GetConfigSerializer()
        {
            return ConfigsEditorUtils.GetConfigSerializer(_isPlatformConfig);
        }
        
        /// <summary>
        /// Serialize config
        /// </summary>
        /// <returns></returns>
        protected static void Serialize(ConfigBase configBase)
        {
            configBase.Save(GetConfigSerializer());
        }
        
        /// <summary>
        /// Serialize config with providing location
        /// </summary>
        /// <param name="configFileLocation"></param>
        /// <param name="configBase"></param>
        protected static void Serialize(ConfigFileLocation configFileLocation, IConfigFile configBase)
        {
            GetConfigSerializer().Serialize(configFileLocation, configBase);
        }
    }
}