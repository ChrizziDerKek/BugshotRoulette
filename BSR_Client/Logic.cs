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

        public string GetPlayerName(int player)
        {
            Button p = PlayerDisplays[player];
            if (!(p.Content is Grid))
                return "";
            UIElement text = (p.Content as Grid).Children[1];
            if (text == null)
                return "";
            if (!(text is TextBlock))
                return "";
            return (text as TextBlock).Text;
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