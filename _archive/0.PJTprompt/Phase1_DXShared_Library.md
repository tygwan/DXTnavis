# Phase 1: DXBase 공유 라이브러리 개발

## 문서 목적
DXrevit과 DXnavis에서 공통으로 사용하는 데이터 모델, 서비스, 유틸리티를 정의하고 구현하는 가이드입니다.

---

## 1. 개요

### 1.1. DXBase의 역할
DXBase는 단순한 DTO 공유를 넘어 **"공통 언어"**이자 **"공용 툴박스"**입니다.

**핵심 기능**:
1. **데이터 청사진**: 두 프로젝트 간 데이터 계약 보장
2. **공용 유틸리티**: 중복 코드 제거 및 일관성 유지
3. **설정 관리**: 통일된 설정 시스템
4. **로깅 관리**: 표준화된 로깅 전략

### 1.2. 기술 스펙
- **프레임워크**: .NET Standard 2.0
- **왜 .NET Standard 2.0?**:
  - DXrevit (.NET Core 8.0 - Revit 2025)과 DXnavis (.NET Framework 4.8) 모두와 호환
  - 향후 .NET Core/.NET 6+ 전환 시에도 호환성 유지

---

## 2. 프로젝트 구조

### 2.1. 폴더 구조
```
DXBase/
├── DXBase.csproj
├── Models/                          # 데이터 모델 (DTOs)
│   ├── MetadataRecord.cs
│   ├── ObjectRecord.cs
│   ├── RelationshipRecord.cs
│   ├── PropertyRecord.cs
│   └── ApiResponse.cs
├── Services/                        # 공용 서비스
│   ├── ConfigurationService.cs
│   ├── LoggingService.cs
│   └── HttpClientService.cs
├── Utils/                           # 유틸리티 함수
│   ├── IdGenerator.cs
│   ├── JsonSerializer.cs
│   └── ValidationHelper.cs
└── Constants/                       # 공용 상수
    ├── ApiEndpoints.cs
    └── ErrorMessages.cs
```

---

## 3. 데이터 모델 (DTOs) 구현

### 3.1. MetadataRecord.cs
**목적**: 버전 메타데이터를 표현하는 DTO

**구현해야 할 것**:
```csharp
namespace DXBase.Models
{
    /// <summary>
    /// BIM 모델 버전의 메타데이터를 표현하는 DTO
    /// </summary>
    public class MetadataRecord
    {
        /// <summary>
        /// 버전 식별자 (예: "v1.0.0", "2024-01-15_Rev01")
        /// </summary>
        public string ModelVersion { get; set; }

        /// <summary>
        /// 스냅샷 생성 시각 (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 프로젝트 이름
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// 생성자 (BIM 엔지니어 이름)
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// 변경 사유 또는 설명
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 총 객체 수 (성능 최적화용)
        /// </summary>
        public int TotalObjectCount { get; set; }

        /// <summary>
        /// Revit 파일 경로 (추적용)
        /// </summary>
        public string RevitFilePath { get; set; }
    }
}
```

**주의사항**:
- ✅ 모든 DateTime은 UTC로 저장 (시간대 문제 방지)
- ✅ ModelVersion은 고유해야 함 (데이터베이스에서 제약조건 필요)
- ❌ 비즈니스 로직 포함 금지 (순수 데이터 컨테이너)

### 3.2. ObjectRecord.cs
**목적**: BIM 객체(Element)의 모든 속성을 표현하는 DTO

**구현해야 할 것**:
```csharp
namespace DXBase.Models
{
    /// <summary>
    /// BIM 객체(Element)의 속성을 표현하는 DTO
    /// </summary>
    public class ObjectRecord
    {
        /// <summary>
        /// 어떤 버전에 속하는지
        /// </summary>
        public string ModelVersion { get; set; }

        /// <summary>
        /// Revit의 고유 식별자 (InstanceGuid 또는 생성된 해시)
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// Revit ElementId (정수형, 세션 종속적)
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// 카테고리 (예: "Walls", "Doors", "Windows")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 패밀리 이름 (예: "Basic Wall", "M_Door-Single")
        /// </summary>
        public string Family { get; set; }

        /// <summary>
        /// 타입 이름 (예: "Generic - 200mm")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 공정 ID (사용자 정의 공유 매개변수)
        /// TimeLiner 자동화의 핵심 필드
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// 모든 매개변수를 JSON 형태로 저장
        /// 유연성과 확장성을 위해
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// 객체의 3D 바운딩 박스 (JSON)
        /// 시각화 및 검색 최적화용
        /// </summary>
        public string BoundingBox { get; set; }
    }
}
```

