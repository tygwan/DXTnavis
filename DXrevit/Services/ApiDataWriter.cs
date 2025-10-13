using DXBase.Models;
using DXBase.Services;
using DXBase.Constants;
using System;
using System.Threading.Tasks;

namespace DXrevit.Services
{
    /// <summary>
    /// API 서버로 데이터 전송 서비스
    /// </summary>
    public class ApiDataWriter
    {
        private readonly HttpClientService _httpClient;

        public ApiDataWriter()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        /// <summary>
        /// 추출된 데이터를 API 서버로 전송
        /// </summary>
        public async Task<bool> SendDataAsync(ExtractedData extractedData)
        {
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
    }
}
