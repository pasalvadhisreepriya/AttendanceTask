using AttendanceTask.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

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

                // Assign the output result to the model
                model.Response = result;

                // Save the response to Session for displaying it in the Index view
                HttpContext.Session.Set("GeneratedResponse", Encoding.UTF8.GetBytes(model.Response));

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                // Handle any exceptions
                Debug.WriteLine(ex.Message);
                return Content("An error occurred during file analysis.");
            }
        }

        public IActionResult DownloadResponse()
        {
            if (HttpContext.Session.TryGetValue("GeneratedResponse", out var generatedResponse))
            {
                var responseFileName = $"{Guid.NewGuid().ToString()}.txt";

                var contentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileNameStar = responseFileName,
                    FileName = responseFileName
                };
                Response.Headers.Add(HeaderNames.ContentDisposition, contentDisposition.ToString());

                var responseStream = new MemoryStream(generatedResponse);
                return new FileStreamResult(responseStream, "text/plain");
            }

            return Content("Response not found.");
        }
    }
}
