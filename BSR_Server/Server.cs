using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Data;
using System.Threading;
using System.Security.Cryptography;

#pragma warning disable IDE0044
#pragma warning disable IDE0058

namespace Server
{
    public class Session
    {
        private RandomNumberGenerator Generator;
        private string Host;
        private List<string> Players;
        private Queue<string> NextHosts;
        private bool Locked;
        private SettingsData Settings;
        private int CurrentPlayer;
        private Dictionary<string, EItem[]> PlayerItems;
        private Dictionary<string, int> PlayerHealth;
        private List<EBullet> ActualBullets;
        private List<EBullet> DisplayedBullets;
        private string Code;
        private int StartLives;
        private bool CanGoAgain;
        private Dictionary<string, List<EItem>> LastGeneratedItems;
        private ERoundFlags NextRoundFlags;
        private EItem LastUsedItem;
        private Dictionary<string, ERoundFlags> PlayerFlags;
        private bool RoundInverted;
        private Dictionary<string, int> Scores;

        public enum EBotFlag
        {
            None = 0,
            KnowsCurrentBullet = 1 << 0,
            CanUseAdrenaline = 1 << 1,
            UsingMedicine = 1 << 2,
            UsingSaw = 1 << 3,
            SawedOff = 1 << 4,
            CuffedPlayer = 1 << 5,
            ItemDecisionDone = 1 << 6,
        }

        private string BotName;
        private bool BotAdded;
        private List<bool> BulletIsKnown;
        private EBullet CurrentlyKnownBullet;
        private bool? BotShouldTargetPlayer;
        private EBotFlag BotFlags;

        public int RNG(int min, int max)
        {
            byte[] data = new byte[4];
            Generator.GetBytes(data);
            int sample = BitConverter.ToInt32(data, 0);
            if (sample < 0)
                sample += int.MaxValue;
            long difference = (long)max - min;
            double rand = sample * 4.6566128752457969E-10;
            return (int)(rand * difference) + min;
        }

        public bool IsBotFlagSet(EBotFlag flag) => (BotFlags & flag) != 0;

        public void SetBotFlag(EBotFlag flag, bool set)
        {
            if (set)
                BotFlags |= flag;
            else
                BotFlags &= ~flag;
        }

        private static Dictionary<EItem, int> ItemLimits = new Dictionary<EItem, int>()
        {
            { EItem.Nothing, 0 },
            { EItem.Handcuffs, 2 },
            { EItem.Cigarettes, 3 },
            { EItem.Saw, 2 },
            { EItem.Magnifying, 3 },
            { EItem.Beer, 3 },
            { EItem.Inverter, 2 },
            { EItem.Medicine, 3 },
            { EItem.Phone, 2 },
            { EItem.Adrenaline, 2 },
            { EItem.Magazine, 2 },
            { EItem.Gunpowder, 2 },
            { EItem.Bullet, 1 },
            { EItem.Trashbin, 1 },
            { EItem.Heroine, 1 },
            { EItem.Katana, 1 },
            { EItem.Swapper, 1 },
            { EItem.Hat, 1 },
            { EItem.Snus, 1 },
            { EItem.Elfbar, 1 },
            { EItem.Scope, 2 },
            { EItem.Remote, 2 },
            { EItem.Count, 0 },
        };

        public Session(string host, string code)
        {
            Host = host;
            Players = new List<string>() { host };
            Locked = false;
            NextHosts = new Queue<string>();
            Settings = new SettingsData();
            CurrentPlayer = 0;
            PlayerItems = new Dictionary<string, EItem[]>();
            ActualBullets = new List<EBullet>();
            DisplayedBullets = new List<EBullet>();
            Code = code;
            StartLives = 0;
            CanGoAgain = false;
            PlayerHealth = new Dictionary<string, int>();
            LastGeneratedItems = new Dictionary<string, List<EItem>>();
            NextRoundFlags = ERoundFlags.None;
            LastUsedItem = EItem.Nothing;
            PlayerFlags = new Dictionary<string, ERoundFlags>();
            BotName = "Dealer";
            BotAdded = false;
            BulletIsKnown = new List<bool>();
            CurrentlyKnownBullet = EBullet.Undefined;
            BotShouldTargetPlayer = null;
            BotFlags = EBotFlag.None;
            RoundInverted = false;
            Scores = new Dictionary<string, int>();
            Generator = RandomNumberGenerator.Create();
        }

        public bool UsingBetaInverters() => Settings.BetaInverters;

        public int GetScore(string player)
        {
            if (!Scores.ContainsKey(player))
                return 0;
            return Scores[player];
        }

        public void AddScore(string player, int score)
        {
            if (!Scores.ContainsKey(player))
                Scores.Add(player, 0);
            Scores[player] += score;
        }

        public void IncreaseMaxHealth() => StartLives++;

        public void ResetGame()
        {
            CurrentPlayer = 0;
            PlayerItems.Clear();
            ActualBullets.Clear();
            DisplayedBullets.Clear();
            StartLives = 0;
            CanGoAgain = false;
            PlayerHealth.Clear();
            LastGeneratedItems.Clear();
            NextRoundFlags = ERoundFlags.None;
            LastUsedItem = EItem.Nothing;
            PlayerFlags.Clear();
            BulletIsKnown.Clear();
            CurrentlyKnownBullet = EBullet.Undefined;
            BotShouldTargetPlayer = null;
            BotFlags = EBotFlag.None;
            RoundInverted = false;
        }

        public int GetNumAlivePlayers()
        {
            int result = 0;
            foreach (string player in Players)
                if (GetHealth(player) > 0)
                    result++;
            return result;
        }

        public string GetWinner()
        {
            foreach (string player in Players)
                if (GetHealth(player) > 0)
                    return player;
            return "";
        }

        public bool IsDead(string player) => GetHealth(player) <= 0;

        public string GetBotName() => BotName;

        public bool IsBot(string player) => BotName == player;

        public bool BotExists() => BotAdded;

        public void SwapItems(string player, string with)
        {
            if (!PlayerItems.ContainsKey(player) || !PlayerItems.ContainsKey(with))
                return;
            (PlayerItems[with], PlayerItems[player]) = (PlayerItems[player], PlayerItems[with]);
        }

        public void SetLastUsedItem(EItem item) => LastUsedItem = item;

        public EItem GetLastUsedItem() => LastUsedItem;

        public void InvertBullet() => ActualBullets[0] = ActualBullets[0] == EBullet.Live ? EBullet.Blank : EBullet.Live;

        public bool HasFlag(ERoundFlags flag, string player = null)
        {
            if (player == null)
                return (NextRoundFlags & flag) != 0;
            if (!PlayerFlags.ContainsKey(player))
                return false;
            return (PlayerFlags[player] & flag) != 0;
        }

        public void SetFlag(ERoundFlags flag, string player = null)
        {
            if (player == null)
            {
                NextRoundFlags |= flag;
                return;
            }
            if (!PlayerFlags.ContainsKey(player))
                PlayerFlags.Add(player, ERoundFlags.None);
            PlayerFlags[player] |= flag;
        }

        public void ResetFlag(ERoundFlags flag, string player = null)
        {
            if (player == null)
            {
                NextRoundFlags &= ~flag;
                return;
            }
            if (!PlayerFlags.ContainsKey(player))
                PlayerFlags.Add(player, ERoundFlags.None);
            PlayerFlags[player] &= ~flag;
        }

        public ERoundFlags GetRoundFlags(string player = null)
        {
            if (player == null)
                return NextRoundFlags;
            if (!PlayerFlags.ContainsKey(player))
                return ERoundFlags.None;
            return PlayerFlags[player];
        }

