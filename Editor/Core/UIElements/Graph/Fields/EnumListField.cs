using System;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class EnumListField<T> : ListField<T> where T : Enum
    {
        public EnumListField(string label, Func<VisualElement> elementCreator, Func<object> valueCreator) : base(label, elementCreator, valueCreator)
        {

        }

        protected override void OnBindListItem(VisualElement e, int i)
        {
            ((EnumField)e).value = value[i];
            ((EnumField)e).RegisterValueChangedCallback((x) => value[i] = (T)x.newValue);
        }
    }
}