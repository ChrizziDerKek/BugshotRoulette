using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

public enum EPacket
{
    Invalid,
    JoinRequest,
    JoinResponse,
    NewPlayer,
    Disconnected,
    RemoveLocalPlayer,
    UpdateSettings,
    StartGame,
    StartRound,
    PassControl,
    Shoot,
    UseItem,
    UsedItem,
}

public enum EJoinResponse
{
    Pending,
    Succeeded,
    SucceededHost,
    FailedLocked,
    FailedInvalidSessionName,
    FailedInvalidSession,
    FailedInvalidPlayerName,
    FailedPlayerNameAlreadyUsed,
}

public enum EBullet
{
    Undefined,
    Blank,
    Live,
}

public enum EItem
{
    Nothing,
    Handcuffs,
    Cigarettes,
    Saw,
    Magnifying,
    Beer,
    Inverter,
    Medicine,
    Phone,
    Adrenaline,
    Magazine,
    Gunpowder,
    Bullet,
    Trashbin,
    Heroine,
    Katana,
    Swapper,
    Hat,
    Count,
}

public enum EShotFlags
{
    None = 0,
    SawedOff = 1 << 0,
    Gunpowdered = 1 << 1,
    Inverted = 1 << 2,
    GunpowderBackfired = 1 << 3,
    AgainBecauseCuffed = 1 << 4,
    HandcuffsJustUsed = 1 << 5,
}

public class SettingsData
{
    public bool BotDealer;
    public int MaxPlayers;
    public int MinHealth;
    public int MaxHealth;
    public int MinItems;
    public int MaxItems;
    public int MinBullets;
    public int MaxBullets;
    public bool DunceDealer;
    public bool OriginalItemsOnly;
    public bool NoItems;
    public Dictionary<EItem, bool> EnabledItems;

    public SettingsData(bool botDealer, int maxPlayers, int minHealth, int maxHealth, int minItems, int maxItems, int minBullets, int maxBullets, bool dunceDealer, bool originalItemsOnly, bool noItems, Dictionary<EItem, bool> enabledItems)
    {
        BotDealer = botDealer;
        MaxPlayers = maxPlayers;
        MinHealth = minHealth;
        MaxHealth = maxHealth;
        MinItems = minItems;
        MaxItems = maxItems;
        MinBullets = minBullets;
        MaxBullets = maxBullets;
        DunceDealer = dunceDealer;
        OriginalItemsOnly = originalItemsOnly;
        NoItems = noItems;
        EnabledItems = enabledItems;
    }

    public SettingsData()
    {
        BotDealer = false;
        MaxPlayers = 5;
        MinHealth = 10;
        MaxHealth = 15;
        MinItems = 1;
        MaxItems = 4;
        MinBullets = 2;
        MaxBullets = 8;
        DunceDealer = false;
        OriginalItemsOnly = false;
        NoItems = false;
        EnabledItems = new Dictionary<EItem, bool>();
        for (EItem i = EItem.Nothing + 1; i != EItem.Count; i++)
            EnabledItems.Add(i, i != EItem.Bullet);
    }
}

class PacketUsedItem : Packet
{
    private string Sender;
    private EItem Item;
    private EBullet Bullet;
    private int Healed;
    private int Index;
    private bool Inverted;
    private bool Trashed;
    private EItem NewItem;

    public override EPacket Id => EPacket.UsedItem;

    public PacketUsedItem(List<byte> data) => Receive(data);

    public PacketUsedItem(string sender, EItem item, bool trashed = false, EItem newitem = EItem.Nothing)
    {
        Sender = sender;
        Item = item;
        Bullet = EBullet.Undefined;
        Healed = 0;
        Index = 0;
        Inverted = false;
        Trashed = trashed;
        NewItem = newitem;
    }

    public PacketUsedItem(string sender, int healed, bool cigs)
    {
        Sender = sender;
        Item = cigs ? EItem.Cigarettes : EItem.Medicine;
        Bullet = EBullet.Undefined;
        Healed = healed;
        Index = 0;
        Inverted = false;
        Trashed = false;
        NewItem = EItem.Nothing;
    }

    public PacketUsedItem(string sender, EBullet bullet, int index = 0)
    {
        Sender = sender;
        Item = index == 0 ? EItem.Magnifying : EItem.Phone;
        Bullet = bullet;
        Healed = 0;
        Index = index;
        Inverted = false;
        Trashed = false;
        NewItem = EItem.Nothing;
    }

    public PacketUsedItem(string sender, EBullet bullet, bool inverted)
    {
        Sender = sender;
        Item = EItem.Beer;
        Bullet = bullet;
        Healed = 0;
        Index = 0;
        Inverted = inverted;
        Trashed = false;
        NewItem = EItem.Nothing;
    }

    public string GetSender() => Sender;

    public EItem GetItem() => Item;

