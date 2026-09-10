using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FreeDM;

public enum DownloadState
{
    Waiting,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled
}

public sealed class DownloadItem : INotifyPropertyChanged
{
    private string _fileName = "";
    private string _filePath = "";
    private string _url = "";
    private string _size = "Unknown";
    private string _speed = "-";
    private string _status = "Waiting";
    private double _progress;
    private DownloadState _state = DownloadState.Waiting;

    public string FileName
    {
        get => _fileName;
        set => SetField(ref _fileName, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetField(ref _filePath, value);
    }

    public string Url
    {
        get => _url;
        set => SetField(ref _url, value);
    }

    public string Size
    {
        get => _size;
        set => SetField(ref _size, value);
    }

    public string Speed
    {
        get => _speed;
        set => SetField(ref _speed, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }

    public DownloadState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;

            _state = value;
            OnPropertyChanged();

            if (value != DownloadState.Failed)
                Status = value.ToString();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
