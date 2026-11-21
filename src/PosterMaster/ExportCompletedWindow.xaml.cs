using System.Windows;

namespace PosterMaster;

public partial class ExportCompletedWindow : Window
{
    public ExportCompletedWindow(string heading, string message, string path)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        MessageText.Text = message;
        PathText.Text = path;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