    public EBullet GetBullet() => Bullet;

    public int GetHealAmount() => Healed;

    public int GetBulletIndex() => Index;

    public bool IsInverted() => Inverted;

    public bool WasTrashed() => Trashed;

    public EItem GetReplacementItem() => NewItem;

    protected override void Serialize(ISync sync)
    {
        int item = (int)Item;
        sync.SerializeInt(ref item);
        Item = (EItem)item;
        sync.SerializeStr(ref Sender);
        sync.SerializeBool(ref Trashed);
        if (Trashed)
        {
            item = (int)NewItem;
            sync.SerializeInt(ref item);
            NewItem = (EItem)item;
        }
        switch (Item)
        {
            case EItem.Handcuffs:
                break;
            case EItem.Saw:
                break;
            case EItem.Magnifying:
                {
                    int bullet = (int)Bullet;
                    sync.SerializeInt(ref bullet);
                    Bullet = (EBullet)bullet;
                }
                break;
            case EItem.Beer:
                {
                    int bullet = (int)Bullet;
                    sync.SerializeInt(ref bullet);
                    Bullet = (EBullet)bullet;
                    sync.SerializeBool(ref Inverted);
                }
                break;
            case EItem.Inverter:
                break;
            case EItem.Cigarettes:
            case EItem.Medicine:
                sync.SerializeInt(ref Healed);
                break;
            case EItem.Phone:
                {
                    sync.SerializeInt(ref Index);
                    int bullet = (int)Bullet;
                    sync.SerializeInt(ref bullet);
                    Bullet = (EBullet)bullet;
                }
                break;
            case EItem.Adrenaline:
                break;
            case EItem.Magazine:
                break;
            case EItem.Gunpowder:
                break;
            case EItem.Bullet:
                break;
            case EItem.Trashbin:
                break;
            case EItem.Heroine:
                break;
            case EItem.Katana:
                break;
            case EItem.Swapper:
                break;
        }
    }
}

class PacketUseItem : Packet
{
    private string Sender;
    private EItem Item;
    private string Target;
    private bool HasTarget;
    private bool Trashed;

    public override EPacket Id => EPacket.UseItem;

    public PacketUseItem(List<byte> data) => Receive(data);

    public PacketUseItem(string sender, EItem item, string target = null)
    {
        Sender = sender;
        Item = item;
        Target = target;
        HasTarget = Target != null;
        Trashed = false;
    }

    public PacketUseItem(string sender, EItem item, bool trashed)
    {
        Sender = sender;
        Item = item;
        Target = null;
        HasTarget = false;
        Trashed = trashed;
    }

    public string GetSender() => Sender;

    public string GetTarget() => Target;

    public EItem GetItem() => Item;

    public bool WasTrashed() => Trashed;

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Sender);
        int temp = (int)Item;
        sync.SerializeInt(ref temp);
        Item = (EItem)temp;
        sync.SerializeBool(ref HasTarget);
        sync.SerializeBool(ref Trashed);
        if (HasTarget)
            sync.SerializeStr(ref Target);
    }

    public override string ToString() => string.Format("{0}: Sender {1}, Target {2}, Item {3}", Id.ToString(), Sender, Target ?? "null", Item.ToString());
}

class PacketShoot : Packet
{
    private string Sender;
    private string Who;
    private EShotFlags Flags;
    private EBullet Type;

    public override EPacket Id => EPacket.Shoot;

    public PacketShoot(List<byte> data) => Receive(data);

    public PacketShoot(string sender, string who, EShotFlags flags = EShotFlags.None, EBullet type = EBullet.Undefined)
    {
        Sender = sender;
        Who = who;
        Flags = flags;
        Type = type;
    }

    public string GetSender() => Sender;

    public string GetTarget() => Who;

    public bool HasFlag(EShotFlags flag) => (Flags & flag) != 0;

    public EShotFlags GetFlags() => Flags;

    public EBullet GetBullet() => Type;

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Sender);
        sync.SerializeStr(ref Who);
        int temp = (int)Flags;
        sync.SerializeInt(ref temp);
        Flags = (EShotFlags)temp;
        temp = (int)Type;
        sync.SerializeInt(ref temp);
        Type = (EBullet)temp;
    }

    public override string ToString() => string.Format("{0}: Sender {1}, Who {2}, Flags {3}, Type {4}", Id.ToString(), Sender, Who, (int)Flags, Type.ToString());
}

class PacketPassControl : Packet
{
    private string Target;

    public override EPacket Id => EPacket.PassControl;

    public PacketPassControl(List<byte> data) => Receive(data);

    public PacketPassControl(string target)
    {
        Target = target;
    }

    public string GetTarget() => Target;

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Target);
    }

    public override string ToString() => string.Format("{0}: Target {1}", Id.ToString(), Target);
}

