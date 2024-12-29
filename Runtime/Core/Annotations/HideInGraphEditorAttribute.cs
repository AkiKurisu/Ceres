using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Fields with this annotation will not be resolved in the Ceres Graph Editor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInGraphEditorAttribute : Attribute
    {

    }
}