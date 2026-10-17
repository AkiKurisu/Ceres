using System;
namespace Ceres.Gameplay
{
    /// <summary>
    /// Tick order of a <see cref="WorldSubsystem"/> in every phase, lowest first, default 0.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class WorldSubsystemOrderAttribute : Attribute
    {
        public int Order { get; }

        public WorldSubsystemOrderAttribute(int order)
        {
            Order = order;
        }
    }
}
