using System;
using System.Reflection;
#if UNITY_2022_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEditor.UIElements;
#endif
using UnityEngine;
namespace Ceres.Editor.Graph
{
    public class BoundsIntResolver : FieldResolver<BoundsIntField, BoundsInt>
    {
        public BoundsIntResolver(FieldInfo fieldInfo) : base(fieldInfo)
        {
        }
        
        protected override BoundsIntField CreateEditorField(FieldInfo fieldInfo)
        {
            return new BoundsIntField(fieldInfo.Name);
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(BoundsInt);
        }
    }
}