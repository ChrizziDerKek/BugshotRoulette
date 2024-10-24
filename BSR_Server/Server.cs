using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Linq;
using System.Data;
using System.Threading;

#pragma warning disable IDE0044
#pragma warning disable IDE0058

namespace Server
{
    public class Session
    {
        private string Host;
        private List<string> Players;
        private Queue<string> NextHosts;
        private bool Locked;
        private SettingsData Settings;
        private int CurrentPlayer;
        private Random RNG;
        private Dictionary<string, EItem[]> PlayerItems;
        private Dictionary<string, int> PlayerHealth;
        private List<EBullet> ActualBullets;
        private List<EBullet> DisplayedBullets;
        private string Code;
        private int StartLives;
        private bool CanGoAgain;
        private Dictionary<string, List<EItem>> LastGeneratedItems;
        private EShotFlags NextBulletFlags;
        private EItem LastUsedItem;

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
            RNG = new Random();
            PlayerItems = new Dictionary<string, EItem[]>();
            ActualBullets = new List<EBullet>();
            DisplayedBullets = new List<EBullet>();
            Code = code;
            StartLives = 0;
            CanGoAgain = false;
            PlayerHealth = new Dictionary<string, int>();
            LastGeneratedItems = new Dictionary<string, List<EItem>>();
            NextBulletFlags = EShotFlags.None;
            LastUsedItem = EItem.Nothing;
        }

        public void SetLastUsedItem(EItem item) => LastUsedItem = item;

        public EItem GetLastUsedItem() => LastUsedItem;

        public void InvertBullet() => ActualBullets[0] = ActualBullets[0] == EBullet.Live ? EBullet.Blank : EBullet.Live;

        public bool BulletHasFlag(EShotFlags flag) => (NextBulletFlags & flag) != 0;

        public void SetBulletFlag(EShotFlags flag) => NextBulletFlags |= flag;

        public void ResetBulletFlag(EShotFlags flag) => NextBulletFlags &= ~flag;

        public EShotFlags GetBulletFlags() => NextBulletFlags;

        public void ResetBulletFlags(bool everything = false)
        {
            if (everything)
            {
                NextBulletFlags = EShotFlags.None;
                return;
            }
            bool again = BulletHasFlag(EShotFlags.AgainBecauseCuffed);
            bool used = BulletHasFlag(EShotFlags.HandcuffsJustUsed);
            NextBulletFlags = EShotFlags.None;
            if (again)
                SetBulletFlag(EShotFlags.AgainBecauseCuffed);
            if (used)
                SetBulletFlag(EShotFlags.HandcuffsJustUsed);
        }

        public Dictionary<string, List<EItem>> GetLastGeneratedItems() => LastGeneratedItems;

        public Random GetRNG() => RNG;

        public int GetHealth(string player) => PlayerHealth[player];

        public void SetHealth(string player, int health) => PlayerHealth[player] = health;

