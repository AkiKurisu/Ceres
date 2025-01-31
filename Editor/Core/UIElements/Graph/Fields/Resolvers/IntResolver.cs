using System;
using System.Reflection;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class IntResolver : FieldResolver<IntegerField, int>
    {
        public IntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override IntegerField CreateEditorField(FieldInfo fieldInfo)
        {
            return new IntegerField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(int);
        }
    }
}