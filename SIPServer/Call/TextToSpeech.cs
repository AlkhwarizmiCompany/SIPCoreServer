using Google.Cloud.Speech.V1;
using NAudio.Wave;
using Google.Cloud.TextToSpeech.V1;
using System.IO;
using SIPServer.Models;
using SIPSorceryMedia.Abstractions;
using Microsoft.Extensions.Configuration;

namespace SIPServer.Call
{

    class TextToSpeech : KHService
    {

        private TextToSpeechClient      _TTSClient;
        private VoiceSelectionParams    _voice;
        private AudioConfig             _audioConfig;

        public TextToSpeech(IConfiguration configuration, SIPCall call) : base(configuration, call)
        {

            Initialization();
        }


        public void Initialization()
        {
            _TTSClient = TextToSpeechClient.Create();

            _voice = new VoiceSelectionParams
            {
                LanguageCode = LanguageCodes.Arabic.Egypt,
                SsmlGender = SsmlVoiceGender.Male
            };

            _audioConfig = new AudioConfig
            {
                AudioEncoding = Google.Cloud.TextToSpeech.V1.AudioEncoding.Linear16,
                SampleRateHertz= 16000
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
            //_call.RtpSession.AudioExtrasSource.SetAudioSourceFormat(audioFormat);
            _call.RtpSession.AudioExtrasSource.SendAudioFromStream(memoryStream, AudioSamplingRatesEnum.Rate16KHz);

            _call.IsRunning = false;
        }

        public override void main()
        {
            while (!cancellationTokenSource.IsCancellationRequested)
            {
                string ChatbotResponse = _call.ChatbotAnswers.Take(); // Blocking call

                if (string.IsNullOrEmpty(ChatbotResponse))
                    continue;

                SynthesisInput input = new SynthesisInput
                {
                    Text = ChatbotResponse
                };
                // hash ChatbotResponse
                // check cache 

                //if(cache miss):
                // google tts
                //if(chach hit)
                //retrive from disk

                var response = _TTSClient.SynthesizeSpeech(input, _voice, _audioConfig);

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

                ////_call.ResponseAudio.Add(response.AudioContent.)
                //using (Stream output = File.Create("G:\\src\\SIP\\SIPServer\\SIPServer\\Assets\\audio\\output1.mp3"))
                //{
                //    response.AudioContent.WriteTo(output);
                //    AppendToLog("Audio content written to file \"output.mp3\"");
                //}
            }
        }


    }
}
