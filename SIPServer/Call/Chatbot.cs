using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Text;
using SIPServer.Models;
using Google.Cloud.TextToSpeech.V1;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using System.Xml.Linq;

namespace SIPServer.Call
{
    public class AskRequest
    {
        public string id { get; set; }
        public string userId { get; set; }
        public string sessionId { get; set; }
        public string message { get; set; }
        public bool voice { get; set; }
    }

    class Chatbot: KHService
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        private readonly string API;
        private readonly string BOT_ID;
        private readonly string USER_ID;

        private string _sessionId = "VoiceBot";
        
        public Chatbot(IConfiguration configuration, SIPCall call): base(configuration, call)
        {

            API = _configuration["Api"] != null ? _configuration["Api"] : "https://orchestrator.alkhwarizmi.xyz/api/BotConnector";
            BOT_ID = _configuration["BotId"] != null ? _configuration["BotId"]: "171";
            USER_ID = _configuration["UserId"] != null ? _configuration["UserId"] : "VoiceBot";

            _call.Log($"Chatbot Data API : {API}, BOT_ID:{BOT_ID}, USER_ID:{USER_ID}");

        }

        public string Ask(string input)
        {

            try
            {
                AskRequest askRequest = new AskRequest { id = BOT_ID, userId = USER_ID, message = input, sessionId = _sessionId, voice=true };

                var content = new StringContent(
                    JsonConvert.SerializeObject(askRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var response = _httpClient.PostAsync($"{API}/Ask", content).GetAwaiter().GetResult();
                
                _call.Log($"Chatbot response => $ {response}");

                if (!response.IsSuccessStatusCode)
                {
                    _call.Log($"عذرا حدث خطأ فى الاتصال");
                    return "عذرا حدث خطأ فى الاتصال";
                }

                string responseString = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                return responseString;

            }
            catch (Exception e)
            {
                return "";
            }
        }
    

        private string GetResponses(string response)
        {
            // Deserialize JSON string to dynamic object
            dynamic bodyJson = JsonConvert.DeserializeObject(response);

            _sessionId = (string)bodyJson.sessionId;

            List<string> output = new List<string>();

            foreach (var Response in bodyJson.Responses)
            {
                if (new[] { "text", "yesno" }.Contains((string)Response.type))
                {
                    if (Response.message == null)
                    {
                        Response.message = "";
                    }

                    output.Add((string)Response.message);
                }
                else if (new[] { "options", "optionsHB", "list" }.Contains((string)Response.type))
                {
                    if (Response.title == null)
                    {
                        Response.title = "";
                    }

                    output.Add((string)Response.title);

                    foreach (var option in Response.rOptions)
                    {
                        if (option.title == null)
                        {
                            option.title = "";
                        }

                        output.Add((string)option.title);
                    }
                }
            }

            List<string> cleantext = new List<string>();

            // Assuming 'output' contains HTML strings
            //foreach (var x in output)
            //{
            //    var htmlDoc = new HtmlDocument();
            //    htmlDoc.LoadHtml(x);
            //    cleantext.Add(htmlDoc.DocumentNode.InnerText);
            //}


            string diac_output = "";

            foreach (var msg in output)
            {
                // diac_output += self.ChatBot.DiacText(msg, self.chatbotBotId);
                // diac_output += ". ";
                diac_output += msg;

                if (!msg.EndsWith("."))
                {
                    diac_output += ". ";
                }
            }

            //if (cleantext.Count == 0)
            //{
            //    cleantext.Add("عذرا حدث خطأ فى الاتصال");
            //}

            return diac_output;
        }

        public override void main()
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                string input = _call.TranscriptedText.Take(); // Blocking call

                string response = Ask(input);

                response = GetResponses(response);
                _call.Log(response);

                _call.ChatbotAnswers.Add(response);
            }
        }


    }

}
