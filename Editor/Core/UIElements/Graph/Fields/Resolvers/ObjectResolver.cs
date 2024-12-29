using System.Reflection;
using UnityEditor.UIElements;
namespace Ceres.Editor.Graph
{
    public class ObjectResolver : FieldResolver<ObjectField, UnityEngine.Object>
    {
        public ObjectResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override ObjectField CreateEditorField(FieldInfo fieldInfo)
        {
            var editorField = new ObjectField(fieldInfo.Name)
            {
                objectType = fieldInfo.FieldType
            };
            return editorField;
        }

    }
}