using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace Nodes
{
    /*
     *  Коды запросов
     *  1 - новая нода
     *  2 - запрос списка друзей у ноды
     *  3 - нода присылает своих друзей
     *  4 - нода присылает символ
     * 
     */
    public class Node
    {
        public struct SendedLetter
        {
            public char letter;
            public byte hash;
            public SendedLetter(char c, byte n)
            {
                this.letter = c;
                this.hash = n;
            }
        }

        protected List<SendedLetter> sendedLetters = new List<SendedLetter>();

        // Максимальное количество букв которое я буду помнить при отправке
        protected static int maxLettersQuery = 200;

        protected IPEndPoint me;

        protected List<IPEndPoint> friends = new List<IPEndPoint>();

        protected List<IPEndPoint> exFriends = new List<IPEndPoint>();

        public TcpListener tcpListener = null;
        protected bool tcpListenerStatus = true;
        // если спустя это время консоль не ответит, считаю ее закрытой
        protected static int tcpConnectionTimeout = 5000;

        public UdpClient server = null;
        protected bool udpServerStatus = true;

        protected static Random rand = new Random((int)DateTime.Now.Ticks);

        // очередь букв для отправки
        protected Queue<Task> lettersForSendQuery = new Queue<Task>();

        #region Events
        protected delegate void FriendActions(IPEndPoint point);

        // Событие появления новой ноды
        protected event FriendActions newFriend = null;

        protected static void NewFriendMessage(IPEndPoint friend) =>
            Console.WriteLine("\nNew friend: " + friend.ToString());

        // Событие исчезновения ноды (обработать ли событие отправкой запроса "node die"?)
        protected event FriendActions dieFriend = null;

        protected static void DieFriendMessage(IPEndPoint friend) =>
            Console.WriteLine("Die friend: " + friend.ToString());

        #endregion

        public IPEndPoint GetMyEndPoint()
        {
            return new IPEndPoint(me.Address, me.Port);
        }
        // Широковещательный запрос
        public void SayHello()
        {
            var client = this.server;

            byte[] data = new byte[1];
            try
            {
                IPEndPoint broadcast = null;
                for (int i = 0; i < 10; i++)
                {
                    broadcast = new IPEndPoint(IPAddress.Broadcast, 8000 + i);
                    client.Send(data, data.Length, broadcast);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        // Слушаю широковещательные запросы от консолей
        public void ListenUDPRequests()
        {
            var server = this.server;
            IPEndPoint RemoteIpEndPoint = null;

            while (true)
            {
                if (this.udpServerStatus == false)
                {
                    server.Close();
                    server.Dispose();
                    return;
                }
                // Ожидание запроса новой консоли
                byte[] data = server.Receive(ref RemoteIpEndPoint);

                if (RemoteIpEndPoint.Equals(this.me) == false)
                {
                    try
                    {
                        // Cоединяюсь по TCP с новой консолью
                        TcpClient tcpClient = new TcpClient();
                        tcpClient.Connect(RemoteIpEndPoint);
                        #region Send

                        NetworkStream stream = tcpClient.GetStream();

                        var message = this.FriendListForSend();

                        stream.Write(message, 0, message.Length);
                        stream.Close();

                        #endregion
                        tcpClient.Close();

                        if (!this.friends.Contains(RemoteIpEndPoint))
                        {
                            this.friends.Add(RemoteIpEndPoint);
                            this.newFriend?.Invoke(RemoteIpEndPoint);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Возникло исключение: " + ex.ToString() + "\n  " + ex.Message);
                    }

                }
            }
        }

        // Слушаю запросы на подключение от консолей
        public void ListenTCPConnections()
        {
            // могут прийти 2 типа запросов:
            //    некая нода присылает друзей включая себя (код 3)
            //    некая нода присылает символ (код 4)

            var server = this.tcpListener;

            server = new TcpListener(this.me);
            server.Start();

            while (true)
            {
                if (this.tcpListenerStatus == false)
                {
                    server.Stop();
                    return;
                }
                TcpClient client = server.AcceptTcpClient();
                var stream = client.GetStream();
                var data = new byte[256];
                stream.Read(data, 0, data.Length);

                if (data[0] == 3)
                {
                    Task.Run(() => this.UpdateFriendsFromBytes(data));
                }
                else if (data[0] == 4)
                {
                    int port = data[3] + 8000;
                    var clientEndPoint = client.Client.RemoteEndPoint.ToIPEndPoint(port);
                    char letter = Convert.ToChar(data[1]);
                    var sl = new SendedLetter(letter, data[2]);

                    Task.Run(() =>this.GetLetterAction(clientEndPoint, sl));
                }
                stream.Close();
                client.Close();

            }
            
        }

        // Подготовка массива байтов для отправки при запросе (кого ты знаешь?)
        protected byte[] FriendListForSend()
        {
            byte[] response = new byte[2 + (this.friends.Count + 1) * 5];
            response[0] = 3;
            response[1] = (byte)(this.friends.Count + 1);

            var myIpBytes = this.me.Address.GetAddressBytes();
            response[2] = myIpBytes[0];
            response[3] = myIpBytes[1];
            response[4] = myIpBytes[2];
            response[5] = myIpBytes[3];
            response[6] = (byte)(this.me.Port - 8000);

            int index = 7;
            for (int i = 0; i < this.friends.Count; i++)
            {
                var ipBytes = this.friends[i].Address.GetAddressBytes();
                int j = index;
                for (; j < index + 4; j++)
                    response[j] = ipBytes[j - index];
                response[j] = (byte)(this.friends[i].Port - 8000);
                index += 5;
            }

            return response;
        }

        // Обновение списка друзей
        protected void UpdateFriendsFromBytes(byte[] data)
        {
            string ip;
            int port = 0;

            int index = 2;
            for (int i = 0; i < data[1]; i++)
            {
                ip = "";
                for (int j = index; j < index + 5; j++)
                {
                    if (j == index + 4)
                        port = data[j] + 8000;
                    else
                    {
                        ip += data[j].ToString();
                        if (j != index + 3)
                            ip += ".";
                    }
                }
                var friend = new IPEndPoint(IPAddress.Parse(ip), port);
                index += 5;
                if (!friend.Equals(this.me) && !this.friends.Contains(friend))
                {
                    this.friends.Add(friend);
                    this.newFriend?.Invoke(friend);
                }
            }
        }

        public static IPEndPoint GetLocalIPEndPoint()
        {
            // определяю свой IP
            IPAddress myIP = null;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    myIP = ip;
                    break;
                }

            // определяю свободный порт
            Socket tcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint local = null;
            int myPort = 8000;
            while (true)
            {
                try
                {
                    local = new IPEndPoint(myIP, myPort);
                    tcp.Bind(local);
                    break;
                }
                catch (Exception ex)
                {
                    myPort++;
                }
            }
            tcp.Close();
            tcp.Dispose();
            return local;
        }

        protected virtual void GetLetterAction(IPEndPoint client, SendedLetter sl)
        {
            if (!this.sendedLetters.Contains(sl))
            {
                Console.Write(sl.letter);

                while (this.sendedLetters.Count >= Node.maxLettersQuery)
                    this.sendedLetters.RemoveAt(0);
                this.sendedLetters.Add(sl);

                var task = new Task(() => this.SendLetterToFriends(sl, client));
                this.lettersForSendQuery.Enqueue(task);
            }
        }

        protected void SendLetterToFriends(SendedLetter sendedLetter, IPEndPoint sender = null)
        {
            byte[] data = new byte[4];
            data[0] = 4;
            data[1] = (byte)Convert.ToInt32(sendedLetter.letter);
            data[2] = sendedLetter.hash;
            data[3] = (byte)(this.me.Port - 8000);


            for (int i = 0; i < this.friends.Count; i++)
            {
                IPEndPoint friend = this.friends[i];

                if (friend.Equals(sender))
                    continue;

                bool good = false;
                // try connect
                Task.Run(() =>
                {
                    try
                    {
                        TcpClient tcpClient = new TcpClient();
                        tcpClient.Connect(friend);
                        good = true;
                        NetworkStream stream = tcpClient.GetStream();
                        stream.Write(data, 0, data.Length);
                        stream.Close();
                        tcpClient.Close();
                        tcpClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine("\ncannot connect to " + friend.ToString());
                    }
                });

                #region WaitTimeOut
                int j = 100;
                int timeout = tcpConnectionTimeout / j;
                while (j-- > 0)
                {
                    if (good) break;
                    Thread.Sleep(timeout);
                }
                if (!good)
                {
                    this.exFriends.Add(friend);
                    this.dieFriend?.Invoke(friend);
                }
                #endregion
            }

        }

        public void LettersForSendQueryHandler()
        {

            while (true)
            {
                while (this.lettersForSendQuery.Count == 0)
                    ;

                foreach (var item in this.exFriends)
                    this.friends.Remove(item);
                this.exFriends.Clear();
                Task task = this.lettersForSendQuery.Dequeue();
                task.Start();
                task.Wait();
            }
        }

        public void PutLetterToQueue(char letter)
        {
            byte hash = (byte)rand.Next(0, 255);

            var sl = new SendedLetter(letter, hash);

            while (this.sendedLetters.Count >= Node.maxLettersQuery)
                this.sendedLetters.RemoveAt(0);
            this.sendedLetters.Add(sl);

            var task = new Task(() => this.SendLetterToFriends(sl));
            this.lettersForSendQuery.Enqueue(task);
        }

        public static void Main(string[] args)
        {
            Node node = new Node();
            node.me = GetLocalIPEndPoint();
            //node.dieFriend = DieFriendMessage;
            node.newFriend = NewFriendMessage;

            node.server = new UdpClient();
            node.server.Client.Bind(node.me);

            node.SayHello();
            Console.WriteLine(node.me.ToString());

            Task.Run(() => node.ListenTCPConnections());
            Task.Run(() => node.ListenUDPRequests());
            Task.Run(() => node.LettersForSendQueryHandler());

            #region SendLetter

            while (true)
            {
                var key = Console.ReadKey();
                char c = key.KeyChar;

                node.PutLetterToQueue(c);
            }
            #endregion
            /*
            node.tcpListener.Stop();
            node.server.Close();
            node.server.Dispose();
            */
        }

        public Node()
        {
            this.me = Node.GetLocalIPEndPoint();
        }
    }

    public static class EndpointExt
    {
        public static IPEndPoint ToIPEndPoint(this EndPoint ep, int port)
        {
            string ip = ep.ToString();
            string clientIp = "";
            for (int i = 0; ; i++)
            {
                if (ip[i] == ':')
                    break;
                clientIp += ip[i];
            }
            return new IPEndPoint(IPAddress.Parse(clientIp), port);
        }
    }
}