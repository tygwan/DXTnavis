using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DXBase.Models;
using Polly;
using Polly.Retry;

namespace DXBase.Services
{
    /// <summary>
    /// API 통신을 위한 공용 HTTP 클라이언트 서비스
    /// Polly를 사용한 재시도 로직 포함
    /// </summary>
    public class HttpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _baseUrl;
        private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

        /// <summary>
        /// 기본 생성자 - baseUrl로 새 HttpClient 생성
        /// </summary>
        public HttpClientService(string baseUrl, int timeoutSeconds = 30)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
            _retryPipeline = CreateRetryPipeline();
        }

        /// <summary>
        /// 테스트용 생성자 - 외부에서 HttpClient 주입
        /// </summary>
        public HttpClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/');
            _retryPipeline = CreateRetryPipeline();
        }

        /// <summary>
        /// Polly 재시도 파이프라인 생성
        /// </summary>
        private static ResiliencePipeline<HttpResponseMessage> CreateRetryPipeline()
        {
            // 재시도 정책: 최대 3회, 지수 백오프
            var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(response =>
                    {
                        // 5xx 서버 오류와 429 Too Many Requests에 대해서만 재시도
                        return (int)response.StatusCode >= 500 ||
                               response.StatusCode == HttpStatusCode.TooManyRequests;
                    }),
                OnRetry = args =>
                {
                    LoggingService.LogWarning(
                        $"재시도 {args.AttemptNumber + 1}/{3 + 1} - " +
                        $"Delay: {args.RetryDelay.TotalSeconds:F2}s");
                    return ValueTask.CompletedTask;
                }
            };

            return new ResiliencePipelineBuilder<HttpResponseMessage>().AddRetry(retryOptions).Build();
        }

        /// <summary>
        /// POST 요청 (JSON) - Polly 재시도 포함
        /// </summary>
        public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
            string endpoint,
            TRequest data)
        {
            try
            {
                string url = _baseUrl != null
                    ? $"{_baseUrl}/{endpoint.TrimStart('/')}"
                    : endpoint;
                string json = JsonSerializer.Serialize(data);

                LoggingService.LogInfo($"POST 요청: {url}");

                // Polly 재시도 파이프라인을 통해 실행
                var response = await _retryPipeline.ExecuteAsync(async cancellationToken =>
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    return await _httpClient.PostAsync(url, content, cancellationToken);
                }, default);

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
