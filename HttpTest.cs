using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Identity;
using System.Net.Http;

namespace func_test
{
    public class HttpTest
    {
        private HttpClient client;

        public HttpTest (IHttpClientFactory factory)
        {
            client = factory.CreateClient();
        }

        [FunctionName("HttpTest")]
        public static async Task<IActionResult> RunHttpTest(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation($"C# HTTP trigger {nameof(RunHttpTest)} processeing a request.");

            string name = req.Query["name"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {name}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("SqlTest")]
        public async Task<IActionResult> RunSqlTest(
                [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
                ILogger log)
        {
            log.LogInformation($"C# HTTP trigger {nameof(RunSqlTest)} processeing a request.");
            string sql = "SELECT COUNT(*) FROM [SalesLT].[Customer]";
            string connString = System.Environment.GetEnvironmentVariable("SQL_CONNECTION", EnvironmentVariableTarget.Process);
            log.LogInformation(connString);
            string responseMessage = "";
            try
            {
                using (SqlConnection connection = new SqlConnection(connString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand(sql, connection))
                    {
                        using (SqlDataReader reader = await command.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                responseMessage = $"{reader.GetInt32(0)} customers found";
                            }
                            else
                            {
                                responseMessage = "No reader result";
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "Something didn't work so well");
                responseMessage = e.Message;
                return new BadRequestObjectResult(responseMessage);
            }

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("BlobReadTest")]
        public async Task<IActionResult> RunBlobReadTest(
                [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
                ILogger log)
        {
            log.LogInformation($"C# HTTP trigger {nameof(RunBlobReadTest)} processeing a request.");
            string connString = System.Environment.GetEnvironmentVariable("STORAGE_CONNECTION", EnvironmentVariableTarget.Process);
            string name = req.Query["name"];
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            if (string.IsNullOrWhiteSpace(name)){
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                name = data?.name ?? "<no name provided>";
            }

            string container = req.Query["container"];
            if (string.IsNullOrWhiteSpace(container)){
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                container = data?.container ?? "<no name provided>";
            }
            
            log.LogInformation($"Accessing {name}");
            string responseMessage = "";
            try
            {
                var serviceClient = new BlobServiceClient(new Uri(connString), new DefaultAzureCredential());
                BlobContainerClient containerClient = serviceClient.GetBlobContainerClient(container);
                if (!string.IsNullOrEmpty(name))
                {
                    var client = containerClient.GetBlobClient(name);
                    var properties = await client.GetPropertiesAsync();
                    responseMessage = $"Last modified: {properties.Value.LastModified}";
                }
                else {
                    await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
                    {
                        responseMessage += blobItem.Name + "\r\n";
                    }
                }
                
                /*string blobContents = "This is a block blob.";
                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(blobContents);

                using (MemoryStream stream = new MemoryStream(byteArray))
                {
                    await containerClient.UploadBlobAsync(blobName, stream);
                } */
            }
            catch (Exception e)
            {
                log.LogError(e, "Something didn't work so well");
                responseMessage = e.Message;
                return new BadRequestObjectResult(responseMessage);
            }

            return new OkObjectResult(responseMessage);
        }

        [FunctionName("WebApiTest")]
        public async Task<IActionResult> RunWebApiTest(
                [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
                ILogger log)
        {
            log.LogInformation($"C# HTTP trigger {nameof(RunWebApiTest)} processeing a request.");
            string apiUrl = System.Environment.GetEnvironmentVariable("WEB_API", EnvironmentVariableTarget.Process);
            string responseMessage = "???";
            try
            {
                responseMessage = await client.GetStringAsync(apiUrl);
            }
            catch (Exception e)
            {
                log.LogError(e, "Something didn't work so well");
                responseMessage = e.Message;
                return new BadRequestObjectResult(responseMessage);
            }

            return new OkObjectResult(responseMessage);
        }
    }
}
