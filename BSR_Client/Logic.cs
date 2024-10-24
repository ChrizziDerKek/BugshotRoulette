using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.Runtime.Remoting.Messaging;
using System.Xml.Linq;

namespace BSR_Client
{
    public partial class MainWindow
    {
        public void Fatal(string str, bool exit = true)
        {
            MessageBox.Show(str, "Error");
            if (exit)
                Close();
        }

        public void Announce(string str)
        {
            Log.Items.Insert(0, str);
        }

        public void Win(int player)
        {
            Gameover.Visibility = Visibility.Visible;
            Game.Visibility = Visibility.Hidden;
            Login.Visibility = Visibility.Hidden;
            Winner.Text = GetPlayerName(player) + " won!";
        }

        public void SetHealth(int player, int health)
        {
            if (HealthBars[player].Value + health > HealthBars[player].Maximum)
                return;
            HealthBars[player].Value = health;
        }

        public int GetHealth(int player)
        {
            if (HealthBars[player].Value > GetMaxHealth())
                return GetMaxHealth();
            return (int)HealthBars[player].Value;
        }

        public void SetMaxHealth(int health)
        {
            foreach (ProgressBar h in HealthBars)
            {
                h.Maximum = health;
                h.Value = health;
            }
        }

        public int GetMaxHealth()
        {
            return (int)HealthBars[0].Maximum;
        }

        public bool DoesPlayerExist(int slot)
        {
            string name = GetPlayerName(slot);
            if (string.IsNullOrEmpty(name))
                return false;
            return name.ToLower() != "none";
        }

        public int GetPlayerIndex(string player)
        {
            for (int i = 0; i < PlayerDisplays.Length; i++)
            {
                Button p = PlayerDisplays[i];
                if (!(p.Content is Grid))
                    continue;
                UIElement text = (p.Content as Grid).Children[1];
                if (text == null)
                    continue;
                if (!(text is TextBlock))
                    continue;
                if ((text as TextBlock).Text == player)
                    return i;
            }
            return -1;
        }

        public string GetPlayerName(int slot)
        {
            Button p = PlayerDisplays[slot];
            if (!(p.Content is Grid))
                return "";
            UIElement text = (p.Content as Grid).Children[1];
            if (text == null)
                return "";
            if (!(text is TextBlock))
                return "";
            return (text as TextBlock).Text;
        }

        public string GetPlayerFromSlot(string slot)
        {
            for (int i = 0; i < PlayerDisplays.Length; i++)
                if (PlayerDisplays[i].Name == slot)
                    return GetPlayerName(i);
            return "";
        }

        public void SetActive(bool active)
        {
            Shoot.IsEnabled = active;
            foreach (Button it in ItemDisplays)
                it.IsEnabled = active;
            foreach (Button it in PlayerDisplays)
                it.IsEnabled = false;
            if (active)
                Announce("Your turn");
        }

        public void PopulatePlayerSlot(Button slot, string player, bool angry)
        {
            if (slot == null)
                return;
            if (!(slot.Content is Grid))
                return;
            UIElement text = (slot.Content as Grid).Children[1];
            if (!(text is TextBlock))
                return;
            bool remove = string.IsNullOrEmpty(player);
            if (!remove)
                (text as TextBlock).Text = player;
            UIElement image = (slot.Content as Grid).Children[0];
            if (!(image is Image))
                return;
            if (remove)
                (image as Image).Source = null;
            else
                (image as Image).Source = new BitmapImage(new Uri(angry ? "textures/dealer2.png" : "textures/dealer1.png", UriKind.Relative));
        }

        public void SetPlayersInteractable(bool interactable)
        {
            if (interactable)
                SetActive(false);
            for (int i = 0; i < PlayerDisplays.Length; i++)
                if (DoesPlayerExist(i))
                    PlayerDisplays[i].IsEnabled = interactable;
        }

