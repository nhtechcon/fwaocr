using FreeWindowsAutoOCR.Models;
using FreeWindowsAutoOCR.Services;

namespace FreeWindowsAutoOCR.UI;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly FolderWatcherService _watcherService;
    private readonly OcrProcessor _ocrProcessor;
    private AppConfig _config;

    public TrayApplicationContext()
    {
        _config = AppConfig.Load();
        _ocrProcessor = new OcrProcessor();
        _watcherService = new FolderWatcherService();
        _watcherService.PdfFileDetected += OnPdfFileDetected;

        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Free Windows Auto OCR",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowConfig();
        StartWatching();
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Configure...", null, (_, _) => ShowConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void ShowConfig()
    {
        using var form = new ConfigForm(_config);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.Config;
            _config.Save();
            StartWatching();
        }
    }

    private void StartWatching()
    {
        _watcherService.UpdateWatchedFolders(_config.WatchedFolders);
    }

    private async void OnPdfFileDetected(object? sender, PdfFileEventArgs e)
    {
        try
        {
            _trayIcon.Text = $"Processing: {Path.GetFileName(e.FilePath)}";

            // Backup original before OCR overwrites it
            if (!string.IsNullOrEmpty(e.BackupPath))
            {
                Directory.CreateDirectory(e.BackupPath);
                var backupFile = GetUniqueFilePath(e.BackupPath, Path.GetFileName(e.FilePath));
                File.Copy(e.FilePath, backupFile);
            }

            await _ocrProcessor.ProcessAsync(e.FilePath);

            _trayIcon.Text = "Free Windows Auto OCR";
            _trayIcon.ShowBalloonTip(3000, "OCR Complete",
                $"Processed: {Path.GetFileName(e.FilePath)}", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            _trayIcon.ShowBalloonTip(5000, "OCR Error",
                $"Failed: {Path.GetFileName(e.FilePath)}\n{ex.Message}", ToolTipIcon.Error);
            _trayIcon.Text = "Free Windows Auto OCR";
        }
    }

    /// <summary>
    /// Returns a unique file path, appending a timestamp suffix if the target already exists.
    /// </summary>
    private static string GetUniqueFilePath(string directory, string fileName)
    {
        var targetPath = Path.Combine(directory, fileName);
        if (!File.Exists(targetPath))
            return targetPath;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return Path.Combine(directory, $"{nameWithoutExt}_{timestamp}{ext}");
    }

    private static Icon CreateDefaultIcon()
    {
        // Load from embedded resource (assets/icon.ico)
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var stream = assembly.GetManifestResourceStream("icon.ico");
        if (stream != null)
            return new Icon(stream, 32, 32);

        // Fallback: simple blue square if resource missing
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.DodgerBlue);
        return Icon.FromHandle(bmp.GetHicon());
    }

    private void ExitApplication()
    {
        _watcherService.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _watcherService.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
