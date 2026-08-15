using System;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UObject = UnityEngine.Object;
namespace Ceres.Editor.Graph
{
    public class ObjectListField<T> : ListField<T> where T : UObject
    {
        public ObjectListField(string label, Func<VisualElement> elementCreator, Func<object> valueCreator) : base(label, elementCreator, valueCreator)
        {
        }
        
        protected override VisualElement OnMakeListItem()
        {
            var field = ElementCreator.Invoke();
            ((ObjectField)field).label = string.Empty;
            ((ObjectField)field).objectType = typeof(T);
            return field;
        }
    }
}