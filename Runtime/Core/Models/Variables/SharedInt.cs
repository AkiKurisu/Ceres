using System;
namespace Ceres
{
    [Serializable]
    public class SharedInt : SharedVariable<int>
    {
        public SharedInt(int value)
        {
            this.value = value;
        }
        public SharedInt()
        {

        }
        protected override SharedVariable<int> CloneT()
        {
            return new SharedInt() { Value = value };
        }
    }
}