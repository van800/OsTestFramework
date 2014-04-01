using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using ZetaLongPaths;

namespace JetBrains.OsTestFramework.Common
{
  /// <summary>
  /// Network connection is not very stable thing, 
  /// so operate with retries after the first failure
  /// </summary>
  public static class ZlpWrapperWithRetry
  {

    private static T DoWithRetry<T>(Func<T> a)
    {
      try
      {
        return a();
      }
      catch
      {
        Thread.Sleep(1500);
        return a();
      }
    }

    public static bool FileExists(string path)
    {
      return DoWithRetry(() => ZlpIOHelper.FileExists(path));
    }

    public static bool DirectoryExists(string path)
    {
      return DoWithRetry(() => ZlpIOHelper.DirectoryExists(path));
    }

    public static void CopyFile(string src, string dst, bool overwrite)
    {
      DoWithRetry<object>(() => {ZlpIOHelper.CopyFile(src, dst, overwrite); return null;});
    }

    public static void CreateDirectory(string path)
    {
      DoWithRetry<object>(() => {if (!ZlpIOHelper.DirectoryExists(path)) ZlpIOHelper.CreateDirectory(path); return null;});
    }
  }

  public static class FileOperations
  {
    /// <summary>
    /// Replaces text in a file.
    /// </summary>
    /// <param name="filePath">Path of the text file.</param>
    /// <param name="searchText">Text to search for.</param>
    /// <param name="replaceText">Text to replace the search text.</param>
    public static void ReplaceInFile(string filePath, string searchText, string replaceText)
    {
      string content = "";
      using (var reader = new StreamReader(filePath))
      {
        content = reader.ReadToEnd();
      }

      content = Regex.Replace(content, searchText, replaceText);

      using (var writer = new StreamWriter(filePath))
      {
        writer.Write(content);
      }
    }

    /// <summary>
    /// Searches text in a file.
    /// </summary>
    /// <param name="filePath">Path of the text file.</param>
    /// <param name="searchPattern">regex pattern to search for.</param>
    public static bool SearchForPatternInFile(string filePath, string searchPattern)
    {
      using (var reader = new StreamReader(filePath))
      {
        while (!reader.EndOfStream)
        {
          string line = reader.ReadLine();
          if (Regex.IsMatch(line, searchPattern))
            return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Copies file from one location to other
    /// </summary>
    /// <param name="sourcePath">Path from.</param>
    /// <param name="destinationPath">Path to.</param>
    public static void CopyFiles(string sourcePath, string destinationPath)
    {
      if (ZlpWrapperWithRetry.FileExists(sourcePath))
      {
        var destinationFile = new ZlpFileInfo(destinationPath);
        if (!destinationFile.Directory.Exists)
        {
          destinationFile.Directory.Create();
        }
        // this is a file, target is a directory or a file
        if (ZlpWrapperWithRetry.DirectoryExists(destinationPath))
        {
          // target is a directory
          ZlpWrapperWithRetry.CopyFile(sourcePath, Path.Combine(destinationPath, Path.GetFileName(sourcePath)), true);
        }
        else
        {
          ZlpWrapperWithRetry.CopyFile(sourcePath, destinationPath, true);
        }
      }
      else
      {
        CopyFolders(destinationPath, sourcePath);
      }
    }

    private static void CopyFolders(string destinationPath, string sourcePath)
    {
      if (!ZlpWrapperWithRetry.DirectoryExists(destinationPath))
      {
        ZlpWrapperWithRetry.CreateDirectory(destinationPath);
      }

      string[] systementries = Directory.GetFileSystemEntries(sourcePath);

      foreach (string systementry in systementries)
      {
        if (ZlpWrapperWithRetry.DirectoryExists(systementry))
        {
          CopyFiles(systementry, Path.Combine(destinationPath, Path.GetFileName(systementry)));
        }
        else
        {
          ZlpWrapperWithRetry.CopyFile(systementry, Path.Combine(destinationPath, Path.GetFileName(systementry)), true);
        }
      }
    }
  }
}