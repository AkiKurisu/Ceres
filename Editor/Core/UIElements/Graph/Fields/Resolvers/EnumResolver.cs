using System;
using System.Linq;
using System.Reflection;
using Chris.Serialization;
namespace Ceres.Editor.Graph
{
    public class EnumResolver : FieldResolver<EnumField, Enum>
    {
        public EnumResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override EnumField CreateEditorField(FieldInfo fieldInfo)
        {
            var fieldType = SerializedType.GenericType(fieldInfo.FieldType);
            var enumValue = Enum.GetValues(fieldType).Cast<Enum>().Select(v => v).ToList();
            return new EnumField(fieldInfo.Name, enumValue, enumValue[0]);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType.IsEnum;
        }
    }
}