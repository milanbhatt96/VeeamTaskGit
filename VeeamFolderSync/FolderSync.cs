using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class FolderSync
{
    private static string sourceFolder;
    private static string replicaFolder;
    private static int syncInterval;
    private static string logFilePath;

    static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: FolderSync <sourceFolder> <replicaFolder> <syncIntervalInSeconds> <logFilePath>");
            return;
        }

        sourceFolder = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
        replicaFolder = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
        logFilePath = args[3];

        if (!int.TryParse(args[2], out syncInterval) || syncInterval <= 0)
        {
            Console.WriteLine("Invalid sync interval. Must be a positive integer.");
            return;
        }

        if (!Directory.Exists(sourceFolder))
        {
            Console.WriteLine("Source folder does not exist.");
            return;
        }

        Directory.CreateDirectory(replicaFolder);

        Console.WriteLine("Synchronization started...");
        while (true)
        {
            try
            {
                SyncFolders();
            }
            catch (Exception ex)
            {
                LogAction($"ERROR: {ex.Message}");
            }
            Thread.Sleep(syncInterval * 1000);
        }
    }

    private static void SyncFolders()
    {
        try
        {
            Parallel.Invoke(
                () => SyncFiles(),
                () => CleanupReplica()
            );
        }
        catch (Exception ex)
        {
            LogAction($"ERROR: {ex.Message}");
        }
    }

    private static void SyncFiles()
    {
        foreach (var file in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(sourceFolder.Length + 1);
            string replicaFilePath = Path.Combine(replicaFolder, relativePath);
            string replicaDirectory = Path.GetDirectoryName(replicaFilePath);

            //  Code that creates missing folders in the replica folder
            if (!Directory.Exists(replicaDirectory))
                Directory.CreateDirectory(replicaDirectory);

            // Code that copies new files to the replica folder
            if (!File.Exists(replicaFilePath) || File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(replicaFilePath))
            {
                CopyFileWithAttributes(file, replicaFilePath);
                LogAction($"Copied: {relativePath}");
            }
        }
    }

    private static void CleanupReplica()
    {
        foreach (var file in Directory.GetFiles(replicaFolder, "*", SearchOption.AllDirectories))
        {
            string relativePath = file.Substring(replicaFolder.Length + 1);
            string sourceFilePath = Path.Combine(sourceFolder, relativePath);

            //  Code that deletes files in the replica folder that do not exist in the source folder
            if (!File.Exists(sourceFilePath))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
                LogAction($"Deleted: {relativePath}");
            }
        }
        // Code that removes empty directories in the replica folder
        foreach (var dir in Directory.GetDirectories(replicaFolder, "*", SearchOption.AllDirectories).Reverse())
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                LogAction($"Removed empty directory: {dir.Substring(replicaFolder.Length + 1)}");
            }
        }
    }

    private static void CopyFileWithAttributes(string source, string destination)
    {
        using (FileStream sourceStream = File.Open(sourceFolder, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (FileStream destStream = File.Create(replicaFolder))
        {
            sourceStream.CopyTo(destStream);
        }
        File.SetAttributes(destination, FileAttributes.Normal);
        File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
    }

    private static void LogAction(string message)
    {
        string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
        Console.WriteLine(logMessage);
        File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
    }
}