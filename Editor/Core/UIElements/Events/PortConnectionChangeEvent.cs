using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public enum PortConnectionChangeType
    {
        Connect,
        Disconnect
    }
    
    public class PortConnectionChangeEvent : EventBase<PortConnectionChangeEvent>, ICeresEvent
    {
        public CeresPortView PortView { get; private set; }
        
        public CeresEdge Edge { get; private set; }
        
        public PortConnectionChangeType ConnectionChangeType { get; private set; }
        
        public static PortConnectionChangeEvent GetPooled(CeresPortView portView, CeresEdge edge, PortConnectionChangeType connectionChangeType)
        {
            var evt = GetPooled();
            evt.PortView = portView;
            evt.Edge = edge;
            evt.ConnectionChangeType = connectionChangeType;
            return evt;
        }
    }
}