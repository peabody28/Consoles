using Microsoft.AspNetCore.Mvc;
using Nodes;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using NodesWeb.Hubs;
using Nodes;


namespace NodesWeb.Controllers
{
    public class HomeController : Controller
    {
        //private readonly ILogger<HomeController> _logger;

        // хаб определен в контроллере [КОСТЫЛЬ]
        public static IHubContext<MyHub> _hub;

        public HomeController(IHubContext<MyHub> mhc)
        {
            _hub = mhc;
        }

        public static WebNode node = null;

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }

        static Task t1 = null;
        static Task t2 = null;
        static Task t3 = null;

        public string Init()
        {
            if (node == null)
            {
                node = new WebNode();
                
                node.server = new UdpClient();
                var myEndPoint = node.GetMyEndPoint();
                node.server.Client.Bind(myEndPoint);
                node.SayHello();

                if (t1 == null)
                    t1 = Task.Run(() => node.ListenTCPConnections());
                if (t2 == null)
                    t2 = Task.Run(() => node.ListenUDPRequests());
                if (t3 == null)
                    t3 = Task.Run(() => node.LettersForSendQueryHandler());
                return myEndPoint.ToString();
            }
            return "";
        }
    }
}
