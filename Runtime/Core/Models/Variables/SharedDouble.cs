using System;
namespace Ceres
{
    [Serializable]
    public class SharedDouble : SharedVariable<double>
    {
        public SharedDouble(int value)
        {
            this.value = value;
        }
        
        public SharedDouble()
        {

        }
        
        protected override SharedVariable<double> CloneT()
        {
            return new SharedDouble { Value = value };
        }
    }
}