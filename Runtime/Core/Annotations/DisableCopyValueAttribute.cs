using System;
namespace Ceres.Annotations
{
    /// <summary>
    /// Disable field value to be copied in Ceres Graph Editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class DisableCopyValueAttribute : Attribute
    {

    }
}
