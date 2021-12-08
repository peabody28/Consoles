using Nodes;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;
using NodesWeb.Hubs;
using System;

namespace NodesWeb
{
    public class WebNode : Node
    {
        public static IHubContext<MyHub> _hub;

        static Task t1 = null;
        static Task t2 = null;
        static Task t3 = null;

        private void SendLetterToWebClients(string message)
        {
            _hub.Clients.All.SendAsync("Send", message);
        }

        protected override void GetLetterAction(IPEndPoint client, SendedLetter sl)
        {
            if (!this.sendedLetters.Contains(sl))
            {
                this.SendLetterToWebClients(sl.letter.ToString());

                while (this.sendedLetters.Count >= WebNode.maxLettersQuery)
                    this.sendedLetters.RemoveAt(0);
                this.sendedLetters.Add(sl);

                var task = new Task(() => this.SendLetterToFriends(sl, client));
                this.lettersForSendQuery.Enqueue(task);
            }
        }

        public void SendLetterToConsoles(string message)
        {
            PutLetterToQueue(Convert.ToChar(message));
        }

        public WebNode(IHubContext<MyHub> hub)
        {
            _hub = hub;
            this.me = GetLocalIPEndPoint();

            server = new UdpClient();
            var myEndPoint = GetMyEndPoint();
            server.Client.Bind(myEndPoint);
            SayHello();

            if (t1 == null)
                t1 = Task.Run(() => ListenTCPConnections());
            if (t2 == null)
                t2 = Task.Run(() => ListenUDPRequests());
            if (t3 == null)
                t3 = Task.Run(() => LettersForSendQueryHandler());
        }
    }
}
