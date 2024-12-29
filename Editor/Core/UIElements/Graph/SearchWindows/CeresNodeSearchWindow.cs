using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using NodeGroup = Ceres.Annotations.NodeGroup;

namespace Ceres.Editor.Graph
{
    public struct NodeSearchContext
    {
        public string[] ShowGroups;
        
        public string[] HideGroups;

        public bool AllowGeneric;

        public Type ParameterType;

        public static readonly NodeSearchContext Default = new()
        {
            ShowGroups = Array.Empty<string>(),
            HideGroups = new []{ NodeGroup.Hidden },
            AllowGeneric = false,
            ParameterType = null
        };
    }

    /// <summary>
    /// Helper for build <see cref="SearchTreeEntry"/> for <see cref="CeresNodeSearchWindow"/>
    /// </summary>
    public class CeresNodeSearchEntryBuilder
    {
        
        private readonly List<SearchTreeEntry> _entries = new();
        
        private readonly Texture _defaultIcon;
        
        private readonly bool _allowGeneric;

        private readonly Type _portValueType;
        public CeresNodeSearchEntryBuilder(Texture entryDefaultIcon, bool allowGeneric = false, Type portValueType = null)
        {
            _defaultIcon = entryDefaultIcon;
            _allowGeneric = allowGeneric;
            _portValueType = portValueType;
        }
        
        public bool AddEntry(SearchTreeEntry entry)
        {
            _entries.Add(entry);
            return true;
        }

        public bool AddGroupEntry(string content, int level)
        {
            return AddEntry(new SearchTreeGroupEntry(new GUIContent(content), level));
        }
        
        public bool AddEntry(Type type, int level)
        {
            var label = CeresLabel.GetLabel(type, false);
            if (_allowGeneric && type.IsGenericTypeDefinition)
            {
                bool hasEntry = false;
                var template = GenericNodeTemplateRegistry.GetTemplate(type);
                if (template != null && template.CanFilterPort(_portValueType))
                {
                    var argumentTypes = template.GetAvailableArgumentTypes(_portValueType);
                    foreach (var argumentType in argumentTypes)
                    {
                        hasEntry = true;
                        AddEntry(new SearchTreeEntry(new GUIContent(template.GetGenericNodeEntryName(label, argumentType), _defaultIcon))
                        {
                            level = level, 
                            userData = new CeresNodeSearchEntryData()
                            {
                                NodeType = type,
                                SubType = argumentType
                            }
                        });
                    }
                }
                return hasEntry;
            }
            AddEntry(new SearchTreeEntry(new GUIContent(label, _defaultIcon))
            {
                level = level, 
                userData = new CeresNodeSearchEntryData()
                {
                    NodeType = type
                }
            });
            return true;
        }
        
        public bool AddAllEntries(IGrouping<string, Type> group, int level, int subCount = 1)
        {
            AddGroupEntry($"Select {group.Key}", level);
            bool hasEntry = false;
            var subGroups = group.SubGroups(subCount).ToArray();
            var left = group.Except(subGroups.SelectMany(x => x));
            foreach (var subGroup in subGroups)
            {
                hasEntry |= AddAllEntries(subGroup,level + 1, subCount + 1);
            }
            foreach (var type in left)
            {
                hasEntry |= AddEntry(type, level + 1);
            }

            if (!hasEntry)
            {
                _entries.RemoveAt(_entries.Count-1);
            }
            return hasEntry;
        }

        public List<SearchTreeEntry> GetEntries()
        {
            return _entries;
        }
    }
    public class CeresNodeSearchEntryData
    {
        /// <summary>
        /// Primary type that entry contained
        /// </summary>
        public Type NodeType;
            
        /// <summary>
        /// Secondary type that entry contained, used for generic argument
        /// </summary>
        public Type SubType;
        
        /// <summary>
        /// Additional data if need
        /// </summary>
        public object Data;
    }
    public abstract class CeresNodeSearchWindow: ScriptableObject, ISearchWindowProvider
    {
        public CeresGraphView GraphView { get; private set; }

        public NodeSearchContext Context { get; private set; }

        public void Initialize(CeresGraphView graphView, NodeSearchContext context)
        {
            GraphView = graphView;
            Context = context;
            OnInitialize();
        }
        
