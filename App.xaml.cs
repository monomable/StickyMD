using System.Windows;
using StickyMD.Services;

namespace StickyMD;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var storageService = new StorageService();
            storageService.Initialize();

            var noteService = new NoteService(storageService);

            MainWindow = new MainWindow(noteService);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"StickyMD 시작 중 오류가 발생했습니다.\n\n{ex.Message}",
                "StickyMD",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }
}
