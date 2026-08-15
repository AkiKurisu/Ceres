using System;
namespace Ceres.Editor.Graph
{
    /// <summary>
    /// Give priority for resolver (node or field)
    /// </summary>
    public sealed class OrderedAttribute : Attribute
    {
        public int Order { get; }

        public OrderedAttribute(int order = 100)
        {
            Order = order;
        }
    }
}