using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Windows.Shapes;
using System.Windows.Input;

namespace BSR_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            version = version.Substring(0, version.Length - 2);
            Title += " " + version;
            BulletDisplays = new Rectangle[] { Bullet1, Bullet2, Bullet3, Bullet4, Bullet5, Bullet6, Bullet7, Bullet8 };
            HealthBars = new ProgressBar[] { Health1, Health2, Health3, Health4, Health5 };
            ItemDisplays = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            PlayerDisplays = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            SetMenuState(EMenuState.Startup);
            PopulateSettings();
        }

        private void Client_OnPacketReceived(ClientWorker sender, EPacket id, List<byte> data)
        {
            Dispatcher.Invoke(() =>
            {
                switch (id)
                {
                    case EPacket.StartGame:
                        {
                            SetMenuState(EMenuState.Gamestart);
                            GameStarted = true;
                        }
                        break;
                    case EPacket.JoinResponse:
                        {
                            PacketJoinResponse packet = new PacketJoinResponse(data);
                            if (packet.DidSucceed())
                            {
                                Host = packet.GetHost();
                                if (packet.GetSession() != Session)
                                {
                                    Fatal("Session id mismatch");
                                    return;
                                }
                                Players = packet.GetPlayers();
                                UpdatePlayerlist();
                            }
                            else
                            {
                                Fatal("Failed to join session\nreason: " + packet.GetResponse().ToString(), false);
                                SessionHost.Content = "Host Session";
                                Connect.Content = "Connect";
                                GameSettings.Content = "";
                                return;
                            }
                        }
                        break;
                    case EPacket.NewPlayer:
                        {
                            PacketNewPlayer packet = new PacketNewPlayer(data);
                            Players.Add(packet.GetPlayer());
                            UpdatePlayerlist();
                        }
                        break;
                    case EPacket.RemoveLocalPlayer:
                        {
                            PacketRemoveLocalPlayer packet = new PacketRemoveLocalPlayer(data);
                            string player = packet.GetPlayer();
                            string host = packet.GetNewHost();
                            bool didMigrate = false;
                            if (You == player)
                                return;
                            Players.Remove(player);
                            if (!string.IsNullOrEmpty(host))
                            {
                                Host = host;
                                didMigrate = true;
                            }
                            UpdatePlayerlist();
                            if (IsHost() && didMigrate)
                                SwitchToHostMenu();
                        }
                        break;
                }
            });
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string action = btn.Name;
            if (btn.Content.ToString() == "")
                return;
            switch (action)
            {
                case "SessionJoin":
                    if (!IsValidIP(IP.Text))
                        return;
                    SetMenuState(EMenuState.Join);
                    break;
                case "SessionStart":
                    if (!IsValidIP(IP.Text))
                        return;
                    SetMenuState(EMenuState.Host);
                    Lobby.Text = Guid.NewGuid().ToString();
                    break;
                case "GameSettings":
                    SetMenuState(EMenuState.Settings);
                    break;
                case "CopySession":
                    Clipboard.SetText(Lobby.Text);
                    break;
                case "SessionHost":
                    AttemptConnect(true, IP.Text);
                    Username.IsReadOnly = true;
                    HostUsername.IsReadOnly = true;
                    break;
                case "Connect":
                    AttemptConnect(false, IP.Text);
                    Username.IsReadOnly = true;
                    HostUsername.IsReadOnly = true;
                    break;
                case "RestartGame":

                    break;
                case "StartGame":
                    if (!IsHost())
                        return;
                    Packet.Send(new PacketStartGame(), Sync);
                    SetMenuState(EMenuState.Gamestart);
                    GameStarted = true;
                    break;
                case "Shoot":

                    break;
                case "Item1":
                case "Item2":
                case "Item3":
                case "Item4":
                case "Item5":
                case "Item6":
                case "Item7":
                case "Item8":

                    break;
                case "Player1":
                case "Player2":
                case "Player3":
                case "Player4":
                case "Player5":

                    break;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (MenuSettings.Visibility == Visibility.Visible)
            {
                SetMenuState(EMenuState.Host);
                e.Cancel = true;
                SyncSettings();
                return;
            }
            if (Sync == null)
                return;
            Packet.Send(new PacketDisconnected(), Sync);
            Sync.Dispose();
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Log.SelectedIndex != -1)
                Log.SelectedIndex = -1;
        }

        private void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                return;
            Clipboard.SetText(Lobby.Text);
        }
    }
}