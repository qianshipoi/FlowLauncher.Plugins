using DevToolkit.AiChat;

﻿using iNKORE.UI.WPF.Modern.Controls.Helpers;

using Microsoft.Web.WebView2.Wpf;

using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DevToolkit.Core.Wpf
{
    public partial class AiChatWindow : Window
    {
        private readonly Settings _settings;
        private readonly Action _saveSettings;
        private FloatingWindow? _floatingWindow;

        public AiChatWindow(Settings settings, Action saveSettings)
        {
            InitializeComponent();
            _settings = settings;
            _saveSettings = saveSettings;
            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnFloatButtonClick(object sender, RoutedEventArgs e)
        {
            if (_floatingWindow == null)
            {
                _floatingWindow = new FloatingWindow(_settings, () =>
                {
                    this.Show();
                    this.WindowState = WindowState.Normal; // Ensure it restores
                    _floatingWindow?.Hide();
                });
            }

            if (_settings.FloatingWindowLeft != -1 && _settings.FloatingWindowTop != -1)
            {
                _floatingWindow.Left = _settings.FloatingWindowLeft;
                _floatingWindow.Top = _settings.FloatingWindowTop;
            }
            else
            {
                _floatingWindow.Left = SystemParameters.WorkArea.Right - 100;
                _floatingWindow.Top = SystemParameters.WorkArea.Top + 50;
            }

            _floatingWindow.Show();
            this.Hide();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            TabControlHelper.SetIsAddTabButtonVisible(ChatTabControl, true);
            TabControlHelper.AddAddTabButtonClickHandler(ChatTabControl, OnAddTabButtonClick);

            if (_settings.WindowWidth > 0) Width = _settings.WindowWidth;
            if (_settings.WindowHeight > 0) Height = _settings.WindowHeight;
            if (_settings.WindowLeft != -1) Left = _settings.WindowLeft;
            if (_settings.WindowTop != -1) Top = _settings.WindowTop;
            if (_settings.WindowMaximized) WindowState = WindowState.Maximized;

            if (_settings.Sites != null)
            {
                foreach(var site in _settings.Sites)
                {
                    if (!site.Enabled) continue;
                    AddTabForSite(site, isClosable: false);
                }

                if (ChatTabControl.Items.Count > 0)
                {
                    ChatTabControl.SelectedIndex = 0;
                }
            }
        }

        private void OnAddTabButtonClick(object sender, RoutedEventArgs e)
        {
            var site = _settings.Sites?.FirstOrDefault(s => s.Enabled);
            if (site == null)
            {
                MessageBox.Show("没有可用的站点，请在设置中添加并启用站点。", "AiChat", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddTabForSite(site, isClosable: true);
            ChatTabControl.SelectedIndex = ChatTabControl.Items.Count - 1;
        }

        private void AddTabForSite(Site site, bool isClosable)
        {
            var tabItem = new TabItem { Header = site.Name };
            var webView = new WebView2
            {
                Source = new Uri(site.Url)
            };
            
            webView.CoreWebView2InitializationCompleted += (s, args) =>
            {
                if (args.IsSuccess)
                {
                    webView.CoreWebView2.NewWindowRequested += (ws, wargs) =>
                    {
                        wargs.Handled = true;
                        try 
                        {
                            Process.Start(new ProcessStartInfo(wargs.Uri) { UseShellExecute = true });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to open link: {ex.Message}");
                        }
                    };
                }
            };

            tabItem.Content = webView;
            TabItemHelper.SetIsClosable(tabItem, isClosable);
            ChatTabControl.Items.Add(tabItem);
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _floatingWindow?.Close();
            _settings.WindowMaximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = Width;
                _settings.WindowHeight = Height;
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
            _saveSettings();
        }
    }
}
