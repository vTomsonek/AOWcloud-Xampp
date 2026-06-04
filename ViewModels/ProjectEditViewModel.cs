using XamppVHostManager.Models;
using XamppVHostManager.Services;

namespace XamppVHostManager.ViewModels;

/// <summary>
/// ViewModel dialogu dodawania/edycji projektu. Operuje na ścieżkach
/// WZGLĘDNYCH; pełną ścieżkę z folder-pickera zamienia na względną przez DriveService.
/// </summary>
public sealed class ProjectEditViewModel : ViewModelBase
{
    private readonly DriveService _drive;

    public bool IsNew { get; }
    public string Title => IsNew ? "Dodaj projekt" : "Edytuj projekt";

    private string _serverName = "";
    public string ServerName
    {
        get => _serverName;
        set { if (SetField(ref _serverName, value)) RefreshValidation(); }
    }

    private string _documentRootRelative = "";
    public string DocumentRootRelative
    {
        get => _documentRootRelative;
        set { if (SetField(ref _documentRootRelative, value)) RefreshValidation(); }
    }

    private int _port = 80;
    public int Port
    {
        get => _port;
        set { if (SetField(ref _port, value)) RefreshValidation(); }
    }

    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    private string _validationMessage = "";
    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetField(ref _validationMessage, value);
    }

    private bool _isValid;
    public bool IsValid
    {
        get => _isValid;
        private set => SetField(ref _isValid, value);
    }

    /// <summary>Pełna ścieżka DocumentRoot (do podglądu/otwarcia) z aktualną literą.</summary>
    public string AbsoluteDocumentRootPreview =>
        string.IsNullOrWhiteSpace(DocumentRootRelative)
            ? "(wybierz folder)"
            : _drive.ToAbsolute(DocumentRootRelative);

    public ProjectEditViewModel(DriveService drive, ProjectModel? existing)
    {
        _drive = drive;
        IsNew = existing == null;
        if (existing != null)
        {
            _serverName = existing.ServerName;
            _documentRootRelative = existing.DocumentRootRelative;
            _port = existing.Port;
            _enabled = existing.Enabled;
        }
        RefreshValidation();
    }

    /// <summary>
    /// Ustawia DocumentRoot z pełnej ścieżki wybranej w pickerze.
    /// Zwraca false, jeśli folder jest POZA dyskiem przenośnym (nie da się zapisać
    /// przenośnie) — wywołujący ma ostrzec użytkownika.
    /// </summary>
    public bool SetDocumentRootFromAbsolute(string absolutePath)
    {
        var rel = _drive.ToRelative(absolutePath);
        if (string.IsNullOrEmpty(rel))
            return false; // poza dyskiem przenośnym

        DocumentRootRelative = rel;
        OnPropertyChanged(nameof(AbsoluteDocumentRootPreview));
        return true;
    }

    /// <summary>Korzeń dysku przenośnego — początkowy katalog dla folder-pickera.</summary>
    public string DriveRoot => _drive.DriveRoot;

    /// <summary>Czy podana ścieżka jest na dysku przenośnym?</summary>
    public bool IsOnPortableDrive(string absolutePath) => _drive.IsOnPortableDrive(absolutePath);

    /// <summary>Sugeruje końcówkę .test, jeśli brak kropki.</summary>
    public void NormalizeServerName()
    {
        if (!string.IsNullOrWhiteSpace(ServerName) && !ServerName.Contains('.'))
            ServerName = ProjectModel.SuggestServerName(ServerName);
    }

    private void RefreshValidation()
    {
        if (!ProjectModel.IsServerNameValid(ServerName))
        {
            IsValid = false;
            ValidationMessage = string.IsNullOrWhiteSpace(ServerName)
                ? "Podaj nazwę hosta (np. projekt1.test)."
                : "Niepoprawna nazwa hosta. Użyj np. projekt1.test";
            OnPropertyChanged(nameof(AbsoluteDocumentRootPreview));
            return;
        }
        if (string.IsNullOrWhiteSpace(DocumentRootRelative))
        {
            IsValid = false;
            ValidationMessage = "Wybierz folder projektu na dysku przenośnym.";
            return;
        }
        if (Port is < 1 or > 65535)
        {
            IsValid = false;
            ValidationMessage = "Port musi być z zakresu 1–65535.";
            return;
        }
        IsValid = true;
        ValidationMessage = "";
        OnPropertyChanged(nameof(AbsoluteDocumentRootPreview));
    }

    /// <summary>Buduje model z aktualnych wartości formularza.</summary>
    public ProjectModel ToModel() => new()
    {
        ServerName = ServerName.Trim(),
        DocumentRootRelative = DocumentRootRelative.Trim(),
        Port = Port,
        Enabled = Enabled
    };
}
