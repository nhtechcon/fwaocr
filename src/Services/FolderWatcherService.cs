using FreeWindowsAutoOCR.Models;

namespace FreeWindowsAutoOCR.Services;

public class PdfFileEventArgs : EventArgs
{
    public string FilePath { get; }
    public string? BackupPath { get; }

    public PdfFileEventArgs(string filePath, string? backupPath)
    {
        FilePath = filePath;
        BackupPath = backupPath;
    }
}

public class FolderWatcherService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly Dictionary<string, string?> _backupPaths = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<PdfFileEventArgs>? PdfFileDetected;

    public void UpdateWatchedFolders(List<WatchedFolder> folders)
    {
        StopAll();

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.FolderPath))
                continue;

            _backupPaths[folder.FolderPath] = folder.BackupPath;

            var watcher = new FileSystemWatcher(folder.FolderPath, "*.pdf")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                EnableRaisingEvents = true
            };

            watcher.Created += OnFileCreated;
            _watchers.Add(watcher);
        }
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Wait for the file to be fully written (e.g., scanner still writing)
        if (!await WaitForFileReady(e.FullPath))
            return;

        var watcherPath = ((FileSystemWatcher)sender).Path;
        _backupPaths.TryGetValue(watcherPath, out var backupPath);

        PdfFileDetected?.Invoke(this, new PdfFileEventArgs(e.FullPath, backupPath));
    }

    /// <summary>
    /// Retries opening the file exclusively to confirm it is no longer being written to.
    /// </summary>
    private static async Task<bool> WaitForFileReady(string path, int maxRetries = 30)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(1000);
            }
        }
        return false;
    }

    private void StopAll()
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        _backupPaths.Clear();
    }

    public void Dispose()
    {
        StopAll();
    }
}
