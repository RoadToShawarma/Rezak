using System.Windows;

namespace PosterMaster;

public partial class ClearImageWindow : Window
{
    public ClearImageWindow()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