        public EBullet PopBullet()
        {
            if (ActualBullets.Count == 0)
                return EBullet.Undefined;
            EBullet bullet = ActualBullets[0];
            ActualBullets.RemoveAt(0);
            return bullet;
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

        public void RemovePlayer(string player)
        {
            Players.Remove(player);
            PlayerItems.Remove(player);
            PlayerHealth.Remove(player);
            if (Players.Count < Settings.MaxPlayers)
                Locked = false;
        }

        public void FixHostQueue(string player) => NextHosts = new Queue<string>(NextHosts.Where(h => h != player));

        public string GetHost() => Host;

        public List<string> GetPlayers() => Players;

        public int GetPlayerCount() => Players.Count;

        public bool ShouldBeDestroyed() => NextHosts.Count == 0;

        public void Lock() => Locked = true;

        public bool IsLocked() => Locked;

        public int GetMaxPlayers() => Settings.MaxPlayers;

        public void UpdateSettings(SettingsData s) => Settings = s;

        public void ResetSettings() => Settings = new SettingsData();

        public void SetFirstPlayer() => CurrentPlayer = RNG.Next(0, Players.Count);

        public int GetMaxHealth() => StartLives;

        public string GetCurrentPlayer() => Players[CurrentPlayer];

        public int SwitchPlayer()
        {
            SetLastUsedItem(EItem.Nothing);
            ResetBulletFlags(true);
            CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            return CurrentPlayer;
        }

        public void RoundStart(bool initial = false, bool noitems = false)
        {
            int nitems = RNG.Next(Settings.MinItems, Settings.MaxItems + 1);
            GenerateBullets();
            if (!noitems)
            {
                LastGeneratedItems.Clear();
                foreach (string player in Players)
                    GenerateItems(player, nitems, false, false);
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
        }

        public void PushBullet()
        {
            bool live = RNG.Next(0, 2) == 0;
            ActualBullets.Add(live ? EBullet.Live : EBullet.Blank);
            DisplayedBullets.Add(EBullet.Undefined);
        }

        private void GenerateLives() => StartLives = RNG.Next(Settings.MinHealth, Settings.MaxHealth + 1);

        private void GenerateBullets()
        {
            ActualBullets.Clear();
            DisplayedBullets.Clear();
            int min = Settings.MinBullets;
            int max = Settings.MaxBullets;
            int even = RNG.Next(0, 100);
            int ntotal = RNG.Next(min, max + 1);
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
                    nblank = RNG.Next(1, ntotal);
                    break;
                case 5:
                case 6:
                    nblank = RNG.Next(2, 4);
                    break;
                case 7:
                    nblank = RNG.Next(3, 5);
                    break;
                case 8:
                    nblank = RNG.Next(3, 6);
                    break;
            }
            int nlive = ntotal - nblank;
            for (int i = 0; i < nblank; i++)
            {
                ActualBullets.Add(EBullet.Blank);
                DisplayedBullets.Add(EBullet.Blank);
            }
            for (int i = 0; i < nlive; i++)
            {
                ActualBullets.Add(EBullet.Live);
                DisplayedBullets.Add(EBullet.Live);
            }
            ShuffleBullets();
        }

        public EItem GenerateItems(string player, int count, bool bot, bool bypasslimits)
        {
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
                    item = (EItem)RNG.Next(start, end);
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
                    if ((item == EItem.Heroine || item == EItem.Katana) && RNG.Next(0, 5) != 0)
                        item = (EItem)RNG.Next(start, end);
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

        public void RemoveItem(string player, EItem item)
        {
            for (int i = 0; i < PlayerItems[player].Length; i++)
            {
                if (PlayerItems[player][i] == item)
                {
                    PlayerItems[player][i] = EItem.Nothing;
                    break;
                }
            }
        }

        public int GetItemCount(string player, EItem item = EItem.Count)
        {
            if (!PlayerItems.TryGetValue(player, out EItem[] items))
                return 0;
            int count = 0;
            foreach (EItem it in items)
                if (item == EItem.Count || it == item)
                    count++;
            return count;
        }

        private void ShuffleBullets()
        {
            int n = ActualBullets.Count;
            while (n > 1)
            {
                int r = RNG.Next(n--);
                (ActualBullets[r], ActualBullets[n]) = (ActualBullets[n], ActualBullets[r]);
            }
            n = DisplayedBullets.Count;
            while (n > 1)
            {
                int r = RNG.Next(n--);
                (DisplayedBullets[r], DisplayedBullets[n]) = (DisplayedBullets[n], DisplayedBullets[r]);
            }
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
                            if (!session.PlayerHasItem(user, item))
                            {
                                Console.WriteLine("Rejected because sender doesn't have the item");
                                return;
                            }
                            session.RemoveItem(user, item);
                            EItem last = session.GetLastUsedItem();
                            session.SetLastUsedItem(item);
                            if (last == EItem.Trashbin)
                            {
                                EItem replacement = session.GenerateItems(user, 1, false, true);
                                Broadcast(new PacketUsedItem(user, item, true, replacement), session, "Item trashed");
                                return;
                            }
                            switch (item)
                            {
                                case EItem.Handcuffs:
                                    {
                                        if (session.BulletHasFlag(EShotFlags.HandcuffsJustUsed))
                                        {
                                            Console.WriteLine("Rejected because handcuffs cannot be stacked");
                                            return;
                                        }
                                        session.SetBulletFlag(EShotFlags.AgainBecauseCuffed);
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Cigarettes:
                                    {
                                        int health = session.GetHealth(user);
                                        session.SetHealth(user, health + 1);
                                        Broadcast(new PacketUsedItem(user, 1, true), session, "Item usage");
                                    }
                                    break;
                                case EItem.Saw:
                                    {
                                        session.SetBulletFlag(EShotFlags.SawedOff);
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Magnifying:
                                    {
                                        EBullet bullet = session.GetNextBullet();
                                        Broadcast(cli => new PacketUsedItem(user, cli.GetPlayer() == user ? bullet : EBullet.Undefined), session, "Item usage");
                                    }
                                    break;
                                case EItem.Beer:
                                    {
                                        EBullet bullet = session.PopBullet();
                                        bool inverted = session.BulletHasFlag(EShotFlags.Inverted);
                                        session.ResetBulletFlags();
                                        Broadcast(new PacketUsedItem(user, bullet, inverted), session, "Item usage");
                                        Thread.Sleep(100);
                                        if (session.GetBulletCount() == 0)
                                        {
                                            session.RoundStart();
                                            Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems()), session, "New Round Start");
                                        }
                                    }
                                    break;
                                case EItem.Inverter:
                                    {
                                        if (session.BulletHasFlag(EShotFlags.Inverted))
                                            session.ResetBulletFlag(EShotFlags.Inverted);
                                        else
                                            session.SetBulletFlag(EShotFlags.Inverted);
                                        session.InvertBullet();
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Medicine:
                                    {
                                        int health = session.GetHealth(user);
                                        int modifier = 2;
                                        if (session.GetRNG().Next(0, 2) == 0)
                                            modifier = -1;
                                        session.SetHealth(user, health + modifier);
                                        Broadcast(new PacketUsedItem(user, modifier, false), session, "Item usage");
                                    }
                                    break;
                                case EItem.Phone:
                                    {
                                        List<EBullet> bullets = session.GetBullets(false);
                                        int index = -1;
                                        if (bullets.Count > 1)
                                            index = session.GetRNG().Next(1, bullets.Count);
                                        EBullet bullet = index == -1 ? EBullet.Undefined : bullets[index];
                                        Broadcast(cli => new PacketUsedItem(user, cli.GetPlayer() == user ? bullet : EBullet.Undefined, index), session, "Item usage");
                                    }
                                    break;
                                case EItem.Adrenaline:
                                    break;
                                case EItem.Magazine:
                                    {
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                        Thread.Sleep(100);
                                        session.RoundStart(false, true);
                                        Broadcast(new PacketStartRound(session.GetBullets(true), null, null, true), session, "New Round Start");
                                    }
                                    break;
                                case EItem.Gunpowder:
                                    {
                                        session.SetBulletFlag(EShotFlags.Gunpowdered);
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Bullet:
                                    {
                                        session.PushBullet();
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Trashbin:
                                    {
                                        if (session.GetItemCount(user) <= 0)
                                            session.SetLastUsedItem(last);
                                        Broadcast(new PacketUsedItem(user, item), session, "Item usage");
                                    }
                                    break;
                                case EItem.Heroine:
                                    break;
                                case EItem.Katana:
                                    break;
                                case EItem.Swapper:
                                    break;
                                case EItem.Hat:
                                    Broadcast(new PacketUsedItem(user, item), session, "Item usage");
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
                            if (packet.GetFlags() != EShotFlags.None)
                            {
                                Console.WriteLine("Rejected because of invalid bullet flags");
                                return;
                            }
                            string target = packet.GetTarget();
                            EBullet type = session.PopBullet();
                            if (actualsender == target && type == EBullet.Blank)
                                session.SetAgain();
                            bool backfired = false;
                            int damage = 0;
                            if (type == EBullet.Live)
                            {
                                damage = 1;
                                if (session.BulletHasFlag(EShotFlags.SawedOff))
                                    damage++;
                                if (session.BulletHasFlag(EShotFlags.Gunpowdered))
                                {
                                    damage += 2;
                                    if (session.GetRNG().Next(0, 2) == 0)
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
                            session.SetHealth(target, health);
                            EShotFlags flags = session.GetBulletFlags();
                            if (backfired)
                                flags |= EShotFlags.GunpowderBackfired;
                            if (session.BulletHasFlag(EShotFlags.Inverted))
                                flags |= EShotFlags.Inverted;
                            bool cuffed = session.BulletHasFlag(EShotFlags.AgainBecauseCuffed);
                            session.ResetBulletFlags();
                            Broadcast(new PacketShoot(actualsender, target, flags, type), session, "Shooting");
                            if (session.GetBulletCount() == 0)
                            {
                                session.RoundStart();
                                Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems()), session, "New Round Start");
                            }
                            Thread.Sleep(100);
                            if (session.ShouldSwitchPlayer())
                            {
                                if (cuffed)
                                {
                                    session.ResetBulletFlag(EShotFlags.AgainBecauseCuffed);
                                    session.SetBulletFlag(EShotFlags.HandcuffsJustUsed);
                                }
                                else session.SwitchPlayer();
                            }
                            string nextplayer = session.GetCurrentPlayer();
                            Broadcast(new PacketPassControl(nextplayer), session, "Pass Control");
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
                            session.Lock();
                            session.SetFirstPlayer();
                            Broadcast(packet, sender, "Game Start");
                            session.RoundStart(true);
                            int health = session.GetMaxHealth();
                            string firstplayer = session.GetCurrentPlayer();
                            bool intense = session.GetRNG().Next(0, 2) == 0;
                            Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), session.GetLastGeneratedItems(), false, intense, health), session, "Round Start");
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
                                    Broadcast(new PacketRemoveLocalPlayer(player, didMigrate ? session.GetHost() : null), sender, "Local Removal");
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