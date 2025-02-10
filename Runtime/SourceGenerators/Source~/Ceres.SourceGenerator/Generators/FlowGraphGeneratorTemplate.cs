﻿using System.Text;
namespace Ceres.SourceGenerator;

internal class FlowGraphGeneratorTemplate
{
    private const string StartTemplate =
        """
        /// <auto-generated>
        /// This file is auto-generated by Ceres.SourceGenerator. 
        /// All changes will be discarded.
        /// </auto-generated>
        using System;
        using System.Collections.Generic;
        using Ceres.Annotations;
        using Ceres.Graph;
        using Ceres.Graph.Flow;
        using Ceres.Graph.Flow.Annotations;
        using Chris.Serialization;
        using UnityEngine;
        using UnityEngine.Assertions;
        using UObject = UnityEngine.Object;
        namespace {NAMESPACE}
        {
            [System.Runtime.CompilerServices.CompilerGenerated]
            public partial class {CLASSNAME}: {INTERFACE}
            {
        """;

    private const string ImplementationNonRuntimeTemplate =
        """
        
                [SerializeField]
                private FlowGraphData graphData;
                
                public UObject Object => this;
        
                public virtual FlowGraph GetFlowGraph()
                {
                    return new FlowGraph(graphData.CloneT<FlowGraphData>());
                }
        
                public virtual void SetGraphData(CeresGraphData graph)
                {
                    graphData = (FlowGraphData)graph;
                }
        
                /// <summary>
                /// Get persistent <see cref="FlowGraphData"/>
                /// </summary>
                /// <returns></returns>
                protected FlowGraphData GetGraphData()
                {
                    return graphData;
                }
            }
        }
        """;
    private const string ImplementationRuntimeTemplate =
        """
        
                [NonSerialized]
                private FlowGraph _graph;
                
                [SerializeField]
                private FlowGraphData graphData;
                
                public UObject Object => this;
        
                public FlowGraph Graph
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
        
                /// <summary>
                /// Release graph instance safely
                /// </summary>
                /// <returns></returns>
                protected void ReleaseGraph()
                {
                    _graph?.Dispose();
                }
        
                public virtual FlowGraph GetFlowGraph()
                {
                    return new FlowGraph(graphData.CloneT<FlowGraphData>());
                }
        
                public virtual void SetGraphData(CeresGraphData graph)
                {
                    graphData = (FlowGraphData)graph;
                }
        
                /// <summary>
                /// Get persistent <see cref="FlowGraphData"/>
                /// </summary>
                /// <returns></returns>
                protected FlowGraphData GetGraphData()
                {
                    return graphData;
                }
            }
        }
        """;

    private const string NonImplementationRuntimeTemplate =
        """
        
                [NonSerialized]
                private FlowGraph _graph;
        
                public FlowGraph Graph
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
        
                /// <summary>
                /// Release graph instance safely
                /// </summary>
                /// <returns></returns>
                protected void ReleaseGraph()
                {
                    _graph?.Dispose();
                }
            }
        }
        """;

    private const string ImplementationInterface = "IFlowGraphContainer";

    private const string RuntimeInterface = "IFlowGraphRuntime";

    public string Namespace;

    public string ClassName;

    public bool GenerateImplementation;

    public bool GenerateRuntime;

    public string GenerateCode()
    {
        var sb = new StringBuilder();
        var namedCode = StartTemplate.Replace("{NAMESPACE}", Namespace).Replace("{CLASSNAME}", ClassName);
        if (GenerateImplementation)
        {
            if (GenerateRuntime)
            {
                sb.Append(namedCode.Replace("{INTERFACE}", string.Join(", ", ImplementationInterface, RuntimeInterface)));
                sb.Append(ImplementationRuntimeTemplate);
            }
            else
            {
                sb.Append(namedCode.Replace("{INTERFACE}", ImplementationInterface));
                sb.Append(ImplementationNonRuntimeTemplate);
            }
        }
        else
        {
            if (GenerateRuntime)
            {
                sb.Append(namedCode.Replace("{INTERFACE}", RuntimeInterface));
                sb.Append(NonImplementationRuntimeTemplate);
            }
            else
            {
                sb.Append(namedCode.Replace("{INTERFACE}", ImplementationInterface));
                sb.Append(
                    """"
                    
                        }
                    }
                    """");
            }
        }
        return sb.ToString();
    }
}