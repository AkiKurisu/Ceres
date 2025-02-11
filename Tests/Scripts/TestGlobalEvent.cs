using Ceres.Graph.Flow.Annotations;
using Chris.Events;

namespace Ceres.Tests
{
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
    }
}