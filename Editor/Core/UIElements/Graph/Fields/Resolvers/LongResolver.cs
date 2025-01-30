using System;
using System.Reflection;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class LongResolver : FieldResolver<LongField, long>
    {
        public LongResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override LongField CreateEditorField(FieldInfo fieldInfo)
        {
            return new LongField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(long);
        }
    }
}