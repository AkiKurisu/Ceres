using System;
namespace Ceres.Gameplay
{
    /// <summary>
    /// Whether <see cref="WorldSubsystem"/> should be created and initialize when world create
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class InitializeOnWorldCreateAttribute : Attribute
    {

    }
}
