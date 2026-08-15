using System;
using System.Reflection;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class BoolResolver : FieldResolver<Toggle, bool>
    {
        public BoolResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override Toggle CreateEditorField(FieldInfo fieldInfo)
        {
            return new Toggle(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(bool);
        }
    }
}