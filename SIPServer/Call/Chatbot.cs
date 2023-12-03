﻿using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Text;
using SIPServer.Models;

namespace SIPServer.Call
{
    public class AskRequest
    {
        public string id { get; set; }
        public string userId { get; set; }
        public string sessionId { get; set; }
        public string message { get; set; }
    }

    class Chatbot
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private readonly string _api = "https://orchestrator.alkhwarizmi.xyz/api/BotConnector";

        private readonly SIPCall Call;
        private readonly Action<string> AppendToLog;

        public Chatbot(SIPCall call, Action<string> appendToLog)
        {
            Call = call;
            AppendToLog = appendToLog;
        }


        public string Ask(string input)
        {

            try
            {
                AskRequest askRequest = new AskRequest { id = "", userId = "", message = input, sessionId = "" };

                var content = new StringContent(
                    JsonConvert.SerializeObject(askRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var response = _httpClient.PostAsync($"{_api}/Ask", content);
                if (!response.IsCompletedSuccessfully)
                    return "عذرا حدث خطأ فى الاتصال";

                string responseString = response.Result.ToString();
                return responseString;

            }
            catch (Exception e)
            {
                return "";
            }
        }
    
    
        public void AskChatbot()
        {
            while (true)
            {
                string input = Call.TranscriptedText.Take(); // Blocking call

                string response = Ask(input);

                Call.ChatbotAnswers.Add(response);
            }
        }

        public async Task Run()
        {

            Thread thread = new Thread(new ThreadStart(AskChatbot));
            thread.Start();

           
        }
    }

}
