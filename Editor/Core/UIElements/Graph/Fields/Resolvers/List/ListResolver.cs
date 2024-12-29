using System.Reflection;
using System.Collections.Generic;
using System;
using System.Linq;
namespace Ceres.Editor.Graph
{
    [ResolveChild]
    public class ListResolver<T> : FieldResolver<ListField<T>, List<T>, IList<T>>
    {
        protected readonly IFieldResolver ChildResolver;
        
        public ListResolver(FieldInfo fieldInfo, IFieldResolver resolver) : base(fieldInfo)
        {
            ChildResolver = resolver;
            ValueGetter = (list) =>
            {
                var iList = (IList<T>)Activator.CreateInstance(fieldInfo.FieldType, list.Count);
                bool isArray = fieldInfo.FieldType.IsArray;
                for (int i = 0; i < list.Count; ++i)
                {
                    if (isArray)
                        iList[i] = list[i];
                    else
                        iList.Add(list[i]);
                }
                return iList;
            };
            ValueSetter = (iList) => iList != null ? iList.ToList() : new List<T>();
        }
        
        protected override ListField<T> CreateEditorField(FieldInfo fieldInfo)
        {
            return new ListField<T>(fieldInfo.Name, () => ChildResolver.CreateField(), () => Activator.CreateInstance(typeof(T)));
        }
    }
}