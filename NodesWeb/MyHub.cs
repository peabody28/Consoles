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
        public static WebNode _node;
        public MyHub(WebNode node)
        {
            _node = node;
        }
        public void Send(string message)
        {
            this.Clients.All.SendAsync("Send", message);
            _node.SendLetterToConsoles(message);
        }
    }
}
