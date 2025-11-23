using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace GreeWindows;

public partial class App : Application
{
    private NotifyIcon trayIcon;
    private PopupWindow popupWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create the tray icon
        trayIcon = new NotifyIcon()
        {
            Icon = new Icon(Application.GetResourceStream(new Uri("pack://application:,,,/gree.ico")).Stream),
            Visible = true,
            Text = "GREE"
        };

        // Create context menu
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Exit", null, Exit);
        trayIcon.ContextMenuStrip = contextMenu;

        // Handle left click to toggle window
        trayIcon.MouseClick += TrayIcon_MouseClick;

        // Create the window once but keep it hidden
        popupWindow = new PopupWindow();
        popupWindow.Closing += PopupWindow_Closing;

        // Prevent application from closing when window is hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    private void PopupWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Prevent the window from actually closing, just hide it instead
        e.Cancel = true;
        popupWindow.Hide();
    }

    private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ToggleWindow();
        }
    }

    private void ToggleWindow()
    {
        if (popupWindow.IsVisible)
        {
            popupWindow.Hide();
        }
        else
        {
            // Position near the tray icon (bottom-right corner)
            var workingArea = SystemParameters.WorkArea;
            popupWindow.Left = workingArea.Right - popupWindow.Width - 10;
            popupWindow.Top = workingArea.Bottom - popupWindow.Height - 10;

            popupWindow.Show();
            popupWindow.Activate();
        }
    }

    private void Exit(object sender, EventArgs e)
    {
        trayIcon.Visible = false;
        trayIcon?.Dispose();

        // Allow the window to actually close now
        popupWindow.Closing -= PopupWindow_Closing;
        popupWindow?.Close();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayIcon?.Dispose();
        base.OnExit(e);
    }
}