**중요 설계 결정**:
- **ObjectId**: InstanceGuid가 있으면 사용, 없으면 경로 기반 해시 생성
- **Properties**: JSON 문자열로 저장하여 스키마 변경 없이 확장 가능
- **ActivityId**: NULL 허용 (모든 객체가 공정 ID를 가지지 않을 수 있음)
- **{custom}Id**: 사용자에 의해 3d model로부터 새롭게 정의된 Id값에 대응할 수 있음

**주의사항**:
- ✅ ObjectId는 버전 간 동일한 객체를 추적하는 유일한 키
- ✅ Properties JSON은 직렬화/역직렬화 시 표준 포맷 사용
- ❌ ElementId는 다른 Revit 세션에서 변경될 수 있으므로 절대 유일 키로 사용하지 말 것

### 3.3. RelationshipRecord.cs
**목적**: 객체 간 관계를 표현하는 DTO

**구현해야 할 것**:
```csharp
namespace DXBase.Models
{
    /// <summary>
    /// BIM 객체 간 관계를 표현하는 DTO
    /// </summary>
    public class RelationshipRecord
    {
        public string ModelVersion { get; set; }

        /// <summary>
        /// 관계의 출발 객체 (예: 벽)
        /// </summary>
        public string SourceObjectId { get; set; }

        /// <summary>
        /// 관계의 도착 객체 (예: 문)
        /// </summary>
        public string TargetObjectId { get; set; }

        /// <summary>
        /// 관계 유형 (예: "Contains", "ConnectsTo", "HostedBy")
        /// </summary>
        public string RelationType { get; set; }

        /// <summary>
        /// 관계 방향성 (true: 단방향, false: 양방향)
        /// </summary>
        public bool IsDirected { get; set; }
    }
}
```

**사용 예시**:
- 벽이 문을 포함: SourceObjectId=WallId, TargetObjectId=DoorId, RelationType="Contains"
- 파이프가 연결: SourceObjectId=Pipe1, TargetObjectId=Pipe2, RelationType="ConnectsTo"

### 3.4. PropertyRecord.cs
**목적**: 객체 속성을 정규화된 형태로 표현 (선택사항)

**구현해야 할 것**:
```csharp
namespace DXBase.Models
{
    /// <summary>
    /// 객체 속성을 정규화된 형태로 표현하는 DTO (선택사항)
    /// ObjectRecord.Properties JSON 대신 별도 테이블로 관리 시 사용
    /// </summary>
    public class PropertyRecord
    {
        public string ModelVersion { get; set; }
        public string ObjectId { get; set; }

        /// <summary>
        /// 속성 이름 (예: "Length", "Volume", "Cost")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 속성 값 (문자열로 저장)
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// 속성 데이터 타입 (예: "Double", "String", "Integer")
        /// </summary>
        public string PropertyType { get; set; }

        /// <summary>
        /// 속성 단위 (예: "m", "m³", "USD")
        /// </summary>
        public string PropertyUnit { get; set; }
    }
}
```

**선택 기준**:
- JSON 방식 (ObjectRecord.Properties): 빠른 개발, 유연한 스키마
- 정규화 방식 (PropertyRecord): 복잡한 쿼리, 성능 최적화

### 3.5. ApiResponse.cs
**목적**: API 응답의 표준 포맷

**구현해야 할 것**:
```csharp
namespace DXBase.Models
{
    /// <summary>
    /// API 응답의 표준 포맷
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 성공 여부
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 응답 데이터 (제네릭)
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 오류 메시지 (실패 시)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// HTTP 상태 코드
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 응답 타임스탬프
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
```

---

## 4. 공용 서비스 구현

### 4.1. ConfigurationService.cs
**목적**: 통일된 설정 관리 시스템

**구현해야 할 것**:
```csharp
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
            catch (Exception ex)
            {
                LoggingService.LogError($"설정 파일 로드 실패: {ex.Message}");
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
            catch (Exception ex)
            {
                LoggingService.LogError($"설정 파일 저장 실패: {ex.Message}");
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
```

**주의사항**:
- ✅ 설정 파일 위치는 사용자 AppData 폴더 사용 (권한 문제 방지)
- ✅ 파일이 없거나 손상되면 자동으로 기본값 생성
- ✅ JSON 포맷 사용 (사람이 읽고 편집 가능)
- ❌ 민감한 정보 (비밀번호 등)는 평문 저장 금지 (향후 암호화 필요)

### 4.2. LoggingService.cs
**목적**: 표준화된 로깅 시스템

