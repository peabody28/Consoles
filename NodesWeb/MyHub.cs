using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NodesWeb.Controllers;

namespace NodesWeb.Hubs
{
    public class MyHub : Hub
    {
        public void Send(string message)
        {
            this.Clients.All.SendAsync("Send", message);
            SendToConsoles(message);
        }

        public void SendToConsoles(string message)
        {
            HomeController.node.PutLetterToQueue(Convert.ToChar(message));
        }
    }
}
