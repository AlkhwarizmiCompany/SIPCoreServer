using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using SIPServer.Models;

namespace SIPServer.Call
{
    class MicAudio : KHService
    {
        private WaveInEvent waveIn;
        public MicAudio(IConfiguration configuration, SIPCall call) : base(configuration, call)
        {

        }

        public override async Task Initialization()
        {

            // Configure microphone input
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(8000, 1) // Sample rate and channels
            };
            waveIn.DataAvailable += (sender, e) =>
            {
                var samples = e.Buffer.Clone() as byte[];

                _call.CallAudio.Add(samples);
            };

        }

        public override void main()
        {
            waveIn.StartRecording();

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                System.Threading.Thread.Sleep(100);
            }

            waveIn.StopRecording();
        }

    }
}

