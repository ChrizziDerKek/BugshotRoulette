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

    public enum EFlags
    {
        None = 0,
        Shooting = 1 << 0,
        UsingPlayerItem = 1 << 1,
        UsingAdrenaline = 1 << 2,
        UsingHeroine = 1 << 3,
        UsingKatana = 1 << 4,
        UsingSwapper = 1 << 5,
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
        private readonly Dictionary<EItem, string> ItemDescriptions = new Dictionary<EItem, string>()
        {
            { EItem.Nothing, null },
            { EItem.Handcuffs, "Skips the enemy's turns so you can shoot 2 times" },
            { EItem.Cigarettes, "Restores 1 Health" },
            { EItem.Saw, "Saws off the shotgun's barrel so that it deals 2 damage\nCan be combined with gunpowder to deal 4 damage or 3 to yourself" },
            { EItem.Magnifying, "Shows you the bullet type that's currently loaded" },
            { EItem.Beer, "Racks the bullet that's currently loaded" },
            { EItem.Inverter, "Inverts the bullet type that's currently loaded\nA live becomes a blank and vice versa" },
            { EItem.Medicine, "Has a 50/50 chance of restoring 2 Health or losing 1" },
            { EItem.Phone, "Call a stranger who tells you the type of a random bullet" },
            { EItem.Adrenaline, "Select a player to look at their items and steal one of them to use it immediately\nStealing adrenaline isn't possible" },
            { EItem.Magazine, "Generates a new set of bullets" },
            { EItem.Gunpowder, "Has a 50/50 chance of dealing 3 damage or exploding in the barrel and dealing 2 to yourself\nCan be combined with a saw to deal 4 damage or 3 to yourself" },
            { EItem.Bullet, "Loads a new random bullet type into the gun\nAlways appears at the end of the round" },
            { EItem.Trashbin, "Allows you to throw away an item and receive a different one" },
            { EItem.Heroine, "Select a player to give them heroine\nThey can't use an item in their next round" },
            { EItem.Katana, "Select a player to cut their fingers off\nThey can only use 1 item in their next round" },
            { EItem.Swapper, "Swaps your items with the ones with the selected player" },
            { EItem.Hat, "Hides the bullets for every player" },
            { EItem.Count, null },
        };

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
        private EFlags Flags = EFlags.None;
        private bool PacketHandled = false;
    }
}