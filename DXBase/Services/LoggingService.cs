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