class PacketStartRound : Packet
{
    private List<EBullet> Bullets;
    private EItem[] Items;
    private int Lives;
    private Dictionary<string, List<EItem>> Generated;
    private bool Intense;
    private bool NoItems;

    public override EPacket Id => EPacket.StartRound;

    public PacketStartRound(List<byte> data) => Receive(data);

    public PacketStartRound(List<EBullet> bullets, EItem[] items, Dictionary<string, List<EItem>> generated, bool noitems = false, bool intense = false, int lives = -1)
    {
        Bullets = bullets;
        Items = items;
        Lives = lives;
        Generated = generated;
        Intense = intense;
        NoItems = noitems;
    }

    public List<EBullet> GetBullets() => Bullets;

    public EItem[] GetItems() => Items;

    public bool ShouldUpdateLives() => Lives != -1;

    public int GetLives() => Lives;

    public List<EItem> GetGeneratedItems(string player) => Generated[player];

    public bool ShouldPlayIntenseTheme() => Intense;

    public bool NoItemsGenerated() => NoItems;

    protected override void Serialize(ISync sync)
    {
        sync.SerializeInt(ref Lives);
        sync.SerializeBool(ref Intense);
        sync.SerializeBool(ref NoItems);
        if (Bullets == null)
        {
            Bullets = new List<EBullet>();
            int count = 0;
            sync.SerializeInt(ref count);
            for (int i = 0; i < count; i++)
            {
                int bullet = 0;
                sync.SerializeInt(ref bullet);
                Bullets.Add((EBullet)bullet);
            }
            if (!NoItems)
            {
                sync.SerializeInt(ref count);
                Items = new EItem[count];
                for (int i = 0; i < count; i++)
                {
                    int item = 0;
                    sync.SerializeInt(ref item);
                    Items[i] = (EItem)item;
                }
                Generated = new Dictionary<string, List<EItem>>();
                sync.SerializeInt(ref count);
                for (int i = 0; i < count; i++)
                {
                    string key = "";
                    sync.SerializeStr(ref key);
                    int items = 0;
                    sync.SerializeInt(ref items);
                    Generated.Add(key, new List<EItem>());
                    for (int j = 0; j < items; j++)
                    {
                        int item = 0;
                        sync.SerializeInt(ref item);
                        Generated[key].Add((EItem)item);
                    }
                }
            }
        }
        else
        {
            int count = Bullets.Count;
            sync.SerializeInt(ref count);
            for (int i = 0; i < count; i++)
            {
                int bullet = (int)Bullets[i];
                sync.SerializeInt(ref bullet);
            }
            if (!NoItems)
            {
                count = Items.Length;
                sync.SerializeInt(ref count);
                for (int i = 0; i < count; i++)
                {
                    int item = (int)Items[i];
                    sync.SerializeInt(ref item);
                }
                count = Generated.Count;
                sync.SerializeInt(ref count);
                for (int i = 0; i < count; i++)
                {
                    string key = Generated.ElementAt(i).Key;
                    sync.SerializeStr(ref key);
                    int items = Generated[key].Count;
                    sync.SerializeInt(ref items);
                    for (int j = 0; j < items; j++)
                    {
                        int item = (int)Generated[key][j];
                        sync.SerializeInt(ref item);
                    }
                }
            }
        }
    }

    public override string ToString() => string.Format("{0} ...", Id.ToString());
}

class PacketStartGame : Packet
{
    public override EPacket Id => EPacket.StartGame;

    public PacketStartGame(List<byte> data) => Receive(data);

    public PacketStartGame()
    {

    }

    protected override void Serialize(ISync sync)
    {

    }

    public override string ToString() => string.Format("{0}", Id.ToString());
}

class PacketUpdateSettings : Packet
{
    private SettingsData Data;

    public override EPacket Id => EPacket.UpdateSettings;

    public PacketUpdateSettings(List<byte> data) => Receive(data);

    public PacketUpdateSettings(SettingsData data)
    {
        Data = data;
    }

    public SettingsData GetSettings() => Data;

    protected override void Serialize(ISync sync)
    {
        bool received = Data == null;
        if (received)
            Data = new SettingsData();
        sync.SerializeBool(ref Data.BotDealer);
        sync.SerializeInt(ref Data.MaxPlayers);
        sync.SerializeInt(ref Data.MinHealth);
        sync.SerializeInt(ref Data.MaxHealth);
        sync.SerializeInt(ref Data.MinItems);
        sync.SerializeInt(ref Data.MaxItems);
        sync.SerializeInt(ref Data.MinBullets);
        sync.SerializeInt(ref Data.MaxBullets);
        sync.SerializeBool(ref Data.DunceDealer);
        sync.SerializeBool(ref Data.OriginalItemsOnly);
        sync.SerializeBool(ref Data.NoItems);
        if (received)
        {
            int count = 0;
            sync.SerializeInt(ref count);
            Data.EnabledItems = new Dictionary<EItem, bool>();
            for (int i = 0; i < count; i++)
            {
                int item = 0;
                bool enabled = false;
                sync.SerializeInt(ref item);
                sync.SerializeBool(ref enabled);
                Data.EnabledItems.Add((EItem)item, enabled);
            }
        }
        else
        {
            int count = Data.EnabledItems.Count;
            sync.SerializeInt(ref count);
            foreach (KeyValuePair<EItem, bool> i in Data.EnabledItems)
            {
                int item = (int)i.Key;
                bool enabled = i.Value;
                sync.SerializeInt(ref item);
                sync.SerializeBool(ref enabled);
            }
        }
    }

