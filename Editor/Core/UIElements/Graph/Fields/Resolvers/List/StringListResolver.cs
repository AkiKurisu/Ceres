using System;
using System.Collections.Generic;
using System.Reflection;
namespace Ceres.Editor.Graph
{
    public class StringListResolver : ListResolver<string>
    {
        public StringListResolver(FieldInfo fieldInfo) : base(fieldInfo, new StringResolver(fieldInfo))
        {

        }
        
        protected override ListField<string> CreateEditorField(FieldInfo fieldInfo)
        {
            return new ListField<string>(fieldInfo.Name, () => ChildResolver.CreateField(), () => string.Empty);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo _)
        {
            return fieldValueType == typeof(List<string>) || fieldValueType == typeof(string[]);
        }
    }
}