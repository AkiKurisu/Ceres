using System.IO;
using UnityEngine;

namespace Ceres.Serialization
{
    /// <summary>
    /// Helper for save load game persistent data
    /// </summary>
    public static class SaveUtility
    {
        static SaveUtility()
        {
            if (!Directory.Exists(SavePath)) Directory.CreateDirectory(SavePath);
        }
        
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        public static readonly string SavePath = Path.Combine(Application.dataPath, "../Saved");
#else
        public static readonly string SavePath = Path.Combine(Application.persistentDataPath, "Saved");
#endif
        
        private static readonly SaveLoadSerializer Serializer = new(SavePath, Extension, BinarySerializeFormatter.Instance);
        
        private const string Extension = "sav";
        
        /// <summary>
        /// Save data to saving
        /// </summary>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        public static void Save<T>(T data)
        {
            Serializer.Serialize(data);
        }
        
        /// <summary>
        /// Delete saving
        /// </summary>
        /// <param name="key"></param>
        public static void Delete(string key)
        {
            Serializer.Delete(key);
        }
        
        /// <summary>
        /// Delete all savings
        /// </summary>
        public static void DeleteAll()
        {
            Serializer.DeleteAll();
        }

        /// <summary>
        /// Save json to saving
        /// </summary>
        /// <param name="key"></param>
        /// <param name="jsonData"></param>
        public static void SaveJson(string key, string jsonData)
        {
            Serializer.Serialize(key, jsonData);
        }
        
        /// <summary>
        /// Whether saving with name exists
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static bool Exists(string key)
        {
            return Serializer.Exists(key);
        }

        /// <summary>
        /// Load json from saving
        /// </summary>
        /// <param name="key"></param>
        /// <param name="jsonData"></param>
        public static bool TryLoadJson(string key, out string jsonData)
        {
            return Serializer.TryDeserialize(key, out jsonData);
        }
        
        /// <summary>
        /// Load json from saving and overwrite object
        /// </summary>
        /// <param name="data"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool Overwrite<T>(T data)
        {
            return Serializer.Overwrite(data);
        }
        
        /// <summary>
        /// Load json from saving and parse to <see cref="T"/> object, if it not exists, allocate new one
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public static T LoadOrNew<T>() where T : class, new()
        {
            return Serializer.DeserializeOrNew<T>();
        }
    }
}
