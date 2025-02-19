using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UObject = UnityEngine.Object;
namespace Ceres
{
    [Serializable]
    internal class UObjectLink
    {
        public int instanceId;
             
        public UObject linkedObject;

        public UObjectLink(UObject uObject)
        {
            linkedObject = uObject;
            instanceId = linkedObject.GetInstanceID();
        }

        /// <summary>
        /// Link UObject reference and return resolved json
        /// </summary>
        /// <param name="uobjectLinks"></param>
        /// <param name="serializedData"></param>
        /// <returns></returns>
        public static string Resolve(UObjectLink[] uobjectLinks, string serializedData)
        {
            var obj = JObject.Parse(serializedData);
            /* Resolve instanceID */
            foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
            {
                if (prop.Name != "instanceID") continue;
                var id = (int)prop.Value;
                var uObject = uobjectLinks.FirstOrDefault(x=> x.instanceId == id);
                if (uObject != null)
                {
                    var linkedUObject = uObject.linkedObject;
                    prop.Value = linkedUObject == null ? 0 : linkedUObject.GetInstanceID();
                    if (linkedUObject && CeresLogger.LogUObjectRelink)
                    {
                        CeresLogger.Log($"Relink UObject {id} to {uObject.linkedObject.name} {prop.Value}");
                    }
                }
            }

            return obj.ToString();
        }
        
        /// <summary>
        /// Parse UObject references from json and fill the array
        /// </summary>
        /// <param name="uobjectLinks"></param>
        /// <param name="serializedData"></param>
        public static void Parse(ref UObjectLink[] uobjectLinks, string serializedData)
        {
#if UNITY_EDITOR
            var obj = JObject.Parse(serializedData);
            /* Persistent instanceID */
            foreach (var prop in obj.Descendants().OfType<JProperty>().ToList())
            {
                if (prop.Name != "instanceID") continue;
                var instanceId = (int)prop.Value;
                var uObject = UnityEditor.EditorUtility.InstanceIDToObject(instanceId);
                if (uObject)
                {
                    Chris.Collections.ArrayUtils.Add(ref uobjectLinks, new UObjectLink(uObject));
                }
            }
#endif
        }
    }
}