using System;
using Ceres.Graph;

namespace Ceres.Annotations
{
    /// <summary>
    /// Force field with type <see cref="SharedVariable"/> in the editor to use sharing mode
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ForceSharedAttribute : Attribute
    {

    }
}