        public void ResetGlobalFlags(bool everything = false)
        {
            if (everything)
            {
                NextRoundFlags = ERoundFlags.None;
                return;
            }
            bool again = HasFlag(ERoundFlags.AgainBecauseCuffed);
            bool used = HasFlag(ERoundFlags.HandcuffsJustUsed);
            NextRoundFlags = ERoundFlags.None;
            if (again)
                SetFlag(ERoundFlags.AgainBecauseCuffed);
            if (used)
                SetFlag(ERoundFlags.HandcuffsJustUsed);
        }

        public Dictionary<string, List<EItem>> GetLastGeneratedItems() => LastGeneratedItems;

        public int GetHealth(string player) => PlayerHealth.ContainsKey(player) ? PlayerHealth[player] : 0;

        public void SetHealth(string player, int health)
        {
            if (!PlayerHealth.ContainsKey(player))
                return;
            PlayerHealth[player] = health;
            if (PlayerHealth[player] < 0)
                PlayerHealth[player] = 0;
            if (PlayerHealth[player] > GetMaxHealth())
                PlayerHealth[player] = GetMaxHealth();
        }

        public EBullet PopBullet()
        {
            if (ActualBullets.Count == 0)
                return EBullet.Undefined;
            EBullet bullet = ActualBullets[0];
            ActualBullets.RemoveAt(0);
            DisplayedBullets.RemoveAt(0);
            BulletIsKnown.RemoveAt(0);
            return bullet;
        }

        public List<EBullet> GetNextBullets()
        {
            List<EBullet> result = new List<EBullet>();
            for (int i = 0; i < Math.Min(3, ActualBullets.Count); i++)
                result.Add(ActualBullets[i]);
            return result;
        }

        public int GetBulletCount() => ActualBullets.Count;

        public string GetSession() => Code;

        public void MigrateHost() => Host = NextHosts.Dequeue();

        public bool IsPlayerConnected(string player) => Players.Contains(player);

        public bool ShouldSwitchPlayer()
        {
            if (CanGoAgain)
            {
                CanGoAgain = false;
                return false;
            }
            return true;
        }

        public void SetAgain(bool again = true) => CanGoAgain = again;

        public void AddPlayer(string player)
        {
            Players.Add(player);
            NextHosts.Enqueue(player);
            if (Players.Count >= Settings.MaxPlayers)
                Locked = true;
        }

        public bool AddBot()
        {
            if (Players.Contains(BotName))
                return false;
            Players.Add(BotName);
            BotAdded = true;
            if (Players.Count >= Settings.MaxPlayers)
                Locked = true;
            return true;
        }

        public void RemovePlayer(string player)
        {
            if (!Players.Contains(player))
                return;
            if (IsBot(player))
                BotAdded = false;
            Players.Remove(player);
            PlayerItems.Remove(player);
            PlayerHealth.Remove(player);
            PlayerFlags.Remove(player);
            if (Players.Count < Settings.MaxPlayers)
                Locked = false;
        }

        public bool RemoveBot()
        {
            if (!Players.Contains(BotName))
                return false;
            RemovePlayer(BotName);
            return true;
        }

        public void FixHostQueue(string player) => NextHosts = new Queue<string>(NextHosts.Where(h => h != player));

        public string GetHost() => Host;

        public List<string> GetPlayers() => Players;

        public int GetPlayerCount() => Players.Count;

        public bool ShouldBeDestroyed() => NextHosts.Count == 0;

        public void Lock() => Locked = true;

        public void Unlock() => Locked = false;

        public bool IsLocked() => Locked;

        public int GetMaxPlayers() => Settings.MaxPlayers;

        public void UpdateSettings(SettingsData s) => Settings = s;

        public void ResetSettings() => Settings = new SettingsData();

        public void SetFirstPlayer()
        {
            do CurrentPlayer = RNG(0, Players.Count);
            while (IsBot(Players[CurrentPlayer]));
        }

        public int GetMaxHealth() => StartLives;

        public string GetCurrentPlayer() => Players[CurrentPlayer];

        public int SwitchPlayer()
        {
            if (Players.Count == 0)
                return -1;
            SetLastUsedItem(EItem.Nothing);
            ResetGlobalFlags(true);
            int modifier = 1;
            if (RoundInverted)
                modifier = -1;
            CurrentPlayer = (CurrentPlayer + modifier) % Players.Count;
            if (CurrentPlayer == -1)
                CurrentPlayer = Players.Count - 1;
            return CurrentPlayer;
        }

        public void InvertRound() => RoundInverted = !RoundInverted;

        public List<string> GetRepeatedHealing()
        {
            List<string> result = new List<string>();
            foreach (string player in Players)
                if (HasFlag(ERoundFlags.RepeatedHealing, player))
                    result.Add(player);
            return result;
        }

        public void RoundStart(bool initial = false, bool noitems = false)
        {
            int nitems = RNG(Settings.MinItems, Settings.MaxItems + 1);
            GenerateBullets();
            if (!noitems)
            {
                LastGeneratedItems.Clear();
                foreach (string player in Players)
                    GenerateItems(player, nitems, IsBot(player), false);
            }
            if (initial)
            {
                GenerateLives();
                foreach (string player in Players)
                {
                    if (!PlayerHealth.ContainsKey(player))
                        PlayerHealth.Add(player, StartLives);
                    else
                        PlayerHealth[player] = StartLives;
                }
            }
            if (BotExists())
                BotRoundStart();
        }

        public void PushBullet()
        {
            bool live = RNG(0, 2) == 0;
            ActualBullets.Add(live ? EBullet.Live : EBullet.Blank);
            DisplayedBullets.Add(EBullet.Undefined);
            BulletIsKnown.Add(false);
        }

        private void GenerateLives() => StartLives = RNG(Settings.MinHealth, Settings.MaxHealth + 1);

        private void GenerateBullets()
        {
            ActualBullets.Clear();
            DisplayedBullets.Clear();
            int min = Settings.MinBullets;
            int max = Settings.MaxBullets;
            int even = RNG(0, 100);
            int ntotal = RNG(min, max + 1);
            if (even > 40 && ntotal % 2 != 0 && ntotal < max)
                ntotal++;
            int nblank = 0;
            switch (ntotal)
            {
                case 2:
                    nblank = 1;
                    break;
                case 3:
                case 4:
                    nblank = RNG(1, ntotal);
                    break;
                case 5:
                case 6:
                    nblank = RNG(2, 4);
                    break;
                case 7:
                    nblank = RNG(3, 5);
                    break;
                case 8:
                    nblank = RNG(3, 6);
                    break;
            }
            int nlive = ntotal - nblank;
            for (int i = 0; i < nblank; i++)
            {
                ActualBullets.Add(EBullet.Blank);
                DisplayedBullets.Add(EBullet.Blank);
                BulletIsKnown.Add(false);
            }
            for (int i = 0; i < nlive; i++)
            {
                ActualBullets.Add(EBullet.Live);
                DisplayedBullets.Add(EBullet.Live);
                BulletIsKnown.Add(false);
            }
            ShuffleBullets();
        }

