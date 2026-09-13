using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ceres.Editor.UIElements
{
    public static class UIToolkitPreviewRegistry
    {
        public static IReadOnlyList<IUIToolkitPreviewProvider> GetProviders()
        {
            var providers = new List<IUIToolkitPreviewProvider>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<IUIToolkitPreviewProvider>())
            {
                if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
                {
                    continue;
                }

                try
                {
                    providers.Add((IUIToolkitPreviewProvider)Activator.CreateInstance(type));
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            IGrouping<string, IUIToolkitPreviewProvider> duplicate = providers
                .GroupBy(provider => provider.Id, StringComparer.Ordinal)
                .FirstOrDefault(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException($"UI Toolkit preview provider id must be non-empty and unique: '{duplicate.Key}'.");
            }

            return providers.OrderBy(provider => provider.DisplayName, StringComparer.Ordinal).ToArray();
        }
    }
}
