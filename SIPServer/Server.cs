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
        private readonly int    SIP_LISTEN_PORT;
        private readonly bool   USE_MIC;
        private SIPTransport    SipTransport;


        private ConcurrentDictionary<string, SIPRegisterAccount>    Registrations;
        private ConcurrentDictionary<string, SIPCall>               AcceptedCalls;
        private ConcurrentDictionary<string, CallManager>           ActiveCalls;

        private readonly IConfiguration     _configuration;
        private readonly IServiceProvider   _serviceProvider;

        private readonly Action<string>     _appendToLog;


        public Server(IConfiguration configuration, IServiceProvider serviceProvider, Action<string> AppendToLog)
        {
            _configuration      = configuration;
            _serviceProvider    = serviceProvider;
            _appendToLog        = AppendToLog;

            if (!int.TryParse(_configuration["port"], out SIP_LISTEN_PORT))
                SIP_LISTEN_PORT = 5060; // default value

            if (!bool.TryParse(_configuration["useMic"], out USE_MIC))
                USE_MIC = false;

            Registrations   = new ConcurrentDictionary<string, SIPRegisterAccount>();
            AcceptedCalls   = new ConcurrentDictionary<string, SIPCall>();
            ActiveCalls     = new ConcurrentDictionary<string, CallManager>();


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

            if (AcceptedCalls.Count <= 0)
                return;

            if (string.IsNullOrEmpty(user))
            {

                var Key = AcceptedCalls.ElementAt(0).Key;

                if (!AcceptedCalls.TryRemove(Key, out call))
                    return;
            }
            else
            {

                if (!AcceptedCalls.TryGetValue(user, out call))
                   return;
            }

            CallManager CallManager = ActivatorUtilities.CreateInstance<CallManager>(_serviceProvider, call);


            if (USE_MIC)
                return;

            ret = await CallManager.AnswerAsync();

            if (!ret)
                call.Log($"Call Not Answerd: from {call.User}");

            ActiveCalls.TryAdd(call.UA.Dialogue.CallId, CallManager);
           
            call.Log($"Call Answerd: from {call.User}");
        }

        public async Task EndCall(string user)
        {
            CallManager CallManager;
            

            if (string.IsNullOrEmpty(user))
            {

                var Key = ActiveCalls.ElementAt(0).Key;

                if (!ActiveCalls.TryRemove(Key, out CallManager))
                    return;
            }
            else
            {

                if (!ActiveCalls.TryGetValue(user, out CallManager))
                    return;
            }

            CallManager.Stop();

        }


        private async Task OnRequest(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest)
        {
            try
            {
                SIPRegisterAccount user;

                user.Username = sipRequest.RemoteSIPEndPoint.ToString();
                user.Password = "xxx";
                user.Expiry = 1;
                user.Domain = "xyz";

                if (sipRequest.Method == SIPMethodsEnum.INVITE)
                {
                    SIPUserAgent ua = new SIPUserAgent(SipTransport, null);
                    ua.OnCallHungup += OnHangup;

                    var uas = ua.AcceptCall(sipRequest);

                    SIPCall call = new SIPCall(ua, uas, sipRequest, _appendToLog);

                    AcceptedCalls.TryAdd($"{call.User}", call);

                    call.InitCallLog($"Incoming call request: {call.User}.");

                    //AnswerCall(call.User);
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
            CallManager CallManager;

            if (dialogue != null)
            {
                string callID = dialogue.CallId;
                if (ActiveCalls.ContainsKey(callID))
                {
                    if (ActiveCalls.TryRemove(callID, out CallManager))
                    {
                        CallManager.Stop();
                    }
                }
            }
        }

    }
}