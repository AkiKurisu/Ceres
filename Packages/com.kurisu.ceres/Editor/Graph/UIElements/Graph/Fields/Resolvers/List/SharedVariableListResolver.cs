using System.Reflection;
using System.Collections.Generic;
using System;
using Ceres.Graph;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    [ResolveChild]
    public class SharedVariableListResolver<T> : ListResolver<T> where T : SharedVariable, new()
    {
        public SharedVariableListResolver(FieldInfo fieldInfo, IFieldResolver resolver) : base(fieldInfo, resolver)
        {
        }
        
        protected override ListField<T> CreateEditorField(FieldInfo fieldInfo)
        {
            return new SharedVariableListField<T>(fieldInfo.Name, () => ChildResolver.CreateField(), () => new T());
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            if (fieldValueType.IsGenericType && fieldValueType.GetGenericTypeDefinition() == typeof(List<>) 
                                       && fieldValueType.GenericTypeArguments[0].IsSubclassOf(typeof(SharedVariable))) return true;
            if (fieldValueType.IsArray && fieldValueType.GetElementType()!.IsSubclassOf(typeof(SharedVariable))) return true;
            return false;
        }
    }
    
    public class SharedVariableListField<T> : ListField<T>, IBindableField where T : SharedVariable
    {
        private CeresGraphView _graphView;
        
        private Action<CeresGraphView> _onTreeViewInitEvent;
        
        public SharedVariableListField(string label, Func<VisualElement> elementCreator, Func<object> valueCreator) : base(label, elementCreator, valueCreator)
        {

        }
        
        public void BindGraph(CeresGraphView graph)
        {
            _graphView = graph;
            _onTreeViewInitEvent?.Invoke(graph);
        }

        protected override VisualElement OnMakeListItem()
        {
            var field = ElementCreator.Invoke();
            ((BaseField<T>)field).label = string.Empty;
            if (_graphView != null) (field as IBindableField)?.BindGraph(_graphView);
            _onTreeViewInitEvent += (view) => { (field as IBindableField)?.BindGraph(view); };
            return field;
        }
    }
}