using System.Windows;
using Microsoft.Win32;

namespace PosterMaster;

public partial class SettingsWindow : Window
{
    private const string SettingsFilter = "Настройки «Резак» (*.json)|*.json";
    private readonly Func<UserSettings> _readSettings;
    private readonly Action<UserSettings> _importSettings;

    internal SettingsWindow(Func<UserSettings> readSettings,
        Action<UserSettings> importSettings)
    {
        InitializeComponent();
        _readSettings = readSettings;
        _importSettings = importSettings;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = _readSettings();
            settings.CreateLayout();
            var dialog = new SaveFileDialog
            {
                Title = "Экспортировать настройки", Filter = SettingsFilter,
                DefaultExt = ".json", FileName = "Rezak.settings.json"
            };
            if (dialog.ShowDialog(this) != true) return;
            settings.Save(dialog.FileName);
            ResultText.Text = "Настройки экспортированы.";
        }
        catch (Exception ex)
        {
            ShowError("Не удалось экспортировать настройки", ex);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Импортировать настройки", Filter = SettingsFilter };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            _importSettings(UserSettings.Load(dialog.FileName));
            ResultText.Text = "Настройки применены.";
        }
        catch (Exception ex)
        {
            ShowError("Не удалось импортировать настройки", ex);
        }
    }

    private void ShowError(string title, Exception ex)
    {
        ResultText.Text = "";
        MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
