﻿using Google.Api.Gax.Grpc;
using Google.Cloud.Speech.V1;
using SIPServer.Models;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SIPServer.Call
{
    class SpeechToText
    {
        private SpeechClient.StreamingRecognizeStream streamingCall;

        private SIPCall Call;
        private readonly Action<string> AppendToLog;
        private CancellationTokenSource cancellationTokenSource;

        public SpeechToText(SIPCall call, Action<string> appendToLog)
        {
            Call = call;
            AppendToLog = appendToLog;
            cancellationTokenSource = new CancellationTokenSource();

            // Initialize asynchronously
            Task.Run(() => Initialize());
        }

        private async Task Initialize()
        {
            // Create the Speech client
            SpeechClient client = SpeechClient.Create();
            streamingCall = client.StreamingRecognize();
            // The response stream
            var responseStream = streamingCall.GetResponseStream();

            var streamingConfig = new StreamingRecognitionConfig
            {
                Config = new RecognitionConfig
                {
                    Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    //Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                    SampleRateHertz = 8000,
                    LanguageCode = LanguageCodes.Arabic.Egypt,
                },
                InterimResults = true,
            };

            await streamingCall.WriteAsync(new StreamingRecognizeRequest
            {
                StreamingConfig = streamingConfig
            });

            // Start tasks
            _ = Transcript(responseStream);
            _ = InfiniteStreamingRecognize(streamingCall);
        }

        private async Task Transcript(AsyncResponseStream<StreamingRecognizeResponse> responseStream)
        {
            string text;

            try
            {
                await foreach (var response in responseStream)
                {
                    foreach (var result in response.Results)
                    {
                        if (!result.IsFinal)
                            continue;

                        text = result.Alternatives[0].Transcript;


                        if (!Call.IsRunning)
                        {
                            AppendToLog($"Transcript Added: {text}");

                            Call.TranscriptedText.Add(text);
                            Call.IsRunning = true;
                        }
                        else
                            AppendToLog($"Transcript Not Added: {text}");

                    }
                }
            }
            catch (Exception e)
            {
                AppendToLog($"Exception: {e}");
            }
        }

        public async Task InfiniteStreamingRecognize(SpeechClient.StreamingRecognizeStream streamingCall)
        {
            try
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    byte[] buffer = Call.CallAudio.Take(); 

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
                AppendToLog("Streaming was canceled.");
            }
            finally
            {
                await streamingCall.WriteCompleteAsync();
            }
        }

        public void Stop()
        {
            cancellationTokenSource.Cancel();
        }
    }
}
