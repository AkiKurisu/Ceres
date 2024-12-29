using System;
using System.Reflection;
using Ceres.Annotations;
using UnityEditor.UIElements;
namespace Ceres.Editor.Graph
{
    [Ordered]
    public class TagResolver : FieldResolver<TagField, string>
    {
        public TagResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override TagField CreateEditorField(FieldInfo fieldInfo)
        {
            return new TagField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(string) && info.GetCustomAttribute<TagAttribute>() != null;
        }
    }
}