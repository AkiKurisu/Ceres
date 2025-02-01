using System;
using Chris.Serialization;

namespace Ceres.Graph
{
    [FormerlySerializedType("Ceres.SharedString, Ceres")]
    [Serializable]
    public class SharedString : SharedVariable<string>
    {
        public SharedString(string value)
        {
            this.value = value;
        }
        public SharedString()
        {

        }
        protected override SharedVariable<string> CloneT()
        {
            return new SharedString() { Value = value };
        }
    }
}