    public override string ToString() => string.Format("{0}: ...", Id.ToString());
}

class PacketRemoveLocalPlayer : Packet
{
    private string Player;
    private string NewHost;

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

class PacketNewPlayer : Packet
{
    private string Player;

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

class PacketJoinResponse : Packet
{
    private string Session;
    private string Host;
    private List<string> Players;
    private EJoinResponse Response;

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

class PacketJoinRequest : Packet
{
    private string Session;
    private string Player;
    private bool JoinExistingSession;

    public override EPacket Id => EPacket.JoinRequest;

    public PacketJoinRequest(List<byte> data) => Receive(data);

    public PacketJoinRequest(string session, string player, bool joinExistingSession)
    {
        Session = session;
        Player = player;
        JoinExistingSession = joinExistingSession;
    }

    protected override void Serialize(ISync sync)
    {
        sync.SerializeStr(ref Session);
        sync.SerializeStr(ref Player);
        sync.SerializeBool(ref JoinExistingSession);
    }

    public string GetSession() => Session;

    public string GetPlayer() => Player;

    public bool IsHosting() => !JoinExistingSession;

    public override string ToString() => string.Format("{0}: Session {1}, Player {2}", Id.ToString(), Session, Player);
}

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
    List<byte> Result { get; }
}

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

public abstract class Packet
{
    protected abstract void Serialize(ISync sync);

    public abstract EPacket Id { get; }

    protected void Receive(List<byte> data)
    {
        SyncReader reader = new SyncReader(data, 0);
        Serialize(reader);
    }

    public static bool Prepare(List<byte> data, out EPacket id)
    {
        id = default;
        if (data[0] != '$')
            return false;
        SyncReader reader = new SyncReader(data, 0);
        byte dummy = 0;
        reader.SerializeByte(ref dummy);
        reader.SerializePacket(ref id);
        for (int i = 0; i < 5; i++)
            data.RemoveAt(0);
        return true;
    }

    public static void Send(Packet pack, ClientWorker cli)
    {
        SyncWriter writer = new SyncWriter();
        EPacket temp = pack.Id;
        byte header = (byte)'$';
        writer.SerializeByte(ref header);
        writer.SerializePacket(ref temp);
        pack.Serialize(writer);
        cli.Send(writer.Result);
    }
}

public class ClientWorker : IDisposable
{
    public delegate void PacketReceived(ClientWorker sender, EPacket id, List<byte> data);
    public event PacketReceived OnPacketReceived;

    public delegate void Disconnected(ClientWorker sender);
    public event Disconnected OnDisconnected;

    private readonly TcpClient Socket;
    private readonly Stream Stream;
    private readonly Mutex Lock;
    private readonly bool IsClient;
    private string Player;
    private string Session;

    public ClientWorker(TcpClient client)
    {
        Socket = client;
        Stream = Socket.GetStream();
        Lock = null;
        IsClient = false;
        Player = null;
    }

    public ClientWorker(string ip, int port)
    {
        Socket = new TcpClient(ip, port);
        Stream = Socket.GetStream();
        Lock = new Mutex();
        IsClient = true;
        Player = null;
    }

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

    public void Send(List<byte> data)
    {
        Lock?.WaitOne();
        Stream.Write(data.ToArray(), 0, data.Count);
        Lock?.ReleaseMutex();
    }

    public void Dispose() => Socket?.Close();

    private void Update()
    {
        byte[] buffer = new byte[0x1000];
        try
        {
            while (true)
            {
                int received = Stream.Read(buffer, 0, buffer.Length);
                if (received == 0)
                    break;
                byte[] packet = new byte[received];
                Array.Copy(buffer, 0, packet, 0, received);
                List<byte> actualPacket = packet.ToList();
                if (!Packet.Prepare(actualPacket, out EPacket id))
                    Console.WriteLine("Ignored invalid Packet");
                else
                    OnPacketReceived?.Invoke(this, id, actualPacket);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        OnDisconnected?.Invoke(this);
        if (IsClient)
        {
            Dispose();
            Environment.Exit(0);
            return;
        }
    }
}