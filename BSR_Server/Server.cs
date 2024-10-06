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

#pragma warning disable IDE0044
#pragma warning disable IDE0058

namespace Server
{
    public class Session
    {
        private string Host; //Player who created the session and can start the game
        private List<string> Players; //All connected players
        private Queue<string> NextHosts; //Backup hosts that will become host when the current one leaves
        private bool Locked; //True if the session is full
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
        private int ExpectedDamage;

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
            ExpectedDamage = 0;
        }

        public void ExpectDamage(int damage) => ExpectedDamage = damage;

        public int GetExpectedDamage() => ExpectedDamage;

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

        public void SetAgain(bool again) => CanGoAgain = again;

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
            CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            return CurrentPlayer;
        }

        public void RoundStart(bool initial)
        {
            int nitems = RNG.Next(Settings.MinItems, Settings.MaxItems + 1);
            GenerateBullets();
            foreach (string player in Players)
                GenerateItems(player, nitems, false);
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

        private void GenerateItems(string player, int count, bool bot)
        {
            if (Settings.NoItems)
                return;
            for (int i = 0; i < count; i++)
            {
                int start = (int)EItem.Nothing + 1;
                int end = (int)EItem.Count;
                if (Settings.OriginalItemsOnly || bot)
                    end = (int)EItem.Adrenaline + 1;
                int attempts = 0;
                EItem item;
                do
                {
                    if (attempts++ > 100)
                    {
                        item = EItem.Nothing;
                        break;
                    }
                    item = (EItem)RNG.Next(start, end);
                    if ((item == EItem.Heroine || item == EItem.Katana) && RNG.Next(0, 5) != 0)
                        item = (EItem)RNG.Next(start, end);
                }
                while (ItemLimits.TryGetValue(item, out int limit) && GetItemCount(player, item) >= limit);
                if (item == EItem.Nothing)
                    break;
                if (!PlayerItems.ContainsKey(player))
                {
                    PlayerItems.Add(player, new EItem[8]);
                    for (int j = 0; j < 8; j++)
                        PlayerItems[player][j] = EItem.Nothing;
                }
                for (int j = 0; j < 8; j++)
                {
                    if (PlayerItems[player][j] == EItem.Nothing)
                    {
                        PlayerItems[player][j] = item;
                        break;
                    }
                }
            }
        }

        public List<EBullet> GetBullets(bool display) => display ? DisplayedBullets : ActualBullets;

        public EItem[] GetItems(string player) => PlayerItems[player];

        private int GetItemCount(string player, EItem item)
        {
            if (!PlayerItems.TryGetValue(player, out EItem[] items))
                return 0;
            int count = 0;
            foreach (EItem it in items)
                if (it == item)
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
        private readonly TcpListener ServerSocket; //Server listener
        private readonly List<ClientWorker> Clients; //Connected clients
        private readonly Dictionary<string, Session> Sessions; //Opened sessions

        /// <summary>
        /// Creates a new server
        /// </summary>
        /// <param name="port">Port to listen to</param>
        public Server(int port)
        {
            Sessions = new Dictionary<string, Session>();
            Clients = new List<ClientWorker>();
            ServerSocket = new TcpListener(IPAddress.Any, port);
            ServerSocket.Start();
        }

        /// <summary>
        /// Starts receiving and listening for clients
        /// </summary>
        public void Start()
        {
            while (true)
            {
                //Will halt the program until a new client connects
                TcpClient socket = ServerSocket.AcceptTcpClient();
                //Create a worker, add it to the list and start receiving
                ClientWorker w = new ClientWorker(socket);
                AddWorker(w);
                w.Start();
            }
        }

        /// <summary>
        /// Adds a client to the list of connected clients
        /// </summary>
        /// <param name="w">Client</param>
        private void AddWorker(ClientWorker w)
        {
            lock (this)
            {
                //Adds a client to the list of clients and assigns events
                Clients.Add(w);
                w.OnDisconnected += Worker_OnDisconnected;
                w.OnPacketReceived += Worker_OnPacketReceived;
            }
        }

        /// <summary>
        /// Checks if the clients are in the same session
        /// </summary>
        /// <param name="me">First client</param>
        /// <param name="w">Second client</param>
        /// <returns>True if the session matches</returns>
        private bool IsInSession(ClientWorker me, ClientWorker w)
        {
            //Pending clients are never in a session
            if (!w.DoesPlayerExist() || !me.DoesPlayerExist())
                return false;
            //Check if the client sessions match
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

        /// <summary>
        /// Sends a packet to all clients that are connected to your session
        /// </summary>
        /// <param name="pack">Packet to send</param>
        /// <param name="cli">Your client</param>
        /// <param name="debugname">Debug log message</param>
        /// <returns>Number of clients that received the packet</returns>
        private int Broadcast(Packet pack, ClientWorker cli, string debugname)
        {
            int n = 0;
            //Loop through all connected clients
            for (int i = 0; i < Clients.Count; i++)
            {
                ClientWorker client = Clients[i];
                try
                {
                    //Skip your own client
                    if (client.GetToken() == cli.GetToken())
                        continue;
                    //Skip all clients that aren't in the same session
                    if (!IsInSession(cli, client))
                        continue;
                    //Send the packet to the current client
                    Packet.Send(pack, client);
                    n++;
                    if (!string.IsNullOrEmpty(debugname))
                        Console.WriteLine("Sent {0} to {1}", debugname, client.GetPlayer());
                }
                catch
                {
                    //Remove the client if it disconnected while broadcasting a packet
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

        /// <summary>
        /// Handles a received packet
        /// </summary>
        /// <param name="sender">Client who sent the packet</param>
        /// <param name="id">Packet id</param>
        /// <param name="data">Raw packet data</param>
        private void Worker_OnPacketReceived(ClientWorker sender, EPacket id, List<byte> data)
        {
            lock (this)
            {
                switch (id)
                {
                    case EPacket.UpdateHealth:
                        {
                            PacketUpdateHealth packet = new PacketUpdateHealth(data);
                            Console.WriteLine(packet.ToString());
                            Session session = Sessions[sender.GetSession()];
                            string target = packet.GetTarget();
                            int health = session.GetHealth(target);
                            if (health - session.GetExpectedDamage() != packet.GetValue())
                            {
                                Console.WriteLine("Rejected because of unexpected health value");
                                return;
                            }
                            session.SetHealth(target, packet.GetValue());
                            Broadcast(packet, session, "Health Sync");
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
                            string target = packet.GetTarget();
                            EBullet type = session.PopBullet();
                            if (actualsender == target && type == EBullet.Blank)
                                session.SetAgain(true);
                            bool backfired = false;
                            int damage = 0;
                            if (type == EBullet.Live)
                            {
                                damage = 1;
                                if (packet.HasFlag(EShotFlags.SawedOff))
                                    damage++;
                                if (packet.HasFlag(EShotFlags.Gunpowdered))
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
                            session.ExpectDamage(damage);
                            Broadcast(cli =>
                            {
                                EShotFlags flags = packet.GetFlags();
                                if (cli.GetPlayer() != target)
                                    flags |= EShotFlags.DisplayOnly;
                                if (backfired)
                                    flags |= EShotFlags.GunpowderBackfired;
                                return new PacketShoot(actualsender, target, flags, type);
                            }, session, "Shooting");
                            if (session.GetBulletCount() == 0)
                            {
                                session.RoundStart(false);
                                Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer())), session, "New Round Start");
                            }
                        }
                        break;
                    case EPacket.ControlRequest:
                        {
                            PacketControlRequest packet = new PacketControlRequest(data);
                            Console.WriteLine(packet.ToString());
                            Session session = Sessions[sender.GetSession()];
                            if (session.GetCurrentPlayer() != sender.GetPlayer())
                            {
                                Console.WriteLine("Rejected because player isn't in control");
                                return;
                            }
                            if (session.ShouldSwitchPlayer())
                                session.SwitchPlayer();
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
                            Broadcast(cli => new PacketStartRound(session.GetBullets(true), session.GetItems(cli.GetPlayer()), health), session, "Round Start");
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
                            //A client disconnected from the server
                            PacketDisconnected packet = new PacketDisconnected();
                            Console.WriteLine(packet.ToString());
                            //If it is a pending client, we just remove the client from our connected list
                            if (sender.DoesPlayerExist())
                            {
                                //Otherwise we need to notify other clients
                                Session session = Sessions[sender.GetSession()];
                                string player = sender.GetPlayer();
                                //Remove the client from its session
                                session.RemovePlayer(player);
                                Console.WriteLine("Removed " + player + " from Session " + sender.GetSession());
                                bool didMigrate = false;
                                bool destroyed = false;
                                //If no hosts are left, the session is empty and we can destroy it
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
                                    //We need to migrate a new host if the host left
                                    do
                                    {
                                        //Get the next host in the queue
                                        session.MigrateHost();
                                    }
                                    while (!session.IsPlayerConnected(session.GetHost()));
                                    Console.WriteLine("New Host: " + session.GetHost());
                                    didMigrate = true;
                                }
                                if (didMigrate)
                                    session.ResetSettings();
                                //If the session wasn't destroyed, we tell other clients to remove our player locally
                                if (!destroyed)
                                    Broadcast(new PacketRemoveLocalPlayer(player, didMigrate ? session.GetHost() : null), sender, "Local Removal");
                            }
                            else Console.WriteLine("Disconnected pending Player");
                            //Remove the client from the connected list
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
                            //Only these chars are allowed for players and sessions
                            string allowed = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-%&/[]()?!.,#";
                            //Create a response that indicates if the join will succeed
                            EJoinResponse response = EJoinResponse.Pending;
                            //Check if the session name is valid
                            foreach (char c in session)
                            {
                                if (!allowed.Contains(c + ""))
                                {
                                    response = EJoinResponse.FailedInvalidSession;
                                    break;
                                }
                            }
                            if (!hosting && !Sessions.ContainsKey(session))
                                response = EJoinResponse.FailedInvalidSession;
                            if (response == EJoinResponse.Pending)
                            {
                                //Check if the player name is valid
                                foreach (char c in player)
                                {
                                    if (!allowed.Contains(c + ""))
                                    {
                                        response = EJoinResponse.FailedInvalidName;
                                        break;
                                    }
                                }
                                //Check if a player with that name is already connected
                                if (Sessions.ContainsKey(session) && Sessions[session].IsPlayerConnected(player))
                                    response = EJoinResponse.FailedInvalidName;
                            }
                            //Check if the session is full
                            if (response == EJoinResponse.Pending)
                                if (Sessions.ContainsKey(session))
                                    if (Sessions[session].IsLocked())
                                        response = EJoinResponse.FailedLocked;
                            if (response == EJoinResponse.Pending)
                            {
                                //At this point we passed all checks
                                //Now we will either create or join the session
                                //depending if the session is already in the list or not
                                if (Sessions.ContainsKey(session))
                                    response = EJoinResponse.Succeeded;
                                else
                                    response = EJoinResponse.SucceededHost;
                            }
                            if (response == EJoinResponse.SucceededHost)
                            {
                                //Create the session if we need to host it
                                Sessions.Add(session, new Session(player, session));
                            }
                            else if (response == EJoinResponse.Succeeded)
                            {
                                //If the session already exists, we add our player to the list
                                //and lock the session if the maximum player count was reached
                                Sessions[session].AddPlayer(player);
                            }
                            if (response == EJoinResponse.Succeeded || response == EJoinResponse.SucceededHost)
                            {
                                //Send a successful join response on success
                                //It contains the host, session and connected players
                                Console.WriteLine("Success " + response.ToString());
                                Packet.Send(new PacketJoinResponse(session, Sessions[session].GetHost(), Sessions[session].GetPlayers(), response), sender);
                                //If our client is pending (which will most likely be the case)
                                //we assign the received playername and session to it
                                if (!sender.DoesPlayerExist())
                                {
                                    sender.AssignData(player, session);
                                    Console.WriteLine("Assigned Data: " + player + ", " + session);
                                }
                                //Broadcast our joining to all connected players so that they can add you locally
                                //This isn't needed if we host the session, as no other players are connected yet
                                if (response == EJoinResponse.Succeeded)
                                    Broadcast(new PacketNewPlayer(player), sender, "Join Sync");
                            }
                            else
                            {
                                //Send a fail response if there were any issues
                                Console.WriteLine("Fail " + response.ToString());
                                Packet.Send(new PacketJoinResponse(session, "INVALID", new List<string>(), response), sender);
                            }
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Removes a client from the connected list when it disconnected
        /// </summary>
        /// <param name="sender">Client to remove</param>
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
            //Start the server on an unused port
            Server s = new Server(19121);
            s.Start();
        }
    }
}
