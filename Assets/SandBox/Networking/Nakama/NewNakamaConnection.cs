using System.Collections;
using System.Collections.Generic;
using Nakama;
using UnityEngine;

public class NewNakamaConnection : MonoBehaviour
{
        public string scheme = "http";
        public string host = "161.118.254.99";
        public int port = 7350;
        public string serverKey = "defaultkey";


        private IClient Client;
        private ISession Session;
        private ISocket Socket;

        private string ticket;
        

        async void Start()
        {
            Client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
            Session = await Client.AuthenticateDeviceAsync(SystemInfo.deviceUniqueIdentifier);
            Socket = Client.NewSocket();
            await Socket.ConnectAsync(Session, true);

            Socket.ReceivedMatchmakerMatched += OnReceivedMatchmakerMatched;
                
            Debug.Log("Session connected");
            Debug.Log(Session);
            Debug.Log("Socket connected");
            Debug.Log(Socket);


        }

        public async void FindMatch()
        {
            Debug.Log("FindMatch ....");

            var matchmakingTicket = await Socket.AddMatchmakerAsync("*", 2, 2);
            ticket = matchmakingTicket.Ticket;
        }

        private async void OnReceivedMatchmakerMatched(IMatchmakerMatched matchmkerMatched)
        {
          var match =  await Socket.JoinMatchAsync(matchmkerMatched);
          Debug.Log("Our Session ID : " + match.Self.SessionId);

          foreach (var user in match.Presences)
          {
              Debug.Log("Connected User Session ID : " + user.SessionId);
          }
        }
        
}

