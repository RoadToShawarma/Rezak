using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace PosterMaster;

public partial class ExportCompletedWindow : Window
{
    private readonly string _folderPath;

    public ExportCompletedWindow(string heading, string message, string path, string folderPath)
    {
        InitializeComponent();
        _folderPath = folderPath;
        HeadingText.Text = heading;
        MessageText.Text = message;
        PathText.Text = path;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(_folderPath))
        {
            OpenFolderError.Text = "Папка недоступна.";
            OpenFolderError.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            using var explorer = Process.Start(new ProcessStartInfo(_folderPath) { UseShellExecute = true });
            OpenFolderError.Visibility = Visibility.Collapsed;
        }
        catch (Win32Exception ex)
        {
            OpenFolderError.Text = $"Не удалось открыть папку. {ex.Message}";
            OpenFolderError.Visibility = Visibility.Visible;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
