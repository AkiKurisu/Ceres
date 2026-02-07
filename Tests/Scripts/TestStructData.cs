using System;
using Ceres.Graph.Flow.Annotations;

namespace Ceres.Tests
{
    /// <summary>
    /// Example: Struct break node source generator
    /// Expected generated: FlowNode_Break_TestStructData
    /// </summary>
    [Serializable]
    [ExecutableEvent]
    public struct TestStructData
    {
        public int id;

        public string name;
    }
}