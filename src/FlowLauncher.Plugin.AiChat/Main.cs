using Flow.Launcher.Plugin;
using DevToolkit.AiChat;
using DevToolkit.Core.Wpf;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Control = System.Windows.Controls.Control;

namespace FlowLauncher.Plugin.AiChat
{
    public class Main : IAsyncPlugin, ISettingProvider
    {
        private PluginInitContext _context = default!;
        private Settings _settings = default!;

        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _settings = _context.API.LoadSettingJsonStorage<Settings>();
            return Task.CompletedTask;
        }

        public Control CreateSettingPanel()
        {
            return new SettingsControl(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
        }

        public Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            return Task.FromResult(new List<Result>
            {
                new Result
                {
                    Title = "Open AiChat",
                    SubTitle = "Open the AI Chat window with multiple tabs",
                    IcoPath = "favicon.ico",
                    Action = _ =>
                    {
                        OpenWindow();
                        return true;
                    }
                }
            });
        }

        private void OpenWindow()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new AiChatWindow(_settings, () => _context.API.SaveSettingJsonStorage<Settings>());
                window.Show();
            });
        }
    }
}
