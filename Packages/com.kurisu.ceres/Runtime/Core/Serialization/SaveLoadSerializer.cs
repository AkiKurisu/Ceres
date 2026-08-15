using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using Ceres.Configs;
using Newtonsoft.Json;
using UnityEngine;

namespace Ceres.Serialization
{
    /// <summary>
    /// Implement to customize serialization format
    /// </summary>
    public interface ISerializeFormatter
    {
        /// <summary>
        /// Serialize data to a stream
        /// </summary>
        /// <param name="stream">Target stream to write to</param>
        /// <param name="data">Data to serialize</param>
        void Serialize(Stream stream, string data);

        /// <summary>
        /// Deserialize data from a stream
        /// </summary>
        /// <param name="stream">Source stream to read from</param>
        /// <returns>Deserialized data</returns>
        string Deserialize(Stream stream);
    }

    /// <summary>
    /// Binary formatter using .NET BinaryFormatter for serialization
    /// </summary>
    public class BinarySerializeFormatter : ISerializeFormatter
    {
        private static readonly BinaryFormatter Formatter = new();

        /// <summary>
        /// Default singleton instance
        /// </summary>
        public static readonly BinarySerializeFormatter Instance = new();

        public void Serialize(Stream stream, string data)
        {
            Formatter.Serialize(stream, data);
        }

        public string Deserialize(Stream stream)
        {
            try
            {
                return (string)Formatter.Deserialize(stream);
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Plain text formatter using UTF-8 encoding for serialization
    /// </summary>
    public class TextSerializeFormatter : ISerializeFormatter
    {
        /// <summary>
        /// Default singleton instance
        /// </summary>
        public static readonly TextSerializeFormatter Instance = new();

        public void Serialize(Stream stream, string data)
        {
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(data);
        }

        public string Deserialize(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            try
            {
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Encrypted formatter using AES encryption
    /// </summary>
    public class EncryptedSerializeFormatter : ISerializeFormatter
    {
        private readonly byte[] _key;

        private readonly byte[] _iv;

        /// <summary>
        /// Creates an encrypted formatter from config
        /// </summary>
        public EncryptedSerializeFormatter(): this(ConfigsConfig.Get().password)
        {

        }

        /// <summary>
        /// Creates an encrypted formatter with a password-based key
        /// </summary>
        /// <param name="password">Password to derive the encryption key from</param>
        public EncryptedSerializeFormatter(string password)
        {
            // Use PBKDF2 to derive a key from the password
            using var deriveBytes = new Rfc2898DeriveBytes(password,
                Encoding.UTF8.GetBytes("Ceres_Salt_Value"),
                10000); // Iterations

            _key = deriveBytes.GetBytes(32); // 32 bytes for AES-256
            _iv = deriveBytes.GetBytes(16);  // 16 bytes for IV
        }

        public void Serialize(Stream stream, string data)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write, leaveOpen: true);
            using var writer = new StreamWriter(cryptoStream, Encoding.UTF8);
            writer.Write(data);
        }

        public string Deserialize(Stream stream)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            try
            {
                using var decryptor = aes.CreateDecryptor();
                using var cryptoStream = new CryptoStream(stream, decryptor, CryptoStreamMode.Read, leaveOpen: true);
                using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Serializer for read and write saving files
    /// </summary>
    public class SaveLoadSerializer
    {
        public static class TypeCache<T>
        {
            public static readonly bool PreferJsonConvert;

            static TypeCache()
            {
                PreferJsonConvert = typeof(T).GetCustomAttribute<PreferJsonConvertAttribute>() != null;
            }
        }

        private readonly string _path;

        private readonly string _extension;

        private readonly ISerializeFormatter _serializeFormatter;

        public SaveLoadSerializer(string path, string extension, ISerializeFormatter serializeFormatter)
        {
            _path = path;
            _extension = extension;
            _serializeFormatter = serializeFormatter;
        }

        public void Serialize<T>(T data)
        {
            Serialize(typeof(T).Name, data);
        }

        public void Serialize<T>(string key, T data)
        {
            string jsonData;
            if (TypeCache<T>.PreferJsonConvert)
                jsonData = JsonConvert.SerializeObject(data);
            else
                jsonData = JsonUtility.ToJson(data);
            if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);
            using var file = File.Create($"{_path}/{key}.{_extension}");
            _serializeFormatter.Serialize(file, jsonData);
        }

        public void Delete(string key)
        {
            if (!Directory.Exists(_path)) return;
            var path = $"{_path}/{key}.{_extension}";
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public void DeleteAll()
        {
            if (Directory.Exists(_path)) Directory.Delete(_path, true);
        }

        public void Serialize(string key, string jsonData)
        {
            if (!Directory.Exists(_path)) Directory.CreateDirectory(_path);
            using var file = File.Create($"{_path}/{key}.{_extension}");
            _serializeFormatter.Serialize(file, jsonData);
        }

        public bool Exists(string key)
        {
            return File.Exists($"{_path}/{key}.{_extension}");
        }

        public bool TryDeserialize(string key, out string jsonData)
        {
            var path = $"{_path}/{key}.{_extension}";
            if (File.Exists(path))
            {
                using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                jsonData = _serializeFormatter.Deserialize(file);
                return true;
            }
            jsonData = null;
            return false;
        }

        public bool Overwrite<T>(T data)
        {
            return Overwrite(typeof(T).Name, data);
        }

        public bool Overwrite<T>(string key, T data)
        {
            var path = $"{_path}/{key}.{_extension}";
            if (File.Exists(path))
            {
                using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (TypeCache<T>.PreferJsonConvert)
                    JsonConvert.PopulateObject(_serializeFormatter.Deserialize(file), data);
                else
                    JsonUtility.FromJsonOverwrite(_serializeFormatter.Deserialize(file), data);
                return true;
            }
            return false;
        }

        public T DeserializeOrNew<T>() where T : class, new()
        {
            return DeserializeOrNew<T>(typeof(T).Name);
        }

        public T DeserializeOrNew<T>(string key) where T : class, new()
        {
            T data = null;
            var path = $"{_path}/{key}.{_extension}";
            if (File.Exists(path))
            {
                using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (TypeCache<T>.PreferJsonConvert)
                    data = JsonConvert.DeserializeObject<T>(_serializeFormatter.Deserialize(file));
                else
                    data = JsonUtility.FromJson<T>(_serializeFormatter.Deserialize(file));
            }
            data ??= new T();
            return data;
        }

        public T Deserialize<T>(string key)
        {
            var path = $"{_path}/{key}.{_extension}";
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (TypeCache<T>.PreferJsonConvert)
                return JsonConvert.DeserializeObject<T>(_serializeFormatter.Deserialize(file));
            return JsonUtility.FromJson<T>(_serializeFormatter.Deserialize(file));
        }

        public object Deserialize(string key, Type type, bool preferJsonConvert)
        {
            var path = $"{_path}/{key}.{_extension}";
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (preferJsonConvert)
                return JsonConvert.DeserializeObject(_serializeFormatter.Deserialize(file), type);
            return JsonUtility.FromJson(_serializeFormatter.Deserialize(file), type);
        }
    }
}