using System.Windows;
using Microsoft.Win32;
using XamppVHostManager.ViewModels;

namespace XamppVHostManager.Views;

public partial class ProjectEditDialog : Window
{
    private readonly ProjectEditViewModel _vm;

    public ProjectEditDialog(ProjectEditViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void ServerName_LostFocus(object sender, RoutedEventArgs e)
    {
        // Po opuszczeniu pola — zasugeruj .test, jeśli brak kropki.
        _vm.NormalizeServerName();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        // Folder picker OGRANICZONY do dysku przenośnego: start w korzeniu dysku.
        var dlg = new OpenFolderDialog
        {
            Title = "Wybierz folder projektu na dysku przenośnym",
            InitialDirectory = _vm.DriveRoot
        };

        if (dlg.ShowDialog(this) != true)
            return;

        var selected = dlg.FolderName;

        // Ostrzeż, jeśli user wybrał folder spoza dysku przenośnego — nie będzie przenośny.
        if (!_vm.IsOnPortableDrive(selected))
        {
            MessageBox.Show(this,
                "Wybrany folder jest POZA dyskiem przenośnym:\n\n" + selected +
                "\n\nProjekt nie będzie przenośny między komputerami. " +
                "Wybierz folder na dysku przenośnym (" + _vm.DriveRoot + ").",
                "Folder poza dyskiem przenośnym",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _vm.SetDocumentRootFromAbsolute(selected);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        _vm.NormalizeServerName();
        if (!_vm.IsValid)
            return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
