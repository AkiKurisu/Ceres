using System;
using Chris.Serialization;
using UnityEngine;
namespace Ceres.Graph
{
    [FormerlySerializedType("Ceres.SharedVector2Int, Ceres")]
    [Serializable]
    public class SharedVector2Int : SharedVariable<Vector2Int>
    {
        public SharedVector2Int(Vector2Int value)
        {
            this.value = value;
        }
        
        public SharedVector2Int()
        {

        }
        
        protected override SharedVariable<Vector2Int> CloneT()
        {
            return new SharedVector2Int { Value = value };
        }
    }
}