using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;

public enum EPacket
{
    Invalid,
    JoinRequest,
    JoinResponse,
    NewPlayer,
    Disconnected,
    RemoveLocalPlayer,
}

enum EJoinResponse
{
    Pending,
    Succeeded,
    SucceededHost,
    FailedLocked,
    FailedInvalidName,
    FailedInvalidSession,
}

/// <summary>
/// Forces a client to remove a player locally
/// </summary>
class PacketRemoveLocalPlayer : Packet
{
    private string Player; //Player to remove
    private string NewHost; //New Host if the host has to be removed, otherwise null

    public override EPacket Id => EPacket.RemoveLocalPlayer;

    public PacketRemoveLocalPlayer(List<byte> data) => Receive(data);

    public PacketRemoveLocalPlayer(string player, string newhost)
    {
        Player = player;
        NewHost = newhost;
    }

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Player);
        bool nohost = NewHost == null;
        sync.SerializeBool(ref nohost);
        if (!nohost)
            sync.SerializeStr(ref NewHost);
    }

    public string GetPlayer() => Player;

    public string GetNewHost() => NewHost;

    public override string ToString() => string.Format("{0}: Player {1}, NewHost {2}", Id.ToString(), Player, NewHost);
}

/// <summary>
/// Disconnects your client from the server
/// </summary>
class PacketDisconnected : Packet
{
    public override EPacket Id => EPacket.Disconnected;

    public PacketDisconnected(List<byte> data) => Receive(data);

    public PacketDisconnected()
    {

    }

    protected override void Serialize(ISync sync)
    {

    }

    public override string ToString() => string.Format("{0}", Id.ToString());
}

/// <summary>
/// Forces a client to add a newly joined player locally
/// </summary>
class PacketNewPlayer : Packet
{
    private string Player; //Player who joined

    public override EPacket Id => EPacket.NewPlayer;

    public PacketNewPlayer(List<byte> data) => Receive(data);

    public PacketNewPlayer(string player)
    {
        Player = player;
    }

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Player);
    }

    public string GetPlayer() => Player;

    public override string ToString() => string.Format("{0}: Player {1}", Id.ToString(), Player);
}

/// <summary>
/// Response to a join request
/// Notifys your client if the join was successful
/// Sends the session host and a list of players that are in the session
/// </summary>
class PacketJoinResponse : Packet
{
    private string Session; //Session that your client joined
    private string Host; //Session host
    private List<string> Players; //Players in the session
    private EJoinResponse Response; //Join status

    public override EPacket Id => EPacket.JoinResponse;

    public PacketJoinResponse(List<byte> data) => Receive(data);

    public PacketJoinResponse(string session, string host, List<string> players, EJoinResponse response)
    {
        Session = session;
        Host = host;
        Players = players;
        Response = response;
    }

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Session);
        sync.SerializeStr(ref Host);
        int temp = (int)Response;
        sync.SerializeInt(ref temp);
        Response = (EJoinResponse)temp;
        if (Players == null)
        {
            int count = 0;
            sync.SerializeInt(ref count);
            Players = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string player = "";
                sync.SerializeStr(ref player);
                Players.Add(player);
            }
        }
        else
        {
            int count = Players.Count;
            sync.SerializeInt(ref count);
            for (int i = 0; i < count; i++)
            {
                string player = Players[i];
                sync.SerializeStr(ref player);
            }
        }
    }

    public string GetSession() => Session;

    public string GetHost() => Host;

    public List<string> GetPlayers() => Players;

    public EJoinResponse GetResponse() => Response;

    public bool DidSucceed() => Response > EJoinResponse.Pending && Response <= EJoinResponse.SucceededHost;

    public override string ToString() => string.Format("{0}: Session {1}, Host {2}, Players {3}, Response {4}", Id.ToString(), Session, Host, Players.Count, Response.ToString());
}

