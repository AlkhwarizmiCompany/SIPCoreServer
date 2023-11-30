using Google.Cloud.Speech.V1;
using Google.Protobuf;
using Grpc.Core;
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorceryMedia.Abstractions;
using System.IO;
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
        Server server;
        private static SpeechClient speechClient;

        public MainWindow()
        {

            InitializeComponent();

            server = new Server(AppendToLog);
        }
        private void AppendToLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                EventsInfoTextBox.AppendText(message + Environment.NewLine);
            });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            //server.AnswerCall("thisis@xyz");

            //ASRFile();
            //ASRStreamFile();
            RecordFile();
        }



        public void ASRFile()
        {
            // Set your Google Cloud credentials
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "G:\\src\\SIP\\SIPServer\\SIPServer\\tensile-axiom-281814-5428b0b1f7b0.json");

            // Set the audio file path
            string audioFilePath = "G:\\src\\SIP\\SIPServer\\SIPServer\\originalSound.wav";

            // Create a SpeechClient
            SpeechClient speech = SpeechClient.Create();

            // Create a RecognitionConfig
            RecognitionConfig config = new RecognitionConfig
            {
                Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                LanguageCode = LanguageCodes.Arabic.Egypt,
            };

            // Create a RecognitionAudio
            RecognitionAudio audio = RecognitionAudio.FromFile(audioFilePath);

            // Create the SpeechRecognitionResponse
            try
            {
                RecognizeResponse response = speech.Recognize(config, audio);

                foreach (var result in response.Results)
                {
                    foreach (var alternative in result.Alternatives)
                    {
                        AppendToLog($"Transcript: {alternative.Transcript}");
                    }
                }
            }
            catch (RpcException e)
            {
                AppendToLog($"Error: {e.Status}");
            }
        }


        //public async Task ASRStreamFile()
        //{
        //    // Set your Google Cloud credentials
        //    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", "G:\\src\\SIP\\SIPServer\\SIPServer\\tensile-axiom-281814-5428b0b1f7b0.json");

        //    // Set the audio file path
        //    string audioFilePath = "G:\\src\\SIP\\SIPServer\\SIPServer\\originalSound.wav";

        //    // Create a SpeechClient
        //    SpeechClient speech = SpeechClient.Create();

        //    // Create a streaming call, with calls to Write() alternating with calls to Read()
        //    using (var call = speech.StreamingRecognize())
        //    {
        //        // Write the initial request with the configuration
        //        await call.WriteAsync(
        //            new StreamingRecognizeRequest
        //            {
        //                StreamingConfig = new StreamingRecognitionConfig
        //                {
        //                    Config = new RecognitionConfig
        //                    {
        //                        Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
        //                        SampleRateHertz = 16000,
        //                        LanguageCode = LanguageCodes.English.UnitedStates,
        //                    },
        //                    InterimResults = true,
        //                },
        //            });

        //        // Open the audio file for reading
        //        using (var audioStream = new FileStream(audioFilePath, FileMode.Open, FileAccess.Read))
        //        {
        //            byte[] buffer = new byte[4096];
        //            int bytesRead;

        //            // Continuously read and send audio data in chunks
        //            while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        //            {
        //                await call.WriteAsync(
        //                    new StreamingRecognizeRequest
        //                    {
        //                        AudioContent = Google.Protobuf.ByteString.CopyFrom(buffer, 0, bytesRead),
        //                    });
        //            }
        //        }

        //        // Complete the stream to signal the end of the audio data
        //        await call.WriteCompleteAsync();

        //        // Read the responses from the server
        //        Task.Run(async () =>
        //        {
        //            while (await call.GetResponseStream().MoveNextAsync())
        //            {
        //                StreamingRecognitionResult result = call.ResponseStream.Current;
        //                foreach (var alternative in result.Alternatives)
        //                {
        //                    Console.WriteLine($"Transcript: {alternative.Transcript}");
        //                }
        //            }
        //        }).Wait();
        //    }

        //}


        static void RecordFile()
        {
            string outputPath = "G:\\src\\SIP\\SIPServer\\SIPServer\\file.wav";

            using (var waveIn = new WaveInEvent())
            {
                waveIn.DeviceNumber = 0; // Set the device number (you may need to adjust this)
                waveIn.WaveFormat = new WaveFormat(44100, 1); // Adjust sample rate and channels as needed

                WaveFileWriter waveWriter = null;

                waveIn.DataAvailable += (sender, e) =>
                {
                    waveWriter?.Write(e.Buffer, 0, e.BytesRecorded);
                };

                waveIn.RecordingStopped += (sender, e) =>
                {
                    waveWriter?.Dispose();
                    waveWriter = null;

                    waveIn.Dispose();
                };

                waveWriter = new WaveFileWriter(outputPath, waveIn.WaveFormat);

                waveIn.StartRecording();

                waveIn.StopRecording();
            }

        }

        private void End_Call(object sender, RoutedEventArgs e)
        {
            server.EndCall();

        }

       
    }
}