        public EItem GenerateItems(string player, int count, bool bot, bool bypasslimits)
        {
            if (bot && !Settings.OriginalItemsOnly)
                count++;
            EItem lastgenerated = EItem.Nothing;
            if (Settings.NoItems)
                return lastgenerated;
            for (int i = 0; i < count; i++)
            {
                int start = (int)EItem.Nothing + 1;
                int end = (int)EItem.Count;
                if (Settings.OriginalItemsOnly || bot)
                    end = (int)EItem.Adrenaline + 1;
                int attempts = 0;
                bool skipped;
                EItem item;
                do
                {
                    skipped = false;
                    if (attempts++ > 100)
                    {
                        item = EItem.Nothing;
                        break;
                    }
                    item = (EItem)RNG(start, end);
                    if (bypasslimits && (item == EItem.Trashbin || item == GetLastUsedItem()))
                    {
                        attempts--;
                        skipped = true;
                        continue;
                    }
                    if (Settings.EnabledItems.TryGetValue(item, out bool enabled) && !enabled)
                    {
                        attempts--;
                        skipped = true;
                        continue;
                    }
                    if ((item == EItem.Heroine || item == EItem.Katana || item == EItem.Elfbar) && RNG(0, 5) != 0)
                    {
                        item = (EItem)RNG(start, end);
                        if (Settings.EnabledItems.TryGetValue(item, out enabled) && !enabled)
                        {
                            attempts--;
                            skipped = true;
                            continue;
                        }
                    }
                }
                while (skipped || (ItemLimits.TryGetValue(item, out int limit) && GetItemCount(player, item) >= limit && !bypasslimits));
                if (item == EItem.Nothing)
                    break;
                if (!PlayerItems.ContainsKey(player))
                {
                    PlayerItems.Add(player, new EItem[8]);
                    for (int j = 0; j < 8; j++)
                        PlayerItems[player][j] = EItem.Nothing;
                }
                bool foundPlace = false;
                for (int j = 0; j < 8; j++)
                {
                    if (PlayerItems[player][j] == EItem.Nothing)
                    {
                        PlayerItems[player][j] = item;
                        foundPlace = true;
                        break;
                    }
                }
                if (foundPlace)
                {
                    if (!LastGeneratedItems.ContainsKey(player))
                        LastGeneratedItems.Add(player, new List<EItem>());
                    LastGeneratedItems[player].Add(item);
                    lastgenerated = item;
                }
            }
            return lastgenerated;
        }

        public List<EBullet> GetBullets(bool display) => display ? DisplayedBullets : ActualBullets;

        public EBullet GetNextBullet() => ActualBullets[0];

        public EItem[] GetItems(string player) => PlayerItems[player];

        public bool PlayerHasItem(string player, EItem item)
        {
            foreach (EItem it in PlayerItems[player])
                if (item == it)
                    return true;
            return false;
        }

        public bool RemoveItem(string player, EItem item)
        {
            for (int i = 0; i < PlayerItems[player].Length; i++)
            {
                if (PlayerItems[player][i] == item)
                {
                    PlayerItems[player][i] = EItem.Nothing;
                    return true;
                }
            }
            return false;
        }

        public int GetItemCount(string player, EItem item = EItem.Count)
        {
            if (!PlayerItems.TryGetValue(player, out EItem[] items))
                return 0;
            int count = 0;
            foreach (EItem it in items)
            {
                if (it == EItem.Nothing)
                    continue;
                if (item == EItem.Count || it == item)
                    count++;
            }
            return count;
        }

        private void ShuffleBullets()
        {
            int n = ActualBullets.Count;
            while (n > 1)
            {
                int r = RNG(0, n--);
                (ActualBullets[r], ActualBullets[n]) = (ActualBullets[n], ActualBullets[r]);
            }
            n = DisplayedBullets.Count;
            while (n > 1)
            {
                int r = RNG(0, n--);
                (DisplayedBullets[r], DisplayedBullets[n]) = (DisplayedBullets[n], DisplayedBullets[r]);
            }
        }

