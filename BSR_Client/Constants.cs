using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BSR_Client
{
    public enum EMenuState
    {
        Startup,
        Join,
        Host,
        Settings,
        Gamestart,
        Gameover,
    }

    public class SettingsItem
    {
        public string ItemName { get; set; }

        public bool IsEnabled { get; set; }

        public SettingsItem(EItem item, bool enabled)
        {
            ItemName = item.ToString();
            IsEnabled = enabled;
        }
    }

    public partial class MainWindow
    {
        private Rectangle[] BulletDisplays = null;
        private ProgressBar[] HealthBars = null;
        private Button[] ItemDisplays = null;
        private Button[] PlayerDisplays = null;
        private ClientWorker Sync = null;
        private List<string> Players = new List<string>();
        private string Host = "";
        private string You = "";
        private string Session = "";
        private bool GameStarted = false;
    }
}