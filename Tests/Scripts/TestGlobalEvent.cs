using Ceres.Graph.Flow.Annotations;
using Chris.Events;

namespace Ceres.Tests
{
    /// <summary>
    /// Example: EventBase source generator
    /// Expected generated: FlowNode_CreateTestGlobalEvent, ExecutableEvent_TestGlobalEvent
    /// </summary>
    [ExecutableEvent]
    public class TestGlobalEvent: EventBase<TestGlobalEvent>
    {
        public int Data { get; private set; }
        
        [ExecutableEvent]
        public static TestGlobalEvent GetPooled(int data)
        {
            var evt = GetPooled();
            evt.Data = data;
            return evt;
        }

        protected override void Init()
        {
            base.Init();
            /* Mark cancellable so that C# script can override default behavior */
            Propagation |= EventPropagation.Cancellable;
        }
    }
}