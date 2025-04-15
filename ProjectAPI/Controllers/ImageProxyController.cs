using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ProjectAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageProxyController : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public ImageProxyController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
                return BadRequest("Image URL is required.");

            try
            {
                var imageResponse = await _httpClient.GetAsync(url);

                if (!imageResponse.IsSuccessStatusCode)
                    return StatusCode((int)imageResponse.StatusCode, "Failed to fetch image");

                var contentType = imageResponse.Content.Headers.ContentType?.ToString();
                var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();

                return File(imageBytes, contentType ?? "image/jpeg");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error fetching image: {ex.Message}");
            }
        }
    }
}
