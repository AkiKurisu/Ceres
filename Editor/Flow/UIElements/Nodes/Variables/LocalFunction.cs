using System;
using Ceres.Graph;

namespace Ceres.Editor.Graph.Flow
{
    /// <summary>
    /// Variable represents local custom function in flow subGraph
    /// </summary>
    /// <remarks>Value is function guid</remarks>
    [Serializable]
    public class LocalFunction : SharedVariable<string>
    {
        public LocalFunction()
        {
            IsShared = true;
            IsExposed = false;
        }
        
        public LocalFunction(string functionName): this()
        {
            Name = functionName;
            Value = Guid.NewGuid().ToString();
        }
        
        protected override SharedVariable<string> CloneT()
        {
            return new LocalFunction { Value = value };
        }
    }
}