using System.Net;
using System.Collections.Concurrent;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPServer.Models;
using SIPServer.Call;
using System.Text;

namespace SIPServer
{

    class Server
    {
        private static int SIP_LISTEN_PORT = 5060;
        private readonly SIPTransport SipTransport;


        private ConcurrentDictionary<string, SIPRegisterAccount> Registrations;
        private ConcurrentDictionary<string, SIPCall>            AcceptedCalls;
        private ConcurrentDictionary<string, SIPCall>            ActiveCalls;

        private Action<string> AppendToLog;

        public Server(Action<string> appendToLog)
        {
            AppendToLog = appendToLog; // Store the logging action

            SipTransport = new SIPTransport();
            SipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, SIP_LISTEN_PORT)));

            //SipTransport.EnableTraceLogs();

            SipTransport.SIPTransportRequestReceived += OnRequest;


            Registrations   = new ConcurrentDictionary<string, SIPRegisterAccount>();
            AcceptedCalls   = new ConcurrentDictionary<string, SIPCall>();
            ActiveCalls     = new ConcurrentDictionary<string, SIPCall>();
        }

        public async Task AnswerCall(string user)
        {
            SIPCall call;
            bool    ret = false;

            if (!AcceptedCalls.TryGetValue(user, out call))
                return;

            CallManager CallManager = new CallManager(call, AppendToLog);

            //ret = await CallManager.AnswerAsync();
            
            //if (!ret)
            //    AppendToLog($"Call Not Answerd: from {call.UA.ContactURI}");

            //ActiveCalls.TryAdd(call.UA.Dialogue.CallId, call);
            //AppendToLog($"Call Answerd: from {call.UA.ContactURI}");
        }
        public async Task EndCall(string CallId)
        {
            SIPCall call;

            if (ActiveCalls.TryRemove(CallId, out call))
            {
                call.UA.Hangup();
                AppendToLog($"Call ended.");
            }
            else
            {
                AppendToLog($"No active call with user {call.User} found.");
            }
        }

        private async Task OnRequest(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                SIPRegisterAccount user;

                user.Username = sipRequest.Header.From.FromURI.User;
                user.Password = "xxx";
                user.Expiry = 1;
                user.Domain = "xyz";
              
                if (sipRequest.Method == SIPMethodsEnum.INVITE)
                {
                    AppendToLog($"Incoming call request: {sipRequest.URI}.");

                    SIPUserAgent ua = new SIPUserAgent(SipTransport, null);
                    ua.OnCallHungup += OnHangup;

                    var uas = ua.AcceptCall(sipRequest);

                    AcceptedCalls.TryAdd($"{user.Username}@{user.Domain}", new SIPCall(ua, uas, sipRequest.URI.User));
                }
                else if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    SIPResponse byeResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                    await SipTransport.SendResponseAsync(byeResponse);
                    AppendToLog($"Call Ended");

                }
                else if (sipRequest.Method == SIPMethodsEnum.SUBSCRIBE)
                {
                    SIPResponse notAllowededResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.MethodNotAllowed, null);
                    await SipTransport.SendResponseAsync(notAllowededResponse);
                }
                else if (sipRequest.Method == SIPMethodsEnum.OPTIONS || sipRequest.Method == SIPMethodsEnum.REGISTER)
                {

                    Registrations.TryAdd($"{user.Username}@{user.Domain}", user);

                    SIPResponse optionsResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await SipTransport.SendResponseAsync(optionsResponse);
                    AppendToLog($"Registration received: {user.Username}@{user.Domain}");

                }
            }
            catch (Exception reqExcp)
            {
                AppendToLog($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
            }
        }

        private void OnHangup(SIPDialogue dialogue)
        {
            SIPCall call;

            if (dialogue != null)
            {
                string callID = dialogue.CallId;
                if (ActiveCalls.ContainsKey(callID))
                {
                    if (ActiveCalls.TryRemove(callID, out call))
                    {
                        // This app only uses each SIP user agent once so here the agent is 
                        // explicitly closed to prevent is responding to any new SIP requests.
                        call.UA.Close();
                    }
                }
            }
        }

    }
}