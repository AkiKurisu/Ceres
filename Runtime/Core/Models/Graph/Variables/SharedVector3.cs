using System;
using Chris.Serialization;
using UnityEngine;
namespace Ceres.Graph
{
    [FormerlySerializedType("Ceres.SharedVector3, Ceres")]
    [Serializable]
    public class SharedVector3 : SharedVariable<Vector3>
    {
        public SharedVector3(Vector3 value)
        {
            this.value = value;
        }
        
        public SharedVector3()
        {

        }
        
        protected override SharedVariable<Vector3> CloneT()
        {
            return new SharedVector3() { Value = value };
        }
    }
}