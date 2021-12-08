using Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using NodesWeb.Controllers;

namespace NodesWeb
{
    public class WebNode : Node
    {

        public string currentString = "";

        public override void GetLetterAction(IPEndPoint client, SendedLetter sl)
        {
            if (!this.sendedLetters.Contains(sl))
            {
                HomeController.SendToClients(sl.letter.ToString());

                while (this.sendedLetters.Count >= WebNode.maxLettersQuery)
                    this.sendedLetters.RemoveAt(0);
                this.sendedLetters.Add(sl);

                var task = new Task(() => SendLetterToFriends(this, sl, client));
                this.lettersForSendQuery.Enqueue(task);
            }
        }
    }
}
