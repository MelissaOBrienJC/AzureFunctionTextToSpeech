using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureFunctionTextToSpeech
{
    
    public static class TextToSpeech
    {

        private static HttpClient _client = new HttpClient();


        [FunctionName("TextToSpeech")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string word = req.Query["word"];           
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            word = word ?? data?.word;
            if (String.IsNullOrEmpty(word))
            {    
                return new BadRequestObjectResult("Please pass word you wish to generate on the query string or in the request body using param: word");
            }
            var bytes = await TextToAudio(word);
            return new FileContentResult(bytes, "audio/mpeg");
        }

        public static async Task<string> GetToken()
        {            
            _client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", 
                System.Environment.GetEnvironmentVariable("TokenApiKey",EnvironmentVariableTarget.Process));
            UriBuilder uriBuilder = new UriBuilder(
                System.Environment.GetEnvironmentVariable("TokenUri", EnvironmentVariableTarget.Process));
            var result = await _client.PostAsync(uriBuilder.Uri.AbsoluteUri, null).ConfigureAwait(false);
            return await result.Content.ReadAsStringAsync().ConfigureAwait(false);           
        }
               
        public static async Task<byte[]> TextToAudio(string word)
        {
            var requestBody = GetAudioMarkup(word);
            var accessToken = await GetToken();
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(Environment.GetEnvironmentVariable("TTSUri", EnvironmentVariableTarget.Process));
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/ssml+xml");
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            request.Headers.Add("Connection", "Keep-Alive");
            request.Headers.Add("User-Agent", Environment.GetEnvironmentVariable("AzureSpeechResourceName", EnvironmentVariableTarget.Process));
            request.Headers.Add("X-Microsoft-OutputFormat", "audio-16khz-32kbitrate-mono-mp3");
            
            var audioResult = await _client.SendAsync(request);
            return await audioResult.Content.ReadAsByteArrayAsync();
        }

        private static string GetAudioMarkup(string word)
        {
            var markup = "<speak version='1.0' xmlns=\"http://www.w3.org/2001/10/synthesis\" xml:lang='en-US'>" +
                        "<voice  name='" + Environment.GetEnvironmentVariable("Voice", EnvironmentVariableTarget.Process) + "'>" +
                        "{{WORD}}" +
                        "</voice> </speak>";
            return markup.Replace("{{WORD}}", word);
        }
    }
}

