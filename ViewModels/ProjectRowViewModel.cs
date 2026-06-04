using System;
using XamppVHostManager.Models;
using XamppVHostManager.Services;

namespace XamppVHostManager.ViewModels;

/// <summary>
/// Wiersz listy projektów w DataGrid. Opakowuje ProjectModel, dodaje
/// powiadomienia oraz pełną ścieżkę do podglądu. Zmiana toggla Enabled
/// zgłasza zdarzenie, by widok mógł od razu zapisać i zastosować.
/// </summary>
public sealed class ProjectRowViewModel : ViewModelBase
{
    private readonly DriveService _drive;
    public ProjectModel Model { get; }

    /// <summary>Wywoływane, gdy użytkownik przełączy Enabled w gridzie.</summary>
    public event Action? EnabledToggled;

    public ProjectRowViewModel(ProjectModel model, DriveService drive)
    {
        Model = model;
        _drive = drive;
    }

    public string ServerName => Model.ServerName;
    public string DocumentRootRelative => Model.DocumentRootRelative;
    public int Port => Model.Port;

    public string AbsolutePath => _drive.ToAbsolute(Model.DocumentRootRelative);

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value) return;
            Model.Enabled = value;
            OnPropertyChanged();
            EnabledToggled?.Invoke();
        }
    }

    /// <summary>Odśwież pola po edycji modelu.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(DocumentRootRelative));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(AbsolutePath));
    }
}
