﻿using System;
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

        private List<SendedLetter> sendedLetters = new List<SendedLetter>();

        private IPEndPoint me;

        private List<IPEndPoint> friends = new List<IPEndPoint>();

        private TcpListener tcpListener = null;

        private UdpClient server = null;

        private static Random rand = new Random((int)DateTime.Now.Ticks);

        // Широковещательный запрос
        private static void SayHello(Node node)
        {
            var client = node.server;

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
                //Console.WriteLine(ex.Message);
            }
            //client.Close();
        }

        // Слушаю широковещательные запросы от консолей
        public static void ListenUDPRequests(Node node)
        {
            var server = node.server;
            IPEndPoint RemoteIpEndPoint = null;
            try
            {
                while (true)
                {
                    // Ожидание запроса новой консоли
                    byte[] data = server.Receive(ref RemoteIpEndPoint);

                    if (RemoteIpEndPoint.Equals(node.me) == false)
                    {
                        // Cоединяюсь по TCP с новой консолью
                        TcpClient tcpClient = new TcpClient();
                        tcpClient.Connect(RemoteIpEndPoint);
                        #region Send

                        NetworkStream stream = tcpClient.GetStream();

                        var message = FriendListForSend(node);

                        stream.Write(message, 0, message.Length);
                        stream.Close();

                        #endregion
                        tcpClient.Close();

                        if (!node.friends.Contains(RemoteIpEndPoint))
                        {
                            node.friends.Add(RemoteIpEndPoint);
                            //Console.WriteLine("I know " + RemoteIpEndPoint.ToString());
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Возникло исключение: " + ex.ToString() + "\n  " + ex.Message);
            }
            //server.Close();
            //server.Dispose();
        }

        // Обновение списка друзей
        private static void UpdateNodesFromBytes(Node node, byte[] data)
        {
            string ip = "";
            int port = 0;

            int index = 2;
            for (int i = 0; i < data[1]; i++)
            {
                for (int j = index; j < index + 5; j++)
                {
                    if (j == index + 4)
                    {
                        port = data[j] + 8000;
                    }
                    else
                    {
                        ip += data[j].ToString();
                        if (j != index + 3)
                            ip += ".";
                    }
                }
                var friend = new IPEndPoint(IPAddress.Parse(ip), port);
                ip = "";
                index += 5;
                if (!friend.Equals(node.me) && !node.friends.Contains(friend))
                {
                    node.friends.Add(friend);
                    //Console.WriteLine("I know " + friend.ToString());
                }

            }
        }

        // Подготовка массива байтов для отправки при запросе (кого ты знаешь?)
        private static byte[] FriendListForSend(Node node)
        {
            byte[] response = new byte[2 + (node.friends.Count + 1) * 5];
            response[0] = 3;
            response[1] = (byte)(node.friends.Count + 1);

            var myIpBytes = node.me.Address.GetAddressBytes();
            response[2] = myIpBytes[0];
            response[3] = myIpBytes[1];
            response[4] = myIpBytes[2];
            response[5] = myIpBytes[3];
            response[6] = (byte)(node.me.Port - 8000);

            int index = 7;
            for (int i = 0; i < node.friends.Count; i++)
            {
                var ipBytes = node.friends[i].Address.GetAddressBytes();
                int j = index;
                for (; j < index + 4; j++)
                    response[j] = ipBytes[j - index];
                response[j] = (byte)(node.friends[i].Port - 8000);
                index += 5;
            }

            return response;
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
                    //Console.WriteLine("Port " + myPort + " isn't free");
                    myPort++;
                }
            }
            tcp.Close();
            tcp.Dispose();
            return local;
        }

        // Слушаю запросы на подключение от консолей
        public static void ListenTCPConnections(Node node)
        {
            // могут прийти 2 типа запросов:
            //    некая нода присылает друзей включая себя (код 3)
            //    некая нода присылает символ (код 4)

            var server = node.tcpListener;

            server = new TcpListener(node.me);
            server.Start();

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                var stream = client.GetStream();
                var data = new byte[1024];
                stream.Read(data, 0, data.Length);

                if (data[0] == 3)
                {
                    UpdateNodesFromBytes(node, data);
                }
                else if (data[0] == 4)
                {
                    int port = data[3] + 8000;
                    var clientEndPoint = client.Client.RemoteEndPoint.ToIPEndPoint(port);
   
                    char letter = Convert.ToChar(data[1]);

                    var sl = new SendedLetter(letter, data[2]);
                   
                    if (!node.sendedLetters.Contains(sl) && !clientEndPoint.Equals(node.me))
                    {
                        Console.Write(letter);

                        while (node.sendedLetters.Count >= 26)
                            node.sendedLetters.RemoveAt(0);
                        node.sendedLetters.Add(sl);

                        SendLetterToFriends(node, sl, clientEndPoint);
                    }
                }
                stream.Close();
                client.Close();
            }
        }

        public static void SendLetterToFriends(Node node, SendedLetter sendedLetter, IPEndPoint sender = null)
        {

            byte[] data = new byte[4];
            data[0] = 4;
            data[1] = (byte)Convert.ToInt32(sendedLetter.letter);
            data[2] = sendedLetter.hash;

            data[3] = (byte)(node.me.Port - 8000);

            for (int i = 0; i < node.friends.Count; i++)
            {
                var friend = node.friends[i];
                if (friend.Equals(sender))
                    continue;

                Task.Run(() =>
                {
                    try
                    {
                        TcpClient tcpClient = new TcpClient();
                        tcpClient.Connect(friend);
                        NetworkStream stream = tcpClient.GetStream();
                        stream.Write(data, 0, data.Length);
                        stream.Close();
                        tcpClient.Close();
                        tcpClient.Dispose();
                    }
                    catch (Exception ex)
                    {
                        node.friends.Remove(friend);
                    }
                });
               
            }

        }

        static void Main(string[] args)
        {
            Node node = new Node();

            node.me = GetLocalIPEndPoint();

            node.server = new UdpClient();
            node.server.Client.Bind(node.me);

            SayHello(node);
            //Console.WriteLine(node.me.ToString());

            Task.Run(() => ListenTCPConnections(node));
            Task.Run(() => ListenUDPRequests(node));

            #region SendLetter

            while (true)
            {
                var key = Console.ReadKey();
                char c = key.KeyChar;

                byte num = (byte)rand.Next(0, 255);

                var sl = new SendedLetter(c, num);

                while (node.sendedLetters.Count >= 26)
                    node.sendedLetters.RemoveAt(0);
                node.sendedLetters.Add(sl);

                SendLetterToFriends(node, sl);
            }
            #endregion
        }
    }

    public static class EndpointExt
    {
        public static IPEndPoint ToIPEndPoint(this EndPoint ep, int port)
        {
            string ip = ep.ToString();
            string clientIp = "";
            for(int i = 0;;i++)
            {
                if (ip[i] == ':')
                    break;
                clientIp += ip[i];
            }
            return new IPEndPoint(IPAddress.Parse(clientIp), port);
        }
    }
}
