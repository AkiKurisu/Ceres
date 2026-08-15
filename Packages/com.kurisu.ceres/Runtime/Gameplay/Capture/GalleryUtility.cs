using System.IO;
using UnityEngine;

namespace Ceres.Gameplay.Capture
{
    public static class GalleryUtility
    {
        public static readonly string SnapshotFolderPath;
        
        static GalleryUtility()
        {
            SnapshotFolderPath = Path.GetDirectoryName(Application.dataPath) + "/Snapshots";
        }
        
#if !UNITY_EDITOR && UNITY_ANDROID
        private static void RequestPermission(UnityEngine.Android.PermissionCallbacks callback)
        {
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
            {
                Debug.Log("[Gallery] Has no permission!");
            }
            else
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageWrite, callback);
            }
        }

        private static void SaveToAndroid(byte[] byteArray, string filename)
        {
            UnityEngine.Android.PermissionCallbacks callbacks = new();
            callbacks.PermissionGranted += permissionName =>
            {
                string path = NativeGallery.GetTemporarySavePath(filename);
                File.WriteAllBytes(path, byteArray);
                NativeGallery.SaveToGalleryInternal(path, "album", NativeGallery.MediaType.Image, null);
            };

            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
            {
                NativeGallery.SaveImageToGallery(byteArray, "album", filename, null);
            }
            else
            {
                RequestPermission(callbacks);
            }
        }
#endif
        
#if !UNITY_EDITOR && UNITY_IOS
        private static void SaveToIOS(byte[] byteArray, string filename)
        {
            NativeGallery.SaveImageToGallery(byteArray, "album", filename);
        }
#endif

        private static void SaveToWindows(byte[] byteArray, string filename)
        {
            if (!Directory.Exists(SnapshotFolderPath))
            {
                Directory.CreateDirectory(SnapshotFolderPath);
            }
            string path = Path.Combine(SnapshotFolderPath, filename);
            Debug.Log($"[Gallery] Save picture to {path}");
            File.WriteAllBytes(path, byteArray);
        }
        
        // =================================== Public API =========================================== //
        /// <summary>
        /// Save png to native gallery.
        /// </summary>
        /// <param name="byteArray"></param>
        public static void SavePngToGallery(byte[] byteArray)
        {
            string fileName = $"Capture-{System.DateTime.Now:MM-dd-yy (HH-mm-ss)}.png";
            SavePngToGallery(fileName, byteArray);
        }
        
        /// <summary>
        /// Save png to native gallery.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="byteArray"></param>
        public static void SavePngToGallery(string fileName, byte[] byteArray)
        {
#if !UNITY_EDITOR && UNITY_IOS
            SaveToIOS(byteArray, fileName);
#elif !UNITY_EDITOR && UNITY_ANDROID
            SaveToAndroid(byteArray, fileName);
#else
            SaveToWindows(byteArray, fileName);
#endif
        }
        // =================================== Public API =========================================== //
    }
}