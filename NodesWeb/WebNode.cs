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
        private static IHubContext<MyHub> _hub;

        private void SendLetterToWebClients(string message)
        {
            _hub.Clients.All.SendAsync("Send", message);
        }

        protected override void UseLetter(char letter)
        {
            this.SendLetterToWebClients(letter.ToString());
        }

        public void SendLetterToConsoles(string message)
        {
            PutLetterToQueue(Convert.ToChar(message));
        }

        public WebNode(IHubContext<MyHub> hub) : base()
        {
            _hub = hub;
        }
    }
}
