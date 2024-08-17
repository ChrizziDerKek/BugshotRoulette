using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.IO;

#pragma warning disable IDE0011
#pragma warning disable IDE0058

namespace BSR_Server
{
    public class MessageEventArgs : EventArgs
    {
        public string Message { get; private set; }

        public MessageEventArgs(string message)
        {
            Message = message;
        }
    }

    public delegate void MessageEventHandler(object sender, MessageEventArgs e);

    public class Worker
    {
        public event MessageEventHandler MessageReceived;
        public event EventHandler Disconnected;
        private readonly TcpClient Socket;
        private readonly Stream Stream;

        public Worker(TcpClient socket)
        {
            Socket = socket;
            Stream = socket.GetStream();
        }

        public void Send(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            Stream.Write(buffer, 0, buffer.Length);
        }

        public void Start()
        {
            Task.Run(Run);
        }

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
                    MessageReceived?.Invoke(this, new MessageEventArgs(message));
                }
            }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        public void Close()
        {
            Socket.Close();
        }
    }

    public class Server
    {
        private readonly TcpListener ServerSocket;
        private readonly List<Worker> Workers;

        public Server(int port)
        {
            Workers = new List<Worker>();
            ServerSocket = new TcpListener(IPAddress.Any, port);
            ServerSocket.Start();
        }

        public void Start()
        {
            while (true)
            {
                TcpClient socket = ServerSocket.AcceptTcpClient();
                Worker w = new Worker(socket);
                AddWorker(w);
                w.Start();
            }
        }

        private void AddWorker(Worker w)
        {
            lock (this)
            {
                Workers.Add(w);
                w.Disconnected += Worker_Disconnected;
                w.MessageReceived += Worker_MessageReceived;
            }
        }

        private void RemoveWorker(Worker w)
        {
            lock (this)
            {
                w.Disconnected -= Worker_Disconnected;
                w.MessageReceived -= Worker_MessageReceived;
                Workers.Remove(w);
                w.Close();
            }
        }

        private void Worker_MessageReceived(object sender, MessageEventArgs e) => Broadcast(sender as Worker, e.Message);

        private void Worker_Disconnected(object sender, EventArgs e) => RemoveWorker(sender as Worker);

        private void Broadcast(Worker sender, string message)
        {
            lock (this)
            {
                for (int i = 0; i < Workers.Count; i++)
                {
                    Worker w = Workers[i];
                    if (w == sender)
                        continue;
                    try
                    {
                        w.Send(message);
                        Console.WriteLine("Sent: " + message);
                    }
                    catch
                    {
                        Workers.RemoveAt(i--);
                        w.Close();
                    }
                }
            }
        }
    }

    public class Program
    {
        private static void Main()
        {
            Server s = new Server(19123);
            s.Start();
        }
    }
}