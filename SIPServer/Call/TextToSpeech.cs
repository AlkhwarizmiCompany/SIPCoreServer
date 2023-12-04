using Google.Cloud.Speech.V1;
using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.TextToSpeech.V1;

using static Google.Cloud.Speech.V1.RecognitionConfig.Types;
using System.IO;
using SIPServer.Models;
using static System.Net.Mime.MediaTypeNames;
using Windows.Media.Protection.PlayReady;
using System;
using System.IO;
using Google.Protobuf;

namespace SIPServer.Call
{

    class TextToSpeech
    {

        private SIPCall        Call;
        private readonly Action<string> AppendToLog;

        private TextToSpeechClient      TTSClient;
        private VoiceSelectionParams    Voice;
        private AudioConfig             AudioConfig;

        public TextToSpeech(SIPCall call, Action<string> appendToLog)
        {

            Call = call;
            AppendToLog = appendToLog;

            Initialization();
        }


        public void Initialization()
        {
            TTSClient = TextToSpeechClient.Create();

            Voice = new VoiceSelectionParams
            {
                LanguageCode = LanguageCodes.Arabic.Egypt,
                SsmlGender = SsmlVoiceGender.Female
            };

            AudioConfig = new AudioConfig
            {
                AudioEncoding = Google.Cloud.TextToSpeech.V1.AudioEncoding.Linear16
            };

        }

        private void PlayByteArray(byte[] byteArray)
        {
            using (MemoryStream memoryStream = new MemoryStream(byteArray))
            {
                // Use NAudio to play the audio
                using (WaveFileReader waveFileReader = new WaveFileReader(memoryStream))
                {
                    using (WaveOutEvent waveOut = new WaveOutEvent())
                    {
                        waveOut.Init(waveFileReader);
                        waveOut.Play();

                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            System.Threading.Thread.Sleep(100);
                        }
                    }
                }
            }

            Call.IsRunning = false;
        }

        public void TTS()
        {
            while(true)
            {
                string ChatbotResponse = Call.ChatbotAnswers.Take(); // Blocking call

                if (string.IsNullOrEmpty(ChatbotResponse))
                    continue;

                SynthesisInput input = new SynthesisInput
                {
                    Text = ChatbotResponse
                };

                var response = TTSClient.SynthesizeSpeech(input, Voice, AudioConfig);

                PlayByteArray(response.AudioContent.ToByteArray());

                ////Call.ResponseAudio.Add(response.AudioContent.)
                //using (Stream output = File.Create("G:\\src\\SIP\\SIPServer\\SIPServer\\Assets\\audio\\output1.mp3"))
                //{
                //    response.AudioContent.WriteTo(output);
                //    AppendToLog("Audio content written to file \"output.mp3\"");
                //}
            }
        }

        public async Task Run()
        {

            Thread thread = new Thread(new ThreadStart(TTS));
            thread.Start();


        }

    }
}



namespace TextToSpeechConsoleApp
{
    class Program
    {

    }
}