        public void BotTurn(List<Packet> packets, List<Dictionary<string, Packet>> playerpackets)
        {
            bool hassaw = false;
            bool hascigs = false;
            EItem wantstouse = EItem.Nothing;
            EBullet racked = EBullet.Undefined;
            bool rackedwasinverted = false;
            int phoneindex = 0;
            if (!IsBotFlagSet(EBotFlag.KnowsCurrentBullet))
            {
                SetBotFlag(EBotFlag.KnowsCurrentBullet, FigureOutBullet());
                if (IsBotFlagSet(EBotFlag.KnowsCurrentBullet))
                    SetKnownBullet();
            }
            int health = GetHealth(BotName);
            int maxhealth = GetMaxHealth();
            int bulletcount = GetBulletCount();
            if (bulletcount == 1)
                SetKnownBullet();
            foreach (EItem item in GetItems(BotName))
            {
                if (item == EItem.Cigarettes)
                    hascigs = true;
                if (item == EItem.Adrenaline)
                    SetBotFlag(EBotFlag.CanUseAdrenaline, true);
            }
            List<KeyValuePair<EItem, string>> availableitems = new List<KeyValuePair<EItem, string>>();
            foreach (EItem it in GetItems(BotName))
                if (it != EItem.Nothing)
                    availableitems.Add(new KeyValuePair<EItem, string>(it, BotName));
            if (IsBotFlagSet(EBotFlag.CanUseAdrenaline))
                foreach (string player in GetPlayers())
                    if (!IsBot(player))
                        foreach (EItem it in GetItems(player))
                            if (it < EItem.Adrenaline && it != EItem.Nothing)
                                availableitems.Add(new KeyValuePair<EItem, string>(it, player));
            foreach (KeyValuePair<EItem, string> it in availableitems)
            {
                EItem item = it.Key;
                switch (item)
                {
                    case EItem.Handcuffs:
                        if (!IsBotFlagSet(EBotFlag.CuffedPlayer) && bulletcount != 1)
                        {
                            wantstouse = item;
                            SetBotFlag(EBotFlag.CuffedPlayer, true);
                            SetFlag(ERoundFlags.AgainBecauseCuffed);
                        }
                        break;
                    case EItem.Cigarettes:
                        if (health < maxhealth)
                        {
                            wantstouse = item;
                            hascigs = false;
                        }
                        break;
                    case EItem.Saw:
                        if (!IsBotFlagSet(EBotFlag.SawedOff) && CurrentlyKnownBullet == EBullet.Live)
                        {
                            wantstouse = item;
                            SetBotFlag(EBotFlag.SawedOff | EBotFlag.UsingSaw, true);
                            SetFlag(ERoundFlags.ShotgunSawedOff);
                        }
                        break;
                    case EItem.Magnifying:
                        if (!IsBotFlagSet(EBotFlag.KnowsCurrentBullet) && bulletcount != 1)
                        {
                            wantstouse = item;
                            SetKnownBullet();
                        }
                        break;
                    case EItem.Beer:
                        if (CurrentlyKnownBullet != EBullet.Live && bulletcount != 1)
                        {
                            wantstouse = item;
                            racked = PopBullet();
                            rackedwasinverted = HasFlag(ERoundFlags.ShotInverted);
                            ResetGlobalFlags();
                            SetBotFlag(EBotFlag.KnowsCurrentBullet, false);
                        }
                        break;
                    case EItem.Inverter:
                        if (IsBotFlagSet(EBotFlag.KnowsCurrentBullet) && CurrentlyKnownBullet == EBullet.Blank)
                        {
                            wantstouse = item;
                            if (HasFlag(ERoundFlags.ShotInverted))
                                ResetFlag(ERoundFlags.ShotInverted);
                            else
                                SetFlag(ERoundFlags.ShotInverted);
                            InvertBullet();
                            SetKnownBullet();
                        }
                        break;
                    case EItem.Medicine:
                        if (health < maxhealth && !hascigs && !IsBotFlagSet(EBotFlag.UsingMedicine) && health != 1)
                        {
                            wantstouse = item;
                            SetBotFlag(EBotFlag.UsingMedicine, true);
                        }
                        break;
                    case EItem.Phone:
                        if (bulletcount >= 2)
                        {
                            int decision = RNG(1, bulletcount);
                            if (decision == 8)
                                decision--;
                            BulletIsKnown[decision] = true;
                            phoneindex = decision;
                            wantstouse = item;
                        }
                        break;
                }
                if (wantstouse != EItem.Nothing)
                    break;
            }
            if (wantstouse == EItem.Nothing)
                SetBotFlag(EBotFlag.ItemDecisionDone, true);
            foreach (EItem item in GetItems(BotName))
                if (item == EItem.Saw)
                    hassaw = true;
            if (IsBotFlagSet(EBotFlag.ItemDecisionDone) && !IsBotFlagSet(EBotFlag.UsingSaw) && hassaw && !IsBotFlagSet(EBotFlag.SawedOff) && CurrentlyKnownBullet != EBullet.Blank)
            {
                if (CoinFlip())
                {
                    BotShouldTargetPlayer = true;
                    wantstouse = EItem.Saw;
                    SetBotFlag(EBotFlag.UsingSaw, true);
                    SetBotFlag(EBotFlag.SawedOff, true);
                    SetFlag(ERoundFlags.ShotgunSawedOff);
                }
                else BotShouldTargetPlayer = false;
            }
            if (wantstouse != EItem.Nothing)
            {
                int medsmodifier = 0;
                switch (wantstouse)
                {
                    case EItem.Cigarettes:
                        {
                            health++;
                            if (health > maxhealth)
                                health = maxhealth;
                            SetHealth(BotName, health);
                        }
                        break;
                    case EItem.Medicine:
                        {
                            if (RNG(0, 2) == 0)
                                medsmodifier = -1;
                            else
                                medsmodifier = 2;
                            health += medsmodifier;
                            if (health < 0)
                                health = 0;
                            if (health > maxhealth)
                                health = maxhealth;
                            SetHealth(BotName, health);
                        }
                        break;
                }
                bool stealing = !RemoveItem(BotName, wantstouse);
                string stealtarget = null;
                if (stealing)
                {
                    List<string> possibletargets = new List<string>();
                    foreach (KeyValuePair<EItem, string> it in availableitems)
                    {
                        EItem item = it.Key;
                        string player = it.Value;
                        if (item == wantstouse && wantstouse != EItem.Adrenaline)
                            possibletargets.Add(player);
                    }
                    if (possibletargets.Count == 1)
                        stealtarget = possibletargets[0];
                    else
                        stealtarget = possibletargets[RNG(0, possibletargets.Count)];
                    packets.Add(new PacketUsedItem(BotName, stealtarget, null, false));
                    RemoveItem(BotName, EItem.Adrenaline);
                    RemoveItem(stealtarget, wantstouse);
                    SetBotFlag(EBotFlag.CanUseAdrenaline, false);
                }
                switch (wantstouse)
                {
                    case EItem.Handcuffs:
                        packets.Add(new PacketUsedItem(BotName, wantstouse, stealtarget));
                        break;
                    case EItem.Cigarettes:
                        packets.Add(new PacketUsedItem(BotName, 1, true, stealtarget, false));
                        break;
                    case EItem.Saw:
                        packets.Add(new PacketUsedItem(BotName, wantstouse, stealtarget));
                        break;
                    case EItem.Magnifying:
                        packets.Add(new PacketUsedItem(BotName, EBullet.Undefined, stealtarget, 0, false));
                        break;
                    case EItem.Beer:
                        packets.Add(new PacketUsedItem(BotName, racked, rackedwasinverted, stealtarget, false));
                        break;
                    case EItem.Inverter:
                        packets.Add(new PacketUsedItem(BotName, wantstouse, stealtarget, false, EItem.Nothing, false));
                        break;
                    case EItem.Medicine:
                        packets.Add(new PacketUsedItem(BotName, medsmodifier, false, stealtarget, false));
                        break;
                    case EItem.Phone:
                        packets.Add(new PacketUsedItem(BotName, EBullet.Undefined, stealtarget, phoneindex, false));
                        break;
                }
                BotTurn(packets, playerpackets);
                return;
            }
            if (BotShouldTargetPlayer == null)
                BotShouldTargetPlayer = CoinFlip();
            EBullet bullet = PopBullet();
            string target = BotName;
            if (BotShouldTargetPlayer == true)
                target = DecideBotPlayerTarget();
            if (bullet == EBullet.Blank && BotShouldTargetPlayer == false)
                SetAgain();
            ERoundFlags flags = GetRoundFlags();
            packets.Add(new PacketShoot(BotName, target, flags, bullet));
            BotShouldTargetPlayer = null;
            CurrentlyKnownBullet = EBullet.Undefined;
            SetBotFlag(EBotFlag.KnowsCurrentBullet, false);
            SetBotFlag(EBotFlag.CanUseAdrenaline, false);
            if (bullet == EBullet.Live)
            {
                ResetFlag(ERoundFlags.RepeatedHealing, target);
                SetHealth(target, GetHealth(target) - (HasFlag(ERoundFlags.ShotgunSawedOff) ? 2 : 1));
                AddScore(target, -50);
            }
            ResetGlobalFlags();
            if (GetBulletCount() == 0)
            {
                RoundStart();
                packets.Add(null);
                Dictionary<string, Packet> temp = new Dictionary<string, Packet>();
                foreach (string player in Players)
                    if (!IsBot(player))
                        temp.Add(player, new PacketStartRound(GetBullets(true), GetItems(player), GetLastGeneratedItems()));
                playerpackets.Add(temp);
            }
        }

        private string DecideBotPlayerTarget()
        {
            List<string> decisions = new List<string>();
            foreach (string player in GetPlayers())
            {
                if (IsBot(player))
                    continue;
                int count = PlayerHealth[player];
                if (HasFlag(ERoundFlags.RepeatedHealing, player))
                    count *= 2;
                if (HasFlag(ERoundFlags.AttackedDealer, player))
                {
                    ResetFlag(ERoundFlags.AttackedDealer, player);
                    count *= 2;
                }
                for (int i = 0; i < count; i++)
                    decisions.Add(player);
            }
            int decision = RNG(0, decisions.Count);
            return decisions[decision];
        }

        public void BotRoundStart()
        {
            SetBotFlag(EBotFlag.ItemDecisionDone, false);
            SetBotFlag(EBotFlag.UsingSaw, false);
            SetBotFlag(EBotFlag.UsingMedicine, false);
            SetBotFlag(EBotFlag.SawedOff, false);
            BotShouldTargetPlayer = null;
        }

        private bool CoinFlip()
        {
            if (Settings.DunceDealer)
                return RNG(0, 2) == 0;
            int nlive = 0;
            int nblank = 0;
            foreach (EBullet bullet in GetBullets(false))
            {
                if (bullet == EBullet.Blank)
                    nblank++;
                else if (bullet == EBullet.Live)
                    nlive++;
            }
            if (nlive == nblank)
                return RNG(0, 2) == 0;
            return nlive > nblank;
        }

        private void SetKnownBullet()
        {
            SetBotFlag(EBotFlag.KnowsCurrentBullet, true);
            CurrentlyKnownBullet = GetNextBullet();
            BotShouldTargetPlayer = CurrentlyKnownBullet == EBullet.Live;
        }

