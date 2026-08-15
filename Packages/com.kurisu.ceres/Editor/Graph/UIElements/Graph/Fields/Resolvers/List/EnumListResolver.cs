using System.Reflection;
using System.Collections.Generic;
using System;
namespace Ceres.Editor.Graph
{
    public class EnumListResolver<T> : ListResolver<T> where T : Enum
    {
        public EnumListResolver(FieldInfo fieldInfo) : base(fieldInfo, new EnumResolver(fieldInfo))
        {

        }
        
        protected override ListField<T> CreateEditorField(FieldInfo fieldInfo)
        {
            return new EnumListField<T>(fieldInfo.Name, () => ChildResolver.CreateField(), () => default(T));
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            if (fieldValueType.IsGenericType && fieldValueType.GetGenericTypeDefinition() == typeof(List<>) && fieldValueType.GenericTypeArguments[0].IsEnum) return true;
            return fieldValueType.IsArray && fieldValueType.GetElementType()!.IsEnum;
        }
    }
}