/// <summary>
/// Asks the server if you can join a specified session with a specified player name
/// The server sends you a join response
/// It also hosts the session if it doesn't exist yet
/// Otherwise it adds your client to the session and other clients
/// </summary>
class PacketJoinRequest : Packet
{
    private string Session; //Session to join
    private string Player; //Player name

    public override EPacket Id => EPacket.JoinRequest;

    public PacketJoinRequest(List<byte> data) => Receive(data);

    public PacketJoinRequest(string session, string player)
    {
        Session = session;
        Player = player;
    }

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Session);
        sync.SerializeStr(ref Player);
    }

    public string GetSession() => Session;

    public string GetPlayer() => Player;

    public override string ToString() => string.Format("{0}: Session {1}, Player {2}", Id.ToString(), Session, Player);
}

/// <summary>
/// Interface for serializing variables in packets
/// </summary>
public interface ISync
{
    void SerializeByte(ref byte val);
    void SerializeShort(ref short val);
    void SerializeInt(ref int val);
    void SerializeUns(ref uint val);
    void SerializeStr(ref string val);
    void SerializePacket(ref EPacket val);
    void SerializeFloat(ref float val);
    void SerializeBool(ref bool val);
    List<byte> Result { get; } //Unused by SyncReader
}

/// <summary>
/// Class for reading data from a bytestream
/// </summary>
public class SyncReader : ISync
{
    private readonly List<byte> Data;
    private int Iterator;

    public List<byte> Result => throw new NotImplementedException();

    public SyncReader(List<byte> data, int it)
    {
        Data = data;
        Iterator = it;
    }

    public void SerializeBool(ref bool val)
    {
        val = Data[Iterator] != 0;
        Iterator++;
    }

    public void SerializeByte(ref byte val)
    {
        val = Data[Iterator];
        Iterator++;
    }

    public void SerializeShort(ref short val)
    {
        val = BitConverter.ToInt16(Data.ToArray(), Iterator);
        Iterator += 2;
    }

    public void SerializeInt(ref int val)
    {
        val = BitConverter.ToInt32(Data.ToArray(), Iterator);
        Iterator += 4;
    }

    public void SerializeUns(ref uint val)
    {
        val = BitConverter.ToUInt32(Data.ToArray(), Iterator);
        Iterator += 4;
    }

    public void SerializeStr(ref string val)
    {
        byte len = Data[Iterator++];
        val = "";
        for (int i = 0; i < len; i++)
            val += (char)Data[Iterator++] + "";
    }

    public void SerializePacket(ref EPacket val)
    {
        val = (EPacket)BitConverter.ToInt32(Data.ToArray(), Iterator);
        Iterator += 4;
    }

    public void SerializeFloat(ref float val)
    {
        val = BitConverter.ToSingle(Data.ToArray(), Iterator);
        Iterator += 4;
    }
}

/// <summary>
/// Class for writing data to a bytestream
/// </summary>
public class SyncWriter : ISync
{
    private readonly List<byte> Data;

    public List<byte> Result => Data;

    public SyncWriter() => Data = new List<byte>();

    public void SerializeBool(ref bool val)
    {
        if (val)
            Data.Add(1);
        else
            Data.Add(0);
    }

    public void SerializeByte(ref byte val)
    {
        Data.Add(val);
    }

    public void SerializeFloat(ref float val)
    {
        byte[] data = BitConverter.GetBytes(val);
        foreach (byte d in data)
            Data.Add(d);
    }

    public void SerializeInt(ref int val)
    {
        byte[] data = BitConverter.GetBytes(val);
        foreach (byte d in data)
            Data.Add(d);
    }

    public void SerializePacket(ref EPacket val)
    {
        byte[] data = BitConverter.GetBytes((int)val);
        foreach (byte d in data)
            Data.Add(d);
    }

    public void SerializeShort(ref short val)
    {
        byte[] data = BitConverter.GetBytes(val);
        foreach (byte d in data)
            Data.Add(d);
    }