**구현해야 할 것**:
```csharp
using System;
using System.IO;

namespace DXBase.Services
{
    /// <summary>
    /// 표준화된 로깅 서비스
    /// </summary>
    public static class LoggingService
    {
        private static string _logDirectory;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 로깅 초기화 (애플리케이션 시작 시 호출)
        /// </summary>
        public static void Initialize(string logDirectory)
        {
            _logDirectory = logDirectory;

            // 로그 디렉토리 생성
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        /// <summary>
        /// 정보 로그
        /// </summary>
        public static void LogInfo(string message, string source = "DX")
        {
            WriteLog("INFO", message, source);
        }

        /// <summary>
        /// 경고 로그
        /// </summary>
        public static void LogWarning(string message, string source = "DX")
        {
            WriteLog("WARNING", message, source);
        }

        /// <summary>
        /// 오류 로그
        /// </summary>
        public static void LogError(string message, string source = "DX", Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}";
            }
            WriteLog("ERROR", fullMessage, source);
        }

        /// <summary>
        /// 로그 파일에 기록
        /// </summary>
        private static void WriteLog(string level, string message, string source)
        {
            try
            {
                // 로그 파일 이름: DX_20240115.log (일별)
                string fileName = $"DX_{DateTime.Now:yyyyMMdd}.log";
                string filePath = Path.Combine(_logDirectory, fileName);

                // 로그 포맷: [2024-01-15 14:30:25] [INFO] [DXrevit] 메시지
                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}] {message}";

                // 멀티스레드 환경에서 안전하게 파일 쓰기
                lock (_lockObject)
                {
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // 로깅 실패 시 콘솔에만 출력 (무한 루프 방지)
                Console.WriteLine($"로깅 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 오래된 로그 파일 정리 (30일 이상)
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                if (string.IsNullOrEmpty(_logDirectory) || !Directory.Exists(_logDirectory))
                    return;

                var files = Directory.GetFiles(_logDirectory, "DX_*.log");
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoffDate)
                    {
                        File.Delete(file);
                        LogInfo($"오래된 로그 파일 삭제: {fileInfo.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("로그 정리 실패", ex: ex);
            }
        }
    }
}
```

**사용 예시**:
```csharp
// 애플리케이션 시작 시
var settings = ConfigurationService.LoadSettings();
LoggingService.Initialize(settings.LogFilePath);

// 로그 기록
LoggingService.LogInfo("애플리케이션 시작", "DXrevit");
LoggingService.LogWarning("API 서버 응답 느림", "DXrevit");
LoggingService.LogError("데이터 저장 실패", "DXrevit", exception);
```

### 4.3. HttpClientService.cs
**목적**: API 통신을 위한 재사용 가능한 HTTP 클라이언트

**구현해야 할 것**:
```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

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
```

---

## 5. 유틸리티 함수 구현

### 5.1. IdGenerator.cs
**목적**: 고유 ID 생성 유틸리티

**구현해야 할 것**:
```csharp
using System;
using System.Security.Cryptography;
using System.Text;

namespace DXBase.Utils
{
    /// <summary>
    /// 고유 ID 생성 유틸리티
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// InstanceGuid 또는 경로 기반 해시로 고유 ID 생성
        /// </summary>
        public static string GenerateObjectId(string instanceGuid, string category, string family, string type)
        {
            // InstanceGuid가 유효하면 그대로 사용
            if (!string.IsNullOrEmpty(instanceGuid) &&
                instanceGuid != Guid.Empty.ToString())
            {
                return instanceGuid;
            }

            // InstanceGuid가 없으면 경로 기반 해시 생성
            string path = $"{category}|{family}|{type}";
            return GenerateSHA256Hash(path);
        }

        /// <summary>
        /// SHA256 해시 생성
        /// </summary>
        private static string GenerateSHA256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);

                // 바이트 배열을 16진수 문자열로 변환
                var builder = new StringBuilder();
                foreach (byte b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// ModelVersion 생성 (타임스탬프 기반)
        /// </summary>
        public static string GenerateModelVersion(string projectName)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return $"{projectName}_{timestamp}";
        }
    }
}
```

### 5.2. JsonSerializer.cs
**목적**: JSON 직렬화/역직렬화 헬퍼

**구현해야 할 것**:
```csharp
using System.Text.Json;

namespace DXBase.Utils
{
    /// <summary>
    /// JSON 직렬화/역직렬화 헬퍼
    /// </summary>
    public static class JsonHelper
    {
        private static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// 객체를 JSON 문자열로 변환
        /// </summary>
        public static string Serialize<T>(T obj)
        {
            return JsonSerializer.Serialize(obj, DefaultOptions);
        }

        /// <summary>
        /// JSON 문자열을 객체로 변환
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, DefaultOptions);
        }
    }
}
```

### 5.3. ValidationHelper.cs
**목적**: 데이터 검증 헬퍼

