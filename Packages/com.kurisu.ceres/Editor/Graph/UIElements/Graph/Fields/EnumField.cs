using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class EnumField : PopupField<Enum>
    {
        public EnumField(string label, List<Enum> choices, Enum defaultValue = null)
            : base(label, choices, defaultValue, null, null)
        {
        }
    }
}