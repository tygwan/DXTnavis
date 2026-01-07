using DXBase.Models;
using DXBase.Services;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace DXrevit.Services
{
    /// <summary>
    /// 리비전 관리 서비스 - 새 스키마 v2.0
    /// </summary>
    public class RevisionManager
    {
        private readonly HttpClientService _httpClient;

        public RevisionManager()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        /// <summary>
        /// 새 리비전 생성 (자동 번호 할당)
        /// </summary>
        public async Task<RevisionInfo> CreateRevisionAsync(
            string projectCode,
            string versionTag,
            string description,
            Document document)
        {
            try
            {
                LoggingService.LogInfo($"리비전 생성 시작: 프로젝트={projectCode}, 버전={versionTag}", "DXrevit");

                // 1. 파일 해시 계산
                string fileHash = null;
                if (!string.IsNullOrEmpty(document.PathName) && File.Exists(document.PathName))
                {
                    fileHash = CalculateFileHash(document.PathName);
                }

                // 2. 리비전 정보 생성
                var revisionInfo = new RevisionInfo
                {
                    VersionTag = versionTag,
                    Description = description,
                    SourceType = "revit",
                    SourceFilePath = document.PathName,
                    SourceFileHash = fileHash,
                    CreatedBy = Environment.UserName,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>
                    {
                        { "revit_version", document.Application.VersionNumber },
                        { "revit_build", document.Application.VersionBuild },
                        { "created_at", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                    }
                };

                // 3. API로 리비전 생성
                var response = await _httpClient.PostAsync<RevisionInfo, RevisionInfo>(
                    $"/api/v1/projects/{projectCode}/revisions",
                    revisionInfo);

                if (!response.Success)
                {
                    throw new Exception($"리비전 생성 실패: {response.ErrorMessage}");
                }

                LoggingService.LogInfo(
                    $"리비전 생성 완료: #{response.Data.RevisionNumber} (ID: {response.Data.Id})",
                    "DXrevit");

                return response.Data;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("리비전 생성 중 오류 발생", "DXrevit", ex);
                throw;
            }
        }

        /// <summary>
        /// 최신 리비전 조회
        /// </summary>
        public async Task<RevisionInfo> GetLatestRevisionAsync(string projectCode)
        {
            try
            {
                var response = await _httpClient.GetAsync<RevisionInfo>(
                    $"/api/v1/projects/{projectCode}/revisions/latest/revit");

                if (response.Success)
                {
                    LoggingService.LogInfo(
                        $"최신 리비전 조회: #{response.Data.RevisionNumber}",
                        "DXrevit");
                    return response.Data;
                }
                else
                {
                    LoggingService.LogInfo("리비전이 없습니다 (첫 리비전 생성 필요)", "DXrevit");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"리비전 조회 실패: {ex.Message}", "DXrevit");
                return null;
            }
        }

        /// <summary>
        /// 리비전 목록 조회
        /// </summary>
        public async Task<System.Collections.Generic.List<RevisionInfo>> GetRevisionsAsync(
            string projectCode,
            int limit = 10)
        {
            var response = await _httpClient.GetAsync<System.Collections.Generic.List<RevisionInfo>>(
                $"/api/v1/projects/{projectCode}/revisions?source_type=revit&limit={limit}");

            return response.Success ? response.Data : new System.Collections.Generic.List<RevisionInfo>();
        }

        /// <summary>
        /// 파일 해시 계산 (SHA256)
        /// </summary>
        private string CalculateFileHash(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogWarning($"파일 해시 계산 실패: {ex.Message}", "DXrevit");
                return null;
            }
        }

        /// <summary>
        /// 객체 데이터를 리비전에 대량 업로드
        /// </summary>
        public async Task<bool> UploadObjectsToRevisionAsync(
            string projectCode,
            int revisionNumber,
            System.Collections.Generic.List<ObjectData> objects)
        {
            try
            {
                LoggingService.LogInfo(
                    $"객체 업로드 시작: {objects.Count}개 (리비전 #{revisionNumber})",
                    "DXrevit");

                var bulkData = new
                {
                    objects = objects
                };

                var response = await _httpClient.PostAsync<object, object>(
                    $"/api/v1/projects/{projectCode}/revisions/{revisionNumber}/objects/bulk?source_type=revit",
                    bulkData);

                if (response.Success)
                {
                    LoggingService.LogInfo($"객체 업로드 완료: {objects.Count}개", "DXrevit");
                    return true;
                }
                else
                {
                    LoggingService.LogError($"객체 업로드 실패: {response.ErrorMessage}", "DXrevit");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("객체 업로드 중 오류 발생", "DXrevit", ex);
                return false;
            }
        }
    }

    /// <summary>
    /// 리비전 정보 모델
    /// </summary>
    public class RevisionInfo
    {
        public string Id { get; set; }
        public string ProjectId { get; set; }
        public int RevisionNumber { get; set; }
        public string VersionTag { get; set; }
        public string Description { get; set; }
        public string SourceType { get; set; }
        public string SourceFilePath { get; set; }
        public string SourceFileHash { get; set; }
        public int TotalObjects { get; set; }
        public int TotalCategories { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 객체 데이터 모델 (v2.0)
    /// </summary>
    public class ObjectData
    {
        public string object_id { get; set; }         // Revit UniqueId
        public int? element_id { get; set; }          // Revit Element ID
        public string display_name { get; set; }      // 표시 이름
        public string category { get; set; }          // 카테고리
        public string family { get; set; }            // 패밀리
        public string type { get; set; }              // 유형
        public string activity_id { get; set; }       // Activity ID (4D)
        public System.Collections.Generic.Dictionary<string, object> properties { get; set; }  // 모든 속성
        public BoundingBoxData bounding_box { get; set; }  // Bounding Box
    }

    /// <summary>
    /// Bounding Box 데이터
    /// </summary>
    public class BoundingBoxData
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MinZ { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
        public double MaxZ { get; set; }
    }
}
