using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Ceres.Settings.UI
{
    /// <summary>
    /// Product-owned images for setting and option preview keys
    /// </summary>
    public interface ISettingsPreviewSource
    {
        /// <summary>
        /// Load the image for a key; null when the product has no image for it
        /// </summary>
        UniTask<Texture2D> LoadAsync(string key, CancellationToken cancellationToken);

        /// <summary>
        /// Release an image returned by <see cref="LoadAsync"/>
        /// </summary>
        void Release(string key);
    }
}
