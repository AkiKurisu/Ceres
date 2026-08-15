using System;
using System.Collections.Generic;
using Ceres.Pool;
namespace Ceres.Events
{
    internal class PropagationPaths
    {
        private static readonly _ObjectPool<PropagationPaths> Pool = new(() => new PropagationPaths());

        [Flags]
        public enum Type
        {
            None = 0,
            TrickleDown = 1,
            BubbleUp = 2
        }

        public readonly List<CallbackEventHandler> TrickleDownPath;
        
        public readonly List<CallbackEventHandler> TargetElements;
        
        public readonly List<CallbackEventHandler> BubbleUpPath;

        private const int k_DefaultPropagationDepth = 16;
        
        private const int k_DefaultTargetCount = 4;

        public PropagationPaths()
        {
            TrickleDownPath = new List<CallbackEventHandler>(k_DefaultPropagationDepth);
            TargetElements = new List<CallbackEventHandler>(k_DefaultTargetCount);
            BubbleUpPath = new List<CallbackEventHandler>(k_DefaultPropagationDepth);
        }

        public PropagationPaths(PropagationPaths paths)
        {
            TrickleDownPath = new List<CallbackEventHandler>(paths.TrickleDownPath);
            TargetElements = new List<CallbackEventHandler>(paths.TargetElements);
            BubbleUpPath = new List<CallbackEventHandler>(paths.BubbleUpPath);
        }

        internal static PropagationPaths Copy(PropagationPaths paths)
        {
            PropagationPaths copyPaths = Pool.Get();
            copyPaths.TrickleDownPath.AddRange(paths.TrickleDownPath);
            copyPaths.TargetElements.AddRange(paths.TargetElements);
            copyPaths.BubbleUpPath.AddRange(paths.BubbleUpPath);

            return copyPaths;
        }

        public static PropagationPaths Build(CallbackEventHandler elem, EventBase evt)
        {
            PropagationPaths paths = Pool.Get();
            // Go through the entire hierarchy.
            for (var ve = elem.Parent; ve != null; ve = ve.Parent)
            {
                // Reach root
                if (ve.IsCompositeRoot && !evt.SkipDisabledElements)
                {
                    paths.TargetElements.Add(ve);
                }
                else
                {
                    if (evt.TricklesDown && ve.HasTrickleDownHandlers())
                    {
                        paths.TrickleDownPath.Add(ve);
                    }
                    if (evt.Bubbles && ve.HasBubbleUpHandlers())
                    {
                        paths.BubbleUpPath.Add(ve);
                    }
                }
            }
            return paths;
        }

        public void Release()
        {
            // Empty paths to avoid leaking CallbackEventHandler.
            BubbleUpPath.Clear();
            TargetElements.Clear();
            TrickleDownPath.Clear();

            Pool.Release(this);
        }
    }
}
