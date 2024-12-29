using System;
namespace Ceres
{
    [Serializable]
    public class SharedBool : SharedVariable<bool>
    {
        public SharedBool(bool value)
        {
            this.value = value;
        }
        public SharedBool()
        {

        }
        protected override SharedVariable<bool> CloneT()
        {
            return new SharedBool() { Value = value };
        }
    }
}