        private bool FigureOutBullet()
        {
            if (BulletIsKnown[0])
                return true;
            List<EBullet> bullets = GetBullets(false);
            int nlive = 0;
            int nblank = 0;
            foreach (EBullet bullet in bullets)
            {
                if (bullet == EBullet.Blank)
                    nblank++;
                else if (bullet == EBullet.Live)
                    nlive++;
            }
            if (nlive == 0 || nblank == 0)
                return true;
            for (int i = 0; i < BulletIsKnown.Count; i++)
            {
                if (!BulletIsKnown[i])
                    continue;
                if (bullets[i] == EBullet.Blank)
                    nblank--;
                else if (bullets[i] == EBullet.Live)
                    nlive--;
            }
            return nlive == 0 || nblank == 0;
        }
    }

    public class Server
    {
        private readonly TcpListener ServerSocket;
        private readonly List<ClientWorker> Clients;
        private readonly Dictionary<string, Session> Sessions;

        public Server(int port)
        {
            Sessions = new Dictionary<string, Session>();
            Clients = new List<ClientWorker>();
            ServerSocket = new TcpListener(IPAddress.Any, port);
            ServerSocket.Start();
        }

        public void Start()
        {
            while (true)
            {
                TcpClient socket = ServerSocket.AcceptTcpClient();
                ClientWorker w = new ClientWorker(socket);
                AddWorker(w);
                w.Start();
            }
        }

        private void AddWorker(ClientWorker w)
        {
            lock (this)
            {
                Clients.Add(w);
                w.OnDisconnected += Worker_OnDisconnected;
                w.OnPacketReceived += Worker_OnPacketReceived;
            }
        }

        private bool IsInSession(ClientWorker me, ClientWorker w)
        {
            if (!w.DoesPlayerExist() || !me.DoesPlayerExist())
                return false;
            return w.GetSession() == me.GetSession();
        }

        private int Broadcast(Packet pack, Session ses, string debugname)
        {
            int n = 0;
            for (int i = 0; i < Clients.Count; i++)
            {
                ClientWorker client = Clients[i];
                try
                {
                    if (client.GetSession() != ses.GetSession())
                        continue;
                    Packet.Send(pack, client);
                    n++;
                    if (!string.IsNullOrEmpty(debugname))
                        Console.WriteLine("Sent {0} to {1}", debugname, client.GetPlayer());
                }
                catch
                {
                    Clients.RemoveAt(i--);
                    client.Dispose();
                }
            }
            return n;
        }

        private int Broadcast(Func<ClientWorker, Packet> pack, Session ses, string debugname)
        {
            int n = 0;
            for (int i = 0; i < Clients.Count; i++)
            {
                ClientWorker client = Clients[i];
                try
                {
                    if (client.GetSession() != ses.GetSession())
                        continue;
                    Packet.Send(pack(client), client);
                    n++;
                    if (!string.IsNullOrEmpty(debugname))
                        Console.WriteLine("Sent {0} to {1}", debugname, client.GetPlayer());
                }
                catch
                {
                    Clients.RemoveAt(i--);
                    client.Dispose();
                }
            }
            return n;
        }

        private int Broadcast(Packet pack, ClientWorker cli, string debugname)
        {
            int n = 0;
            for (int i = 0; i < Clients.Count; i++)
            {
                ClientWorker client = Clients[i];
                try
                {
                    if (!client.DoesPlayerExist())
                        continue;
                    if (client.GetToken() == cli.GetToken())
                        continue;
                    if (!IsInSession(cli, client))
                        continue;
                    Packet.Send(pack, client);
                    n++;
                    if (!string.IsNullOrEmpty(debugname))
                        Console.WriteLine("Sent {0} to {1}", debugname, client.GetPlayer());
                }
                catch
                {
                    Clients.RemoveAt(i--);
                    client.Dispose();
                }
            }
            return n;
        }

        private bool IsHost(ClientWorker sender)
        {
            if (!sender.DoesPlayerExist())
                return false;
            Session session = Sessions[sender.GetSession()];
            return session.GetHost() == sender.GetPlayer();
        }

