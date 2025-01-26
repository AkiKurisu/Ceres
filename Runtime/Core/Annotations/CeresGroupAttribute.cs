using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Class or methods are categorized in the editor dropdown menu.
    /// Can be sub-categorized with the '/' symbol.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
    public sealed class CeresGroupAttribute : Attribute
    {
        public string Group { get; }
        public CeresGroupAttribute(string group)
        {
            Group = group;
        }
    }

    public static class CeresGroup
    {
        public const string Hidden = "Hidden";
    }
}