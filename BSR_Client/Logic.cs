using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BSR_Client
{
    public partial class MainWindow
    {
        public void ShowBullets(EBullet[] bullets)
        {
            if (bullets.Length > 8)
                return;
            Rectangle[] displays = new Rectangle[] { Bullet1, Bullet2, Bullet3, Bullet4, Bullet5, Bullet6, Bullet7, Bullet8 };
            for (int i = 0; i < bullets.Length; i++)
                displays[i].Fill = bullets[i] == EBullet.Live ? Brushes.Red : Brushes.Gray;
        }

        public void ResetBullets()
        {
            Rectangle[] displays = new Rectangle[] { Bullet1, Bullet2, Bullet3, Bullet4, Bullet5, Bullet6, Bullet7, Bullet8 };
            for (int i = 0; i < displays.Length; i++)
                displays[i].Fill = Brushes.Transparent;
        }

        public void Dead()
        {
            Announce("You died");
            PlayersAlive--;
            DeadPlayers.Add(MyName);
            Packet.Create(EPacket.Dead).Add(MyName).Send(Sync);
        }

        public void ShowEndscreen()
        {
            string winner = "";
            foreach (string player in Players)
            {
                if (!DeadPlayers.Contains(player))
                {
                    winner = player;
                    break;
                }
            }
            PlayBackground(3, false);
            Gameover.Visibility = Visibility.Visible;
            Game.Visibility = Visibility.Hidden;
            Winner.Text = winner + " won!";
        }

        public void GenerateLives()
        {
            int maxhealth = RNG.Next(2, 6);
            foreach (string player in Players)
                UpdateLives(player, maxhealth, true, false, true);
        }

        public void RemovePlayer(string player)
        {
            Players.Remove(player);
            PlayerTurns.Remove(player);
            ProgressBar[] displays = new ProgressBar[] { Health1, Health2, Health3, Health4, Health5 };
            Button[] players = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            int it = 0;
            foreach (Button b in players)
            {
                if (IsPlayerSlot(b, player))
                {
                    displays[it].Value = 0;
                    break;
                }
                it++;
            }
        }

        public void GenerateItems(bool remoteGenerate, int remoteCount)
        {
            int count = RNG.Next(1, 5);
            if (remoteCount > 0)
                count = remoteCount;
            string msg = "";
            Packet temp = Packet.Create(EPacket.ReceiveItems).Add(MyName).Add(count).Add(remoteGenerate);
            for (int i = 0; i < count; i++)
            {
                EItem item = (EItem)RNG.Next((int)EItem.Nothing + 1, (int)EItem.Count);
                if (!SetItem(item))
                    continue;
                msg += "#" + item.ToString();
            }
            if (msg != "")
                msg = msg.Substring(1);
            else
                msg = "No items";
            Announce("You got: " + msg.Replace("#", ", "));
            temp = temp.Add(msg);
            temp.Send(Sync);
        }

        public void UpdateLives(string player, int lives, bool set, bool lose, bool sync)
        {
            if (set && lose)
                return;
            ProgressBar[] displays = new ProgressBar[] { Health1, Health2, Health3, Health4, Health5 };
            Button[] players = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            if (sync)
                Packet.Create(EPacket.UpdateLives).Add(player).Add(lives).Add(set).Add(lose).Send(Sync);
            int it = 0;
            foreach (Button b in players)
            {
                if (IsPlayerSlot(b, player))
                {
                    if (set)
                        displays[it].Value = lives;
                    else if (lose)
                        displays[it].Value -= lives;
                    else
                        displays[it].Value += lives;
                    break;
                }
                it++;
            }
            if (player == MyName && GetHealth() <= 0)
            {
                Dead();
                ShowEndscreen();
            }
        }

        public void GenerateBullets(bool sync = true)
        {
            int count = RNG.Next(2, 9);
            int numblank = 0;
            switch (count)
            {
                case 2:
                    numblank = 1;
                    break;
                case 3:
                    numblank = RNG.Next(1, 3);
                    break;
                case 4:
                    numblank = RNG.Next(1, 4);
                    break;
                case 5:
                    numblank = RNG.Next(2, 4);
                    break;
                case 6:
                    numblank = RNG.Next(2, 4);
                    break;
                case 7:
                    numblank = RNG.Next(3, 5);
                    break;
                case 8:
                    numblank = RNG.Next(3, 6);
                    break;
            }
            for (int i = 0; i < numblank; i++)
                Bullets.Add(EBullet.Blank);
            for (int i = 0; i < count - numblank; i++)
                Bullets.Add(EBullet.Live);
            Shuffle();
            ShowBullets(Bullets.ToArray());
            Packet temp = Packet.Create(EPacket.SetBullets).Add(Bullets.Count).Add(true);
            foreach (EBullet b in Bullets)
                temp = temp.Add(b == EBullet.Blank);
            if (sync) temp.Send(Sync);
            Shuffle();
            temp = Packet.Create(EPacket.SetBullets).Add(Bullets.Count).Add(false);
            foreach (EBullet b in Bullets)
                temp = temp.Add(b == EBullet.Blank);
            if (sync) temp.Send(Sync);
            Bullets.CopyTo(InitialBullets);
            InitialBulletCount = Bullets.Count;
        }

        public void Shuffle()
        {
            int n = Bullets.Count;
            while (n > 1)
            {
                int r = RNG.Next(n--);
                (Bullets[r], Bullets[n]) = (Bullets[n], Bullets[r]);
            }
        }

        public bool AddPlayer(string username)
        {
            if (Players.Contains(username))
                return false;
            Players.Add(username);
            Button[] displays = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            for (int i = 0; i < displays.Length; i++)
            {
                if (IsPlayerSlot(displays[i], "None"))
                {
                    SetPlayerData(displays[i], username, true);
                    break;
                }
            }
            return true;
        }

        public void Announce(string str)
        {
            Log.Items.Insert(0, str);
        }

        public void DebugInfo(string str)
        {
            Packet.Create(EPacket.DebugInfo).Add(str).Send(Sync);
        }

        public string FindNextPlayer()
        {
            int yourId = 0;
            for (int i = 0; i < PlayerTurns.Count; i++)
            {
                if (PlayerTurns[i] == MyName)
                {
                    yourId = i;
                    break;
                }
            }
            if (yourId + 1 < PlayerTurns.Count)
                return PlayerTurns[yourId + 1];
            return PlayerTurns[0];
        }

        public bool IsPlayerActive()
        {
            return Shoot.IsEnabled;
        }

        public void SetActive(bool active)
        {
            Shoot.IsEnabled = active;
            Button[] items = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            foreach (Button i in items)
                i.IsEnabled = false;
            items = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            foreach (Button i in items)
                i.IsEnabled = false;
        }

        public void HideInactivePlayers()
        {
            Button[] items = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            foreach (Button i in items)
                if (IsPlayerSlot(i, "None"))
                    i.Visibility = Visibility.Hidden;
        }

        public void PrepareGun()
        {
            Shoot.IsEnabled = false;
            Button[] items = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            foreach (Button i in items)
                i.IsEnabled = false;
            items = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            ProgressBar[] displays = new ProgressBar[] { Health1, Health2, Health3, Health4, Health5 };
            int it = 0;
            foreach (Button i in items)
            {
                if (!IsPlayerSlot(i, "None") && displays[it].Value > 0)
                    i.IsEnabled = true;
                it++;
            }
        }

        public bool IsPlayerSlot(Button slot, string name)
        {
            if (slot == null)
                return false;
            if (!(slot.Content is Grid))
                return false;
            UIElement text = (slot.Content as Grid).Children[1];
            if (text == null)
                return false;
            if (!(text is TextBlock))
                return false;
            return (text as TextBlock).Text == name;
        }

        public bool IsItemSlot(Button slot, string type)
        {
            return IsPlayerSlot(slot, type);
        }

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

        public void SetAngry(string player)
        {
            Button[] items = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            foreach (Button i in items)
            {
                if (IsPlayerSlot(i, player))
                {
                    SetPlayerData(i, "", false);
                    break;
                }
            }
        }

        public void SetPlayerData(Button slot, string name, bool? dealer)
        {
            if (slot == null)
                return;
            if (!(slot.Content is Grid))
                return;
            UIElement text = (slot.Content as Grid).Children[1];
            if (!(text is TextBlock))
                return;
            if (!string.IsNullOrEmpty(name))
                (text as TextBlock).Text = name;
            UIElement image = (slot.Content as Grid).Children[0];
            if (!(image is Image))
                return;
            if (dealer == null)
                (image as Image).Source = null;
            else
                (image as Image).Source = new BitmapImage(new Uri(dealer == true ? "textures/dealer1.png" : "textures/dealer2.png", UriKind.Relative));
        }

        public void SetItemData(Button slot, EItem type)
        {
            if (slot == null)
                return;
            if (!(slot.Content is Grid))
                return;
            UIElement text = (slot.Content as Grid).Children[1];
            if (!(text is TextBlock))
                return;
            (text as TextBlock).Text = type.ToString();
            UIElement image = (slot.Content as Grid).Children[0];
            if (!(image is Image))
                return;
            string texture = type.ToString().ToLower();
            if (type == EItem.Nothing)
                (image as Image).Source = null;
            else
                (image as Image).Source = new BitmapImage(new Uri("textures/" + texture + ".png", UriKind.Relative));
        }

        public int GetHealth()
        {
            Button[] items = new Button[] { Player1, Player2, Player3, Player4, Player5 };
            ProgressBar[] displays = new ProgressBar[] { Health1, Health2, Health3, Health4, Health5 };
            int it = 0;
            foreach (Button i in items)
            {
                if (IsPlayerSlot(i, MyName))
                    return (int)displays[it].Value;
                it++;
            }
            return 0;
        }

        public bool SetItem(EItem type, bool enable = false)
        {
            Button[] displays = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            for (int i = 0; i < displays.Length; i++)
            {
                if (IsItemSlot(displays[i], "Nothing"))
                {
                    displays[i].Visibility = Visibility.Visible;
                    SetItemData(displays[i], type);
                    if (enable)
                        displays[i].IsEnabled = true;
                    return true;
                }
            }
            return false;
        }

        public void HideEmptyItemSlots()
        {
            Button[] displays = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            for (int i = 0; i < displays.Length; i++)
                if (IsItemSlot(displays[i], "Nothing"))
                    displays[i].Visibility = Visibility.Hidden;
        }

        public EItem UseItem(string slot)
        {
            Button it = null;
            Button[] displays = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            for (int i = 0; i < displays.Length; i++)
            {
                if (displays[i].Name == slot)
                {
                    it = displays[i];
                    break;
                }
            }
            if (it == null)
                return EItem.Nothing;
            if (Enum.TryParse(GetItemType(it), out EItem item))
            {
                Announce("You used " + item.ToString());
                Packet.Create(EPacket.UseItem).Add(MyName).Add(item.ToString()).Send(Sync);
                SetItemData(it, EItem.Nothing);
                it.IsEnabled = false;
                return item;
            }
            return EItem.Nothing;
        }

        public void UnlockItems()
        {
            Button[] displays = new Button[] { Item1, Item2, Item3, Item4, Item5, Item6, Item7, Item8 };
            for (int i = 0; i < displays.Length; i++)
            {
                if (!IsItemSlot(displays[i], "Nothing"))
                    displays[i].IsEnabled = true;
            }
        }

        public bool ProcessIsRunning()
        {
            Process current = Process.GetCurrentProcess();
            Process[] processes = Process.GetProcessesByName(current.ProcessName);
            foreach (Process process in processes)
                if (process.Id != current.Id)
                    if (Assembly.GetExecutingAssembly().Location.Replace("/", "\\") == current.MainModule.FileName)
                        return true;
            return false;
        }

        public void PlayBackground(int id, bool sync)
        {
            if (sync)
                Packet.Create(EPacket.ChangeMusic).Add(id).Send(Sync);
            if (ProcessIsRunning() && !WasPlayingSounds)
                return;
            WasPlayingSounds = true;
            Sound.Title.MediaEnded -= Sound.Media_Ended;
            Sound.Background1.MediaEnded -= Sound.Media_Ended;
            Sound.Background2.MediaEnded -= Sound.Media_Ended;
            Sound.End.MediaEnded -= Sound.Media_Ended;
            Sound.Title.Stop();
            Sound.Background1.Stop();
            Sound.Background2.Stop();
            Sound.End.Stop();
            Sound.Title.MediaEnded += Sound.Media_Ended;
            Sound.Background1.MediaEnded += Sound.Media_Ended;
            Sound.Background2.MediaEnded += Sound.Media_Ended;
            Sound.End.MediaEnded += Sound.Media_Ended;
            switch (id)
            {
                case 0:
                    Sound.Title.Play();
                    break;
                case 1:
                    Sound.Background1.Play();
                    break;
                case 2:
                    Sound.Background2.Play();
                    break;
                case 3:
                    Sound.End.Play();
                    break;
            }
        }

        public void PlaySfx(bool live)
        {
            if (live)
            {
                Sound.Shot.Position = TimeSpan.Zero;
                Sound.Shot.Play();
            }
            else
            {
                Sound.Empty.Position = TimeSpan.Zero;
                Sound.Empty.Play();
            }
        }

        public void PlaySfx(EItem item)
        {
            switch (item)
            {
                case EItem.Handcuffs:
                    Sound.Handcuff.Position = TimeSpan.Zero;
                    Sound.Handcuff.Play();
                    break;
                case EItem.Cigarettes:
                    Sound.Cig.Position = TimeSpan.Zero;
                    Sound.Cig.Play();
                    break;
                case EItem.Saw:
                    Sound.Saw.Position = TimeSpan.Zero;
                    Sound.Saw.Play();
                    break;
                case EItem.Magnifying:
                    Sound.Magnify.Position = TimeSpan.Zero;
                    Sound.Magnify.Play();
                    break;
                case EItem.Beer:
                    Sound.Beer.Position = TimeSpan.Zero;
                    Sound.Beer.Play();
                    break;
                case EItem.Inverter:
                    Sound.Inverter.Position = TimeSpan.Zero;
                    Sound.Inverter.Play();
                    break;
                case EItem.Medicine:
                    Sound.Medicine.Position = TimeSpan.Zero;
                    Sound.Medicine.Play();
                    break;
                case EItem.Phone:
                    Sound.Phone.Position = TimeSpan.Zero;
                    Sound.Phone.Play();
                    break;
                case EItem.Adrenaline:
                    Sound.Adrenaline.Position = TimeSpan.Zero;
                    Sound.Adrenaline.Play();
                    break;
            }
        }
    }
}