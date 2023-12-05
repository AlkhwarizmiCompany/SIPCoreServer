using System.Net;
using System.Collections.Concurrent;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPServer.Models;
using SIPServer.Call;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SIPServer
{
    class Server
    {
        private int SIP_LISTEN_PORT;
        private SIPTransport SipTransport;


        private ConcurrentDictionary<string, SIPRegisterAccount>    Registrations;
        private ConcurrentDictionary<string, SIPCall>               AcceptedCalls;
        private ConcurrentDictionary<string, SIPCall>               ActiveCalls;

        private readonly IConfiguration _configuration;
        private readonly IServiceProvider _serviceProvider;
        string callId;
        private readonly Action<string> _appendToLog;

        public Server(IConfiguration configuration, IServiceProvider serviceProvider, Action<string> AppendToLog)
        {
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _appendToLog = AppendToLog;

            if (!int.TryParse(_configuration["port"], out SIP_LISTEN_PORT))
                SIP_LISTEN_PORT = 5060; // default value
            
            Registrations   = new ConcurrentDictionary<string, SIPRegisterAccount>();
            AcceptedCalls   = new ConcurrentDictionary<string, SIPCall>();
            ActiveCalls     = new ConcurrentDictionary<string, SIPCall>();


            var credPath = _configuration["GoogleAppCredentials"];
            
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credPath);
        }

        public void Start()
        {
            SipTransport = new SIPTransport();
            SipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, SIP_LISTEN_PORT)));

            //SipTransport.EnableTraceLogs();

            SipTransport.SIPTransportRequestReceived += OnRequest;
        }


        public async Task AnswerCall(string user)
        {
            SIPCall call;
            bool ret = false;

            if (!AcceptedCalls.TryGetValue(user, out call))
                return;

            CallManager CallManager = ActivatorUtilities.CreateInstance<CallManager>(_serviceProvider, call);

            ret = await CallManager.AnswerAsync();

            if (!ret)
                call.Log($"Call Not Answerd: from {call.UA.ContactURI}");

            callId = call.UA.Dialogue.CallId;

            ActiveCalls.TryAdd(call.UA.Dialogue.CallId, call);
           
            call.Log($"Call Answerd: from {call.UA.ContactURI}");
        }

        public async Task EndCall(string CallId)
        {
            SIPCall call;
            
            CallId = callId;

            if (ActiveCalls.TryGetValue(CallId, out call))
            {
                call.UA.Hangup();
                call.Log($"Call ended.");
            }
            else
            {
                call.Log($"No active call with user {call.User} found.");
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
                    SIPUserAgent ua = new SIPUserAgent(SipTransport, null);
                    ua.OnCallHungup += OnHangup;

                    var uas = ua.AcceptCall(sipRequest);

                    SIPCall call = new SIPCall(ua, uas, sipRequest, _appendToLog);

                    AcceptedCalls.TryAdd($"{user.Username}@{user.Domain}", call);

                    call.InitCallLog($"Incoming call request: {sipRequest.URI}.");
                }
                else if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    SIPResponse byeResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.CallLegTransactionDoesNotExist, null);
                    await SipTransport.SendResponseAsync(byeResponse);
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
                }
            }
            catch (Exception reqExcp)
            {
                //call.Log($"Exception handling {sipRequest.Method}. {reqExcp.Message}");
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
                        call.WaveFile.Close();
                    }
                }
            }
        }

    }
}