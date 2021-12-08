using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nodes;
using NodesWeb;
using NodesWeb.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
//using Microsoft.AspNet.SignalR;
using Microsoft.AspNetCore.SignalR;
using NodesWeb.Hubs;


namespace NodesWeb.Controllers
{
   
    public class HomeController : Controller
    {
        //private readonly ILogger<HomeController> _logger;

        public static IHubContext<MyHub> _hub;

        public HomeController(IHubContext<MyHub> mhc)
        {
            _hub = mhc;
        }

        public static WebNode node = null;

        static Task t1 = null;
        static Task t2 = null;
        static Task t3 = null;

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        public string Init()
        {
            if (node == null)
            {
                node = new WebNode();
                node.me = WebNode.GetLocalIPEndPoint();

                node.server = new UdpClient();
                node.server.Client.Bind(node.me);

                if (node != null && node.server != null)
                    WebNode.SayHello(node);

                if (t1 == null)
                    t1 = Task.Run(() => Node.ListenTCPConnections(node));
                if (t2 == null)
                    t2 = Task.Run(() => Node.ListenUDPRequests(node));
                if (t3 == null)
                    t3 = Task.Run(() => Node.LettersForSendQueryHandler(node));

                //WebNode.HubContext = GlobalHost.ConnectionManager.;
            }
            return node.me.ToString();
        }
        /*
        [HttpPost]
        public void Index(Letter letter)
        {
            if(letter.LetterText != null)
            {
                char lastLetter = letter.LetterText[letter.LetterText.Length - 1];
                node.PutLetterToQueue(lastLetter);

                //node.currentString += lastLetter;
                Hub(lastLetter.ToString());
            }
               
        }
        */

        /*
        [HttpGet]
        public string GetString()
        {
            if (node != null)
            {
                string x = (string)node.currentString.Clone();
                //node.currentString = "";
                return x;
            }
            return "";
        }
        */
        public void Hub(string letter)
        {
            // 
        }
            
        public static void SendToClients(string message)
        {
            _hub.Clients.All.SendAsync("Send", message);
        }
        /*
        public void SendToClientsAsync(string data)
        {
            //_hub.Clients.All.Send(data);
        }
        */
    }
}
