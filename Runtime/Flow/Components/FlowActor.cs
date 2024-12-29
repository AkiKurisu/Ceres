using System;
using System.Runtime.CompilerServices;
using Chris;
using Ceres.Graph.Flow.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Ceres.Graph.Flow.Components
{
    /// <summary>
    /// Base class for flow-executable actor
    /// </summary>
    public class FlowActor : Actor, IFlowGraphContainer
    {
        [NonSerialized]
        private FlowGraph _graph;
        
        [SerializeField]
        private FlowGraphData graphData;
        
        public Object Object => this;

        protected FlowGraph Graph
        {
            get
            {
                if (_graph == null)
                {
                    _graph = GetFlowGraph();
                    _graph.Compile();
                }

                return _graph;
            }
        }

        [ImplementableEvent]
        protected override void Awake()
        {
            _graph = GetFlowGraph();
            _graph.Compile();
            base.Awake();
            ProcessEvent(); 
        }

        [ImplementableEvent]
        protected virtual void Start()
        {
            ProcessEvent(); 
        }

        [ImplementableEvent]
        protected virtual void OnEnable()
        {
            ProcessEvent(); 
        }
        
                
        [ImplementableEvent]
        protected virtual void Update()
        {
            ProcessEvent(); 
        }
        
        [ImplementableEvent]
        protected virtual void FixedUpdate()
        {
            ProcessEvent(); 
        }
        
        [ImplementableEvent]
        protected virtual void LateUpdate()
        {
            ProcessEvent(); 
        }

        [ImplementableEvent]
        protected virtual void OnDisable()
        {
            ProcessEvent(); 
        }

        [ImplementableEvent]
        protected override void OnDestroy()
        {
            ProcessEvent(); 
            base.OnDestroy();
        }
        
        protected void ProcessEvent([CallerMemberName] string eventName = "", params object[] parameters)
        {
            using var evt = ExecuteFlowEvent.Create(eventName, parameters);
            /* Execute event in quiet way */
            Graph.TryExecuteEvent(this, evt.FunctionName, evt);
        }
        
        public CeresGraph GetGraph()
        {
            return GetFlowGraph();
        }

        public FlowGraph GetFlowGraph()
        {
            return new FlowGraph(graphData.CloneT<FlowGraphData>());
        }

        public void SetGraphData(CeresGraphData graph)
        {
            graphData = (FlowGraphData)graph;
        }
    }
}
