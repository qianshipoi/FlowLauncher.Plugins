using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace FlowLauncher.Plugin.AiChat
{
    public partial class FloatingWindow : Window
    {
        private readonly Action _onRestoreProxy;
        private readonly Settings _settings;
        private bool _isDragging = false;

        public FloatingWindow(Settings settings, Action onRestoreProxy)
        {
            InitializeComponent();
            _settings = settings;
            _onRestoreProxy = onRestoreProxy;
        }

        private void DragThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isDragging = false;
        }

        private void DragThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            this.Left += e.HorizontalChange;
            this.Top += e.VerticalChange;
            
            _settings.FloatingWindowLeft = this.Left;
            _settings.FloatingWindowTop = this.Top;

            if (Math.Abs(e.HorizontalChange) > 1 || Math.Abs(e.VerticalChange) > 1)
            {
                _isDragging = true;
            }
        }

        private void DragThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!_isDragging)
            {
                _onRestoreProxy?.Invoke();
            }
        }
    }
}
