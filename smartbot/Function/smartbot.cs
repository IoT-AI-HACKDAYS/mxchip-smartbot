using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading;
using System.Net.Http;
using System.Net;

namespace IoTAIHackdays
{
    public static class smartbot
    {

        // Find UIR's : https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/rest-speech-to-text#authentication

        [FunctionName("smartbot")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {

            //Get the function config values
            string speechToTextRegion = Environment.GetEnvironmentVariable("SpeechToTextRegion");
            string speechToTextSubscriptionKey = Environment.GetEnvironmentVariable("SpeechToTextSubscriptionKey");
            string luisEndPoint = Environment.GetEnvironmentVariable("LuisEndPoint");


            log.LogInformation("SmartBot function entered");

            log.LogInformation("Converting audio to text");
           
            var textResult = SpeechToText(log, req.Body, speechToTextRegion, speechToTextSubscriptionKey);

            if ((textResult != null) && (textResult.GetResultType() == SpeechResultType.Success))
            {
                log.LogInformation("Converting to intent");
                var intent = await GetIntent(log, luisEndPoint, textResult.DisplayText);
                if (!string.IsNullOrEmpty(intent))
                {
                    return new OkObjectResult($"{intent}");
                }

            }

            return new BadRequestObjectResult("An error occured resolving the command");
        }

        public static SpeechResult SpeechToText(ILogger log, Stream input, string sttRegion, string subscriptionKey)
        {
             var sttEndPoint = $"https://{sttRegion}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language=en-GB";

            var contentType = @"audio/wav; codec=""audio/pcm""; samplerate=16000";
            string responseString;

            log.LogInformation($"Request Uri: {sttEndPoint}\n\r");

            try
            {
                HttpWebRequest request = null;
                request = (HttpWebRequest)HttpWebRequest.Create(sttEndPoint);
                request.SendChunked = true;
                request.Accept = @"application/json;text/xml";
                request.Method = "POST";
                request.ProtocolVersion = HttpVersion.Version11;
                request.Host = $"{sttRegion}.stt.speech.microsoft.com";
                request.ContentType = contentType;
                request.Headers["Ocp-Apim-Subscription-Key"] = subscriptionKey;

                using (Stream requestStream = request.GetRequestStream())
                {
                    input.CopyTo(requestStream);
                    requestStream.Flush();
                }

                log.LogInformation($"Execute query to Speeech to text service\n\r");
                using (var response = request.GetResponse())
                {
                    var httpResponce = (HttpWebResponse)response;

                    log.LogInformation($"Received a responce from Speeech to text service\n\r");
                    log.LogInformation($"HttpResponce : {httpResponce.StatusCode.ToString()}");

                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        responseString = sr.ReadToEnd();
                    }

                    var speechResult = JsonConvert.DeserializeObject<SpeechResult>(responseString);

                    log.LogInformation($"Result: {speechResult.DisplayText}");

                    return speechResult;
                }

            }
            catch (Exception ex)
            {
                log.LogError(ex.ToString());
                log.LogError(ex.Message);
                return null;
            }
        }

        public static async Task<string> GetIntent(ILogger log, string luisEndPoint, string text)
        {
            log.LogInformation("Resolving intent");
            //expection an endpoint with the "q=" parameter at the end..
            var luisCall = string.Concat(luisEndPoint, text);
            var client = new HttpClient();
            var response = await client.GetAsync(luisCall);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                log.LogInformation($"Intent resolved from LUIS : {responseContent}");

                dynamic result = JsonConvert.DeserializeObject(responseContent);

                var intent = result?.topScoringIntent?.intent;
                log.LogInformation($"Intent : {intent}");

                return intent;
            }
            return "";
        }

    }

    public class SpeechResult
    {
        public string RecognitionStatus { get; set; }
        public string DisplayText { get; set; }
        public int Offset { get; set; }
        public int Duration { get; set; }

        public SpeechResultType GetResultType()
        {
            var result = SpeechResultType.NotValid;

            if (Enum.TryParse<SpeechResultType>(this.RecognitionStatus, true, out result))
            {
                return result;
            }
            return SpeechResultType.NotValid;
        }
    }

    public enum SpeechResultType
    {
        NotValid,
        Success,
        NoMatch,
        InitialSilenceTimeout,
        BabbleTimeout,
        Error
    }
}


