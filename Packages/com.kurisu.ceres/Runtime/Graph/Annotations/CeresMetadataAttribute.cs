using System;
using System.Linq;
using System.Reflection;
using UnityEngine.Pool;
namespace Ceres.Annotations
{
    /// <summary>
    /// Describe metadata for Ceres Graph Editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Method 
                    | AttributeTargets.Parameter | AttributeTargets.ReturnValue)]
    public sealed class CeresMetadataAttribute : Attribute
    {
        public string[] Meta { get; }
        
        public CeresMetadataAttribute(params string[] meta)
        {
            Meta = meta;
        }
    }

    /// <summary>
    /// Util class for parsing ceres metadata
    /// </summary>
    public static class CeresMetadata
    {
        public static string[] GetMetadata(Type type, string tag)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = type.GetCustomAttribute<CeresMetadataAttribute>(true);
                var metadata = attribute?.Meta.FirstOrDefault(x => x.StartsWith($"{tag} = "));
                if (metadata != null)
                {
                    dataList.Add(metadata.Replace($"{tag} = ", string.Empty));
                }
                return dataList.ToArray();
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }
        
                
        public static string[] GetMetadata(MemberInfo memberInfo, string tag)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = memberInfo.GetCustomAttribute<CeresMetadataAttribute>(true);
                var metadata = attribute?.Meta.FirstOrDefault(x => x.StartsWith($"{tag} = "));
                if (metadata != null)
                {
                    dataList.Add(metadata.Replace($"{tag} = ", string.Empty));
                }
                return dataList.ToArray();
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }
        
        public static string[] GetMetadata(ParameterInfo parameterInfo, string tag)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = parameterInfo.GetCustomAttribute<CeresMetadataAttribute>(true);
                var metadata = attribute?.Meta.FirstOrDefault(x => x.StartsWith($"{tag} = "));
                if (metadata != null)
                {
                    dataList.Add(metadata.Replace($"{tag} = ", string.Empty));
                }
                return dataList.ToArray();
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }

        public static bool IsDefined(Type type, string tag, string metadata)
        {
            var array = GetMetadata(type, tag);
            return Array.IndexOf(array, metadata) >= 0;
        }
        
        public static bool IsDefined(Type type, string metadata)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = type.GetCustomAttribute<CeresMetadataAttribute>(true);
                return attribute != null && attribute.Meta.Contains(metadata);
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }

        public static bool IsDefined(MemberInfo memberInfo, string tag, string metadata)
        {
            var array = GetMetadata(memberInfo, tag);
            return Array.IndexOf(array, metadata) >= 0;
        }
        
        public static bool IsDefined(MemberInfo memberInfo, string metadata)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = memberInfo.GetCustomAttribute<CeresMetadataAttribute>(true);
                return attribute != null && attribute.Meta.Contains(metadata);
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }
        
        public static bool IsDefined(ParameterInfo parameterInfo, string metadata)
        {
            var dataList = ListPool<string>.Get();
            try
            {
                var attribute = parameterInfo.GetCustomAttribute<CeresMetadataAttribute>(true);
                return attribute != null && attribute.Meta.Contains(metadata);
            }
            finally
            {
                ListPool<string>.Release(dataList);
            }
        }
    }
}