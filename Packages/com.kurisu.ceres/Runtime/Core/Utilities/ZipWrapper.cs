using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using UnityEngine;

namespace Ceres
{
    /// <summary>
    /// Helper class for zip lib
    /// </summary>
    public static class ZipWrapper
    {
        public static bool Zip(string[] fileOrDirectoryArray, string outputPathName, string password = null)
        {
            if ((null == fileOrDirectoryArray) || string.IsNullOrEmpty(outputPathName))
            {

                return false;
            }

            ZipOutputStream zipOutputStream = new(File.Create(outputPathName));
            zipOutputStream.SetLevel(6);
            if (!string.IsNullOrEmpty(password))
                zipOutputStream.Password = password;

            for (int index = 0; index < fileOrDirectoryArray.Length; ++index)
            {
                bool result = false;
                string fileOrDirectory = fileOrDirectoryArray[index];
                if (Directory.Exists(fileOrDirectory))
                    result = ZipDirectory(fileOrDirectory, string.Empty, zipOutputStream);
                else if (File.Exists(fileOrDirectory))
                    result = ZipFile(fileOrDirectory, string.Empty, zipOutputStream);

                if (!result)
                {
                    return false;
                }
            }

            zipOutputStream.Finish();
            zipOutputStream.Close();

            return true;
        }
        
        public static bool UnzipFile(string filePathName, string outputPath, string password = null)
        {
            if (string.IsNullOrEmpty(filePathName) || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            try
            {
                return UnzipFile(File.OpenRead(filePathName), outputPath, password);
            }
            catch (Exception e)
            {
                Debug.LogError("[ZipWrapper]: " + e);

                return false;
            }
        }
        
        public static bool UnzipFile(byte[] fileBytes, string outputPath, string password = null)
        {
            if ((null == fileBytes) || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            bool result = UnzipFile(new MemoryStream(fileBytes), outputPath, password);
            return result;
        }

        public static bool UnzipFile(Stream inputStream, string outputPath, string password = null)
        {
            if ((null == inputStream) || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);
            using ZipInputStream zipInputStream = new(inputStream);
            if (!string.IsNullOrEmpty(password))
                zipInputStream.Password = password;


            while (zipInputStream.GetNextEntry() is { } entry)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                string filePathName = Path.Combine(outputPath, entry.Name);

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(filePathName);
                    continue;
                }

                try
                {
                    using var fileStream = File.Create(filePathName);
                    byte[] bytes = new byte[1024];
                    while (true)
                    {
                        int count = zipInputStream.Read(bytes, 0, bytes.Length);
                        if (count > 0)
                            fileStream.Write(bytes, 0, count);
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[ZipWrapper]: " + e);
                    return false;
                }
            }
            return true;
        }

        private static bool ZipFile(string filePathName, string parentRelPath, ZipOutputStream zipOutputStream)
        {
            FileStream fileStream = null;
            try
            {
                string entryName = parentRelPath + '/' + Path.GetFileName(filePathName);
                ZipEntry entry = new(entryName)
                {
                    DateTime = DateTime.Now
                };

                fileStream = File.OpenRead(filePathName);
                byte[] buffer = new byte[fileStream.Length];
                fileStream.Read(buffer, 0, buffer.Length);
                fileStream.Close();

                entry.Size = buffer.Length;

                zipOutputStream.PutNextEntry(entry);
                zipOutputStream.Write(buffer, 0, buffer.Length);
            }
            catch (Exception e)
            {
                Debug.LogError("[ZipWrapper]: " + e);
                return false;
            }
            finally
            {
                if (null != fileStream)
                {
                    fileStream.Close();
                    fileStream.Dispose();
                }
            }

            return true;
        }

        private static bool ZipDirectory(string path, string parentRelPath, ZipOutputStream zipOutputStream)
        {
            try
            {
                string entryName = Path.Combine(parentRelPath, Path.GetFileName(path) + '/');
                ZipEntry entry = new(entryName)
                {
                    DateTime = DateTime.Now,
                    Size = 0
                };

                zipOutputStream.PutNextEntry(entry);
                zipOutputStream.Flush();

                string[] files = Directory.GetFiles(path);
                for (int index = 0; index < files.Length; ++index)
                {
                    if (files[index].EndsWith(".meta"))
                    {
                        continue;
                    }

                    ZipFile(files[index], Path.Combine(parentRelPath, Path.GetFileName(path)), zipOutputStream);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ZipWrapper]: " + e);
                return false;
            }

            string[] directories = Directory.GetDirectories(path);
            for (int index = 0; index < directories.Length; ++index)
            {
                if (!ZipDirectory(directories[index], Path.Combine(parentRelPath, Path.GetFileName(path)), zipOutputStream))
                {
                    return false;
                }
            }
            return true;
        }
    }
}