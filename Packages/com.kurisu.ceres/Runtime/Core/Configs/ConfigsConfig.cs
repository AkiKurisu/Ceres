using System;
using Ceres.Serialization;
using UnityEngine;

namespace Ceres.Configs
{
    [Serializable]
    [ConfigPath("Ceres.Configs")]
    public class ConfigsConfig : Config<ConfigsConfig>
    {
        [SerializeField]
        internal SerializedType<ISerializeFormatter> configSerializer = SerializedType<ISerializeFormatter>.FromType(typeof(TextSerializeFormatter));

        [SerializeField]
        internal string password;
    }
}