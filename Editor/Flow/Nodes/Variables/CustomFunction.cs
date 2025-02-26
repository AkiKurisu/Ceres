using System;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Variable represents custom function in flow subGraph
    /// </summary>
    /// <remarks>Value is function guid</remarks>
    [Serializable]
    public class CustomFunction : SharedVariable<string>
    {
        public CustomFunction()
        {
            IsShared = true;
            IsExposed = false;
        }
        
        public CustomFunction(string functionName): this()
        {
            Name = functionName;
            Value = Guid.NewGuid().ToString();
        }
        
        protected override SharedVariable<string> CloneT()
        {
            return new CustomFunction { Value = value };
        }
    }
}