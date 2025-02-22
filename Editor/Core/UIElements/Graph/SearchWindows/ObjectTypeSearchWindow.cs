using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using Ceres.Utilities;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Search window for getting type
    /// </summary>
    public class ObjectTypeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        private Texture2D _indentationIcon;
        
        private Action<Type> _typeSelectCallBack;
        
        private Func<IEnumerable<Type>, IEnumerable<Type>> _customTypeFilters;

        private Type _baseType;
        
        public void Initialize(Action<Type> typeSelectCallBack, Type baseType = null, 
            Func<IEnumerable<Type>, IEnumerable<Type>> customTypeProvideFunc = null)
        {
            _baseType = baseType ?? typeof(UObject);
            _typeSelectCallBack = typeSelectCallBack;
            _customTypeFilters = customTypeProvideFunc;
            _indentationIcon = new Texture2D(1, 1);
            _indentationIcon.SetPixel(0, 0, new Color(0, 0, 0, 0));
            _indentationIcon.Apply();
        }
        
        List<SearchTreeEntry> ISearchWindowProvider.CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Select Object Type")),
                new(new GUIContent("<Null>", _indentationIcon)) { level = 1, userData = null }
            };

            IEnumerable<Type> nodeTypes;
            
            if (_customTypeFilters == null)
            {
                nodeTypes = SubClassSearchUtility.FindSubClassTypes(_baseType).Where(x=> !x.IsGenericType);
            }
            else
            {
                nodeTypes = _customTypeFilters(SubClassSearchUtility.FindSubClassTypes(_baseType));
            }
            
            var groups = nodeTypes.GroupBy(t => t.Assembly);
            foreach (var group in groups)
            {
                entries.Add(new SearchTreeGroupEntry(new GUIContent($"Select {group.Key.GetName().Name}"), 1));
                var subGroups = group.GroupBy(x => x.Namespace);
                foreach (var subGroup in subGroups)
                {
                    entries.Add(new SearchTreeGroupEntry(new GUIContent($"Select {subGroup.Key}"), 2));
                    entries.AddRange(subGroup.Select(type => new SearchTreeEntry(new GUIContent(type.Name, _indentationIcon))
                    {
                        level = 3, userData = type
                    }));
                }
            }
            return entries;
        }
        
        bool ISearchWindowProvider.OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            var type = searchTreeEntry.userData as Type;
            _typeSelectCallBack?.Invoke(type);
            return true;
        }
    }
}
