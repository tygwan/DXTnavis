using System;
using System.IO;
using System.Text.Json;

namespace DXBase.Services
{
    /// <summary>
    /// 설정 파일을 읽고 쓰는 공용 서비스
    /// </summary>
    public class ConfigurationService
    {
        private const string SettingsFileName = "settings.json";
        private static readonly string SettingsDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "DX_Platform");
        private static readonly string SettingsFilePath =
            Path.Combine(SettingsDirectory, SettingsFileName);

        /// <summary>
        /// 설정 데이터 모델
        /// </summary>
        public class AppSettings
        {
            public string ApiServerUrl { get; set; } = "https://localhost:5000";
            public string DatabaseConnectionString { get; set; } = string.Empty;
            public string DefaultUsername { get; set; } = string.Empty;
            public int TimeoutSeconds { get; set; } = 30;
            public int BatchSize { get; set; } = 100;
            public string LogFilePath { get; set; } =
                Path.Combine(SettingsDirectory, "Logs");
        }

        /// <summary>
        /// 설정 파일 로드
        /// 파일이 없으면 기본값 생성
        /// </summary>
        public static AppSettings LoadSettings()
        {
            try
            {
                // 디렉토리가 없으면 생성
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                // 파일이 없으면 기본값으로 생성
                if (!File.Exists(SettingsFilePath))
                {
                    var defaultSettings = new AppSettings();
                    SaveSettings(defaultSettings);
                    return defaultSettings;
                }

                // 파일 읽기
                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json)
                       ?? new AppSettings();
            }
            catch (Exception)
            {
                // 로깅 초기화 전에 예외 발생 가능하므로 로그 생략
                return new AppSettings(); // 기본값 반환
            }
        }

        /// <summary>
        /// 설정 파일 저장
        /// </summary>
        public static bool SaveSettings(AppSettings settings)
        {
            try
            {
                // 디렉토리가 없으면 생성
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                // JSON으로 직렬화
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);

                // 파일 쓰기
                File.WriteAllText(SettingsFilePath, json);

                LoggingService.LogInfo("설정 파일 저장 성공");
                return true;
            }
            catch (Exception)
            {
                // 로깅 초기화 전에 예외 발생 가능하므로 로그 생략
                return false;
            }
        }

        /// <summary>
        /// 설정 파일 경로 반환 (UI에서 사용)
        /// </summary>
        public static string GetSettingsFilePath()
        {
            return SettingsFilePath;
        }
    }
}
