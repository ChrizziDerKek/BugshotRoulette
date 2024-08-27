using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Xml.Linq;

namespace BSR_Client
{
    public partial class MainWindow
    {
        public void ShowBullets(EBullet[] bullets)
        {
            if (bullets.Length > 8)
                return;
            int nblank = 0;
            int nlive = 0;
            foreach (EBullet bullet in bullets)
            {
                if (bullet == EBullet.Live)
                    nlive++;
                else
                    nblank++;
            }
            Announce(nlive + " lives, " + nblank + " blanks");
            for (int i = 0; i < bullets.Length; i++)
                Elements.Bullets[i].Fill = bullets[i] == EBullet.Live ? Brushes.Red : Brushes.Gray;
        }

        public void ResetBullets()
        {
            for (int i = 0; i < Elements.Bullets.Length; i++)
                Elements.Bullets[i].Fill = Brushes.Transparent;
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
            int maxhealth = RNG.Next(5, 16);
            foreach (string player in Players)
                UpdateLives(player, maxhealth, true, false, true);
        }

        public void RemovePlayer(string player)
        {
            Players.Remove(player);
            PlayerTurns.Remove(player);
            Playerlist.Items.Remove(player);
            PlayersAlive--;
            int it = 0;
            foreach (Button b in Elements.Players)
            {
                if (IsPlayerSlot(b, player))
                {
                    Elements.HealthBars[it].Value = 0;
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
                int start = (int)EItem.Nothing + 1;
                if (DebugMode == EDebugMode.GetOwnItems)
                    start = (int)EItem.Magazine;
                EItem item = (EItem)RNG.Next(start, (int)EItem.Count);
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

        public int GetItemCount()
        {
            int count = 0;
            for (int i = 0; i < Elements.Items.Length; i++)
                if (!IsItemSlot(Elements.Items[i], "Nothing"))
                    count++;
            return count;
        }

        public void UpdateLives(string player, int lives, bool set, bool lose, bool sync)
        {
            if (set && lose)
                return;
            if (lose && DebugMode == EDebugMode.InfiniteHealth)
                return;
            if (sync)
                Packet.Create(EPacket.UpdateLives).Add(player).Add(lives).Add(set).Add(lose).Send(Sync);
            int it = 0;
            foreach (Button b in Elements.Players)
            {
                if (IsPlayerSlot(b, player))
                {
                    if (set)
                        Elements.HealthBars[it].Value = lives;
                    else if (lose)
                        Elements.HealthBars[it].Value -= lives;
                    else
                        Elements.HealthBars[it].Value += lives;
                    break;
                }
                it++;
            }
            if (player == MyName && GetHealth() <= 0)
            {
                Dead();
                if (PlayersAlive <= 1)
                    ShowEndscreen();
            }
        }

        public void GenerateBullets(bool sync = true)
        {
            int randomDecision = RNG.Next(0, 100);
            int count = randomDecision > 60 ? RNG.Next(2, 9) : RNG.Next(1, 3) * 2;  
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
            if (DebugMode == EDebugMode.GenerateLivesOnly)
                numblank = 0;
            if (DebugMode == EDebugMode.GenerateBlanksOnly)
                numblank = 8;
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
            Playerlist.Items.Insert(0, username);
            for (int i = 0; i < Elements.Players.Length; i++)
            {
                if (IsPlayerSlot(Elements.Players[i], "None"))
                {
                    SetPlayerData(Elements.Players[i], username, true);
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

        public void BlockPlayers()
        {
            foreach (Button i in Elements.Players)
                i.IsEnabled = false;
        }

        public void SetActive(bool active)
        {
            Shoot.IsEnabled = active;
            foreach (Button i in Elements.Items)
                i.IsEnabled = false;
            foreach (Button i in Elements.Players)
                i.IsEnabled = false;
        }

        public void HideInactivePlayers()
        {
            foreach (Button i in Elements.Players)
                if (IsPlayerSlot(i, "None"))
                    i.Visibility = Visibility.Hidden;
        }

        public void PrepareGun()
        {
            Shoot.IsEnabled = false;
            foreach (Button i in Elements.Items)
                i.IsEnabled = false;
            int it = 0;
            foreach (Button i in Elements.Players)
            {
                if (!IsPlayerSlot(i, "None") && Elements.HealthBars[it].Value > 0)
                    i.IsEnabled = true;
                it++;
            }
        }

        public void PreparePlayerItem()
        {
            Shoot.IsEnabled = false;
            foreach (Button i in Elements.Items)
                i.IsEnabled = false;
            int it = 0;
            foreach (Button i in Elements.Players)
            {
                if (!IsPlayerSlot(i, "None") && !IsPlayerSlot(i, MyName) && Elements.HealthBars[it].Value > 0)
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
            foreach (Button i in Elements.Players)
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
            slot.Visibility = type == EItem.Nothing ? Visibility.Hidden : Visibility.Visible;
            if (type != EItem.Nothing)
            {
                if (!ItemDescriptions.TryGetValue(type, out string desc))
                    desc = "NO DESCRIPTION";
                slot.ToolTip = type.ToString() + "\n\n" + desc;
            }
            else slot.ToolTip = null;
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
            int it = 0;
            foreach (Button i in Elements.Players)
            {
                if (IsPlayerSlot(i, MyName))
                    return (int)Elements.HealthBars[it].Value;
                it++;
            }
            return 0;
        }

        public void SaveItems()
        {
            ItemStorage.Clear();
            for (int i = 0; i < Elements.Items.Length; i++)
            {
                if (!Enum.TryParse(GetItemType(Elements.Items[i]), out EItem item))
                    continue;
                ItemStorage.Add(item);
            }
        }

        public void RestoreItems()
        {
            int slot = 0;
            foreach (EItem item in ItemStorage)
                ForceSetItem(slot++, item);
        }

        public void ForceSetItem(int slot, EItem type)
        {
            for (int i = 0; i < Elements.Items.Length; i++)
            {
                if (slot == i)
                {
                    Elements.Items[i].Visibility = Visibility.Visible;
                    SetItemData(Elements.Items[i], type);
                    Elements.Items[i].IsEnabled = true;
                }
            }
        }

        public bool SetItem(EItem type, bool enable = false)
        {
            if (DebugMode == EDebugMode.GetNoItems)
                return false;
            for (int i = 0; i < Elements.Items.Length; i++)
            {
                if (IsItemSlot(Elements.Items[i], "Nothing"))
                {
                    Elements.Items[i].Visibility = Visibility.Visible;
                    SetItemData(Elements.Items[i], type);
                    if (enable)
                        Elements.Items[i].IsEnabled = true;
                    return true;
                }
            }
            return false;
        }

        public List<string> GetItemTypes()
        {
            List<string> result = new List<string>();
            foreach (Button item in Elements.Items)
                result.Add(GetItemType(item));
            return result;
        }

        public void HideEmptyItemSlots()
        {
            for (int i = 0; i < Elements.Items.Length; i++)
                if (IsItemSlot(Elements.Items[i], "Nothing"))
                    Elements.Items[i].Visibility = Visibility.Hidden;
        }

        public EItem UseItem(string slot, bool sync)
        {
            Button it = null;
            for (int i = 0; i < Elements.Items.Length; i++)
            {
                if (Elements.Items[i].Name == slot)
                {
                    it = Elements.Items[i];
                    break;
                }
            }
            if (it == null)
                return EItem.Nothing;
            if (Enum.TryParse(GetItemType(it), out EItem item))
            {
                if (sync)
                {
                    Announce("You used " + item.ToString());
                    Packet.Create(EPacket.UseItem).Add(MyName).Add(item.ToString()).Send(Sync);
                }
                SetItemData(it, EItem.Nothing);
                it.IsEnabled = false;
                return item;
            }
            return EItem.Nothing;
        }

        public void UnlockItems()
        {
            if (LockedItems)
                return;
            for (int i = 0; i < Elements.Items.Length; i++)
                if (!IsItemSlot(Elements.Items[i], "Nothing"))
                    Elements.Items[i].IsEnabled = true;
        }

        public void LockItems()
        {
            LockedItems = true;
            for (int i = 0; i < Elements.Items.Length; i++)
                if (!IsItemSlot(Elements.Items[i], "Nothing"))
                    Elements.Items[i].IsEnabled = false;
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

        public void PlaySfx(bool live, bool gunpowdered)
        {
            if (live)
            {
                if (gunpowdered)
                {
                    Sound.GunpowderShot.Position = TimeSpan.Zero;
                    Sound.GunpowderShot.Play();
                }
                else
                {
                    Sound.Shot.Position = TimeSpan.Zero;
                    Sound.Shot.Play();
                }
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
                case EItem.Magazine:
                    Sound.Magazine.Position = TimeSpan.Zero;
                    Sound.Magazine.Play();
                    break;
                case EItem.Gunpowder:
                    Sound.Gunpowder.Position = TimeSpan.Zero;
                    Sound.Gunpowder.Play();
                    break;
                //case EItem.Bullet:
                //    Sound.Bullet.Position = TimeSpan.Zero;
                //    Sound.Bullet.Play();
                //    break;
                case EItem.Trashbin:
                    Sound.Trashbin.Position = TimeSpan.Zero;
                    Sound.Trashbin.Play();
                    break;
            }
        }

        public bool IsNameValid(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            if (DebugMode == EDebugMode.AllowAllNameChars)
                return true;
            if (name.Length > 0xFF)
                return false;
            if (name.Contains(","))
                return false;
            if (name.Contains("#"))
                return false;
            if (name.Contains("$"))
                return false;
            if (name.ToLower().Trim() == "none")
                return false;
            if (name.Trim() == null || name.Trim().Length == 0)
                return false;
            string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_ .-+*/!?";
            foreach (char c in name)
                if (!allowed.Contains("" + c))
                    return false;
            return true;
        }

        public EDebugMode IsDebugName(string name)
        {
            if (!name.StartsWith("#debug_") || !name.Contains(" "))
                return EDebugMode.None;
            string[] split = name.Split(' ');
            if (split.Length != 2)
                return EDebugMode.None;
            if (split[1].Trim() == "")
                return EDebugMode.None;
            switch (split[0].Replace("#debug_", "").ToLower())
            {
                case "gni":
                    return EDebugMode.GetNoItems;
                case "goi":
                    return EDebugMode.GetOwnItems;
                case "ih":
                    return EDebugMode.InfiniteHealth;
                case "gbo":
                    return EDebugMode.GenerateBlanksOnly;
                case "glo":
                    return EDebugMode.GenerateLivesOnly;
                case "aanc":
                    return EDebugMode.AllowAllNameChars;
            }
            return EDebugMode.None;
        }

        public void RenameDebugName()
        {
            string[] split = MyName.Split(' ');
            MyName = split[1];
        }

        public bool IsIpValid(string ip)
        {
            string[] splitted = ip.Split('.');
            if (splitted.Length != 4)
                return false;
            foreach (string s in splitted)
                if (!int.TryParse(s, out int value) || value > 0xFF || value < 0)
                    return false;
            return true;
        }

        public string GetPlayerFromSlot(int slotId)
        {
            Button slot = Elements.Players[slotId];
            if (slot == null)
                return null;
            if (!(slot.Content is Grid))
                return null;
            UIElement text = (slot.Content as Grid).Children[1];
            if (!(text is TextBlock))
                return null;
            return (text as TextBlock).Text;
        }
    }
}