    public void SerializeStr(ref string val)
    {
        byte[] data = Encoding.ASCII.GetBytes(val);
        Data.Add((byte)data.Length);
        foreach (byte d in data)
            Data.Add(d);
    }

    public void SerializeUns(ref uint val)
    {
        byte[] data = BitConverter.GetBytes(val);
        foreach (byte d in data)
            Data.Add(d);
    }
}

/// <summary>
/// Interface for sending and receiving netpackets
/// </summary>
public abstract class Packet
{
    /// <summary>
    /// Serializes your data when sending or receiving a packet
    /// </summary>
    /// <param name="sync">Reader or writer</param>
    protected abstract void Serialize(ISync sync);

    /// <summary>
    /// Packet id, will get serialized automatically
    /// </summary>
    public abstract EPacket Id { get; }

    /// <summary>
    /// Should be called when receiving this packet
    /// </summary>
    /// <param name="data">Bytestream that was received</param>
    protected void Receive(List<byte> data)
    {
        SyncReader reader = new SyncReader(data, 0);
        Serialize(reader);
    }

    /// <summary>
    /// Gets used by the client to receive a packet
    /// </summary>
    /// <param name="data">Bytestream that was received</param>
    /// <param name="id">Packet id output</param>
    /// <param name="error">Error code if it failed</param>
    /// <returns>True on success</returns>
    public static bool Prepare(List<byte> data, out EPacket id, out int error)
    {
        error = 0;
        id = default;
        //Check if the packet header is valid
        if (data[0] != '$')
        {
            error = 1;
            return false;
        }
        //Create a reader for reading the whole packet
        SyncReader reader = new SyncReader(data, 0);
        //Create a checksum from the received data
        //(exclude the last 4 bytes because it contains the checksum itself)
        int checksum = 0x68751223;
        for (int i = 0; i < data.Count - 4; i++)
        {
            byte b = 0;
            reader.SerializeByte(ref b);
            checksum ^= b;
            checksum *= b;
        }
        //Read the checksum that came with the packet and convert it to an integer
        List<byte> cstemp = new List<byte>();
        for (int i = data.Count - 4; i < data.Count; i++)
            cstemp.Add(data[i]);
        int actualchecksum = BitConverter.ToInt32(cstemp.ToArray(), 0);
        //Check if the checksums matched
        if (actualchecksum != checksum)
        {
            id = (EPacket)checksum;
            error = actualchecksum;
            return false;
        }
        //Remove the checksum bytes
        for (int i = 0; i < 4; i++)
            data.RemoveAt(data.Count - 1);
        //Create a new reader to read the packet id
        reader = new SyncReader(data, 0);
        byte dummy = 0;
        reader.SerializeByte(ref dummy);
        reader.SerializePacket(ref id);
        //Remove the packet header
        for (int i = 0; i < 5; i++)
            data.RemoveAt(0);
        return true;
    }

    /// <summary>
    /// Sends the packet to the server
    /// </summary>
    /// <param name="pack">Packet to send</param>
    /// <param name="cli">Local client</param>
    public static void Send(Packet pack, ClientWorker cli)
    {
        //Create a writer and write the packet header to it
        SyncWriter writer = new SyncWriter();
        EPacket temp = pack.Id;
        byte header = (byte)'$';
        writer.SerializeByte(ref header);
        writer.SerializePacket(ref temp);
        //Serialize the packet
        pack.Serialize(writer);
        List<byte> result = writer.Result;
        //Create a checksum and add it to the resulting bytestream
        int checksum = 0x68751223;
        foreach (byte r in result)
        {
            checksum ^= r;
            checksum *= r;
        }
        foreach (byte b in BitConverter.GetBytes(checksum))
            result.Add(b);
        //Send the bytestream to the server
        cli.Send(result);
    }
}

