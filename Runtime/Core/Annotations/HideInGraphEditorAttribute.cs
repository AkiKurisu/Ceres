using System;

namespace Ceres.Annotations
{
    /// <summary>
    /// Notify field not be displayed in the Ceres Graph Editor.
    /// </summary>
    /// <remarks>Hide port's internal field if this attribute is annotated on <see cref="Ceres.Graph.CeresPort"/></remarks>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class HideInGraphEditorAttribute : Attribute
    {

    }
}