using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Markup;
using System.Net.Sockets;

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
            GameVersion = version;
            Elements = new ElementLib(this);
            int r1 = RNG.Next();
            int r2 = RNG.Next();
            RNG.Next(Math.Min(r1, r2), Math.Max(r1, r2));
            PlayBackground(0, false);
            Shoot.ToolTip =
                "Shoot with the shotgun\n\n" +
                "Shooting your enemy with a live deals 1 damage to them\n" +
                "Shooting yourself with a blank skips the enemy's turns\n" +
                "If you decide to shoot, you can't use items anymore";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string action = (sender as Button).Name;
            switch (action)
            {
                case "Connect":
                    if ((sender as Button).Content.ToString() == "")
                        return;
                    MyName = Username.Text;
                    IP = HostIP.Text;
                    DebugMode = IsDebugName(MyName);
                    if (DebugMode != EDebugMode.None)
                        RenameDebugName();
                    if (!IsNameValid(MyName))
                        return;
                    if (!IsIpValid(IP))
                        return;
                    Sync = new Client(IP, PORT, this);
                    Sync.Start();
                    if (!AddPlayer(MyName))
                        return;
                    Title += " - " + MyName;
                    Packet.Create(EPacket.Connect).Add(MyName).Send(Sync);
                    Start.Content = Players.Count > 1 ? "Start Game" : "";
                    Connect.Content = "";
                    break;
                case "Start":
                    if ((sender as Button).Content.ToString() == "")
                        return;
                    Packet temp = Packet.Create(EPacket.StartGame);
                    foreach (string p in Players)
                    {
                        temp = temp.Add(p);
                        PlayerTurns.Add(p);
                    }
                    PlayersAlive = Players.Count;
                    temp.Send(Sync);
                    Login.Visibility = Visibility.Hidden;
                    Game.Visibility = Visibility.Visible;
                    GenerateBullets();
                    GenerateLives();
                    GenerateItems(true, 0);
                    HideEmptyItemSlots();
                    Packet.Create(EPacket.SetPlayer).Add(Players[0]).Send(Sync);
                    SetActive(true);
                    Announce("Your turn");
                    UnlockItems();
                    PlayBackground(RNG.Next(1, 3), true);
                    HideInactivePlayers();
                    break;
                case "Shoot":
                    UsedShotgun = true;
                    Packet.Create(EPacket.HideBullets).Send(Sync);
                    ResetBullets();
                    PrepareGun();
                    Announce("Select a player to shoot");
                    break;
                case "Item1":
                case "Item2":
                case "Item3":
                case "Item4":
                case "Item5":
                case "Item6":
                case "Item7":
                case "Item8":
                    Packet.Create(EPacket.HideBullets).Send(Sync);
                    ResetBullets();
                    if (AreItemsCloned)
                        UsedTrashBin = false;
                    EItem item = UseItem(action, !UsedTrashBin);
                    if (AreItemsCloned)
                    {
                        AreItemsCloned = false;
                        RestoreItems();
                        int slot = int.Parse(action.Replace("Item", "")) - 1;
                        Announce("You stole " + item.ToString() + " from " + ItemCloneTarget);
                        Packet.Create(EPacket.StealItem).Add(MyName).Add(ItemCloneTarget).Add(slot).Add(item.ToString()).Send(Sync);
                        Shoot.IsEnabled = true;
                    }
                    if (UsedTrashBin)
                    {
                        UsedTrashBin = false;
                        EItem newitem;
                        do newitem = (EItem)RNG.Next((int)EItem.Nothing + 1, (int)EItem.Count);
                        while (newitem == EItem.Trashbin || newitem == item);
                        SetItem(newitem, true);
                        Announce("You trashed " + item.ToString() + " and got: " + newitem.ToString());
                        Packet.Create(EPacket.ItemTrashed).Add(MyName).Add(item.ToString()).Add(newitem.ToString()).Send(Sync);
                        return;
                    }
                    if (BlockItems && CanUseOneItem)
                    {
                        LockItems();
                        BlockItems = false;
                        CanUseOneItem = false;
                    }
                    PlaySfx(item);
                    switch (item)
                    {
                        case EItem.Handcuffs:
                            CanShootAgain = true;
                            break;
                        case EItem.Cigarettes:
                            UpdateLives(MyName, 1, false, false, true);
                            break;
                        case EItem.Saw:
                            NextShotSawed = true;
                            break;
                        case EItem.Magnifying:
                            Announce("Current Bullet: " + Bullets[0].ToString());
                            break;
                        case EItem.Beer:
                            Announce("Racked Bullet: " + Bullets[0].ToString());
                            Bullets.RemoveAt(0);
                            if (Bullets.Count == 0)
                            {
                                GenerateBullets();
                                GenerateItems(true, 0);
                                HideEmptyItemSlots();
                                if (IsPlayerActive())
                                    UnlockItems();
                            }
                            break;
                        case EItem.Inverter:
                            if (Bullets[0] == EBullet.Live)
                            {
                                //Announce("Inverted live Bullet to blank");
                                Bullets[0] = EBullet.Blank;
                            }
                            else
                            {
                                //Announce("Inverted blank Bullet to live");
                                Bullets[0] = EBullet.Live;
                            }
                            break;
                        case EItem.Medicine:
                            bool fail = RNG.Next(0, 2) == 0;
                            UpdateLives(MyName, fail ? 1 : 2, false, fail, true);
                            break;
                        case EItem.Phone:
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
                            int num = RNG.Next(1, InitialBulletCount);
                            Announce(numbers[num] + " Bullet: " + InitialBullets[num].ToString());
                            break;
                        case EItem.Adrenaline:
                            //EItem newitem;
                            //do newitem = (EItem)RNG.Next((int)EItem.Nothing + 1, (int)EItem.Count);
                            //while (newitem == EItem.Adrenaline);
                            //SetItem(newitem, true);
                            //Announce("You got: " + newitem.ToString());
                            //Packet.Create(EPacket.ReceiveItems).Add(MyName).Add(1).Add(false).Add(newitem.ToString()).Send(Sync);
                            UsedAdrenaline = true;
                            PreparePlayerItem();
                            Announce("Select a player to see their items");
                            break;
                        case EItem.Magazine:
                            Bullets.Clear();
                            GenerateBullets();
                            UnlockItems();
                            break;
                        case EItem.Gunpowder:
                            NextShotGunpowdered = true;
                            break;
                        //case EItem.Bullet:
                        //    bool live = RNG.Next(0, 2) == 0;
                        //    Bullets.Add(live ? EBullet.Live : EBullet.Blank);
                        //    Packet.Create(EPacket.ExtraBullet).Add(live).Send(Sync);
                        //    break;
                        case EItem.Trashbin:
                            if (GetItemCount() > 0)
                                UsedTrashBin = true;
                            break;
                        case EItem.Heroine:
                            UsedHeroine = true;
                            PreparePlayerItem();
                            Announce("Select a player to give them Heroine");
                            break;
                        case EItem.Katana:
                            UsedKatana = true;
                            PreparePlayerItem();
                            Announce("Select a player to use the Katana on");
                            break;
                    }
                    break;
                case "Player1":
                case "Player2":
                case "Player3":
                case "Player4":
                case "Player5":
                    if (UsedAdrenaline)
                    {
                        UsedAdrenaline = false;
                        SaveItems();
                        string target = Players[int.Parse(action.Replace("Player", "")) - 1];
                        Packet.Create(EPacket.RequestItems).Add(target).Add(MyName).Send(Sync);
                        BlockPlayers();
                    }
                    if (UsedHeroine || UsedKatana)
                    {
                        string target = Players[int.Parse(action.Replace("Player", "")) - 1];
                        Packet.Create(EPacket.BlockItemUsage).Add(target).Add(UsedKatana).Send(Sync);
                        if (UsedHeroine)
                            Announce("Gave Heroine to " + target);
                        else
                            Announce("Used Katana on " + target);
                        UsedHeroine = false;
                        UsedKatana = false;
                        Shoot.IsEnabled = true;
                        BlockPlayers();
                    }
                    if (UsedShotgun)
                    {
                        UsedShotgun = false;
                        string target = Players[int.Parse(action.Replace("Player", "")) - 1];
                        bool you = target == MyName;
                        if (you)
                            Announce("Shooting yourself");
                        else
                            Announce("Shooting " + target);
                        EBullet bullet = Bullets[0];
                        Bullets.RemoveAt(0);
                        Packet.Create(EPacket.Shoot).Add(MyName).Add(target).Add(you).Add(bullet == EBullet.Blank).Add(NextShotGunpowdered).Send(Sync);
                        bool again = CanShootAgain;
                        bool wasAbleToShootAgain = CanShootAgain;
                        if (CanShootAgain)
                            CanShootAgain = false;
                        PlaySfx(bullet == EBullet.Live, NextShotGunpowdered);
                        if (bullet == EBullet.Live)
                        {
                            int damage = 1;
                            if (NextShotSawed)
                                damage++;
                            if (NextShotGunpowdered)
                            {
                                damage += 2;
                                if (RNG.Next(0, 2) == 0)
                                {
                                    damage--;
                                    target = MyName;
                                    you = true;
                                }
                            }
                            UpdateLives(target, damage, false, true, true);
                            Announce("*Boom* the bullet was a live");
                            if (you)
                            {
                                SetAngry(MyName);
                                Packet.Create(EPacket.BecomeAngry).Add(MyName).Send(Sync);
                            }
                        }
                        else if (you)
                        {
                            Announce("*Click* the bullet was a blank");
                            again = true;
                            if (wasAbleToShootAgain)
                                CanShootAgain = true;
                        }
                        else Announce("*Click* the bullet was a blank");
                        if (Bullets.Count == 0)
                        {
                            GenerateBullets();
                            GenerateItems(true, 0);
                            HideEmptyItemSlots();
                            if (IsPlayerActive())
                                UnlockItems();
                        }
                        if (!again)
                        {
                            SetActive(false);
                            Packet.Create(EPacket.SetPlayer).Add(FindNextPlayer()).Send(Sync);
                            Announce(FindNextPlayer() + "'s turn");
                        }
                        else
                        {
                            SetActive(true);
                            Packet.Create(EPacket.SetPlayer).Add(MyName).Send(Sync);
                            Announce("Your turn");
                            UnlockItems();
                        }
                        NextShotSawed = false;
                        NextShotGunpowdered = false;
                    }
                    break;
            }
        }

        public void Receive(string message)
        {
            Packet data = new Packet(message);           
            PacketHandled = false;
            Dispatcher.Invoke(() =>
            {
                if (data.GetVersion() != GetGameVersion())
                {
                    MessageBox.Show("Version Mismatch", "Error");
                    Environment.Exit(0);
                }
                switch (data.GetId())
                {
                    case EPacket.Connect:
                        AddPlayer(data.ReadStr(0));
                        Packet.Create(EPacket.ConnectAck).Add(MyName).Send(Sync);
                        Start.Content = Players.Count > 1 ? "Start Game" : "";
                        break;
                    case EPacket.ConnectAck:
                        AddPlayer(data.ReadStr(0));
                        Start.Content = Players.Count > 1 ? "Start Game" : "";
                        break;
                    case EPacket.StartGame:
                        for (int i = 0; i < Players.Count; i++)
                            PlayerTurns.Add(data.ReadStr(i));
                        PlayersAlive = Players.Count;
                        Login.Visibility = Visibility.Hidden;
                        Game.Visibility = Visibility.Visible;
                        HideInactivePlayers();
                        break;
                    case EPacket.SetPlayer:
                        string player = data.ReadStr(0);
                        bool yourturn = false;
                        if (player == MyName)
                        {
                            if (GetHealth() == 0)
                            {
                                SetActive(false);
                                Packet.Create(EPacket.SetPlayer).Add(FindNextPlayer()).Send(Sync);
                                Announce(FindNextPlayer() + "'s turn");
                            }
                            else
                            {
                                yourturn = true;
                                LockedItems = false;
                                SetActive(true);
                            }
                        }
                        else SetActive(false);
                        if (!yourturn)
                            Announce(player + "'s turn");
                        else
                        {
                            Announce("Your turn");
                            UnlockItems();
                            if (BlockItems)
                            {
                                if (CanUseOneItem)
                                    Announce("You can only use 1 item this round");
                                else
                                    Announce("You can't use items this round");
                                if (!CanUseOneItem)
                                {
                                    LockItems();
                                    BlockItems = false;
                                }
                            }
                        }
                        break;
                    case EPacket.Shoot:
                        string sender = data.ReadStr(0);
                        string target = data.ReadStr(1);
                        bool self = data.ReadBool(2);
                        bool gunpowdered = data.ReadBool(4);
                        if (self)
                            target = "themselves";
                        else if (target == MyName)
                            target = "you";
                        Announce(sender + " shoots " + target);
                        Bullets.RemoveAt(0);
                        bool blank = data.ReadBool(3);
                        PlaySfx(!blank, gunpowdered);
                        if (!blank)
                        {
                            Announce("*Boom* the bullet was a live");
                            if (target == "you")
                            {
                                SetAngry(MyName);
                                Packet.Create(EPacket.BecomeAngry).Add(MyName).Send(Sync);
                            }
                        }
                        else Announce("*Click* the bullet was a blank");
                        break;
                    case EPacket.SetBullets:
                        Bullets.Clear();
                        for (int i = 0; i < data.ReadInt(0); i++)
                            Bullets.Add(data.ReadBool(i + 2) ? EBullet.Blank : EBullet.Live);
                        if (data.ReadBool(1))
                            ShowBullets(Bullets.ToArray());
                        Bullets.CopyTo(InitialBullets);
                        InitialBulletCount = Bullets.Count;
                        break;
                    case EPacket.HideBullets:
                        ResetBullets();
                        break;
                    case EPacket.UpdateLives:
                        UpdateLives(data.ReadStr(0), data.ReadInt(1), data.ReadBool(2), data.ReadBool(3), false);
                        break;
                    case EPacket.ReceiveItems:
                        if (data.ReadBool(2))
                            GenerateItems(false, data.ReadInt(1));
                        HideEmptyItemSlots();
                        Announce(data.ReadStr(0) + " got: " + data.ReadStr(3).Replace("#", ", "));
                        break;
                    case EPacket.Disconnect:
                        if (Players.Count == 2)
                            Environment.Exit(0);
                        RemovePlayer(data.ReadStr(0));
                        break;
                    case EPacket.UseItem:
                        Announce(data.ReadStr(0) + " used " + data.ReadStr(1));
                        if (Enum.TryParse(data.ReadStr(1), out EItem item))
                        {
                            PlaySfx(item);
                            switch (item)
                            {
                                case EItem.Beer:
                                    Announce("Racked Bullet: " + Bullets[0].ToString());
                                    Bullets.RemoveAt(0);
                                    break;
                                case EItem.Inverter:
                                    Bullets[0] = Bullets[0] == EBullet.Live ? EBullet.Blank : EBullet.Live;
                                    break;
                                case EItem.Magazine:
                                    Bullets.Clear();
                                    break;
                            }
                        }
                        break;
                    case EPacket.BecomeAngry:
                        SetAngry(data.ReadStr(0));
                        break;
                    case EPacket.ChangeMusic:
                        PlayBackground(data.ReadInt(0), false);
                        break;
                    case EPacket.Dead:
                        Announce(data.ReadStr(0) + " died");
                        DeadPlayers.Add(data.ReadStr(0));
                        PlayersAlive--;
                        if (PlayersAlive <= 1)
                            ShowEndscreen();
                        break;
                    case EPacket.ExtraBullet:
                        Bullets.Add(data.ReadBool(0) ? EBullet.Live : EBullet.Blank);
                        break;
                    case EPacket.ItemTrashed:
                        Announce(data.ReadStr(0) + " trashed " + data.ReadStr(1) + " and got: " + data.ReadStr(2));
                        break;
                    case EPacket.RequestItems:
                        if (MyName == data.ReadStr(0))
                        {
                            List<string> itemlist = GetItemTypes();
                            Packet temp = Packet.Create(EPacket.RequestItemsAck).Add(data.ReadStr(1)).Add(MyName).Add(itemlist.Count);
                            foreach (string itemtype in itemlist)
                                temp = temp.Add(itemtype);
                            temp.Send(Sync);
                        }
                        break;
                    case EPacket.RequestItemsAck:
                        if (MyName == data.ReadStr(0))
                        {
                            bool hasAnyItems = false;
                            for (int i = 0; i < data.ReadInt(2); i++)
                            {
                                if (Enum.TryParse(data.ReadStr(3 + i), out EItem it))
                                {
                                    ForceSetItem(i, it);
                                    if (it == EItem.Adrenaline)
                                        Elements.Items[i].IsEnabled = false;
                                    if (it != EItem.Nothing && it != EItem.Adrenaline)
                                        hasAnyItems = true;
                                }
                            }
                            ItemCloneTarget = data.ReadStr(1);
                            AreItemsCloned = true;
                            Announce("Looking at " + ItemCloneTarget + "'s items");
                            if (!hasAnyItems)
                            {
                                AreItemsCloned = false;
                                RestoreItems();
                                Shoot.IsEnabled = true;
                                Announce("No items to steal");
                            }
                        }
                        break;
                    case EPacket.StealItem:
                        string stealtarget = data.ReadStr(1);
                        if (MyName == data.ReadStr(1))
                        {
                            Button stoleitem = Elements.Items[data.ReadInt(2)];
                            SetItemData(stoleitem, EItem.Nothing);
                            stoleitem.IsEnabled = false;
                            stealtarget = "you";
                        }
                        Announce(data.ReadStr(0) + " stole " + data.ReadStr(3) + " from " + stealtarget);
                        break;
                    case EPacket.BlockItemUsage:
                        if (MyName == data.ReadStr(0))
                        {
                            BlockItems = true;
                            CanUseOneItem = data.ReadBool(1);
                            if (CanUseOneItem)
                                Announce("You can only use 1 item next round");
                            else
                                Announce("You can't use items next round");
                        }
                        break;
                }
                PacketHandled = true;
            });
            while (!PacketHandled)
                Task.Delay(1).Wait();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Sync == null)
                return;
            if (IsPlayerActive())
            {
                SetActive(false);
                Packet.Create(EPacket.SetPlayer).Add(FindNextPlayer()).Send(Sync);
                Announce(FindNextPlayer() + "'s turn");
            }
            Packet.Create(EPacket.Disconnect).Add(MyName).Send(Sync);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Log.SelectedIndex != -1)
                Log.SelectedIndex = -1;
        }
    }
}