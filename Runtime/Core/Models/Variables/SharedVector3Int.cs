using System;
using UnityEngine;
namespace Ceres
{
    [Serializable]
    public class SharedVector3Int : SharedVariable<Vector3Int>
    {
        public SharedVector3Int(Vector3Int value)
        {
            this.value = value;
        }
        public SharedVector3Int()
        {

        }
        protected override SharedVariable<Vector3Int> CloneT()
        {
            return new SharedVector3Int { Value = value };
        }
    }
}