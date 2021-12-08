using Nodes;
using System.Net;
using System.Threading.Tasks;
using NodesWeb.Controllers;
using Microsoft.AspNetCore.SignalR;
using System.Net.Sockets;

namespace NodesWeb
{
    public class WebNode : Node
    {
        private void SendLetterToWebClients(string message)
        {
            HomeController._hub.Clients.All.SendAsync("Send", message);
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

        public WebNode()
        {
            this.me = Node.GetLocalIPEndPoint();
        }
    }
}
