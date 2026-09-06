using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace Ceres.AnimationPacking
{
    public static class AnimationBinaryUtility
    {
        public static void Pack(string animationAssetPath, string outputPath)
        {
            string source = ResolveExistingFile(animationAssetPath, ".anim");
            if (!HasYamlHeader(source))
                throw new InvalidDataException("Animation Binary only accepts text-serialized .anim files.");
            ValidateAnimationFile(source);
            string destination = Path.GetFullPath(outputPath);
            WriteNewFile(destination, "." + AnimationBinaryFormat.Extension, temporary =>
            {
                using FileStream input = File.OpenRead(source);
                using FileStream output = File.Create(temporary);
                using GZipStream gzip = new(output, System.IO.Compression.CompressionLevel.Optimal, false);
                input.CopyTo(gzip);
            });
        }

        public static void Extract(string animationBinaryPath, string outputAnimationPath)
        {
            string source = ResolveExistingFile(animationBinaryPath, "." + AnimationBinaryFormat.Extension);
            WriteNewFile(outputAnimationPath, ".anim", temporary =>
            {
                using FileStream input = File.OpenRead(source);
                using GZipStream gzip = new(input, CompressionMode.Decompress, false);
                using FileStream output = File.Create(temporary);
                gzip.CopyTo(output);
            }, ValidateAnimationFile);
        }

        internal static void Decompress(string sourcePath, string outputPath)
        {
            using FileStream input = File.OpenRead(sourcePath);
            using GZipStream gzip = new(input, CompressionMode.Decompress, false);
            using FileStream output = File.Create(outputPath);
            gzip.CopyTo(output);
        }

        internal static AnimationClip LoadSingleClip(string path)
        {
            UnityEngine.Object[] objects = InternalEditorUtility.LoadSerializedFileAndForget(path);
            if (objects.Length == 1 && objects[0] is AnimationClip clip)
                return clip;
            Destroy(objects);
            throw new InvalidDataException("Animation Binary payload must contain exactly one AnimationClip.");
        }

        private static void ValidateAnimationFile(string path)
        {
            AnimationClip clip = LoadSingleClip(path);
            UnityEngine.Object.DestroyImmediate(clip);
        }

        private static string ResolveExistingFile(string path, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A source path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Source file does not exist.", fullPath);
            if (!string.Equals(Path.GetExtension(fullPath), extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected a " + extension + " source file.");
            return fullPath;
        }

        private static void WriteNewFile(
            string outputPath,
            string extension,
            Action<string> write,
            Action<string> validate = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output path is required.", nameof(outputPath));
            string destination = Path.GetFullPath(outputPath);
            if (!string.Equals(Path.GetExtension(destination), extension, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected a " + extension + " output file.");
            if (File.Exists(destination))
                throw new IOException("Output file already exists: " + destination);
            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException());
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                write(temporary);
                validate?.Invoke(temporary);
                File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private static bool HasYamlHeader(string path)
        {
            using FileStream stream = File.OpenRead(path);
            byte[] header = new byte[5];
            return stream.Read(header, 0, header.Length) == header.Length &&
                   header.SequenceEqual(new byte[] { (byte)'%', (byte)'Y', (byte)'A', (byte)'M', (byte)'L' });
        }

        private static void Destroy(UnityEngine.Object[] objects)
        {
            foreach (UnityEngine.Object value in objects)
                if (value)
                    UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
