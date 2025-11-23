using System.Net;
using System.Windows;
using System.Windows.Input;

namespace GreeWindows;

public partial class SettingsWindow : Window
{
    public string BroadcastIp { get; private set; }

    public SettingsWindow(string currentBroadcast)
    {
        InitializeComponent();
        BroadcastIpTextBox.Text = currentBroadcast;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var input = BroadcastIpTextBox.Text.Trim();

        if (IPAddress.TryParse(input, out _))
        {
            BroadcastIp = input;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("Please enter a valid IP address.", "Invalid IP",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}