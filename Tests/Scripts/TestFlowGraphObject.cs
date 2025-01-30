using System.Diagnostics;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace Ceres.Tests
{
    public class TestFlowGraphObject : FlowGraphObject
    {
        [ImplementableEvent]
        public void Start()
        {
            this.ProcessEvent();
        }
        
        [ImplementableEvent]
        public void OnDestroy()
        {
            Debug.Log("Script side received OnDestroy", this);
            using var evt = ExecuteFlowEvent.Create(nameof(OnDestroy), ExecuteFlowEvent.DefaultArgs);
            if (this.GetRuntimeFlowGraph().TryExecuteEvent(Object, evt.FunctionName, evt))
            {
                Debug.Log("Call graph side event OnDestroy succeed", this);
            }
            ReleaseGraph();
        }

        [ExecutableFunction]
        public void TestFunctionVoid()
        {
            Debug.Log($"Execute {nameof(TestFunctionVoid)}");
        }
        
        [ExecutableFunction]
        public string TestFunctionReturn(string input1, string input2)
        {
            var returnValue = $"{input1} {input2}";
            Debug.Log($"Execute {nameof(TestFunctionReturn)}, input1: {input1} input2: {input2}, return {returnValue}");
            return returnValue;
        }
        
        [ExecutableFunction]
        public void TestImplementableEvent()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();
            Flow_OnCall(gameObject);
            stopWatch.Stop(); 
            Debug.Log($"Execute {nameof(TestImplementableEvent)}, used: {stopWatch.ElapsedMilliseconds}ms");
        }
        
        [ImplementableEvent]
        private void Flow_OnCall(GameObject inGameObject)
        {
            using (new FlowGraphDependencyTracker(this.GetRuntimeFlowGraph()).Auto())
            {
                this.ProcessEvent(inGameObject);
            }
        }
    }
}