        private void Worker_OnPacketReceived(ClientWorker sender, EPacket id, List<byte> data)
        {
            lock (this)
            {
                switch (id)
                {
                    case EPacket.UseItem:
                        {
                            PacketUseItem packet = new PacketUseItem(data);
                            Console.WriteLine(packet.ToString());
                            string user = packet.GetSender();
                            string target = packet.GetTarget();
                            EItem item = packet.GetItem();
                            Session session = Sessions[sender.GetSession()];
                            bool stealing = session.HasFlag(ERoundFlags.StealingItems, user);
                            if (stealing)
                                session.ResetFlag(ERoundFlags.StealingItems, user);
                            if (session.HasFlag(ERoundFlags.HasHeroineEffect, user))
                            {
                                Console.WriteLine("Rejected because of Heroine");
                                return;
                            }
                            if (stealing && item == EItem.Adrenaline)
                            {
                                if (!session.IsPlayerConnected(target))
                                {
                                    Console.WriteLine("Rejected because of invalid target");
                                    return;
                                }
                                foreach (string player in session.GetPlayers())
                                {
                                    if (session.HasFlag(ERoundFlags.StealingTarget, player))
                                    {
                                        session.ResetFlag(ERoundFlags.StealingTarget, player);
                                        break;
                                    }
                                }
                                session.SetFlag(ERoundFlags.StealingItems, user);
                                session.SetFlag(ERoundFlags.StealingTarget, target);
                                if (session.GetItemCount(target) == 0)
                                {
                                    session.ResetFlag(ERoundFlags.StealingItems, user);
                                    session.ResetFlag(ERoundFlags.StealingTarget, target);
                                }
                                Broadcast(new PacketUsedItem(user, target, session.GetItems(target)), session, "Item usage");
                                return;
                            }
                            if (!session.PlayerHasItem(user, item) && !stealing)
                            {
                                Console.WriteLine("Rejected because sender doesn't have the item");
                                return;
                            }
                            if (user == target)
                            {
                                Console.WriteLine("Rejected because sender can't target themselves");
                                return;
                            }
                            string removetarget = user;
                            foreach (string player in session.GetPlayers())
                            {
                                if (session.HasFlag(ERoundFlags.StealingTarget, player))
                                {
                                    removetarget = player;
                                    session.ResetFlag(ERoundFlags.StealingTarget, player);
                                    break;
                                }
                            }
                            string stealtarget = stealing ? removetarget : null;
                            if (!session.PlayerHasItem(removetarget, item) || (!stealing && user != removetarget))
                            {
                                Console.WriteLine("Rejected because remove target doesn't have the item");
                                return;
                            }
                            session.RemoveItem(removetarget, item);
                            EItem last = session.GetLastUsedItem();
                            session.SetLastUsedItem(item);
                            bool once = session.HasFlag(ERoundFlags.AllowOnce);
                            if (last == EItem.Trashbin)
                            {
                                EItem replacement = session.GenerateItems(user, 1, false, true);
                                Broadcast(new PacketUsedItem(user, item, stealtarget, true, replacement, false), session, "Item trashed");
                                if (session.HasFlag(ERoundFlags.HasKatanaEffect, user))
                                    session.SetFlag(ERoundFlags.AllowOnce);
                                return;
                            }
                            if (!once && session.HasFlag(ERoundFlags.HasKatanaEffect, user) && session.HasFlag(ERoundFlags.HasUsedAnything, user))
                            {
                                Console.WriteLine("Rejected because of Katana");
                                return;
                            }
                            if (once)
                                session.ResetFlag(ERoundFlags.AllowOnce);
                            bool shouldblock = false;
                            if (session.HasFlag(ERoundFlags.HasKatanaEffect, user) && !session.HasFlag(ERoundFlags.HasUsedAnything, user) && item != EItem.Adrenaline)
                            {
                                session.SetFlag(ERoundFlags.HasUsedAnything, user);
                                shouldblock = true;
                            }
                            if (once)
                                shouldblock = item != EItem.Adrenaline;
                            session.AddScore(user, 500);
                            switch (item)
                            {
                                case EItem.Handcuffs:
                                    {
                                        if (session.HasFlag(ERoundFlags.HandcuffsJustUsed))
                                        {
                                            Console.WriteLine("Rejected because handcuffs can't be stacked");
                                            return;
                                        }
                                        session.SetFlag(ERoundFlags.AgainBecauseCuffed);
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Cigarettes:
                                    {
                                        int health = session.GetHealth(user);
                                        session.SetHealth(user, health + 1);
                                        session.AddScore(user, 300);
                                        Broadcast(new PacketUsedItem(user, 1, true, stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Saw:
                                    {
                                        session.SetFlag(ERoundFlags.ShotgunSawedOff);
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Magnifying:
                                    {
                                        EBullet bullet = session.GetNextBullet();
                                        Broadcast(cli => new PacketUsedItem(user, cli.GetPlayer() == user ? bullet : EBullet.Undefined, stealtarget, 0, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Beer:
                                    {
                                        session.AddScore(user, 300);
                                        EBullet bullet = session.PopBullet();
                                        bool inverted = session.HasFlag(ERoundFlags.ShotInverted);
                                        session.ResetGlobalFlags();
                                        Broadcast(new PacketUsedItem(user, bullet, inverted, stealtarget, shouldblock), session, "Item usage");
                                        if (session.GetBulletCount() == 0)
                                        {
                                            session.RoundStart();
                                            Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems()), session, "New Round Start");
                                        }
                                    }
                                    break;
                                case EItem.Inverter:
                                    {
                                        if (session.HasFlag(ERoundFlags.ShotInverted))
                                            session.ResetFlag(ERoundFlags.ShotInverted);
                                        else
                                            session.SetFlag(ERoundFlags.ShotInverted);
                                        session.InvertBullet();
                                        Broadcast(cli =>
                                        {
                                            EBullet bullet = EBullet.Undefined;
                                            if (cli.GetPlayer() == user && session.UsingBetaInverters())
                                                bullet = session.GetNextBullet();
                                            return new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock, bullet);
                                        }, session, "Item usage");
                                    }
                                    break;
                                case EItem.Medicine:
                                    {
                                        int health = session.GetHealth(user);
                                        int modifier = 2;
                                        if (session.RNG(0, 2) == 0)
                                            modifier = -1;
                                        session.AddScore(user, modifier == -1 ? -300 : 400);
                                        session.SetHealth(user, health + modifier);
                                        Broadcast(new PacketUsedItem(user, modifier, false, stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Phone:
                                    {
                                        List<EBullet> bullets = session.GetBullets(false);
                                        int index = -1;
                                        if (bullets.Count > 1)
                                            index = session.RNG(1, bullets.Count);
                                        EBullet bullet = index == -1 ? EBullet.Undefined : bullets[index];
                                        Broadcast(cli => new PacketUsedItem(user, cli.GetPlayer() == user ? bullet : EBullet.Undefined, stealtarget, index, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Adrenaline:
                                    {
                                        if (!session.IsPlayerConnected(target))
                                        {
                                            Console.WriteLine("Rejected because of invalid target");
                                            return;
                                        }
                                        session.SetFlag(ERoundFlags.StealingItems, user);
                                        session.SetFlag(ERoundFlags.StealingTarget, target);
                                        if (session.HasFlag(ERoundFlags.HasKatanaEffect, user))
                                            session.SetFlag(ERoundFlags.AllowOnce);
                                        if (session.GetItemCount(target) == 0)
                                        {
                                            session.ResetFlag(ERoundFlags.StealingItems, user);
                                            session.ResetFlag(ERoundFlags.StealingTarget, target);
                                        }
                                        Broadcast(new PacketUsedItem(user, target, session.GetItems(target), shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Magazine:
                                    {
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                        session.RoundStart(false, true);
                                        Broadcast(new PacketStartRound(session.GetBullets(true), null, null, true), session, "New Round Start");
                                    }
                                    break;
                                case EItem.Gunpowder:
                                    {
                                        session.SetFlag(ERoundFlags.ShotGunpowdered);
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Bullet:
                                    {
                                        session.PushBullet();
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Trashbin:
                                    {
                                        if (session.GetItemCount(user) <= 0)
                                            session.SetLastUsedItem(last);
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, false), session, "Item usage");
                                    }
                                    break;
                                case EItem.Heroine:
                                    {
                                        if (!session.IsPlayerConnected(target))
                                        {
                                            Console.WriteLine("Rejected because of invalid target");
                                            return;
                                        }
                                        if (!session.HasFlag(ERoundFlags.HasHeroineEffect, target))
                                            session.SetFlag(ERoundFlags.HasHeroineEffect, target);
                                        Broadcast(new PacketUsedItem(user, target, item, stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Katana:
                                    {
                                        if (!session.IsPlayerConnected(target))
                                        {
                                            Console.WriteLine("Rejected because of invalid target");
                                            return;
                                        }
                                        if (!session.HasFlag(ERoundFlags.HasKatanaEffect, target))
                                            session.SetFlag(ERoundFlags.HasKatanaEffect, target);
                                        Broadcast(new PacketUsedItem(user, target, item, stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Swapper:
                                    {
                                        if (!session.IsPlayerConnected(target))
                                        {
                                            Console.WriteLine("Rejected because of invalid target");
                                            return;
                                        }
                                        session.SwapItems(user, target);
                                        Broadcast(new PacketUsedItem(user, target, session.GetItems(user), session.GetItems(target), stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Hat:
                                    Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    break;
                                case EItem.Snus:
                                    {
                                        session.SetFlag(ERoundFlags.RepeatedHealing, user);
                                        session.SetFlag(ERoundFlags.RepeatedHealingJustUsed, user);
                                        Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Elfbar:
                                    {
                                        session.IncreaseMaxHealth();
                                        Broadcast(new PacketUsedItem(user, session.GetMaxHealth(), stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Scope:
                                    {
                                        EBullet[] bullets = session.GetNextBullets().ToArray();
                                        if (session.RNG(0, 3) == 0)
                                        {
                                            for (int i = 0; i < bullets.Length; i++)
                                            {
                                                if (bullets[i] == EBullet.Live)
                                                    bullets[i] = EBullet.Blank;
                                                else if (bullets[i] == EBullet.Blank)
                                                    bullets[i] = EBullet.Live;
                                            }
                                        }
                                        Broadcast(new PacketUsedItem(user, bullets, stealtarget, shouldblock), session, "Item usage");
                                    }
                                    break;
                                case EItem.Remote:
                                    session.InvertRound();
                                    Broadcast(new PacketUsedItem(user, item, stealtarget, false, EItem.Nothing, shouldblock), session, "Item usage");
                                    break;
                            }
                        }
                        break;
                    case EPacket.Shoot:
                        {
                            PacketShoot packet = new PacketShoot(data);
                            Console.WriteLine(packet.ToString());
                            Session session = Sessions[sender.GetSession()];
                            string actualsender = packet.GetSender();
                            if (actualsender != sender.GetPlayer())
                            {
                                Console.WriteLine("Rejected because sender doesn't match");
                                return;
                            }
                            if (session.GetCurrentPlayer() != sender.GetPlayer())
                            {
                                Console.WriteLine("Rejected because player isn't in control");
                                return;
                            }
                            if (packet.GetFlags() != ERoundFlags.None)
                            {
                                Console.WriteLine("Rejected because of invalid round flags");
                                return;
                            }
                            string target = packet.GetTarget();
                            if (session.IsDead(target))
                            {
                                Console.WriteLine("Rejected because target is dead");
                                return;
                            }
                            EBullet type = session.PopBullet();
                            if (actualsender == target && type == EBullet.Blank)
                            {
                                session.SetAgain();
                                session.AddScore(actualsender, 300);
                            }
                            else if (actualsender == target && type == EBullet.Live)
                                session.AddScore(actualsender, -500);
                            if ((actualsender == target && type == EBullet.Blank) || (actualsender != target && type == EBullet.Live))
                                session.AddScore(actualsender, 300);
                            bool backfired = false;
                            int damage = 0;
                            if (type == EBullet.Live)
                            {
                                damage = 1;
                                if (session.HasFlag(ERoundFlags.ShotgunSawedOff))
                                    damage++;
                                if (session.HasFlag(ERoundFlags.ShotGunpowdered))
                                {
                                    damage += 2;
                                    if (session.RNG(0, 2) == 0)
                                    {
                                        target = actualsender;
                                        backfired = true;
                                        damage--;
                                    }
                                }
                            }
                            int health = session.GetHealth(target);
                            health -= damage;
                            if (health < 0)
                                health = 0;
                            if (health == 0)
                                session.AddScore(actualsender, 1000);
                            else if (actualsender == target)
                                session.AddScore(actualsender, 200);
                            switch (damage)
                            {
                                case 1:
                                    session.AddScore(actualsender, 500);
                                    break;
                                case 2:
                                    session.AddScore(actualsender, 800);
                                    break;
                                case 3:
                                    session.AddScore(actualsender, backfired ? -400 : 1000);
                                    break;
                                case 4:
                                    session.AddScore(actualsender, backfired ? -800 : 2000);
                                    break;
                            }
                            session.SetHealth(target, health);
                            if (damage > 0)
                            {
                                session.ResetFlag(ERoundFlags.RepeatedHealing, target);
                                session.AddScore(target, -50);
                                if (session.IsBot(target))
                                    session.SetFlag(ERoundFlags.AttackedDealer, actualsender);
                            }
                            ERoundFlags flags = session.GetRoundFlags();
                            if (backfired)
                                flags |= ERoundFlags.GunpowderBackfired;
                            if (session.HasFlag(ERoundFlags.ShotInverted))
                                flags |= ERoundFlags.ShotInverted;
                            bool cuffed = session.HasFlag(ERoundFlags.AgainBecauseCuffed);
                            session.ResetGlobalFlags();
                            Broadcast(new PacketShoot(actualsender, target, flags, type), session, "Shooting");
                            if (session.HasFlag(ERoundFlags.HasHeroineEffect, actualsender))
                                session.ResetFlag(ERoundFlags.HasHeroineEffect, actualsender);
                            if (session.HasFlag(ERoundFlags.HasKatanaEffect, actualsender))
                                session.ResetFlag(ERoundFlags.HasKatanaEffect, actualsender);
                            if (session.HasFlag(ERoundFlags.HasUsedAnything, actualsender))
                                session.ResetFlag(ERoundFlags.HasUsedAnything, actualsender);
                            if (session.HasFlag(ERoundFlags.AllowOnce))
                                session.ResetFlag(ERoundFlags.AllowOnce);
                            if (session.GetBulletCount() == 0)
                            {
                                session.RoundStart();
                                Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems()), session, "New Round Start");
                            }
                            while (true)
                            {
                                if (session.ShouldSwitchPlayer())
                                {
                                    bool botcuffed = false;
                                    if (session.IsBotFlagSet(Session.EBotFlag.CuffedPlayer))
                                    {
                                        session.SetBotFlag(Session.EBotFlag.CuffedPlayer, false);
                                        cuffed = true;
                                        botcuffed = true;
                                    }
                                    if (cuffed)
                                    {
                                        session.ResetFlag(ERoundFlags.AgainBecauseCuffed);
                                        if (!botcuffed)
                                            session.SetFlag(ERoundFlags.HandcuffsJustUsed);
                                        cuffed = false;
                                    }
                                    else
                                    {
                                        session.SwitchPlayer();
                                        List<string> heal = session.GetRepeatedHealing();
                                        if (heal.Count > 0)
                                        {
                                            List<string> targets = new List<string>();
                                            foreach (string player in heal)
                                            {
                                                if (session.HasFlag(ERoundFlags.RepeatedHealingJustUsed, player))
                                                {
                                                    session.ResetFlag(ERoundFlags.RepeatedHealingJustUsed, player);
                                                    continue;
                                                }
                                                session.SetHealth(player, session.GetHealth(player) + 2);
                                                targets.Add(player);
                                                session.AddScore(player, 50);
                                            }
                                            Broadcast(new PacketRoundHeal(targets, 2), session, "Round heal");
                                        }
                                    }
                                }
                                string nextplayer = session.GetCurrentPlayer();
                                foreach (string player in session.GetPlayers())
                                    if (!session.IsDead(player))
                                        session.AddScore(player, 500);
                                if (session.GetNumAlivePlayers() == 1)
                                {
                                    string winner = session.GetWinner();
                                    session.AddScore(winner, 5000);
                                    Broadcast(cli => new PacketEndGame(winner, session.GetScore(cli.GetPlayer())), session, "Game over");
                                    session.Unlock();
                                    return;
                                }
                                while (session.GetHealth(nextplayer) <= 0)
                                {
                                    session.SwitchPlayer();
                                    nextplayer = session.GetCurrentPlayer();
                                }
                                bool isbot = session.BotExists() && session.IsBot(nextplayer);
                                Broadcast(new PacketPassControl(nextplayer), session, "Pass Control");
                                if (!isbot)
                                    break;
                                List<Packet> packets = new List<Packet>();
                                List<Dictionary<string, Packet>> playerpackets = new List<Dictionary<string, Packet>>();
                                session.BotTurn(packets, playerpackets);
                                int index = 0;
                                foreach (Packet pack in packets)
                                {
                                    Thread.Sleep(2000);
                                    if (pack == null)
                                    {
                                        Broadcast(cli => playerpackets[index][cli.GetPlayer()], session, "New Round Start");
                                        index++;
                                        continue;
                                    }
                                    Broadcast(pack, session, "Dealer Sync");
                                }
                            }
                        }
                        break;
                    case EPacket.StartGame:
                        {
                            PacketStartGame packet = new PacketStartGame(data);
                            Console.WriteLine(packet.ToString());
                            if (!IsHost(sender))
                            {
                                Console.WriteLine("Rejected because player isn't the host");
                                return;
                            }
                            Session session = Sessions[sender.GetSession()];
                            session.ResetGame();
                            session.Lock();
                            session.SetFirstPlayer();
                            Broadcast(packet, sender, "Game Start");
                            session.RoundStart(true);
                            int health = session.GetMaxHealth();
                            string firstplayer = session.GetCurrentPlayer();
                            EMusic music = EMusic.BackgroundBearing;
                            do music = (EMusic)session.RNG((int)EMusic.Undefined + 1, (int)EMusic.Count);
                            while (music == EMusic.Title || music == EMusic.Gameover);
                            Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems(), false, music, health), session, "Round Start");
                            Broadcast(new PacketPassControl(firstplayer), session, "Pass Control");
                        }
                        break;
                    case EPacket.UpdateSettings:
                        {
                            PacketUpdateSettings packet = new PacketUpdateSettings(data);
                            Console.WriteLine(packet.ToString());
                            Session session = Sessions[sender.GetSession()];
                            if (!IsHost(sender))
                            {
                                Console.WriteLine("Rejected because player isn't the host");
                                return;
                            }
                            session.UpdateSettings(packet.GetSettings());
                            if (packet.GetSettings().BotDealer && !session.IsLocked())
                            {
                                if (session.AddBot())
                                    Broadcast(new PacketNewPlayer(session.GetBotName()), session, "Bot Join Sync");
                            }
                            else
                            {
                                if (session.RemoveBot())
                                    Broadcast(new PacketRemoveLocalPlayer(session.GetBotName(), null), session, "Bot Local Removal");
                            }
                        }
                        break;
                    case EPacket.Disconnected:
                        {
                            PacketDisconnected packet = new PacketDisconnected();
                            Console.WriteLine(packet.ToString());
                            if (sender.DoesPlayerExist())
                            {
                                Session session = Sessions[sender.GetSession()];
                                string player = sender.GetPlayer();
                                session.RemovePlayer(player);
                                Console.WriteLine("Removed " + player + " from Session " + sender.GetSession());
                                bool didMigrate = false;
                                bool destroyed = false;
                                if (session.ShouldBeDestroyed())
                                {
                                    Console.WriteLine("Destroying Session because no Players are left");
                                    Sessions.Remove(sender.GetSession());
                                    destroyed = true;
                                }
                                if (!destroyed && !IsHost(sender))
                                    session.FixHostQueue(player);
                                if (!destroyed && IsHost(sender))
                                {
                                    do session.MigrateHost();
                                    while (!session.IsPlayerConnected(session.GetHost()));
                                    Console.WriteLine("New Host: " + session.GetHost());
                                    didMigrate = true;
                                }
                                if (didMigrate)
                                    session.ResetSettings();
                                if (!destroyed)
                                {
                                    Broadcast(new PacketRemoveLocalPlayer(player, didMigrate ? session.GetHost() : null), sender, "Local Removal");
                                    if (session.GetNumAlivePlayers() == 1)
                                    {
                                        string winner = session.GetWinner();
                                        session.AddScore(winner, 5000);
                                        Broadcast(cli => new PacketEndGame(winner, session.GetScore(cli.GetPlayer())), session, "Game over");
                                        session.Unlock();
                                        return;
                                    }
                                }
                                while (true)
                                {
                                    session.ShouldSwitchPlayer();
                                    session.ResetFlag(ERoundFlags.AgainBecauseCuffed);
                                    session.ResetFlag(ERoundFlags.HandcuffsJustUsed);
                                    session.SwitchPlayer();
                                    string nextplayer = session.GetCurrentPlayer();
                                    if (session.GetNumAlivePlayers() == 1)
                                    {
                                        string winner = session.GetWinner();
                                        session.AddScore(winner, 5000);
                                        Broadcast(cli => new PacketEndGame(winner, session.GetScore(cli.GetPlayer())), session, "Game over");
                                        session.Unlock();
                                        return;
                                    }
                                    while (session.GetHealth(nextplayer) <= 0)
                                    {
                                        session.SwitchPlayer();
                                        nextplayer = session.GetCurrentPlayer();
                                    }
                                    bool isbot = session.BotExists() && session.IsBot(nextplayer);
                                    if (!isbot)
                                    {
                                        Broadcast(new PacketPassControl(nextplayer), session, "Pass Control");
                                        break;
                                    }
                                    else
                                    {
                                        List<Packet> packets = new List<Packet>();
                                        List<Dictionary<string, Packet>> playerpackets = new List<Dictionary<string, Packet>>();
                                        session.BotTurn(packets, playerpackets);
                                        int index = 0;
                                        foreach (Packet pack in packets)
                                        {
                                            if (pack == null)
                                            {
                                                Broadcast(cli => playerpackets[index][cli.GetPlayer()], session, "New Round Start");
                                                index++;
                                                continue;
                                            }
                                            Broadcast(pack, session, "Dealer Sync");
                                            Thread.Sleep(1000);
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Disconnected pending Player");
                            Clients.Remove(sender);
                            sender.Dispose();
                        }
                        break;
                    case EPacket.JoinRequest:
                        {
                            PacketJoinRequest packet = new PacketJoinRequest(data);
                            Console.WriteLine(packet.ToString());
                            string session = packet.GetSession();
                            string player = packet.GetPlayer();
                            bool hosting = packet.IsHosting();
                            string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-%&/[]()?!.,# ";
                            EJoinResponse response = EJoinResponse.Pending;
                            foreach (char c in session)
                            {
                                if (!allowed.Contains(c + ""))
                                {
                                    response = EJoinResponse.FailedInvalidSessionName;
                                    break;
                                }
                            }
                            if (!hosting && !Sessions.ContainsKey(session))
                                response = EJoinResponse.FailedInvalidSession;
                            if (response == EJoinResponse.Pending)
                            {
                                if (player == "Dealer" || player == "God")
                                    response = EJoinResponse.FailedPlayerNameAlreadyUsed;
                                foreach (char c in player)
                                {
                                    if (!allowed.Contains(c + ""))
                                    {
                                        response = EJoinResponse.FailedInvalidPlayerName;
                                        break;
                                    }
                                }
                                if (Sessions.ContainsKey(session) && Sessions[session].IsPlayerConnected(player))
                                    response = EJoinResponse.FailedPlayerNameAlreadyUsed;
                            }
                            if (response == EJoinResponse.Pending)
                                if (Sessions.ContainsKey(session))
                                    if (Sessions[session].IsLocked())
                                        response = EJoinResponse.FailedLocked;
                            if (response == EJoinResponse.Pending)
                            {
                                if (Sessions.ContainsKey(session))
                                    response = EJoinResponse.Succeeded;
                                else
                                    response = EJoinResponse.SucceededHost;
                            }
                            if (response == EJoinResponse.SucceededHost)
                                Sessions.Add(session, new Session(player, session));
                            else if (response == EJoinResponse.Succeeded)
                                Sessions[session].AddPlayer(player);
                            if (response == EJoinResponse.Succeeded || response == EJoinResponse.SucceededHost)
                            {
                                Console.WriteLine("Success " + response.ToString());
                                Packet.Send(new PacketJoinResponse(session, Sessions[session].GetHost(), Sessions[session].GetPlayers(), response), sender);
                                if (!sender.DoesPlayerExist())
                                {
                                    sender.AssignData(player, session);
                                    Console.WriteLine("Assigned Data: " + player + ", " + session);
                                }
                                if (response == EJoinResponse.Succeeded)
                                    Broadcast(new PacketNewPlayer(player), sender, "Join Sync");
                            }
                            else
                            {
                                Console.WriteLine("Fail " + response.ToString());
                                Packet.Send(new PacketJoinResponse(session, "INVALID", new List<string>(), response), sender);
                            }
                        }
                        break;
                }
            }
        }

        private void Worker_OnDisconnected(ClientWorker sender)
        {
            lock (this)
            {
                sender.OnDisconnected -= Worker_OnDisconnected;
                sender.OnPacketReceived -= Worker_OnPacketReceived;
                Clients.Remove(sender);
                sender.Dispose();
            }
        }
    }

    public class Program
    {
        private static void Main()
        {
            Server s = new Server(19121);
            s.Start();
        }
    }
}