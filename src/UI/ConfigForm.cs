using FreeWindowsAutoOCR.Models;

namespace FreeWindowsAutoOCR.UI;

public class ConfigForm : Form
{
    private readonly ListView _listView;
    private readonly Button _addButton;
    private readonly Button _removeButton;
    private readonly Button _setBackupButton;
    private readonly Button _clearBackupButton;
    private readonly Button _okButton;
    private readonly Button _cancelButton;

    public AppConfig Config { get; private set; }

    public ConfigForm(AppConfig config)
    {
        // Deep-copy config so Cancel doesn't mutate the original
        Config = new AppConfig
        {
            WatchedFolders = config.WatchedFolders.Select(f => new WatchedFolder
            {
                FolderPath = f.FolderPath,
                BackupPath = f.BackupPath
            }).ToList()
        };

        Text = "Free Windows Auto OCR - Configuration";
        Size = new Size(620, 420);
        MinimumSize = new Size(480, 320);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        var label = new Label
        {
            Text = "Watched Folders:",
            Location = new Point(12, 12),
            AutoSize = true
        };

        _listView = new ListView
        {
            Location = new Point(12, 34),
            Size = new Size(580, 280),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };
        _listView.Columns.Add("Folder Path", 290);
        _listView.Columns.Add("Backup Path", 270);

        _addButton = new Button
        {
            Text = "Add Folder...",
            Size = new Size(100, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _removeButton = new Button
        {
            Text = "Remove",
            Size = new Size(80, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _setBackupButton = new Button
        {
            Text = "Set Backup...",
            Size = new Size(100, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _clearBackupButton = new Button
        {
            Text = "Clear Backup",
            Size = new Size(100, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        _okButton = new Button
        {
            Text = "OK",
            Size = new Size(80, 28),
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(80, 28),
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };

        _addButton.Click += OnAddFolder;
        _removeButton.Click += OnRemoveFolder;
        _setBackupButton.Click += OnSetBackup;
        _clearBackupButton.Click += OnClearBackup;

        Controls.AddRange(new Control[]
        {
            label, _listView,
            _addButton, _removeButton, _setBackupButton, _clearBackupButton,
            _okButton, _cancelButton
        });

        AcceptButton = _okButton;
        CancelButton = _cancelButton;

        PositionButtons();
        Resize += (_, _) => PositionButtons();
        RefreshList();
    }

    private void PositionButtons()
    {
        int y = ClientSize.Height - 40;
        _addButton.Location = new Point(12, y);
        _removeButton.Location = new Point(118, y);
        _setBackupButton.Location = new Point(204, y);
        _clearBackupButton.Location = new Point(310, y);
        _okButton.Location = new Point(ClientSize.Width - 176, y);
        _cancelButton.Location = new Point(ClientSize.Width - 92, y);
    }

    private void RefreshList()
    {
        _listView.Items.Clear();
        foreach (var folder in Config.WatchedFolders)
        {
            var item = new ListViewItem(folder.FolderPath);
            item.SubItems.Add(folder.BackupPath ?? "(none)");
            _listView.Items.Add(item);
        }
    }

    private void OnAddFolder(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder to watch for new PDF files"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        bool isDuplicate = Config.WatchedFolders
            .Any(f => f.FolderPath.Equals(dialog.SelectedPath, StringComparison.OrdinalIgnoreCase));

        if (isDuplicate)
        {
            MessageBox.Show("This folder is already being watched.",
                "Duplicate", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Config.WatchedFolders.Add(new WatchedFolder { FolderPath = dialog.SelectedPath });
        RefreshList();
    }

    private void OnRemoveFolder(object? sender, EventArgs e)
    {
        if (_listView.SelectedIndices.Count == 0)
            return;

        var index = _listView.SelectedIndices[0];
        Config.WatchedFolders.RemoveAt(index);
        RefreshList();
    }

    private void OnSetBackup(object? sender, EventArgs e)
    {
        if (_listView.SelectedIndices.Count == 0)
            return;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Select backup folder for original (non-OCR) files"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            Config.WatchedFolders[_listView.SelectedIndices[0]].BackupPath = dialog.SelectedPath;
            RefreshList();
        }
    }

    private void OnClearBackup(object? sender, EventArgs e)
    {
        if (_listView.SelectedIndices.Count == 0)
            return;

        Config.WatchedFolders[_listView.SelectedIndices[0]].BackupPath = null;
        RefreshList();
    }
}
