using System.Windows;

namespace PosterMaster;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Версия {typeof(AboutWindow).Assembly.GetName().Version?.ToString(3)}";
    }
}
