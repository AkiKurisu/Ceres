using System;
using System.Linq;
using System.Reflection;
using Ceres.Annotations;
using Ceres.Graph;
using UnityEditor;
using UnityEngine;
using Ceres.Utilities;
namespace Ceres.Editor
{
    [CustomPropertyDrawer(typeof(NodeGroupSelectorAttribute))]
    public class NodeGroupSelectorPropertyDrawer : PropertyDrawer
    {
        private static readonly Type[] DefaultTypes = { typeof(CeresNode) };
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (EditorGUI.DropdownButton(position, new GUIContent(property.stringValue, property.tooltip), FocusType.Passive))
            {
                var types = ((NodeGroupSelectorAttribute)attribute).Types ?? DefaultTypes; 
                var groups = SubClassSearchUtility.FindSubClassTypes(types)
                .Where(x => x.GetCustomAttribute<CeresGroupAttribute>() != null)
                .Select(x => SubClassSearchUtility.SplitGroupName(x.GetCustomAttribute<CeresGroupAttribute>().Group)[0])
                .Distinct()
                .ToList();
                var menu = new GenericMenu();
                foreach (var group in groups)
                {
                    if(group == CeresGroup.Hidden) continue;
                    menu.AddItem(new GUIContent(group), false, () => property.stringValue = group);
                }
                menu.ShowAsContext();
            }
            EditorGUI.EndProperty();
        }
    }
}
