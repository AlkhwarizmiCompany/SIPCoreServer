using Google.Cloud.Speech.V1;
using NAudio.Wave;
using Google.Cloud.TextToSpeech.V1;
using System.IO;
using SIPServer.Models;
using SIPSorceryMedia.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Security.Cryptography;

namespace SIPServer.Call
{

    class TextToSpeech : KHService
    {

        private TextToSpeechClient      _TTSClient;
        private VoiceSelectionParams    _voice;
        private AudioConfig             _audioConfig;

        public readonly string          CACHE_PATH;
        public readonly int             SAMPLE_RATE;

        public TextToSpeech(IConfiguration configuration, SIPCall call) : base(configuration, call)
        {
            CACHE_PATH = configuration["cachePath"] != null ? _configuration["cachePath"] : Path.Combine(Directory.GetCurrentDirectory(),"TTS");

            if(!int.TryParse(_configuration["ttsSampleRate"], out SAMPLE_RATE))
                SAMPLE_RATE =  16000;
        }

        public override async Task Initialization()
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
                SampleRateHertz = SAMPLE_RATE
            };


            if (!Directory.Exists(CACHE_PATH))
            {
                Directory.CreateDirectory(CACHE_PATH);
            }
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

        private static string ComputeHash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++)
                {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }

        private void PlayByteArray(byte[] byteArray)
        {
            //PlayByteArrayToSpeaker(byteArray);

            MemoryStream memoryStream = new MemoryStream(byteArray);
            _call.RtpSession.AudioExtrasSource.SendAudioFromStream(memoryStream, AudioSamplingRatesEnum.Rate16KHz);

            _call.IsRunning = false;
        }

        public override void main()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                string ChatbotResponse = _call.ChatbotAnswers.Take(); // Blocking call
                if (string.IsNullOrEmpty(ChatbotResponse))
                    continue;

                string hash = ComputeHash(ChatbotResponse);

                string cachedFilePath = Path.Combine("E:\\repos", hash + ".wav");

                byte[] audioBytes;

                if (File.Exists(cachedFilePath))
                {
                    audioBytes = File.ReadAllBytes(cachedFilePath);
                }
                else
                {
                    SynthesisInput input = new SynthesisInput { Text = ChatbotResponse };

                    var response = _TTSClient.SynthesizeSpeech(input, _voice, _audioConfig);

                    audioBytes = response.AudioContent.ToByteArray();

                    File.WriteAllBytes(cachedFilePath, audioBytes);
                }

                PlayByteArray(audioBytes);
            }
        }


    }
}
