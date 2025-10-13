using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DXBase.Models;

namespace DXBase.Services
{
    /// <summary>
    /// API 통신을 위한 공용 HTTP 클라이언트 서비스
    /// </summary>
    public class HttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public HttpClientService(string baseUrl, int timeoutSeconds = 30)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        /// <summary>
        /// POST 요청 (JSON)
        /// </summary>
        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest data)
        {
            try
            {
                string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
                string json = JsonSerializer.Serialize(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                LoggingService.LogInfo($"POST 요청: {url}");

                var response = await _httpClient.PostAsync(url, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<TResponse>(responseJson);
                    return new ApiResponse<TResponse>
                    {
                        Success = true,
                        Data = result,
                        StatusCode = (int)response.StatusCode,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    LoggingService.LogError($"POST 실패: {response.StatusCode} - {responseJson}");
                    return new ApiResponse<TResponse>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {responseJson}",
                        StatusCode = (int)response.StatusCode,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"POST 예외: {endpoint}", ex: ex);
                return new ApiResponse<TResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    StatusCode = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// GET 요청 (JSON)
        /// </summary>
        public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(string endpoint)
        {
            try
            {
                string url = $"{_baseUrl}/{endpoint.TrimStart('/')}";

                LoggingService.LogInfo($"GET 요청: {url}");

                var response = await _httpClient.GetAsync(url);
                string responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<TResponse>(responseJson);
                    return new ApiResponse<TResponse>
                    {
                        Success = true,
                        Data = result,
                        StatusCode = (int)response.StatusCode,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    LoggingService.LogError($"GET 실패: {response.StatusCode} - {responseJson}");
                    return new ApiResponse<TResponse>
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {responseJson}",
                        StatusCode = (int)response.StatusCode,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"GET 예외: {endpoint}", ex: ex);
                return new ApiResponse<TResponse>
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    StatusCode = 0,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
    }
}
