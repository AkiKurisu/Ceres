using System;
using Ceres.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph
{
    internal class TypeContainer: VisualElement
    {
        private readonly Label _typeLabel;

        public TypeContainer(Type objectType)
        {
            name = nameof(TypeContainer);
            Add(new Label
            {
                name = "typeLabel",
                text = "Type"
            });
            Add(_typeLabel = new Label
            {
                name = "typeName",
                style =
                {
                    unityTextAlign = TextAnchor.MiddleRight
                }
            });
            if (objectType != null)
            {
                SetType(objectType);
            }
        }

        public void SetType(Type inType)
        {
            _typeLabel.text = inType == null ? string.Empty : CeresLabel.GetTypeName(inType);
            tooltip = inType?.FullName ?? string.Empty;
        }
    }
}