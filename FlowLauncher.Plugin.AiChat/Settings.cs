using System.Collections.Generic;

namespace FlowLauncher.Plugin.AiChat
{
    public class Settings
    {
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowTop { get; set; } = -1;
        public double WindowLeft { get; set; } = -1;
        public bool WindowMaximized { get; set; }

        public double FloatingWindowTop { get; set; } = -1;
        public double FloatingWindowLeft { get; set; } = -1;

        public List<Site> Sites { get; set; } = new List<Site>
        {
            new Site { Name = "豆包", Url = "https://www.doubao.com" },
        };
    }

    public class Site
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
}
