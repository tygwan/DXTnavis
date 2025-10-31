using DXBase.Models;
using DXBase.Services;
using DXBase.Constants;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DXrevit.Services
{
    public class ApiDataWriter
    {
        private readonly HttpClientService _httpClient;
        private readonly HttpClient _httpClientDirect;
        private readonly string _primaryEndpoint;
        private readonly string _secondaryEndpoint;

        public ApiDataWriter()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        public ApiDataWriter(HttpClient httpClient, string primaryEndpoint, string secondaryEndpoint)
        {
            _httpClientDirect = httpClient;
            _primaryEndpoint = primaryEndpoint;
            _secondaryEndpoint = secondaryEndpoint;
        }

        public async Task<bool> SendDataAsync(ExtractedData extractedData)
        {
            if (_httpClientDirect != null)
            {
                return await SendWithFallbackAsync(extractedData);
            }

            try
            {
                LoggingService.LogInfo("API 서버로 데이터 전송 시작", "DXrevit");

                var response = await _httpClient.PostAsync<ExtractedData, object>(
                    ApiEndpoints.Ingest,
                    extractedData);

                if (response.Success)
                {
                    LoggingService.LogInfo("데이터 전송 성공", "DXrevit");
                    return true;
                }
                else
                {
                    LoggingService.LogError($"데이터 전송 실패: {response.ErrorMessage}", "DXrevit");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("데이터 전송 중 예외 발생", "DXrevit", ex);
                return false;
            }
        }

        private async Task<bool> SendWithFallbackAsync(ExtractedData extractedData)
        {
            try
            {
                var response = await TryEndpointAsync(_primaryEndpoint, extractedData);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                
                response = await TryEndpointAsync(_secondaryEndpoint, extractedData);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<HttpResponseMessage> TryEndpointAsync(string endpoint, ExtractedData data)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return await _httpClientDirect.PostAsync(endpoint, content);
        }
    }
}
