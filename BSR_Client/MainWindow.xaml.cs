using System;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Threading.Tasks;

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
            Sound = new SoundLib(this);
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
                                    Username.IsReadOnly = false;
                                    HostUsername.IsReadOnly = false;
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
                                ResetFlag(EFlags.NextItemTrashed);
                                Console.WriteLine(packet.ToString());
                                List<EBullet> bullets = packet.GetBullets();
                                bool noitems = packet.NoItemsGenerated();
                                Announce(string.Format("{0} lives, {1} blanks", GetBulletCount(bullets, EBullet.Live), GetBulletCount(bullets, EBullet.Blank)));
                                ShowBullets(bullets.ToArray());
                                if (!noitems)
                                {
                                    OverrideItems(packet.GetItems());
                                    foreach (string player in Players)
                                    {
                                        List<EItem> items = packet.GetGeneratedItems(player);
                                        string playername = player;
                                        if (playername == You)
                                            playername = "You";
                                        string itemlist = "";
                                        if (items != null)
                                        {
                                            foreach (EItem item in items)
                                                itemlist += ", " + item.ToString();
                                            itemlist = itemlist.Substring(2);
                                        }
                                        else itemlist = "no items";
                                        Announce(string.Format("{0} got {1}", playername, itemlist));
                                    }
                                }
                                bool initial = packet.ShouldUpdateLives();
                                if (initial)
                                {
                                    Sound.PlayMusic(packet.ShouldPlayIntenseTheme() ? EMusic.BackgroundIntense : EMusic.Background);
                                    SetMaxHealth(packet.GetLives());
                                    ResetPlayerSlots();
                                    RemoveInactivePlayerSlots();
                                }
                            }
                            break;
                        case EPacket.PassControl:
                            {
                                PacketPassControl packet = new PacketPassControl(data);
                                Console.WriteLine(packet.ToString());
                                if (packet.GetTarget() != You)
                                {
                                    ResetFlag(EFlags.HandcuffUsageBlocked);
                                    Announce(packet.GetTarget() + "'s turn");
                                    return;
                                }
                                SetActive(true);
                                Announce("Your turn");
                            }
                            break;
                        case EPacket.Shoot:
                            {
                                PacketShoot packet = new PacketShoot(data);
                                Console.WriteLine(packet.ToString());
                                string shooter = packet.GetSender();
                                string target = packet.GetTarget();
                                ERoundFlags flags = packet.GetFlags();
                                string targetstr = target;
                                if (shooter == You && target == shooter)
                                    targetstr = "yourself";
                                if (shooter != You && target == You)
                                    targetstr = "you";
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
                                bool inverted = packet.HasFlag(ERoundFlags.ShotInverted);
                                if (type == EBullet.Blank)
                                    Announce("The bullet was a blank");
                                else
                                    Announce("The bullet was a live");
                                HideBullet(type, inverted);
                                int damage = 0;
                                if (type == EBullet.Live)
                                {
                                    damage = 1;
                                    if (packet.HasFlag(ERoundFlags.ShotgunSawedOff))
                                        damage++;
                                    if (packet.HasFlag(ERoundFlags.ShotGunpowdered))
                                        damage += 2;
                                    if (packet.HasFlag(ERoundFlags.GunpowderBackfired))
                                        damage--;
                                }
                                Sound.PlayShotSfx(type, flags);
                                UpdateHealth(GetHealth(target) - damage, target);
                                if (IsFlagSet(EFlags.Shooting))
                                    ResetFlag(EFlags.Shooting);
                            }
                            break;
                        case EPacket.UsedItem:
                            {
                                PacketUsedItem packet = new PacketUsedItem(data);
                                string user = packet.GetSender();
                                EItem item = packet.GetItem();
                                bool trashed = packet.WasTrashed();
                                if (!trashed)
                                    Sound.PlayItemSfx(item);
                                string userstr = user;
                                if (userstr == You)
                                    userstr = "You";
                                bool shouldapply = userstr == "You";
                                bool shouldtarget = false;
                                string targetstr = "";
                                if (packet.DoesHaveTarget())
                                {
                                    string target = packet.GetTarget();
                                    if (target == You)
                                        targetstr = "you";
                                    else
                                        targetstr = target;
                                    shouldtarget = target == You;
                                }
                                bool stolen = packet.IsStolen();
                                bool stealingfromyou = false;
                                if (stolen)
                                {
                                    string stealstr = packet.GetStealingTarget();
                                    if (stealstr == You)
                                    {
                                        stealstr = "you";
                                        stealingfromyou = true;
                                    }
                                    Announce(string.Format("{0} stole {1} from {2}", userstr, item, stealstr));
                                }
                                if (packet.DoesHaveTarget())
                                    Announce(string.Format("{0} used {1} on {2}", userstr, item, targetstr));
                                else if (!trashed)
                                    Announce(string.Format("{0} used: {1}", userstr, item));
                                else
                                    Announce(string.Format("{0} trashed {1} and got: {2}", userstr, item, packet.GetReplacementItem()));
                                if (stealingfromyou)
                                {
                                    foreach (Button slot in ItemDisplays)
                                    {
                                        if (UseItem(slot.Name, false) == item)
                                        {
                                            UseItem(slot.Name);
                                            break;
                                        }
                                    }
                                }
                                if (trashed)
                                {
                                    if (shouldapply)
                                        PushItem(packet.GetReplacementItem());
                                    return;
                                }
                                switch (item)
                                {
                                    case EItem.Handcuffs:
                                        {
                                            if (!shouldapply)
                                                return;
                                            SetFlag(EFlags.HandcuffUsageBlocked);
                                        }
                                        break;
                                    case EItem.Magnifying:
                                        {
                                            if (!shouldapply)
                                                return;
                                            Announce(string.Format("Current Bullet: {0}", packet.GetBullet().ToString()));
                                        }
                                        break;
                                    case EItem.Beer:
                                        {
                                            Announce(string.Format("Racked Bullet: {0}", packet.GetBullet().ToString()));
                                            HideBullet(packet.GetBullet(), packet.IsInverted());
                                        }
                                        break;
                                    case EItem.Medicine:
                                    case EItem.Cigarettes:
                                        UpdateHealth(GetHealth(user) + packet.GetHealAmount(), user);
                                        break;
                                    case EItem.Phone:
                                        {
                                            if (!shouldapply)
                                                return;
                                            string[] numbers = new string[]
                                            {
                                                "First",
                                                "Second",
                                                "Third",
                                                "Fourth",
                                                "Fifth",
                                                "Sixth",
                                                "Seventh",
                                                "Eighth"
                                            };
                                            int index = packet.GetBulletIndex();
                                            EBullet bullet = packet.GetBullet();
                                            if (index <= 0 || index >= numbers.Length || bullet == EBullet.Undefined)
                                            {
                                                Announce("Nothing to see");
                                                return;
                                            }
                                            Announce(string.Format("{0} Bullet: {1}", numbers[index], bullet.ToString()));
                                        }
                                        break;
                                    case EItem.Adrenaline:
                                        {
                                            if (!IsFlagSet(EFlags.UsingAdrenaline))
                                                return;
                                            StoreItems();
                                            EItem[] items = packet.GetItems();
                                            OverrideItems(items);
                                        }
                                        break;
                                    case EItem.Trashbin:
                                        {
                                            if (!shouldapply)
                                                return;
                                            if (GetItemCount() > 0)
                                                SetFlag(EFlags.NextItemTrashed);
                                        }
                                        break;
                                    case EItem.Heroine:
                                        {
                                            if (!shouldtarget)
                                                return;
                                            Announce("You can't use any Items next Round");
                                            SetFlag(EFlags.ItemUsageBlockedCompletely);
                                        }
                                        break;
                                    case EItem.Katana:
                                        break;
                                    case EItem.Swapper:
                                        {
                                            EItem[] swapped = null;
                                            if (shouldtarget)
                                                swapped = packet.GetOtherItems();
                                            else if (shouldapply)
                                                swapped = packet.GetItems();
                                            if (swapped != null)
                                                OverrideItems(swapped);
                                        }
                                        break;
                                    case EItem.Hat:
                                        HideBullets();
                                        break;
                                }
                            }
                            break;
                        case EPacket.EndGame:
                            {
                                PacketEndGame packet = new PacketEndGame(data);
                                SetMenuState(EMenuState.Gameover);
                                Sound.PlayMusic(EMusic.Gameover);
                                Winner.Text = packet.GetWinner() + " won!";
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
                Task.Delay(1).Wait();
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
                        if (UseItem(action, false) == EItem.Handcuffs && IsFlagSet(EFlags.HandcuffUsageBlocked))
                            return;
                        EItem item = UseItem(action);
                        if (IsFlagSet(EFlags.UsingAdrenaline))
                        {
                            ResetFlag(EFlags.UsingAdrenaline);
                            RestoreItems();
                        }
                        switch (item)
                        {
                            case EItem.Adrenaline:
                                SetFlag(EFlags.UsingAdrenaline | EFlags.UsingPlayerItem);
                                break;
                            case EItem.Heroine:
                                SetFlag(EFlags.UsingHeroine | EFlags.UsingPlayerItem);
                                break;
                            case EItem.Katana:
                                SetFlag(EFlags.UsingKatana | EFlags.UsingPlayerItem);
                                break;
                            case EItem.Swapper:
                                SetFlag(EFlags.UsingSwapper | EFlags.UsingPlayerItem);
                                break;
                        }
                        if (IsFlagSet(EFlags.NextItemTrashed))
                        {
                            ResetFlag(EFlags.NextItemTrashed);
                            Packet.Send(new PacketUseItem(You, item), Sync);
                            return;
                        }
                        if (IsFlagSet(EFlags.UsingPlayerItem))
                            SetPlayersInteractable(true);
                        else
                            Packet.Send(new PacketUseItem(You, item), Sync);
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
                            SetPlayersInteractable(false);
                            string target = GetPlayerFromSlot(action);
                            Packet.Send(new PacketShoot(You, target), Sync);
                        }
                        else if (IsFlagSet(EFlags.UsingPlayerItem))
                        {
                            string target = GetPlayerFromSlot(action);
                            EItem item = EItem.Nothing;
                            if (IsFlagSet(EFlags.UsingAdrenaline))
                                item = EItem.Adrenaline;
                            else if (IsFlagSet(EFlags.UsingHeroine))
                                item = EItem.Heroine;
                            else if (IsFlagSet(EFlags.UsingKatana))
                                item = EItem.Katana;
                            else if (IsFlagSet(EFlags.UsingSwapper))
                                item = EItem.Swapper;
                            Packet.Send(new PacketUseItem(You, item, target), Sync);
                            ResetFlag(EFlags.UsingPlayerItem);
                            ResetFlag(EFlags.UsingHeroine);
                            ResetFlag(EFlags.UsingKatana);
                            ResetFlag(EFlags.UsingSwapper);
                            SetPlayersInteractable(false);
                            SetActive(true);
                            if (item == EItem.Adrenaline)
                                SetEverythingInteractable();
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