using System;
using UnityEngine;
namespace Ceres
{
    [Serializable]
    public class SharedVector2 : SharedVariable<Vector2>
    {
        public SharedVector2(Vector2 value)
        {
            this.value = value;
        }
        public SharedVector2()
        {

        }
        protected override SharedVariable<Vector2> CloneT()
        {
            return new SharedVector2 { Value = value };
        }
    }
}