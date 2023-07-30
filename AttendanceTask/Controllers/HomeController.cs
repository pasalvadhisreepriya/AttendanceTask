// pseudo code
// Controller HomeController:
//     Constants:
//         API_KEY = "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"

//     Private Variables:
//         client: HttpClient
//         _logger: ILogger<HomeController>

//     Constructor HomeController(logger):
//         _logger = logger
//         client = HttpClient
//         client.Timeout = TimeSpan.FromSeconds(500)

//     Method Index():
//         model = Create new instance of Model
//         if Session.TryGetValue("GeneratedResponse", generatedResponse):
//             model.Response = Convert generatedResponse to UTF-8 string
//         Return View "Index" with model

//     Method DownloadResponse():
//         if Session.TryGetValue("GeneratedResponse", generatedResponse):
//             responseFileName = Generate random GUID and convert to string + ".csv"
//             contentDisposition = Create new instance of ContentDispositionHeaderValue
//             contentDisposition.FileNameStar = responseFileName
//             contentDisposition.FileName = responseFileName

//             responseCsvData = Convert generatedResponse to UTF-8 string
//             responseStream = Convert responseCsvData to MemoryStream

//             Return FileStreamResult with responseStream and "text/csv" content type
//         Return Content "Response not found."

//     Method Create(model, file):
//         try:
//             if file is null or file.Length is 0:
//                 AddModelError to ModelState with message "Please upload a CSV file."
//                 Return View "Index" with model

//             if file.FileName does not end with ".csv":
//                 AddModelError to ModelState with message "Please upload only CSV files."
//                 Return View "Index" with model

//             Read the content of the uploaded CSV file into fileContent

//             options = Create new dictionary with keys "model", "messages", "max_tokens", "temperature"
//             Set options["model"] = "gpt-3.5-turbo"
//             Set options["max_tokens"] = 3500
//             Set options["temperature"] = 0.2

//             Set client.DefaultRequestHeaders.Authorization with "Bearer" + API_KEY

//             Set options["messages"] as an array with one element:
//                 Create new object with keys "role" and "content"
//                 Set "role" = "user"
//                 Set "content" = user-provided input prompt or filtered CSV data + " "

//             Convert options to JSON string and assign it to json
//             Create new StringContent with json, UTF-8 encoding, and "application/json" content type and assign it to content

//             Send POST request to "https://api.openai.com/v1/chat/completions" with content and assign response

//             Ensure response has a successful status code

//             Read the response content into responseBody

//             Deserialize responseBody into jsonResponse

//             Set result = jsonResponse.choices[0].message.content

//             Save result as CSV data in the session:
//                 Convert result to UTF-8 bytes and store it as "GeneratedResponse" in the session

//             Redirect to Index action
//         catch Exception as ex:
//             Log ex.Message
//             Return Content "An error occurred during file analysis."

//     Method Download(fileName):
//         if fileName is not null or empty:
//             responseFileName = fileName
//             filePath = Combine fileName with temporary folder path
//             if File.Exists(filePath):
//                 fileBytes = Read all bytes from filePath
//                 Return FileResult with fileBytes and "text/csv" content type
//         Return NotFoundResult


using AttendanceTask.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AttendanceTask.Controllers
{
    public class HomeController : Controller
    {
        private const string API_KEY = "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";
        private static readonly HttpClient client = new HttpClient();

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var model = new Model();
            if (HttpContext.Session.TryGetValue("GeneratedResponse", out var generatedResponse))
            {
                model.Response = System.Text.Encoding.UTF8.GetString(generatedResponse);
            }

            return View(model);
        }

        public IActionResult DownloadResponse()
        {
            if (HttpContext.Session.TryGetValue("GeneratedResponse", out var generatedResponse))
            {
                var responseFileName = $"{Guid.NewGuid().ToString()}.csv";

                var contentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileNameStar = responseFileName,
                    FileName = responseFileName
                };
                Response.Headers.Add(HeaderNames.ContentDisposition, contentDisposition.ToString());

                var responseCsvData = Encoding.UTF8.GetString(generatedResponse);
                var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseCsvData));

                return new FileStreamResult(responseStream, "text/csv");
            }

            return Content("Response not found.");
        }

        [HttpPost]
        public async Task<IActionResult> Create(Model model, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    ModelState.AddModelError("", "Please upload a CSV file.");
                    return View("Index", model);
                }

                if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("", "Please upload only CSV files.");
                    return View("Index", model);
                }

                // Read the content of the uploaded CSV file
                string fileContent;
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    fileContent = await reader.ReadToEndAsync();
                }

                // Prepare the data for OpenAI API request
                var options = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new
                        {
                            role = "system",
                            content = " "
                        },
                        new
                        {
                            role = "user",
                            content = $" "
                        }
                    },
                    max_tokens = 3500,
                    temperature = 0.2
                };

                var json = JsonConvert.SerializeObject(options);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", API_KEY);

                // Send request to OpenAI API for text analysis
                var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync();

                dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);
                string result = jsonResponse.choices[0].message.content;

                // Save the response as CSV data in the session instead of plain text
                HttpContext.Session.Set("GeneratedResponse", Encoding.UTF8.GetBytes(result));

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                Debug.WriteLine(ex.Message);
                return Content("An error occurred during file analysis.");
            }
        }
    }
}
