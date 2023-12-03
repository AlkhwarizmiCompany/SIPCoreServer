using Google.Api.Gax.Grpc;
using Google.Cloud.Speech.V1;
using SIPServer.Models;
using System.Collections.Concurrent;
using static System.Net.Mime.MediaTypeNames;

namespace SIPServer.Call
{

    class SpeechToText
    {
        private AsyncResponseStream<StreamingRecognizeResponse> responseStream;
        private SpeechClient.StreamingRecognizeStream streamingCall;

        private Task recognizeTask;
        private Task TranscriptTask;
        private Task StreamingRecognizeTask;
        private Task InitializeTask;

        private readonly SIPCall Call;
        private readonly Action<string> AppendToLog;

        public SpeechToText(SIPCall call, Action<string> appendToLog)
        {

            InitializeTask = Initialize();

            Call = call;
            AppendToLog = appendToLog;
        }

        private async Task Initialize()
        {
            // Create the Speech client
            SpeechClient client = SpeechClient.Create();
            streamingCall = client.StreamingRecognize();
            // The response stream
            responseStream = streamingCall.GetResponseStream();


            var streamingConfig = new StreamingRecognitionConfig
            {
                Config = new RecognitionConfig
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    SampleRateHertz = 16000,
                    LanguageCode = LanguageCodes.Arabic.Egypt,
                },
                InterimResults = true,
            };

            await streamingCall.WriteAsync(new StreamingRecognizeRequest
            {
                StreamingConfig = streamingConfig
            });

            TranscriptTask = Transcript(responseStream);
            StreamingRecognizeTask = InfiniteStreamingRecognize(streamingCall);

        }


        private async Task Transcript(AsyncResponseStream<StreamingRecognizeResponse> responseStream)
        {
            string text;

            await foreach (var response in responseStream)
            {
                foreach (var result in response.Results)
                {
                    if (!result.IsFinal)
                        continue;

                    text = result.Alternatives[0].Transcript;

                    AppendToLog($"Transcript: {text}");

                    Call.TranscriptedText.Add(text);
                }
            }
        }

        public async Task InfiniteStreamingRecognize(SpeechClient.StreamingRecognizeStream streamingCall)
        {

            // Send audio from the microphone to the Speech API
            try
            {
                while (true)
                {
                    byte[] buffer = Call.CallAudio.Take(); // Blocking call
                    if (buffer.Length > 0)
                    {
                        await streamingCall.WriteAsync(new StreamingRecognizeRequest
                        {
                            AudioContent = Google.Protobuf.ByteString.CopyFrom(buffer)
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This can be triggered by stopping the recording
                AppendToLog("Streaming was canceled.");
            }
            finally
            {
                await streamingCall.WriteCompleteAsync();
            }
            // Wait for the response handler to complete processing
            //await responseHandler;
            // Shutdown the client
            //client.Dispose();
        }

    }
}


