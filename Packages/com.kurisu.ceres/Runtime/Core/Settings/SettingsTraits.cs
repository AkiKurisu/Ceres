using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ceres.Settings
{
    /// <summary>
    /// Platform and capability traits that setting conditions test instead of compile-time platform checks
    /// </summary>
    public static class SettingsTraits
    {
        public const string Desktop = "Desktop";

        public const string Mobile = "Mobile";

        public const string Touch = "Touch";

        private static readonly HashSet<string> Traits = new();

        private static HashSet<string> _emulated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Traits.Clear();
            _emulated = null;
        }

        private static HashSet<string> Current
        {
            get
            {
                if (_emulated != null && !Application.isPlaying) return _emulated;
                if (Traits.Count == 0) AddPlatformTraits(Traits);
                return Traits;
            }
        }

        private static void AddPlatformTraits(HashSet<string> traits)
        {
#if UNITY_ANDROID || UNITY_IOS
            traits.Add(Mobile);
            traits.Add(Touch);
#else
            traits.Add(Desktop);
#endif
        }

        public static bool Has(string trait)
        {
            return Current.Contains(trait);
        }

        /// <summary>
        /// Add a product-defined trait of the running device
        /// </summary>
        public static void Add(string trait)
        {
            if (Traits.Count == 0) AddPlatformTraits(Traits);
            Traits.Add(trait);
        }

        /// <summary>
        /// Replace the trait set outside Play Mode until disposed, so Editor previews can show another platform's settings
        /// </summary>
        public static IDisposable Emulate(params string[] traits)
        {
            var previous = _emulated;
            _emulated = new HashSet<string>(traits);
            return new Restore(previous);
        }

        private sealed class Restore : IDisposable
        {
            private readonly HashSet<string> _previous;

            public Restore(HashSet<string> previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                _emulated = _previous;
            }
        }
    }
}
