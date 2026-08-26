using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace FlowLauncher.Plugin.AiChat
{
    public partial class SettingsControl : UserControl
    {
        private readonly Settings _settings;
        private readonly Action _saveAction;

        public SettingsControl(Settings settings, Action saveAction)
        {
            InitializeComponent();
            _settings = settings;
            _saveAction = saveAction;
            
            SitesDataGrid.ItemsSource = _settings.Sites;
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (SitesDataGrid.SelectedItem is Site site)
            {
                _settings.Sites.Remove(site);
                SitesDataGrid.Items.Refresh();
                _saveAction?.Invoke();
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _saveAction?.Invoke();
            MessageBox.Show("Settings saved!");
        }
    }
}
