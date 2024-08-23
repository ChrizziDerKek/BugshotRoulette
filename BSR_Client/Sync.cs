using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.IO;
using System.Threading;

namespace BSR_Client
{
    public class Client
    {
        private readonly TcpClient Socket;
        private readonly Stream Stream;
        private readonly MainWindow Owner;
        private readonly Mutex Lock;

        public Client(string ip, int port, MainWindow owner)
        {
            Socket = new TcpClient(ip, port);
            Stream = Socket.GetStream();
            Owner = owner;
            Lock = new Mutex();
        }

        public void Send(string message)
        {
            Lock.WaitOne();
            message = "$" + message;
            Task.Delay(100).Wait();
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            Stream.Write(buffer, 0, buffer.Length);
            Lock.ReleaseMutex();
        }

        public void Start() => Task.Run(Run);

        private void Run()
        {
            byte[] buffer = new byte[0x1000];
            try
            {
                while (true)
                {
                    int received = Stream.Read(buffer, 0, buffer.Length);
                    if (received <= 0)
                        break;
                    string message = Encoding.UTF8.GetString(buffer, 0, received);
                    string[] messages = message.Split('$');
                    foreach (string msg in messages)
                        if (!string.IsNullOrEmpty(msg))
                            Owner.Receive(msg);
                    //Owner.Receive(message);
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            Close();
            Environment.Exit(0);
        }

        public void Close() => Socket.Close();
    }

    public class Packet
    {
        private EPacket Id;
        private List<string> Data;

        public static Packet Create(EPacket pack)
        {
            return new Packet() { Id = pack, Data = new List<string>() };
        }

        public EPacket GetId()
        {
            return Id;
        }

        public Packet Add(string data)
        {
            Data.Add(data);
            return this;
        }

        public Packet Add(int data)
        {
            Data.Add("" + data);
            return this;
        }

        public Packet Add(bool data)
        {
            Data.Add(data ? "1" : "0");
            return this;
        }

        public void Send(Client c)
        {
            //Task.Delay(100).Wait();
            string str = Id.ToString();
            foreach (string d in Data)
                str += "," + d;
            c.Send(str);
        }

        public Packet() { }

        public Packet(string data)
        {
            Data = new List<string>();
            string[] temp = data.Split(',');
            Id = (EPacket)Enum.Parse(typeof(EPacket), temp[0]);
            if (temp.Length == 1)
                return;
            for (int i = 1; i < temp.Length; i++)
                Data.Add(temp[i]);
        }

        public string ReadStr(int idx)
        {
            return Data[idx];
        }

        public int ReadInt(int idx)
        {
            return int.Parse(Data[idx]);
        }

        public bool ReadBool(int idx)
        {
            return Data[idx] != "0";
        }
    }
}