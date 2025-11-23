using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using GreeAC.Library.Models;

namespace GreeWindows;

public partial class DeviceSelectionWindow : Window
{
    public GreeDevice SelectedDevice { get; private set; }

    public DeviceSelectionWindow(List<GreeDevice> devices)
    {
        InitializeComponent();
        DeviceListBox.ItemsSource = devices;

        if (devices.Count > 0)
        {
            DeviceListBox.SelectedIndex = 0;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            this.DragMove();
        }
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceListBox.SelectedItem is GreeDevice device)
        {
            SelectedDevice = device;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("Please select a device.", "No Selection",
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

    private void DeviceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DeviceListBox.SelectedItem is GreeDevice device)
        {
            SelectedDevice = device;
            DialogResult = true;
            Close();
        }
    }
}