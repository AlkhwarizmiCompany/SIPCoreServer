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

namespace SIPServer.Call
{

    class TextToSpeech
    {

        private readonly SIPCall        Call;
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
                AudioEncoding = Google.Cloud.TextToSpeech.V1.AudioEncoding.Mp3
            };

        }

        public async Task Run()
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

                //Call.ResponseAudio.Add(response.AudioContent.)
                using (Stream output = File.Create("output.mp3"))
                {
                    response.AudioContent.WriteTo(output);
                    AppendToLog("Audio content written to file \"output.mp3\"");
                }
            }
        }

    }
}



namespace TextToSpeechConsoleApp
{
    class Program
    {

    }
}