**구현해야 할 것**:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXBase.Utils
{
    /// <summary>
    /// 데이터 검증 헬퍼
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// 문자열이 비어있지 않은지 검증
        /// </summary>
        public static bool IsNotEmpty(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// ModelVersion 형식 검증 (영문, 숫자, -, _ 만 허용)
        /// </summary>
        public static bool IsValidModelVersion(string modelVersion)
        {
            if (string.IsNullOrWhiteSpace(modelVersion))
                return false;

            return modelVersion.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        /// <summary>
        /// URL 형식 검증
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// 필수 필드 검증
        /// </summary>
        public static List<string> ValidateMetadata(MetadataRecord metadata)
        {
            var errors = new List<string>();

            if (!IsNotEmpty(metadata.ModelVersion))
                errors.Add("ModelVersion은 필수 입력 항목입니다.");

            if (!IsValidModelVersion(metadata.ModelVersion))
                errors.Add("ModelVersion 형식이 올바르지 않습니다.");

            if (!IsNotEmpty(metadata.ProjectName))
                errors.Add("ProjectName은 필수 입력 항목입니다.");

            if (!IsNotEmpty(metadata.CreatedBy))
                errors.Add("CreatedBy는 필수 입력 항목입니다.");

            return errors;
        }
    }
}
```

---

## 6. 상수 정의

### 6.1. ApiEndpoints.cs
```csharp
namespace DXBase.Constants
{
    /// <summary>
    /// API 엔드포인트 상수
    /// </summary>
    public static class ApiEndpoints
    {
        // 데이터 수집
        public const string Ingest = "/api/v1/ingest";

        // 버전 관리
        public const string GetVersions = "/api/v1/models/versions";
        public const string GetVersionSummary = "/api/v1/models/{version}/summary";

        // 버전 비교
        public const string CompareVersions = "/api/v1/models/compare";

        // TimeLiner 연동
        public const string GetTimelinerMapping = "/api/v1/timeliner/{version}/mapping";
    }
}
```

### 6.2. ErrorMessages.cs
```csharp
namespace DXBase.Constants
{
    /// <summary>
    /// 오류 메시지 상수
    /// </summary>
    public static class ErrorMessages
    {
        public const string ConnectionFailed = "API 서버 연결 실패. 서버 주소를 확인하세요.";
        public const string InvalidData = "유효하지 않은 데이터 형식입니다.";
        public const string Timeout = "요청 시간 초과. 네트워크 상태를 확인하세요.";
        public const string Unauthorized = "인증이 필요합니다.";
        public const string ServerError = "서버 오류가 발생했습니다.";
    }
}
```

---

## 7. 프로젝트 설정

### 7.1. DXBase.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" Version="6.0.0" />
  </ItemGroup>

</Project>
```

---

## 8. 개발 가이드라인

### 8.1. ✅ 해야 할 것

**코드 품질**:
- ✅ 모든 public 메서드에 XML 주석 작성
- ✅ 예외 처리 및 로깅 포함
- ✅ 네이밍 컨벤션 준수 (PascalCase for public, camelCase for private)

**테스트**:
- ✅ 단위 테스트 작성 (xUnit 권장)
- ✅ 다양한 시나리오 테스트
- ✅ 경계값 테스트

### 8.2. ❌ 하지 말아야 할 것

**의존성**:
- ❌ Revit API나 Navisworks API 참조 금지
- ❌ .NET Framework 전용 라이브러리 사용 금지
- ❌ 플랫폼 종속적 코드 작성 금지

**비즈니스 로직**:
- ❌ DTO에 비즈니스 로직 포함 금지
- ❌ 서비스에 UI 로직 포함 금지

---

## 9. 테스트 계획

### 9.1. 단위 테스트 예시
```csharp
using Xunit;

namespace DXBase.Tests
{
    public class IdGeneratorTests
    {
        [Fact]
        public void GenerateObjectId_WithValidGuid_ReturnsGuid()
        {
            // Arrange
            string guid = Guid.NewGuid().ToString();

            // Act
            string result = IdGenerator.GenerateObjectId(guid, "Walls", "Basic Wall", "Generic");

            // Assert
            Assert.Equal(guid, result);
        }

        [Fact]
        public void GenerateObjectId_WithoutGuid_ReturnsHash()
        {
            // Arrange
            string emptyGuid = Guid.Empty.ToString();

            // Act
            string result = IdGenerator.GenerateObjectId(emptyGuid, "Walls", "Basic Wall", "Generic");

            // Assert
            Assert.NotEqual(emptyGuid, result);
            Assert.NotEmpty(result);
        }
    }
}
```

---

## 10. 다음 단계

DXBase 구현 완료 후:
1. DXrevit과 DXnavis에서 프로젝트 참조 추가
2. 기존 중복 코드를 DXBase로 마이그레이션
3. 통합 테스트 수행
4. Phase 2 (DXrevit 개발) 진행
