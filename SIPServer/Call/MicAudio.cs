using NAudio.Wave;
using SIPServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SIPServer.Call
{
    class MicAudio
    {
        private readonly SIPCall Call;
        private readonly Action<string> AppendToLog;

        private bool isRecording = false;
        private WaveInEvent waveIn;

        public MicAudio(SIPCall call, Action<string> appendToLog)
        {

            Call = call;
            AppendToLog = appendToLog;

            Initialization();
        }


        public void Initialization()
        {

            // Configure microphone input
            waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 1) // Sample rate and channels
            };
            waveIn.DataAvailable += (sender, e) =>
            {
                Call.CallAudio.Add(e.Buffer.Clone() as byte[]);
            };

            waveIn.StartRecording();
        }
    }
}

