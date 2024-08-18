using System;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace BSR_Client
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            int r1 = RNG.Next();
            int r2 = RNG.Next();
            RNG.Next(Math.Min(r1, r2), Math.Max(r1, r2));
            PlayBackground(0, false);
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
                    if (!IsNameValid(MyName))
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
                    Packet.Create(EPacket.HideBullets).Send(Sync);
                    ResetBullets();
                    PrepareGun();
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
                    EItem item = UseItem(action);
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
                            EItem newitem;
                            do newitem = (EItem)RNG.Next((int)EItem.Nothing + 1, (int)EItem.Count);
                            while (newitem == EItem.Adrenaline);
                            SetItem(newitem, true);
                            Announce("You got: " + newitem.ToString());
                            Packet.Create(EPacket.ReceiveItems).Add(MyName).Add(1).Add(false).Add(newitem.ToString()).Send(Sync);
                            break;
                    }
                    break;
                case "Player1":
                case "Player2":
                case "Player3":
                case "Player4":
                case "Player5":
                    string target = Players[int.Parse(action.Replace("Player", "")) - 1];
                    bool you = target == MyName;
                    if (you)
                        Announce("Shooting yourself");
                    else
                        Announce("Shooting " + target);
                    EBullet bullet = Bullets[0];
                    Bullets.RemoveAt(0);
                    Packet.Create(EPacket.Shoot).Add(MyName).Add(target).Add(you).Add(bullet == EBullet.Blank).Send(Sync);
                    bool again = CanShootAgain;
                    bool wasAbleToShootAgain = CanShootAgain;
                    if (CanShootAgain)
                        CanShootAgain = false;
                    PlaySfx(bullet == EBullet.Live);
                    if (bullet == EBullet.Live)
                    {
                        UpdateLives(target, NextShotSawed ? 2 : 1, false, true, true);
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
                    }
                    else
                    {
                        SetActive(true);
                        Packet.Create(EPacket.SetPlayer).Add(MyName).Send(Sync);
                        Announce("Your turn");
                        UnlockItems();
                    }
                    NextShotSawed = false;
                    break;
            }
        }

        public void Receive(string message)
        {
            Packet data = new Packet(message);           
            PacketHandled = false;
            Dispatcher.Invoke(() =>
            {
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
                            }
                            else
                            {
                                yourturn = true;
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
                        }
                        break;
                    case EPacket.Shoot:
                        string sender = data.ReadStr(0);
                        string target = data.ReadStr(1);
                        bool self = data.ReadBool(2);
                        if (self)
                            target = "themselves";
                        else if (target == MyName)
                            target = "you";
                        Announce(sender + " shoots " + target);
                        Bullets.RemoveAt(0);
                        bool blank = data.ReadBool(3);
                        PlaySfx(!blank);
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