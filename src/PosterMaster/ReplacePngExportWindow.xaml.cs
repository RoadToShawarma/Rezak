using System.Windows;

namespace PosterMaster;

public partial class ReplacePngExportWindow : Window
{
    public ReplacePngExportWindow(string destination)
    {
        InitializeComponent();
        PathText.Text = destination;
    }

    private void Replace_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
