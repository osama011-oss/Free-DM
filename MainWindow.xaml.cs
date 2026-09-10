using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;

namespace FreeDM;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DownloadManager _downloadManager = new();

    private DownloadItem? _selectedDownload;

    private string _statusText =
        "Ready";

    public ObservableCollection<DownloadItem> Downloads { get; } =
        new();

    public DownloadItem? SelectedDownload
    {
        get => _selectedDownload;

        set
        {
            if (_selectedDownload == value)
                return;

            _selectedDownload = value;

            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;

        set
        {
            if (_statusText == value)
                return;

            _statusText = value;

            OnPropertyChanged();
        }
    }

    public MainWindow()
    {
        InitializeComponent();

        DataContext = this;
    }

    private async void AddUrl_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new AddUrlWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        string url = dialog.Url.Trim();

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(
                "Please enter a valid HTTP or HTTPS URL.",
                "FreeDM",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        string suggestedName =
            GetFileNameFromUrl(uri);

        var saveDialog = new SaveFileDialog
        {
            Title = "Save Download",
            FileName = suggestedName,
            Filter = "All files (*.*)|*.*",
            DefaultExt = "bin",
            AddExtension = false,
            InitialDirectory =
                GetDownloadFolder()
        };

        if (saveDialog.ShowDialog() != true)
            return;

        var item = new DownloadItem
        {
            Url = url,
            FilePath = saveDialog.FileName,
            FileName =
                Path.GetFileName(saveDialog.FileName),
            State = DownloadState.Waiting
        };

        Downloads.Add(item);
        SelectedDownload = item;

        StatusText = "Downloading...";

        // Start without blocking the UI.
        _ = _downloadManager.StartAsync(item);

        await Task.CompletedTask;
    }

    private async void Start_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SelectedDownload == null)
        {
            MessageBox.Show(
                "Select a download first.",
                "FreeDM",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        if (SelectedDownload.State ==
            DownloadState.Completed)
        {
            StatusText = "Download already completed.";
            return;
        }

        StatusText = "Starting download...";

        _ = _downloadManager.StartAsync(
            SelectedDownload);

        await Task.CompletedTask;
    }

    private void Pause_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SelectedDownload == null)
            return;

        _downloadManager.Pause(
            SelectedDownload);

        StatusText = "Download paused.";
    }

    private void Remove_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (SelectedDownload == null)
            return;

        _downloadManager.Cancel(
            SelectedDownload);

        Downloads.Remove(
            SelectedDownload);

        SelectedDownload = null;

        StatusText = "Download removed.";
    }

    private void OpenFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        string folder = GetDownloadFolder();

        Directory.CreateDirectory(folder);

        Process.Start(
            new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
    }

    private static string GetDownloadFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            "Downloads");
    }

    private static string GetFileNameFromUrl(
        Uri uri)
    {
        string name =
            Path.GetFileName(uri.LocalPath);

        if (string.IsNullOrWhiteSpace(name))
            name = "download";

        foreach (char invalidChar in
                 Path.GetInvalidFileNameChars())
        {
            name = name.Replace(
                invalidChar,
                '_');
        }

        return name;
    }

    public event PropertyChangedEventHandler?
        PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(
                propertyName));
    }
}


// ---------------------------------------------------------
// Add URL dialog
// ---------------------------------------------------------

internal sealed class AddUrlWindow : Window
{
    private readonly
        System.Windows.Controls.TextBox _urlTextBox;

    public string Url =>
        _urlTextBox.Text;

    public AddUrlWindow()
    {
        Title = "Add Download";
        Width = 560;
        Height = 210;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation =
            WindowStartupLocation.CenterOwner;

        var root =
            new System.Windows.Controls.Grid
            {
                Margin = new Thickness(20)
            };

        root.RowDefinitions.Add(
            new System.Windows.Controls.RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new System.Windows.Controls.RowDefinition
            {
                Height = GridLength.Auto
            });

        root.RowDefinitions.Add(
            new System.Windows.Controls.RowDefinition
            {
                Height = GridLength.Auto
            });

        var label =
            new System.Windows.Controls.TextBlock
            {
                Text = "Enter download URL:",
                FontSize = 15,
                Margin =
                    new Thickness(0, 0, 0, 8)
            };

        System.Windows.Controls.Grid.SetRow(
            label,
            0);

        root.Children.Add(label);

        _urlTextBox =
            new System.Windows.Controls.TextBox
            {
                Height = 34,
                FontSize = 14
            };

        System.Windows.Controls.Grid.SetRow(
            _urlTextBox,
            1);

        root.Children.Add(_urlTextBox);

        var buttons =
            new System.Windows.Controls.StackPanel
            {
                Orientation =
                    System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment =
                    HorizontalAlignment.Right,
                Margin =
                    new Thickness(0, 15, 0, 0)
            };

        var cancel =
            new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 90
            };

        cancel.Click += (_, _) =>
        {
            DialogResult = false;
            Close();
        };

        var download =
            new System.Windows.Controls.Button
            {
                Content = "Download",
                Width = 100
            };

        download.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(
                    _urlTextBox.Text))
            {
                MessageBox.Show(
                    "Enter a URL first.",
                    "FreeDM",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            DialogResult = true;
            Close();
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(download);

        System.Windows.Controls.Grid.SetRow(
            buttons,
            2);

        root.Children.Add(buttons);

        Content = root;

        Loaded += (_, _) =>
        {
            _urlTextBox.Focus();
        };
    }
}
