using System;
using Chris.Serialization;

namespace Ceres.Graph
{
    [FormerlySerializedType("Ceres.SharedFloat, Ceres")]
    [Serializable]
    public class SharedFloat : SharedVariable<float>
    {
        public SharedFloat(float value)
        {
            this.value = value;
        }
        
        public SharedFloat()
        {

        }
        
        protected override SharedVariable<float> CloneT()
        {
            return new SharedFloat() { Value = value };
        }
    }
}