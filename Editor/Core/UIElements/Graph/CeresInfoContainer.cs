using System;
using System.Reflection;
using Ceres.Annotations;
using UnityEngine;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    public class CeresInfoContainer : VisualElement
    {
        private readonly Label _label;
        public CeresInfoContainer(string defaultInfo = "")
        {
            Clear();
            Add(_label = new Label(defaultInfo));
            styleSheets.Add(Resources.Load<StyleSheet>($"Ceres/InfoContainer"));
        }
        public void DisplayNodeInfo(Type nodeType)
        {
            DisplayInfo(NodeInfo.GetInfo(nodeType));
        }

        public void DisplayInfo(string info)
        {
            _label.text = info;
        }
    }
}