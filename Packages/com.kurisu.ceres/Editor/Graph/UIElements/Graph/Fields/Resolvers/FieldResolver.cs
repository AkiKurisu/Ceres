using System.Reflection;
using UnityEngine.UIElements;
using UnityEngine;
using System;
using Ceres.Annotations;
namespace Ceres.Editor.Graph
{
    public delegate void ValueChangeDelegate(object newValue);
    
    public interface IBindableField
    {
        void BindGraph(CeresGraphView graph);
    }
    
    public interface IFieldResolver
    {
        object Value { get; set; }
        
        /// <summary>
        /// Get field resolver's visual input element
        /// </summary>
        VisualElement EditorField { get; }
        
        /// <summary>
        /// Create new visual input element
        /// </summary>
        /// <returns></returns>
        VisualElement CreateField();
        
        void Restore(object @object);
        
        void Commit(object @object);
        
        void Copy(IFieldResolver resolver);
        
        /// <summary>
        /// Register a typeless object value change callback
        /// </summary>
        /// <param name="fieldChangeCallback"></param>
        void RegisterValueChangeCallback(ValueChangeDelegate fieldChangeCallback);

        /// <summary>
        /// Whether this resolver can accept field
        /// </summary>
        /// <param name="fieldValueType"></param>
        /// <param name="fieldInfo"></param>
        /// <returns></returns>
        bool IsAcceptable(Type fieldValueType, FieldInfo fieldInfo);
    }

    public abstract class FieldResolver<TField, TKValue> : IFieldResolver where TField : BaseField<TKValue>
    {
        private readonly FieldInfo _fieldInfo;

        public VisualElement EditorField => BaseField;
        
        public TField BaseField { get; }
        
        public virtual object Value
        {
            get => BaseField.value;
            set => BaseField.value = (TKValue)value;
        }
        
        protected FieldResolver(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
            {
                return;
            }
            _fieldInfo = fieldInfo;
            BaseField = LocalCreateEditorField();
            BaseField.label = CeresLabel.GetLabel(fieldInfo);
            var tooltip = fieldInfo.GetCustomAttribute<TooltipAttribute>();
            EditorField.tooltip = tooltip?.tooltip ?? string.Empty;
        }
        
        public VisualElement CreateField()
        {
            return CreateEditorField(_fieldInfo);
        }
        
        public void Copy(IFieldResolver resolver)
        {
            if (resolver is not FieldResolver<TField, TKValue>) return;
            if (_fieldInfo.GetCustomAttribute<DisableCopyValueAttribute>() != null) return;
            Value = resolver.Value;
        }
        
        public void Restore(object @object)
        {
            Value = _fieldInfo.GetValue(@object);
        }
        
        public void Commit(object @object)
        {
            _fieldInfo.SetValue(@object, Value);
        }
        
        public void RegisterValueChangeCallback(ValueChangeDelegate fieldChangeCallback)
        {
            BaseField.RegisterValueChangedCallback(evt => fieldChangeCallback(evt.newValue));
        }

        public virtual bool IsAcceptable(Type fieldValueType, FieldInfo fieldInfo)
        {
            return false;
        }

        private TField LocalCreateEditorField()
        {
            return CreateEditorField(_fieldInfo);
        }
        
        /// <summary>
        /// Create <see cref="BaseField{T}"/>
        /// </summary>
        /// <param name="fieldInfo"></param>
        /// <returns></returns>
        protected abstract TField CreateEditorField(FieldInfo fieldInfo);
    }
    
    public abstract class FieldResolver<TField, TKValue, TFInterface> : FieldResolver<TField, TKValue> where TField : BaseField<TKValue> where TKValue : TFInterface
    {
        protected FieldResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }

        public sealed override object Value
        {
            get => ValueGetter != null ? ValueGetter(BaseField.value) : BaseField.value;
            set => BaseField.value = ValueSetter != null ? ValueSetter((TFInterface)value) : (TKValue)value;
        }

        /// <summary>
        /// Bridge for setting value from <see cref="TKValue"/> to <see cref="TFInterface"/>
        /// </summary>
        /// <value></value>
        protected Func<TFInterface, TKValue> ValueSetter { get; set; }
        
        /// <summary>
        /// Bridge for setting value from <see cref="TKValue"/> to <see cref="TFInterface"/>
        /// </summary>
        /// <value></value>
        protected Func<TKValue, object> ValueGetter { get; set; }
    }

    public static class FieldResolverExtensions
    {
        /// <summary>
        /// Get <see cref="IFieldResolver.EditorField"/> and try bind the graph view
        /// </summary>
        /// <param name="fieldResolver"></param>
        /// <param name="ceresGraphView"></param>
        /// <returns></returns>
        public static VisualElement GetField(this IFieldResolver fieldResolver, CeresGraphView ceresGraphView)
        {
            if (fieldResolver.EditorField is IBindableField bindableField) bindableField.BindGraph(ceresGraphView);
            return fieldResolver.EditorField;
        }
    }
}