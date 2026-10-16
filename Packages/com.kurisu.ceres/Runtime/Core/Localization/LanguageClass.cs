using System;
using R3;
using UnityEngine.UIElements;

namespace Ceres.Localization
{
    /// <summary>
    /// USS class naming the current language, so styles can choose fonts per language
    /// </summary>
    public static class LanguageClass
    {
        public const string Prefix = "ceres-lang-";

        /// <summary>
        /// Keep the current language's class on an element until the returned subscription is disposed
        /// </summary>
        public static IDisposable Bind(VisualElement element)
        {
            string current = null;
            Apply();
            return LocalizationSystem.Changed.Subscribe(_ => Apply());

            void Apply()
            {
                var next = Prefix + LocalizationSystem.Language;
                if (next == current) return;
                if (current != null) element.RemoveFromClassList(current);
                element.AddToClassList(next);
                current = next;
            }
        }
    }
}
