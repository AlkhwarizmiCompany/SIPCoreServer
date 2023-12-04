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
using SIPSorceryMedia.Abstractions;

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
                AudioEncoding = Google.Cloud.TextToSpeech.V1.AudioEncoding.Linear16,
                SampleRateHertz= 8000
            };

        }

        private void PlayByteArrayToSpeaker(byte[] byteArray)
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


        }

        private void PlayByteArray(byte[] byteArray)
        {
            //PlayByteArrayToSpeaker(byteArray);

            //AudioFormat audioFormat = new AudioFormat(AudioCodecsEnum.PCMA, 1);


            MemoryStream memoryStream = new MemoryStream(byteArray);
            //Call.RtpSession.AudioExtrasSource.SetAudioSourceFormat(audioFormat);
            Call.RtpSession.AudioExtrasSource.SendAudioFromStream(memoryStream, AudioSamplingRatesEnum.Rate8KHz);

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

                string encodedAudioContent = response.AudioContent.ToBase64(); // Your base64-encoded audio content
                byte[] audioBytes = Convert.FromBase64String(encodedAudioContent);

                //var audioContent = response.AudioContent.ToString();
                //byte[] audioBytes = Convert.FromBase64String(audioContent);

                //// Assuming the audio is in WAV format
                //using (MemoryStream mp3Stream = new MemoryStream(audioBytes))
                //{
                //    using (WaveStream waveStream = new WaveFileReader(mp3Stream))
                //    {
                //        // Convert to PCM format
                //        WaveFormat pcmFormat = new WaveFormat(waveStream.WaveFormat.SampleRate, 16, waveStream.WaveFormat.Channels);
                //        using (WaveStream pcmStream = new WaveFormatConversionStream(pcmFormat, waveStream))
                //        {
                //            // Write the PCM data to a file
                //            string outputFile = "output.raw";
                //            using (FileStream fileStream = new FileStream(outputFile, FileMode.Create))
                //            {
                //                pcmStream.CopyTo(fileStream);
                //            }
                //        }
                //    }
                //}

                //PlayByteArray(response.AudioContent.ToByteArray());
                PlayByteArray(audioBytes);

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
