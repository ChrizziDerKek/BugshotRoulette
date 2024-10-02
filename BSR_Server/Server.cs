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

#pragma warning disable IDE0058

namespace Server
{
    public class Session
    {
        public string Host; //Player who created the session and can start the game
        public List<string> Players; //All connected players
        public Queue<string> NextHosts; //Backup hosts that will become host when the current one leaves
        public bool Locked; //True if the session is full
        public SettingsData Settings;
        public int CurrentPlayer;
        public Random RNG;

        public void SetFirstPlayer()
        {
            CurrentPlayer = RNG.Next(0, Players.Count);
        }

        public int SwitchPlayer()
        {
            CurrentPlayer = (CurrentPlayer + 1) % Players.Count;
            return CurrentPlayer;
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

        /// <summary>
        /// Sends a packet to all clients that are connected to your session
        /// </summary>
        /// <param name="pack">Packet to send</param>
        /// <param name="cli">Your client</param>
        /// <param name="debugname">Debug log message</param>
        /// <returns>Number of clients that received the packet</returns>
        private int Broadcast(Packet pack, ClientWorker cli, string debugname)
        {
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
            return Clients.Count;
        }

        private bool IsHost(ClientWorker sender)
        {
            if (!sender.DoesPlayerExist())
                return false;
            Session session = Sessions[sender.GetSession()];
            return session.Host == sender.GetPlayer();
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
                    case EPacket.StartGame:
                        {
                            PacketStartGame packet = new PacketStartGame();
                            Console.WriteLine(packet.ToString());
                            if (!IsHost(sender))
                            {
                                Console.WriteLine("Rejected because player isn't the host");
                                return;
                            }
                            Session session = Sessions[sender.GetSession()];
                            session.Locked = true;
                            session.SetFirstPlayer();
                            Broadcast(packet, sender, "Game Start");
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
                            session.Settings = packet.GetSettings();
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
                                session.Players.Remove(player);
                                Console.WriteLine("Removed " + player + " from Session " + sender.GetSession());
                                bool didMigrate = false;
                                bool destroyed = false;
                                //If no hosts are left, the session is empty and we can destroy it
                                if (session.NextHosts.Count == 0)
                                {
                                    Console.WriteLine("Destroying Session because no Players are left");
                                    Sessions.Remove(sender.GetSession());
                                    destroyed = true;
                                }
                                if (!destroyed && !IsHost(sender))
                                    session.NextHosts = new Queue<string>(session.NextHosts.Where(h => h != player));
                                if (!destroyed && IsHost(sender))
                                {
                                    //We need to migrate a new host if the host left
                                    do
                                    {
                                        //Get the next host in the queue
                                        session.Host = session.NextHosts.Dequeue();
                                    }
                                    while (!session.Players.Contains(session.Host));
                                    Console.WriteLine("New Host: " + session.Host);
                                    didMigrate = true;
                                }
                                //If the session wasn't destroyed, we tell other clients to remove our player locally
                                if (!destroyed)
                                    Broadcast(new PacketRemoveLocalPlayer(player, didMigrate ? session.Host : null), sender, "Local Removal");
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
                                if (Sessions.ContainsKey(session) && Sessions[session].Players.Contains(player))
                                    response = EJoinResponse.FailedInvalidName;
                            }
                            //Check if the session is full
                            if (response == EJoinResponse.Pending)
                                if (Sessions.ContainsKey(session))
                                    if (Sessions[session].Locked)
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
                                Sessions.Add(session, new Session()
                                {
                                    Host = player, //We are the host
                                    Players = new List<string>() { player }, //We are the only connected player
                                    Locked = false, //Session isn't full
                                    NextHosts = new Queue<string>(), //No backup hosts are connected yet
                                    Settings = new SettingsData(),
                                    CurrentPlayer = 0,
                                    RNG = new Random(),
                                });
                            }
                            else if (response == EJoinResponse.Succeeded)
                            {
                                //If the session already exists, we add our player to the list
                                Sessions[session].Players.Add(player);
                                Sessions[session].NextHosts.Enqueue(player);
                                //Lock the session if the maximum player count was reached
                                if (Sessions[session].Players.Count >= Sessions[session].Settings.MaxPlayers)
                                    Sessions[session].Locked = true;
                            }
                            if (response == EJoinResponse.Succeeded || response == EJoinResponse.SucceededHost)
                            {
                                //Send a successful join response on success
                                //It contains the host, session and connected players
                                Console.WriteLine("Success " + response.ToString());
                                Packet.Send(new PacketJoinResponse(session, Sessions[session].Host, Sessions[session].Players, response), sender);
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
