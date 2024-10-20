﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Sockets;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Threading;
using System.Runtime.InteropServices;

namespace BSR_Client
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll")] static extern bool AllocConsole();

        public MainWindow()
        {
            AllocConsole();
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
            PacketHandled = false;
            Dispatcher.Invoke(() =>
            {
                try
                {
                    switch (id)
                    {
                        case EPacket.StartGame:
                            {
                                PacketStartGame packet = new PacketStartGame(data);
                                Console.WriteLine(packet.ToString());
                                SetMenuState(EMenuState.Gamestart);
                                GameStarted = true;
                            }
                            break;
                        case EPacket.JoinResponse:
                            {
                                PacketJoinResponse packet = new PacketJoinResponse(data);
                                Console.WriteLine(packet.ToString());
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
                                Console.WriteLine(packet.ToString());
                                Players.Add(packet.GetPlayer());
                                UpdatePlayerlist();
                            }
                            break;
                        case EPacket.RemoveLocalPlayer:
                            {
                                PacketRemoveLocalPlayer packet = new PacketRemoveLocalPlayer(data);
                                Console.WriteLine(packet.ToString());
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
                        case EPacket.StartRound:
                            {
                                PacketStartRound packet = new PacketStartRound(data);
                                Console.WriteLine(packet.ToString());
                                List<EBullet> bullets = packet.GetBullets();
                                Announce(string.Format("{0} lives, {1} blanks", GetBulletCount(bullets, EBullet.Live), GetBulletCount(bullets, EBullet.Blank)));
                                ShowBullets(bullets.ToArray());
                                OverrideItems(packet.GetItems());
                                bool initial = packet.ShouldUpdateLives();
                                if (initial)
                                {
                                    SetMaxHealth(packet.GetLives());
                                    ResetPlayerSlots();
                                }
                            }
                            break;
                        case EPacket.PassControl:
                            {
                                PacketPassControl packet = new PacketPassControl(data);
                                Console.WriteLine(packet.ToString());
                                if (packet.GetTarget() != You)
                                {
                                    Announce(packet.GetTarget() + "'s turn");
                                    return;
                                }
                                SetActive(true);
                            }
                            break;
                        case EPacket.Shoot:
                            {
                                PacketShoot packet = new PacketShoot(data);
                                Console.WriteLine(packet.ToString());
                                string shooter = packet.GetSender();
                                string target = packet.GetTarget();
                                EShotFlags flags = packet.GetFlags();
                                string targetstr = target;
                                if (shooter == You && target == shooter)
                                    targetstr = "yourself";
                                if (shooter != You && target == You)
                                    target = "you";
                                if (shooter != You && target == shooter)
                                    targetstr = "themselves";
                                string shooterstr = shooter;
                                string plural = "s";
                                if (shooter == You)
                                {
                                    shooterstr = "You";
                                    plural = "";
                                }
                                Announce(string.Format("{0} shoot{1} {2}", shooterstr, plural, targetstr));
                                EBullet type = packet.GetBullet();
                                bool inverted = packet.HasFlag(EShotFlags.Inverted);
                                if (type == EBullet.Blank)
                                    Announce("The bullet was a blank");
                                else
                                    Announce("The bullet was a live");
                                HideBullet(type, inverted);
                                if (IsFlagSet(EFlags.Shooting))
                                {
                                    ResetFlag(EFlags.Shooting);
                                    Packet.Send(new PacketControlRequest(), Sync);
                                }
                                if (packet.HasFlag(EShotFlags.DisplayOnly))
                                    return;
                                int damage = 0;
                                if (type == EBullet.Live)
                                {
                                    damage = 1;
                                    if (packet.HasFlag(EShotFlags.SawedOff))
                                        damage++;
                                    if (packet.HasFlag(EShotFlags.Gunpowdered))
                                        damage += 2;
                                    if (packet.HasFlag(EShotFlags.GunpowderBackfired))
                                        damage--;
                                }
                                UpdateHealth(GetHealth() - damage, true);
                            }
                            break;
                        case EPacket.UpdateHealth:
                            {
                                PacketUpdateHealth packet = new PacketUpdateHealth(data);
                                Console.WriteLine(packet.ToString());
                                if (packet.GetTarget() == You)
                                    return;
                                UpdateHealth(packet.GetValue(), false, packet.GetTarget());
                            }
                            break;
                    }
                }
                finally
                {
                    PacketHandled = true;
                }
            });
            while (!PacketHandled)
                Thread.Sleep(1);
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
                    {
                        if (!IsValidIP(IP.Text))
                            return;
                        SetMenuState(EMenuState.Join);
                    }
                    break;
                case "SessionStart":
                    {
                        if (!IsValidIP(IP.Text))
                            return;
                        SetMenuState(EMenuState.Host);
                        Lobby.Text = Guid.NewGuid().ToString();
                    }
                    break;
                case "GameSettings":
                    {
                        SetMenuState(EMenuState.Settings);
                    }
                    break;
                case "CopySession":
                    {
                        Clipboard.SetText(Lobby.Text);
                    }
                    break;
                case "SessionHost":
                    {
                        AttemptConnect(true, IP.Text);
                        Username.IsReadOnly = true;
                        HostUsername.IsReadOnly = true;
                    }
                    break;
                case "Connect":
                    {
                        AttemptConnect(false, IP.Text);
                        Username.IsReadOnly = true;
                        HostUsername.IsReadOnly = true;
                        UpdateTitle();
                    }
                    break;
                case "RestartGame":
                    {

                    }
                    break;
                case "StartGame":
                    {
                        if (!IsHost())
                            return;
                        Packet.Send(new PacketStartGame(), Sync);
                        SetMenuState(EMenuState.Gamestart);
                        GameStarted = true;
                        UpdateTitle();
                    }
                    break;
                case "Shoot":
                    {
                        SetFlag(EFlags.Shooting);
                        SetPlayersInteractable(true);
                    }
                    break;
                case "Item1":
                case "Item2":
                case "Item3":
                case "Item4":
                case "Item5":
                case "Item6":
                case "Item7":
                case "Item8":
                    {

                    }
                    break;
                case "Player1":
                case "Player2":
                case "Player3":
                case "Player4":
                case "Player5":
                    {
                        if (IsFlagSet(EFlags.Shooting))
                        {
                            string target = GetPlayerFromSlot(action);
                            Packet.Send(new PacketShoot(You, target, EShotFlags.None), Sync);
                        }
                    }
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