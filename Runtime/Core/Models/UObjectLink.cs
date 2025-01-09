using System;
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
    }
}