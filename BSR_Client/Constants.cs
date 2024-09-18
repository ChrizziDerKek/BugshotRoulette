using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace BSR_Client
{
    public enum EBullet
    {
        Undefined,
        Blank,
        Live,
    }

    public enum EItem
    {
        Nothing,
        Handcuffs,
        Cigarettes,
        Saw,
        Magnifying,
        Beer,
        Inverter,
        Medicine,
        Phone,
        Adrenaline,
        Magazine,
        Gunpowder,
        Bullet,
        Trashbin,
        Heroine,
        Katana,
        Swapper,
        Hat,
        Count,
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
    }
}