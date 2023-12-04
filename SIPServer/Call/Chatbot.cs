using Newtonsoft.Json;
using System.Net.Http;
using System.Net;
using System.Text;
using SIPServer.Models;
using Google.Cloud.TextToSpeech.V1;
using System.Windows.Forms;

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

    class Chatbot
    {
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        private readonly string _api = "https://orchestrator.alkhwarizmi.xyz/api/BotConnector";

        private SIPCall Call;
        private readonly Action<string> AppendToLog;
        
        private string BotId = "171";
        private string UserId = "VoiceBot";
        private string SessionId = "VoiceBot";
        
        public Chatbot(SIPCall call, Action<string> appendToLog)
        {
            Call = call;
            AppendToLog = appendToLog;
        }

        public async Task<string> Ask(string input)
        {

            try
            {
                AskRequest askRequest = new AskRequest { id = BotId, userId = UserId, message = input, sessionId = SessionId, voice=true };

                var content = new StringContent(
                    JsonConvert.SerializeObject(askRequest),
                    Encoding.UTF8,
                    "application/json"
                );

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var response = await _httpClient.PostAsync($"{_api}/Ask", content);
                if (!response.IsSuccessStatusCode)
                    return "عذرا حدث خطأ فى الاتصال";

                string responseString = await response.Content.ReadAsStringAsync();
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

            SessionId = (string)bodyJson.sessionId;

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

        public async Task AskChatbotAsync()
        {
            while (true)
            {
                string input = Call.TranscriptedText.Take(); // Blocking call

                string response = await Ask(input);

                response = GetResponses(response);  
                AppendToLog(response);

                Call.ChatbotAnswers.Add(response);
            }
        }

        //public async Task Run()
        //{
        //    Thread thread = new Thread(new ThreadStart(AskChatbotAsync));
        //    thread.Start();
        //}

        public async Task Run()
        {
            await Task.Run(async () => await AskChatbotAsync());
        }

    }

}
