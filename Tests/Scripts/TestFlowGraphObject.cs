using System.Diagnostics;
using Ceres.Graph;
using Ceres.Graph.Flow;
using Ceres.Graph.Flow.Annotations;
using Chris.Schedulers;
using R3;
using UnityEngine;
using Debug = UnityEngine.Debug;
namespace Ceres.Tests
{
    public class TestFlowGraphObject : FlowGraphObject
    {
        public bool overrideCustomEventImplementation;
        
        [ImplementableEvent]
        public void Start()
        {
            /* Test ILPP work */
#if CERES_DISABLE_ILPP
            this.ProcessEvent();
#endif
            /* Test custom event work */
            Scheduler.Delay(1f, TestSendCustomEvent);
            
            /* Test override custom event implementation work */
            if (overrideCustomEventImplementation)
            {
                this.OverrideEventImplementation<TestGlobalEvent>(ReceiveCustomEvent).AddTo(this);
            }
        }

        private void TestSendCustomEvent()
        {
            using var evt = TestGlobalEvent.GetPooled(100);
            Debug.Log($"Script send {nameof(TestSendCustomEvent)}", this);
            this.SendEvent(evt);
        }
        
        private void ReceiveCustomEvent(TestGlobalEvent evt)
        {
            Debug.Log($"Script receive event {nameof(TestSendCustomEvent)}", this);
            /* Prevent executing flow event */
            evt.PreventDefault();
        }
        
        [ImplementableEvent]
        public void OnDestroy()
        {
#if CERES_DISABLE_ILPP
            /* Manually call flow event */
            using var evt = ExecuteFlowEvent.Create(nameof(OnDestroy), ExecuteFlowEvent.DefaultArgs);
            if (this.GetRuntimeFlowGraph().TryExecuteEvent(Object, evt.FunctionName, evt))
            {
                Debug.Log("Execute graph event OnDestroy succeed without ILPP", this);
            }
#endif
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
            /* Test tracker scope */
            using (new FlowGraphDependencyTracker(this.GetRuntimeFlowGraph()).Auto())
            {
                /* ILPP will recognize this instruction and skip injecting IL */
                this.ProcessEvent(inGameObject);
            }
        }

        public override FlowGraph GetFlowGraph()
        {
            var graph = base.GetFlowGraph();
            /* Test subGraph initialization */
            if (graph.SubGraphSlots == null || graph.SubGraphSlots.Length == 0)
            {
                graph.SubGraphSlots = new CeresSubGraphSlot[]
                {
                    new()
                    {
                        Name = "Test SubGraph",
                        Graph = new FlowSubGraph(new FlowGraphSerializedData())
                    }
                };
            }
            return graph;
        }
    }
}
