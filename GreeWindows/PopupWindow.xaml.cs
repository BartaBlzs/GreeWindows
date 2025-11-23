using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreeAC.Library;
using GreeAC.Library.Models;

namespace GreeWindows;

public partial class PopupWindow : Window
{
    private GreeAcController _controller;
    private bool _isUpdatingUI = false;
    private bool _isInitialized = false;
    private bool _isSliderDragging = false;
    private int _pendingTemperature = 24;

    public PopupWindow()
    {
        InitializeComponent();


        // Close when clicking outside (losing focus)
        Deactivated += (s, e) => Hide();

        _controller = new GreeAcController();
        _controller.LogMessage += OnLogMessage;
        _controller.StatusUpdated += OnStatusUpdated;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        SetLoading(true, "Initializing...");

        try
        {
            var config = new GreeAC.Library.Managers.ConfigManager().LoadConfig();

            if (!string.IsNullOrEmpty(config.FavoriteDeviceId) && config.FavoriteDevice != null)
            {
                SetLoading(true, "Connecting to device...");

                var connected = await _controller.ConnectToFavoriteAsync();

                if (connected)
                {
                    UpdateDeviceDisplay();
                    SetLoading(true, "Loading status...");
                    await RefreshStatusAsync();
                    ControlsPanel.IsEnabled = true;
                    _isInitialized = true;
                }
                else
                {
                    await ShowDeviceSelectionAsync();
                }
            }
            else
            {
                await ShowDeviceSelectionAsync();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to initialize: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task ShowDeviceSelectionAsync()
    {
        SetLoading(true, "Searching for devices...");

        try
        {
            var devices = await _controller.SearchDevicesAsync();

            SetLoading(false);

            if (devices == null || devices.Count == 0)
            {
                System.Windows.MessageBox.Show("No AC devices found on the network. Please check your network connection and try again.",
                    "No Devices Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectionWindow = new DeviceSelectionWindow(devices);
            if (selectionWindow.ShowDialog() == true)
            {
                var selectedDevice = selectionWindow.SelectedDevice;
                await _controller.SetFavoriteDeviceAsync(selectedDevice.Id);
                UpdateDeviceDisplay();
                await RefreshStatusAsync();
                ControlsPanel.IsEnabled = true;
                _isInitialized = true;
            }
        }
        catch (Exception ex)
        {
            SetLoading(false);
            System.Windows.MessageBox.Show($"Failed to search for devices: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDeviceDisplay()
    {
        if (_controller?.CurrentDevice != null)
        {
            DeviceNameText.Text = _controller.CurrentDevice.DisplayName;
        }
        else
        {
            DeviceNameText.Text = "No device selected";
        }
    }

    private async Task RefreshStatusAsync()
    {
        if (_controller?.CurrentDevice == null) return;

        SetLoading(true, "Refreshing...");

        try
        {
            var status = await _controller.GetStatusAsync();

            if (status != null)
            {
                UpdateUIFromStatus(status);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to communicate with AC: {ex.Message}\n\nTry refreshing the connection.",
                "Communication Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void UpdateUIFromStatus(AcStatus status)
    {
        _isUpdatingUI = true;

        try
        {
            PowerToggle.IsChecked = status.Power;
            PowerStatusText.Text = status.Power ? "On" : "Off";

            TemperatureSlider.Value = status.Temperature;
            TemperatureValueText.Text = $"{status.Temperature}°C";
            _pendingTemperature = status.Temperature;

            switch (status.Mode)
            {
                case 0: ModeAuto.IsChecked = true; break;
                case 1: ModeCool.IsChecked = true; break;
                case 2: ModeDry.IsChecked = true; break;
                case 3: ModeFan.IsChecked = true; break;
                case 4: ModeHeat.IsChecked = true; break;
            }

            switch (status.FanSpeed)
            {
                case 0: FanAuto.IsChecked = true; break;
                case 1: FanLow.IsChecked = true; break;
                case 2: FanMedium.IsChecked = true; break;
                case 3: FanHigh.IsChecked = true; break;
            }

            TurboCheckBox.IsChecked = status.Turbo;
            QuietCheckBox.IsChecked = status.Quiet;
            LightCheckBox.IsChecked = status.Light;
            HealthCheckBox.IsChecked = status.Health;
        }
        finally
        {
            _isUpdatingUI = false;
        }
    }

    private async void PowerToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;

        PowerStatusText.Text = "On";

        try
        {
            await _controller.SetPowerAsync(true);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to set power: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PowerToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;

        PowerStatusText.Text = "Off";

        try
        {
            await _controller.SetPowerAsync(false);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to set power: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TemperatureSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingUI) return;

        int temp = (int)e.NewValue;
        _pendingTemperature = temp;

        if (TemperatureValueText != null)
        {
            TemperatureValueText.Text = $"{temp}°C";
        }
    }

    private async void TemperatureSlider_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isInitialized || _controller == null || !_controller.IsConnected) return;

        try
        {
            await _controller.SetTemperatureAsync(_pendingTemperature);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to set temperature: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;

        var radioButton = sender as System.Windows.Controls.RadioButton;
        if (radioButton?.Tag != null && int.TryParse(radioButton.Tag.ToString(), out int mode))
        {
            try
            {
                await _controller.SetModeAsync(mode);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to set mode: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void FanSpeed_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;

        var radioButton = sender as System.Windows.Controls.RadioButton;
        if (radioButton?.Tag != null && int.TryParse(radioButton.Tag.ToString(), out int speed))
        {
            try
            {
                await _controller.SetFanSpeedAsync(speed);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to set fan speed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void Option_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;
        await UpdateOptionAsync(sender as System.Windows.Controls.CheckBox);
    }

    private async void Option_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingUI || !_isInitialized || _controller == null) return;
        await UpdateOptionAsync(sender as System.Windows.Controls.CheckBox);
    }

    private async Task UpdateOptionAsync(System.Windows.Controls.CheckBox checkBox)
    {
        if (checkBox == null || _controller == null) return;

        try
        {
            var parameters = new Dictionary<string, int>();

            if (checkBox == TurboCheckBox)
                parameters["Tur"] = checkBox.IsChecked == true ? 1 : 0;
            else if (checkBox == QuietCheckBox)
                parameters["Quiet"] = checkBox.IsChecked == true ? 1 : 0;
            else if (checkBox == LightCheckBox)
                parameters["Lig"] = checkBox.IsChecked == true ? 1 : 0;
            else if (checkBox == HealthCheckBox)
                parameters["Health"] = checkBox.IsChecked == true ? 1 : 0;

            await _controller.SetParametersAsync(parameters);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to update option: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ChangeDevice_Click(object sender, RoutedEventArgs e)
    {
        await ShowDeviceSelectionAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var config = new GreeAC.Library.Managers.ConfigManager().LoadConfig();
        var settingsWindow = new SettingsWindow(config.Broadcast ?? "192.168.0.255");

        if (settingsWindow.ShowDialog() == true)
        {
            config.Broadcast = settingsWindow.BroadcastIp;
            new GreeAC.Library.Managers.ConfigManager().SaveConfig(config);

            // Recreate controller with new broadcast
            _controller.LogMessage -= OnLogMessage;
            _controller.StatusUpdated -= OnStatusUpdated;

            _controller = new GreeAcController("ac_config.json");
            _controller.LogMessage += OnLogMessage;
            _controller.StatusUpdated += OnStatusUpdated;

            System.Windows.MessageBox.Show($"Broadcast IP updated to {config.Broadcast}\n\nClick 'Change' to search for AC units.",
                "Settings Updated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void OnLogMessage(object sender, string message)
    {
        System.Diagnostics.Debug.WriteLine($"[AC Controller] {message}");
    }

    private void OnStatusUpdated(object sender, AcStatus status)
    {
        Dispatcher.Invoke(() => UpdateUIFromStatus(status));
    }

    private void SetLoading(bool isLoading, string message = "Loading...")
    {
        if (LoadingOverlay != null && LoadingText != null)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            LoadingText.Text = message;
        }
    }
}