        public void ResetPlayerSlots()
        {
            int yourslot = 0;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i] == You)
                    yourslot = i;
                PopulatePlayerSlot(PlayerDisplays[i], Players[i], false);
            }
            if (yourslot == 0)
                return;
            string other = GetPlayerName(0);
            PopulatePlayerSlot(PlayerDisplays[0], You, false);
            PopulatePlayerSlot(PlayerDisplays[yourslot], other, false);
            Players[0] = You;
            Players[yourslot] = other;
        }

        public void PutItemInSlot(Button slot, EItem item)
        {
            if (slot == null)
                return;
            slot.Visibility = item == EItem.Nothing ? Visibility.Hidden : Visibility.Visible;
            if (item != EItem.Nothing)
            {
                if (!ItemDescriptions.TryGetValue(item, out string desc))
                    desc = "NO DESCRIPTION";
                slot.ToolTip = item.ToString() + "\n\n" + desc;
            }
            else slot.ToolTip = null;
            if (!(slot.Content is Grid))
                return;
            UIElement text = (slot.Content as Grid).Children[1];
            if (!(text is TextBlock))
                return;
            (text as TextBlock).Text = item.ToString();
            UIElement image = (slot.Content as Grid).Children[0];
            if (!(image is Image))
                return;
            string texture = item.ToString().ToLower();
            if (item == EItem.Nothing)
                (image as Image).Source = null;
            else
                (image as Image).Source = new BitmapImage(new Uri("textures/" + texture + ".png", UriKind.Relative));
        }

        public void OverrideItems(EItem[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                Button slot = ItemDisplays[i];
                PutItemInSlot(slot, items[i]);
            }
        }

        public void PushItem(EItem item)
        {
            foreach (Button itemd in ItemDisplays)
            {
                if (Enum.TryParse(GetItemType(itemd), out EItem type) && type == EItem.Nothing)
                {
                    PutItemInSlot(itemd, item);
                    break;
                }
            }
        }

        public void HideBullets()
        {
            foreach (Rectangle b in BulletDisplays)
                b.Fill = Brushes.Transparent;
        }

        public void ShowBullets(EBullet[] bulletlist)
        {
            if (bulletlist.Length > 8)
                return;
            HideBullets();
            int nblank = 0;
            int nlive = 0;
            foreach (EBullet b in bulletlist)
            {
                if (b == EBullet.Live)
                    nlive++;
                if (b == EBullet.Blank)
                    nblank++;
            }
            for (int i = 0; i < bulletlist.Length; i++)
                BulletDisplays[i].Fill = bulletlist[i] == EBullet.Live ? Brushes.Red : bulletlist[i] == EBullet.Blank ? Brushes.Gray : Brushes.Green;
        }

        public void HideBullet(EBullet bullet, bool inverted)
        {
            Brush target = null;
            if (!inverted)
            {
                if (bullet == EBullet.Live)
                    target = Brushes.Red;
                else if (bullet == EBullet.Blank)
                    target = Brushes.Gray;
            }
            else
            {
                if (bullet == EBullet.Live)
                    target = Brushes.Gray;
                else if (bullet == EBullet.Blank)
                    target = Brushes.Red;
            }
            foreach (Rectangle b in BulletDisplays)
            {
                if (b.Fill == target)
                {
                    b.Fill = Brushes.Transparent;
                    break;
                }
            }
        }

        public void RemoveBullet(EBullet type)
        {
            Brush target = null;
            if (type == EBullet.Blank)
                target = Brushes.Gray;
            if (type == EBullet.Live)
                target = Brushes.Red;
            foreach (Rectangle b in BulletDisplays)
            {
                if (b.Fill == target)
                {
                    b.Fill = Brushes.Transparent;
                    break;
                }
            }
        }

        public void UpdateTitle()
        {
            string extension = " - " + You;
            if (Title.Contains(extension))
                return;
            Title += extension;
        }

        public void RemoveInactivePlayerSlots()
        {
            for (int i = 0; i < PlayerDisplays.Length; i++)
            {
                if (DoesPlayerExist(i))
                    continue;
                PlayerDisplays[i].Visibility = Visibility.Hidden;
                HealthBars[i].Visibility = Visibility.Hidden;
            }
        }

        public void SwitchToHostMenu()
        {
            if (GameStarted)
                return;
            SetMenuState(EMenuState.Host);
            UpdatePlayerlist();
            HostUsername.Text = Username.Text;
            Lobby.Text = LobbyJoin.Text;
        }

        public void UpdatePlayerlist()
        {
            Playerlist.Items.Clear();
            foreach (string player in Players)
                Playerlist.Items.Add(player);
            Playerlist.Items.Refresh();
            StartGame.Content = IsHost() && Players.Count > 1 ? "Start Game" : "";
        }

        public bool IsValidIP(string ip)
        {
            int ndots = 0;
            foreach (char c in ip)
                if (c == '.')
                    ndots++;
            if (ndots != 3)
                return false;
            string[] data = ip.Split('.');
            foreach (string s in data)
            {
                if (!int.TryParse(s, out int i))
                    return false;
                if (i > 255 || i < 0)
                    return false;
            }
            return true;
        }

        public bool IsHost()
        {
            if (string.IsNullOrEmpty(Host) || string.IsNullOrEmpty(You))
                return false;
            return Host == You;
        }

        public void AttemptConnect(bool host, string ip)
        {
            string username = host ? HostUsername.Text : Username.Text;
            string session = host ? Lobby.Text : LobbyJoin.Text;
            try
            {
                Sync = new ClientWorker(ip, 19121);
                Sync.OnPacketReceived += Client_OnPacketReceived;
                Sync.Start();
                You = username;
                Session = session;
                Packet.Send(new PacketJoinRequest(Session, You, !host), Sync);
                GameSettings.Content = "Game Settings";
                SessionHost.Content = "";
                Connect.Content = "";
            }
            catch
            {
                Fatal("Failed to connect to server");
            }
        }

        public void UpdateHealth(int health, string player = null)
        {
            if (player == null)
                player = You;
            if (health < 0)
                health = 0;
            if (health > GetMaxHealth())
                health = GetMaxHealth();
            if (GetHealth(player) == health)
                return;
            for (int i = 0; i < Players.Count; i++)
            {
                if (Players[i] == player)
                {
                    HealthBars[i].Value = health;
                    break;
                }
            }
        }

        public int GetBulletCount(List<EBullet> bullets, EBullet type)
        {
            int count = 0;
            foreach (EBullet bullet in bullets)
                if (type == bullet)
                    count++;
            return count;
        }

        public int GetHealth(string player = null)
        {
            if (player == null)
                player = You;
            for (int i = 0; i < Players.Count; i++)
                if (Players[i] == player)
                    return (int)HealthBars[i].Value;
            return -1;
        }

        public void PopulateSettings()
        {
            SettingsData defaultdata = new SettingsData();
            foreach (KeyValuePair<EItem, bool> it in defaultdata.EnabledItems)
                SettingsItems.Items.Add(new SettingsItem(it.Key, it.Value));
            SettingsBotDealer.IsChecked = defaultdata.BotDealer;
            SettingsMaxPlayers.Value = defaultdata.MaxHealth;
            SettingsMinHealth.Value = defaultdata.MinHealth;
            SettingsMaxHealth.Value = defaultdata.MaxHealth;
            SettingsMinItems.Value = defaultdata.MinItems;
            SettingsMaxItems.Value = defaultdata.MaxItems;
            SettingsMinBullets.Value = defaultdata.MinBullets;
            SettingsMaxBullets.Value = defaultdata.MaxBullets;
            SettingsDunceDealer.IsChecked = defaultdata.DunceDealer;
            SettingsOriginalItemsOnly.IsChecked = defaultdata.OriginalItemsOnly;
            SettingsNoItems.IsChecked = defaultdata.NoItems;
        }

        public void SyncSettings()
        {
            Dictionary<EItem, bool> enabled = new Dictionary<EItem, bool>();
            foreach (SettingsItem item in SettingsItems.Items)
            {
                if (!Enum.TryParse(item.ItemName, out EItem it))
                    continue;
                enabled.Add(it, item.IsEnabled);
            }
            SettingsData data = new SettingsData(
                SettingsBotDealer.IsChecked == true,
                (int)SettingsMaxPlayers.Value,
                (int)SettingsMinHealth.Value,
                (int)SettingsMaxHealth.Value,
                (int)SettingsMinItems.Value,
                (int)SettingsMaxItems.Value,
                (int)SettingsMinBullets.Value,
                (int)SettingsMaxBullets.Value,
                SettingsDunceDealer.IsChecked == true,
                SettingsOriginalItemsOnly.IsChecked == true,
                SettingsNoItems.IsChecked == true,
                enabled
            );
            Packet.Send(new PacketUpdateSettings(data), Sync);
        }

        public void SetFlag(EFlags flag) => Flags |= flag;

        public void ResetFlag(EFlags flags) => Flags &= ~flags;

        public bool IsFlagSet(EFlags flag) => (Flags & flag) != 0;

        public string GetItemType(Button slot)
        {
            if (slot == null)
                return "";
            if (!(slot.Content is Grid))
                return "";
            UIElement text = (slot.Content as Grid).Children[1];
            if (text == null)
                return "";
            if (!(text is TextBlock))
                return "";
            return (text as TextBlock).Text;
        }

        public int GetItemCount(EItem item = EItem.Count)
        {
            int result = 0;
            foreach (Button itemd in ItemDisplays)
            {
                string typestr = GetItemType(itemd);
                if (!Enum.TryParse(typestr, out EItem type))
                    continue;
                if ((item == EItem.Count && type != EItem.Nothing) || (item != EItem.Count && item == type))
                    result++;
            }
            return result;
        }

        public EItem UseItem(string slot, bool remove = true)
        {
            Button it = null;
            foreach (Button itemd in ItemDisplays)
            {
                if (itemd.Name == slot)
                {
                    it = itemd;
                    break;
                }
            }
            if (it == null)
                return EItem.Nothing;
            if (Enum.TryParse(GetItemType(it), out EItem item))
            {
                if (remove)
                    PutItemInSlot(it, EItem.Nothing);
                return item;
            }
            return EItem.Nothing;
        }

        public void SetMenuState(EMenuState state)
        {
            Playerlist.Visibility = state == EMenuState.Settings ? Visibility.Hidden : Visibility.Visible;
            switch (state)
            {
                case EMenuState.Startup:
                    MenuSessionJoin.Visibility = Visibility.Hidden;
                    MenuSessionStart.Visibility = Visibility.Hidden;
                    MainMenu.Visibility = Visibility.Visible;
                    Login.Visibility = Visibility.Visible;
                    Gameover.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Hidden;
                    MenuSettings.Visibility = Visibility.Hidden;
                    break;
                case EMenuState.Join:
                    MenuSessionJoin.Visibility = Visibility.Visible;
                    MenuSessionStart.Visibility = Visibility.Hidden;
                    MainMenu.Visibility = Visibility.Hidden;
                    Login.Visibility = Visibility.Visible;
                    Gameover.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Hidden;
                    MenuSettings.Visibility = Visibility.Hidden;
                    break;
                case EMenuState.Host:
                    MenuSessionJoin.Visibility = Visibility.Hidden;
                    MenuSessionStart.Visibility = Visibility.Visible;
                    MainMenu.Visibility = Visibility.Hidden;
                    Login.Visibility = Visibility.Visible;
                    Gameover.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Hidden;
                    MenuSettings.Visibility = Visibility.Hidden;
                    break;
                case EMenuState.Settings:
                    MenuSessionJoin.Visibility = Visibility.Hidden;
                    MenuSessionStart.Visibility = Visibility.Hidden;
                    MainMenu.Visibility = Visibility.Hidden;
                    Login.Visibility = Visibility.Visible;
                    Gameover.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Hidden;
                    MenuSettings.Visibility = Visibility.Visible;
                    break;
                case EMenuState.Gamestart:
                    MenuSessionJoin.Visibility = Visibility.Hidden;
                    MenuSessionStart.Visibility = Visibility.Hidden;
                    MainMenu.Visibility = Visibility.Hidden;
                    Login.Visibility = Visibility.Hidden;
                    Gameover.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Visible;
                    MenuSettings.Visibility = Visibility.Hidden;
                    
                    break;
                case EMenuState.Gameover:
                    MenuSessionJoin.Visibility = Visibility.Hidden;
                    MenuSessionStart.Visibility = Visibility.Hidden;
                    MainMenu.Visibility = Visibility.Hidden;
                    Login.Visibility = Visibility.Hidden;
                    Gameover.Visibility = Visibility.Visible;
                    Game.Visibility = Visibility.Hidden;
                    MenuSettings.Visibility = Visibility.Hidden;
                    break;
            }
        }
    }
}