/// <summary>
/// Client for sending and receiving packets
/// </summary>
public class ClientWorker : IDisposable
{
    /// <summary>
    /// Event callback for received packets
    /// </summary>
    /// <param name="sender">Client who sent the packet on server or current client on client</param>
    /// <param name="id">Packet id</param>
    /// <param name="data">Raw packet data</param>
    public delegate void PacketReceived(ClientWorker sender, EPacket id, List<byte> data);
    public event PacketReceived OnPacketReceived;

    /// <summary>
    /// Event callback for a client that was disconnected
    /// Note: Only used by server to destroy clients
    /// </summary>
    /// <param name="sender">Client who disconnected</param>
    public delegate void Disconnected(ClientWorker sender);
    public event Disconnected OnDisconnected;

    private readonly TcpClient Socket; //Tcp client
    private readonly Stream Stream; //Stream for io actions
    private readonly Mutex Lock; //Mutex to avoid invalid packets
    private readonly bool IsClient; //False if the client is in the server, otherwise true
    private string Player; //Player name, only used by the server to distinguish clients
    private string Session; //Session of the client, same as above

    /// <summary>
    /// Creates a server copy of an existing client
    /// </summary>
    /// <param name="client">Existing client</param>
    public ClientWorker(TcpClient client)
    {
        Socket = client;
        Stream = Socket.GetStream();
        Lock = null;
        IsClient = false;
        Player = null;
    }

    /// <summary>
    /// Creates a client that connects itself to the server
    /// </summary>
    /// <param name="ip">Server ip</param>
    /// <param name="port">Server port</param>
    public ClientWorker(string ip, int port)
    {
        Socket = new TcpClient(ip, port);
        Stream = Socket.GetStream();
        Lock = new Mutex();
        IsClient = true;
        Player = null;
    }

    /// <summary>
    /// Assigns player data to the client in the server
    /// </summary>
    /// <param name="player">Player name</param>
    /// <param name="session">Session</param>
    public void AssignData(string player, string session)
    {
        Player = player;
        Session = session;
    }

    public string GetPlayer() => Player;

    public string GetSession() => Session;

    public int GetToken() => Player.GetHashCode() + Session.GetHashCode();

    public bool DoesPlayerExist() => !string.IsNullOrEmpty(Player);

    public void Start() => Task.Run(Update);

    /// <summary>
    /// Sends raw binary data to the server or the client
    /// </summary>
    /// <param name="data">Data to send</param>
    public void Send(List<byte> data)
    {
        if (IsClient)
            Lock.WaitOne();
        Stream.Write(data.ToArray(), 0, data.Count);
        if (IsClient)
            Lock.ReleaseMutex();
    }

    /// <summary>
    /// Destroys the client and disconnects it from the server
    /// </summary>
    public void Dispose() => Socket?.Close();

    /// <summary>
    /// Updates the client to receive packets
    /// </summary>
    private void Update()
    {
        byte[] buffer = new byte[0x1000];
        try
        {
            while (true)
            {
                //Will halt the thread until we received something
                int received = Stream.Read(buffer, 0, buffer.Length);
                //Break the loop if we didn't receive anything usable
                if (received <= 0)
                    break;
                //Migrate the received data to an array with its actual size
                byte[] packet = new byte[received];
                Array.Copy(buffer, 0, packet, 0, received);
                //Convert it to a list and prepare the packet
                List<byte> actualPacket = packet.ToList();
                if (!Packet.Prepare(actualPacket, out EPacket id, out int error))
                {
                    //Check for errors
                    if (error != 1)
                        Console.WriteLine("Ignored Packet because of Checksum mismatch ({0} != {1})", (int)id, error);
                    else
                        Console.WriteLine("Ignored Packet because of missing header");
                }
                else OnPacketReceived?.Invoke(this, id, actualPacket); //Receive the packet and handle it
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        //Call the disconnect event for proper cleaning
        OnDisconnected?.Invoke(this);
        //If the client isn't in the server, we exit the program
        if (IsClient)
        {
            Dispose();
            Environment.Exit(0);
            return;
        }
    }
}
