namespace Dedicated
{
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using System.Web;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    public static class Dedicated
    {
        [FunctionName("Dedicated")]
        public static async Task Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "<key for dpx-xic-cogcv-01-central-dev cognitive service>");

            // Call Read and save the Operation-location to use in the next call
            var operationLocation = await ReadAsync(client, log);

            // Call Get Read Results every 2s until it returns succeeded status
            var succeeded = false;
            while (!succeeded)
            {
                log.LogInformation(succeeded.ToString());
                var delayTask = Task.Delay(2000);
                succeeded = await GetReadResult(operationLocation, client, log);
                await delayTask;
            }
        }

        // OCR: https://docs.microsoft.com/en-us/azure/cognitive-services/computer-vision/concept-recognizing-text
        // Read (post) API: https://westcentralus.dev.cognitive.microsoft.com/docs/services/computer-vision-v3-ga/operations/5d986960601faab4bf452005
        private static async Task<string> ReadAsync(HttpClient client, ILogger log)
        {
            // Request parameters
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            queryString["language"] = "en";

            // Request body
            byte[] byteData = Encoding.UTF8.GetBytes("{\"url\":\"https://englishhippy.files.wordpress.com/2013/08/img_6954.jpg\"}"); // some random pictures of In-N-Out menu

            HttpResponseMessage response;
            using (var content = new ByteArrayContent(byteData))
            {
                var uri = "https://dpx-xic-cogcv-01-central-dev.cognitiveservices.azure.com/vision/v3.0/read/analyze?" + queryString;
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                response = await client.PostAsync(uri, content);
            }

            var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
            log.LogInformation("Operation-Location: " + operationLocation);

            return operationLocation;
        }

        // Get Read Results (get) API: https://westcentralus.dev.cognitive.microsoft.com/docs/services/computer-vision-v3-ga/operations/5d9869604be85dee480c8750
        private static async Task<bool> GetReadResult(string operationLocation, HttpClient client, ILogger log)
        {
            var queryString = HttpUtility.ParseQueryString(string.Empty);
            var uri = operationLocation + "?" + queryString;
            var response = await client.GetAsync(uri);

            var responseBody = await response.Content.ReadAsStringAsync();
            var status = JObject.Parse(responseBody)["status"].ToString();
            log.LogInformation("Status: " + status);
            var succeeded = status == "succeeded";
            // Print the response only for the successful call
            if (succeeded)
            {
                log.LogInformation(responseBody);
            }

            return succeeded;
        }
    }
}
