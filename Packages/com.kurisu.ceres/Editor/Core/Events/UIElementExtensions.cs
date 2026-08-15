using System;
using UnityEngine.UIElements;
namespace Ceres.Events.Editor
{
    internal class MissingVisualElementException : Exception
    {
        public MissingVisualElementException()
        {
        }

        public MissingVisualElementException(string message)
            : base(message)
        {
        }
    }
    public static class UIElementExtensions
    {
        public static VisualElement GetRootVisualElement(this IPanel panel)
        {
            if (panel == null)
            {
                return null;
            }

            VisualElement visualTree = panel.visualTree;
            if (visualTree.childCount == 1)
            {
                return null;
            }

            return visualTree[1];
        }
        public static T MandatoryQ<T>(this VisualElement e, string name, string className = null) where T : VisualElement
        {
            var element = e.Q<T>(name, className) ?? throw new MissingVisualElementException("Element not found: " + name);
            return element;
        }
        public static VisualElement MandatoryQ(this VisualElement e, string name, string className = null)
        {
            var element = e.Q<VisualElement>(name, className) ?? throw new MissingVisualElementException("Element not found: " + name);
            return element;
        }
    }
}