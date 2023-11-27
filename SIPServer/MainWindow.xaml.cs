using Microsoft.VisualBasic.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Windows;
using System.Net;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SIPServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string DEFAULT_DESTINATION_SIP_URI = "sip:Magdy@localhost:5060";

            Console.WriteLine("SIPSorcery client user agent example.");
            Console.WriteLine("Press ctrl-c to exit.");

            // Plumbing code to facilitate a graceful exit.
            ManualResetEvent exitMre = new ManualResetEvent(false);
            bool preferIPv6 = false;
            bool isCallHungup = false;
            bool hasCallFailed = false;


            SIPURI callUri = SIPURI.ParseSIPURI(DEFAULT_DESTINATION_SIP_URI);

            Console.WriteLine($"Call destination {callUri}.");
            

            // Set up a default SIP transport.
            var sipTransport = new SIPTransport();
            sipTransport.PreferIPv6NameResolution = preferIPv6;
            sipTransport.EnableTraceLogs();

            var audioSession = new WindowsAudioEndPoint(new AudioEncoder());
            audioSession.RestrictFormats(x => x.Codec == AudioCodecsEnum.PCMA || x.Codec == AudioCodecsEnum.PCMU);
            //audioSession.RestrictFormats(x => x.Codec == AudioCodecsEnum.G722);
            var rtpSession = new VoIPMediaSession(audioSession.ToMediaEndPoints());

            var offerSDP = rtpSession.CreateOffer(preferIPv6 ? IPAddress.IPv6Any : IPAddress.Any);

            // Create a client user agent to place a call to a remote SIP server along with event handlers for the different stages of the call.
            var uac = new SIPClientUserAgent(sipTransport);
            uac.CallTrying += (uac, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Trying: {resp.StatusCode} {resp.ReasonPhrase}.");
            uac.CallRinging += async (uac, resp) =>
            {
                Console.WriteLine($"{uac.CallDescriptor.To} Ringing: {resp.StatusCode} {resp.ReasonPhrase}.");
                if (resp.Status == SIPResponseStatusCodesEnum.SessionProgress)
                {
                    if (resp.Body != null)
                    {
                        var result = rtpSession.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(resp.Body));
                        if (result == SetDescriptionResultEnum.OK)
                        {
                            await rtpSession.Start();
                            Console.WriteLine($"Remote SDP set from in progress response. RTP session started.");
                        }
                    }
                }
            };
            uac.CallFailed += (uac, err, resp) =>
            {
                Console.WriteLine($"Call attempt to {uac.CallDescriptor.To} Failed: {err}");
                hasCallFailed = true;
            };
            uac.CallAnswered += async (iuac, resp) =>
            {
                if (resp.Status == SIPResponseStatusCodesEnum.Ok)
                {
                    Console.WriteLine($"{uac.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");

                    if (resp.Body != null)
                    {
                        var result = rtpSession.SetRemoteDescription(SdpType.answer, SDP.ParseSDPDescription(resp.Body));
                        if (result == SetDescriptionResultEnum.OK)
                        {
                            await rtpSession.Start();
                        }
                        else
                        {
                            Console.WriteLine($"Failed to set remote description {result}.");
                            uac.Hangup();
                        }
                    }
                    else if (!rtpSession.IsStarted)
                    {
                        Console.WriteLine($"Failed to set get remote description in session progress or final response.");
                        uac.Hangup();
                    }
                }
                else
                {
                    Console.WriteLine($"{uac.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");
                }
            };

            // The only incoming request that needs to be explicitly handled for this example is if the remote end hangs up the call.
            sipTransport.SIPTransportRequestReceived += async (SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest) =>
            {
                if (sipRequest.Method == SIPMethodsEnum.BYE)
                {
                    SIPResponse okResponse = SIPResponse.GetResponse(sipRequest, SIPResponseStatusCodesEnum.Ok, null);
                    await sipTransport.SendResponseAsync(okResponse);

                    if (uac.IsUACAnswered)
                    {
                        Console.WriteLine("Call was hungup by remote server.");
                        isCallHungup = true;
                        exitMre.Set();
                    }
                }
            };

            // Start the thread that places the call.
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(
                SIPConstants.SIP_DEFAULT_USERNAME,
                null,
                callUri.ToString(),
                SIPConstants.SIP_DEFAULT_FROMURI,
                callUri.CanonicalAddress,
                null, null, null,
            SIPCallDirection.Out,
                SDP.SDP_MIME_CONTENTTYPE,
                offerSDP.ToString(),
                null);

            uac.Call(callDescriptor, null);

            // Ctrl-c will gracefully exit the call at any point.
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e)
            {
                e.Cancel = true;
                exitMre.Set();
            };

            // Wait for a signal saying the call failed, was cancelled with ctrl-c or completed.
            exitMre.WaitOne();

            Console.WriteLine("Exiting...");

            rtpSession.Close(null);

            if (!isCallHungup && uac != null)
            {
                if (uac.IsUACAnswered)
                {
                    Console.WriteLine($"Hanging up call to {uac.CallDescriptor.To}.");
                    uac.Hangup();
                }
                else if (!hasCallFailed)
                {
                    Console.WriteLine($"Cancelling call to {uac.CallDescriptor.To}.");
                    uac.Cancel();
                }

                // Give the BYE or CANCEL request time to be transmitted.
                Console.WriteLine("Waiting 1s for call to clean up...");
                Task.Delay(1000).Wait();
            }

            if (sipTransport != null)
            {
                Console.WriteLine("Shutting down SIP transport...");
                sipTransport.Shutdown();
            }
        }
    }
}