        /// <summary>
        /// Initialize search window
        /// </summary>
        protected virtual void OnInitialize()
        {

        }
        
        
        /// <summary>
        /// Util function for searching types grouped by <see cref="NodeGroupAttribute"/>
        /// </summary>
        /// <param name="baseTypes"></param>
        /// <param name="nodeSearchContext"></param>
        /// <returns></returns>
        public static (List<IGrouping<string,Type>>, List<Type>) SearchTypes(Type[] baseTypes, NodeSearchContext nodeSearchContext)
        {
            var subClasses = SubClassSearchUtility.FindSubClassTypes(baseTypes)
                                                        .Where(x=> CanShowType(x, nodeSearchContext))
                                                        .ToArray();
            var list = subClasses.GroupsByNodeGroup().ToList(); ;
            var nodeTypes = subClasses.Except(list.SelectMany(x => x)).ToList();
            var groups = list.SelectGroup(nodeSearchContext.ShowGroups).ExceptGroup(nodeSearchContext.HideGroups).ToList();
            return (groups, nodeTypes);
        }

        protected static bool CanShowType(Type type, NodeSearchContext context)
        {
            /* Normal filter without generic type */
            if (!context.AllowGeneric)
            {
                return !type.IsGenericType;
            }
                
            /* Not support not definition generic type */
            if (type.IsGenericType && !type.IsGenericTypeDefinition) return false;

            var hasPort = context.ParameterType != null;
            /* Validate generic definition has template */
            if(type.IsGenericTypeDefinition)
            {
                var template = GenericNodeTemplateRegistry.GetTemplate(type);
                if (template == null) return false;

                return !template.RequirePort() || hasPort;
            }

            var settingsAttribute = type.GetCustomAttribute<RequirePortAttribute>();
            
            /* Normal node type without require port can only show when not specific port */
            if (settingsAttribute == null) return !hasPort;
            
            if(settingsAttribute.PortType ==null)
            {
                return hasPort;
            }

            if (!hasPort) return false;
                
            // Validate port type
            if (settingsAttribute.AllowSubclass && settingsAttribute.PortType.IsAssignableFrom(context.ParameterType))
            {
                return true;
            }
            return settingsAttribute.PortType == context.ParameterType;
        }

        /// <summary>
        /// Create node from <see cref="CeresNodeSearchEntryData"/> after click tree entry
        /// </summary>
        /// <param name="entryData"></param>
        /// <param name="rect"></param>
        protected virtual bool OnSelectEntry(CeresNodeSearchEntryData entryData, Rect rect)
        {
            GraphView.AddNodeView(CreateNodeView(entryData), rect);
            return true;
        }

        /// <summary>
        /// Create node view from entry data by current <see cref="Context"/>
        /// </summary>
        /// <param name="entryData"></param>
        /// <returns></returns>
        public ICeresNodeView CreateNodeView(CeresNodeSearchEntryData entryData)
        {
            var nodeView = Context.AllowGeneric ? CreateGenericNodeView(entryData) : CreateNonGenericNodeView(entryData);
            return nodeView;
        }
        
        /// <summary>
        /// Create node view from entry data and not allowed construct generic node instance
        /// </summary>
        /// <param name="entryData"></param>
        /// <returns></returns>
        public ICeresNodeView CreateNonGenericNodeView(CeresNodeSearchEntryData entryData)
        {
            var nodeType = entryData.NodeType;
            return NodeViewFactory.Get().CreateInstance(nodeType, GraphView);
        }

        /// <summary>
        /// Create node view from entry data and allowed construct generic node instance
        /// </summary>
        /// <param name="entryData"></param>
        /// <returns></returns>
        public ICeresNodeView CreateGenericNodeView(CeresNodeSearchEntryData entryData)
        {
            var nodeType = entryData.NodeType;
            if(nodeType.IsGenericType)
            {
                var template = GenericNodeTemplateRegistry.GetTemplate(nodeType);
                Assert.IsNotNull(template, "Generic node must have a template");
                var parameters = template.GetGenericArguments(Context.ParameterType, entryData.SubType);
                return NodeViewFactory.Get().CreateInstanceResolved(nodeType, GraphView, parameters);
            }
            return NodeViewFactory.Get().CreateInstanceResolved(nodeType, GraphView);
        }
        
        public abstract List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context);

        public virtual bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            Rect newRect = new(GraphView.Screen2GraphPosition(context.screenMousePosition), new Vector2(100, 100));
            var entryData = (CeresNodeSearchEntryData)searchTreeEntry.userData;
            return OnSelectEntry(entryData, newRect);
        }

    }
}