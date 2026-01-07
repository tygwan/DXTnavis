using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DXBase.Services
{
    /// <summary>
    /// 설정 파일을 읽고 쓰는 공용 서비스
    /// 파일 변경 감지 및 자동 리로드 지원
    /// </summary>
    public class ConfigurationService : IDisposable
    {
        // Instance members for file-watching configuration
        private readonly string _configFilePath;
        private readonly FileSystemWatcher? _fileWatcher;
        private readonly Dictionary<string, string> _configCache = new Dictionary<string, string>();
        private readonly object _cacheLock = new object();

        /// <summary>
        /// 설정 파일이 리로드될 때 발생하는 이벤트
        /// </summary>
        public event EventHandler? ConfigurationReloaded;

        /// <summary>
        /// 파일 감시 기능을 사용하는 인스턴스 생성자
        /// </summary>
        /// <param name="configFilePath">감시할 설정 파일 경로</param>
        public ConfigurationService(string configFilePath)
        {
            _configFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));

            // 초기 로드
            LoadConfiguration();

            // 파일 감시 설정
            if (File.Exists(_configFilePath))
            {
                string directory = Path.GetDirectoryName(_configFilePath) ?? string.Empty;
                string fileName = Path.GetFileName(_configFilePath);

                _fileWatcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Changed += OnConfigFileChanged;
            }
        }

        /// <summary>
        /// 설정 값 조회 (캐시 사용)
        /// </summary>
        /// <param name="key">설정 키</param>
        /// <returns>설정 값 (없으면 null)</returns>
        public string? Get(string key)
        {
            lock (_cacheLock)
            {
                return _configCache.TryGetValue(key, out string? value) ? value : null;
            }
        }

        /// <summary>
        /// 설정 파일 로드 (키=값 형식)
        /// </summary>
        private void LoadConfiguration()
        {
            lock (_cacheLock)
            {
                _configCache.Clear();

                if (!File.Exists(_configFilePath))
                {
                    return;
                }

                try
                {
                    string[] lines = File.ReadAllLines(_configFilePath);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith(";"))
                        {
                            continue; // Skip empty lines and comments
                        }

                        int separatorIndex = line.IndexOf('=');
                        if (separatorIndex > 0)
                        {
                            string key = line.Substring(0, separatorIndex).Trim();
                            string value = line.Substring(separatorIndex + 1).Trim();
                            _configCache[key] = value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.LogError($"설정 파일 로드 실패: {_configFilePath}", ex: ex);
                }
            }
        }

        /// <summary>
        /// 파일 변경 이벤트 핸들러
        /// </summary>
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 파일 시스템 이벤트는 여러 번 발생할 수 있으므로 짧은 대기
                System.Threading.Thread.Sleep(100);

                LoadConfiguration();
                ConfigurationReloaded?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"설정 리로드 실패: {_configFilePath}", ex: ex);
            }
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.Changed -= OnConfigFileChanged;
                _fileWatcher.Dispose();
            }
        }

        // ============================================================
        // Static methods (기존 호환성 유지)
        // ============================================================

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
            public string ApiServerUrl { get; set; } = "http://127.0.0.1:8000/";
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
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<AppSettings>(json, options)
                       ?? new AppSettings();
            }
            catch (Exception ex)
            {
                // 디버그용 상세 로그
                System.Diagnostics.Debug.WriteLine($"LoadSettings Exception: {ex}");
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
                LoggingService.LogInfo($"설정 저장 시작: {SettingsFilePath}");

                // 디렉토리가 없으면 생성
                if (!Directory.Exists(SettingsDirectory))
                {
                    LoggingService.LogInfo($"설정 디렉토리 생성: {SettingsDirectory}");
                    Directory.CreateDirectory(SettingsDirectory);
                }

                // JSON으로 직렬화
                LoggingService.LogInfo("JSON 직렬화 시작");
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                string json = JsonSerializer.Serialize(settings, options);
                LoggingService.LogInfo($"JSON 직렬화 완료: {json.Length} bytes");

                // 파일 쓰기
                LoggingService.LogInfo("파일 쓰기 시작");
                File.WriteAllText(SettingsFilePath, json, Encoding.UTF8);

                LoggingService.LogInfo("설정 파일 저장 성공");
                return true;
            }
            catch (Exception ex)
            {
                // 상세한 로그
                LoggingService.LogError($"설정 저장 실패: {ex.Message}", "ConfigurationService", ex);
                System.Diagnostics.Debug.WriteLine($"SaveSettings Exception: {ex}");
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
