using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Intent;
using Microsoft.CognitiveServices.Speech.Audio;

namespace IoTAIHack
{
    public static class SpeechUnderstanding
    {

        private const string NoneIntent = "none";

        [FunctionName("understand")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("Starting to process captured audio");

            var config = SpeechConfig.FromSubscription(Environment.GetEnvironmentVariable("SpeechSubscriptionKey"), Environment.GetEnvironmentVariable("SpeechRegion"));

            log.LogInformation("Converting speech to intent");
            var intent = await SpeechToIntent(req.Body, log, config);

            log.LogInformation($"Returning {intent}");
            return (ActionResult)new OkObjectResult(intent);
        }

        private static async Task<string> SpeechToIntent(Stream audioStream, ILogger log, SpeechConfig config)
        {
            var extractedIntent = NoneIntent;

            using (var pushStream = AudioInputStream.CreatePushStream(AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1)))
            {

                    //read the adio stream in for a one shot recognition
                    var reader = new BinaryReader(audioStream);
                    var audioBytes = reader.ReadBytes((int)audioStream.Length);

                    pushStream.Write(audioBytes, (int)audioStream.Length);
                    pushStream.Close();
               
                using (var audioInput = AudioConfig.FromStreamInput(pushStream))
                {
                    using (var recognizer = new IntentRecognizer(config, audioInput))
                    {
                        //load the LUIS model for recognition of intents
                        var understandingModel = LanguageUnderstandingModel.FromAppId(Environment.GetEnvironmentVariable("LuisApplicationId"));
                        recognizer.AddAllIntents(understandingModel);

                        recognizer.Recognizing += (s, e) =>
                        {
                            log.LogInformation($"RECOGNIZING: Text={e.Result.Text}");
                        };

                        //perform the recognition and resolution of teh intent in one go
                        var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
                        extractedIntent = (!string.IsNullOrEmpty(result.IntentId)) ? result.IntentId : NoneIntent;
                    }

                }
            }
            return extractedIntent;
        }
    }
}
