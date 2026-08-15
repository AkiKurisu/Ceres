using System.Collections.Generic;
using System.Linq;
using Ceres.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ceres.Configs
{
    [PreferJsonConvert]
    internal class ConfigFile : Dictionary<string, string>, IConfigFile
    {
        public ConfigFileLocation Location { get; internal set; }

        public ConfigFile()
        {

        }

        public ConfigFile(ConfigFileLocation location)
        {
            Location = location;
        }

        public (string configName, string configData)[] GetAllConfigData()
        {
            return this.Select(pair => (pair.Key, pair.Value)).ToArray();
        }

        public bool TryGetConfig(IConfigLocation location, out ConfigBase config)
        {
            config = null;
            if (TryGetValue(location.ConfigName, out var data))
            {
                if (location.PreferJsonConvert)
                {
                    config = JsonConvert.DeserializeObject(data, location.Type) as ConfigBase;
                }
                else
                {
                    config = JsonUtility.FromJson(data, location.Type) as ConfigBase;
                }
            }
            return config != null;
        }

        public void SetConfig(IConfigLocation location, ConfigBase config)
        {
            string jsonData;
            if (location.PreferJsonConvert)
            {
                jsonData = JsonConvert.SerializeObject(config);
            }
            else
            {
                jsonData = JsonUtility.ToJson(config);
            }
            this[location.ConfigName] = jsonData;
        }

        public void MergeConfigFile(IConfigFile targetFile)
        {
            var allConfigDataEntries = targetFile.GetAllConfigData();
            foreach (var configDataEntry in allConfigDataEntries)
            {
                if (TryGetValue(configDataEntry.configName, out var currentData))
                {
                    // Merge fields using JToken
                    var currentJToken = JToken.Parse(currentData);
                    var targetJToken = JToken.Parse(configDataEntry.configData);

                    var mergedJToken = MergeProperties(currentJToken, targetJToken);
                    this[configDataEntry.configName] = mergedJToken.ToString(Formatting.None);
                }
                else
                {
                    // If current config doesn't exist, use the target data directly
                    this[configDataEntry.configName] = configDataEntry.configData;
                }
            }
        }

        /// <summary>
        /// Merge two JTokens at property level non-recursively.
        /// </summary>
        private static JToken MergeProperties(JToken current, JToken target)
        {
            if (current is JObject currentObj && target is JObject targetObj)
            {
                var merged = new JObject();

                // Merge or override with target object properties
                foreach (var property in targetObj.Properties())
                {
                    merged[property.Name] = property.Value;
                }

                // Overwrite all properties from current object
                foreach (var property in currentObj.Properties())
                {
                    merged[property.Name] = property.Value;
                }

                return merged;
            }

            return target;
        }
    }
}