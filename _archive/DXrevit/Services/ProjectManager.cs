using DXBase.Models;
using DXBase.Services;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Revit.DB;

namespace DXrevit.Services
{
    /// <summary>
    /// 프로젝트 관리 서비스 - 새 스키마 v2.0
    /// </summary>
    public class ProjectManager
    {
        private readonly HttpClientService _httpClient;

        public ProjectManager()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        /// <summary>
        /// Revit 문서에서 프로젝트 정보 추출 및 등록
        /// </summary>
        public async Task<ProjectInfo> RegisterOrGetProjectAsync(Document document)
        {
            try
            {
                // 1. 파일명에서 프로젝트 코드 생성
                string fileName = Path.GetFileNameWithoutExtension(document.PathName);
                string projectCode = GenerateProjectCode(fileName);

                LoggingService.LogInfo($"프로젝트 코드 생성: {fileName} → {projectCode}", "DXrevit");

                // 2. 프로젝트 존재 확인
                var existingProject = await CheckProjectExistsAsync(projectCode);
                if (existingProject != null)
                {
                    LoggingService.LogInfo($"기존 프로젝트 발견: {projectCode}", "DXrevit");
                    return existingProject;
                }

                // 3. 새 프로젝트 생성
                var projectInfo = ExtractProjectInfo(document, projectCode);
                var createdProject = await CreateProjectAsync(projectInfo);

                LoggingService.LogInfo($"새 프로젝트 생성 완료: {projectCode}", "DXrevit");
                return createdProject;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("프로젝트 등록 중 오류 발생", "DXrevit", ex);
                throw;
            }
        }

        /// <summary>
        /// 파일명을 프로젝트 코드로 변환
        /// </summary>
        private string GenerateProjectCode(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "UNKNOWN_PROJECT";

            // 공백, 하이픈을 언더스코어로
            string code = fileName.Replace(" ", "_").Replace("-", "_").ToUpperInvariant();

            // 특수문자 제거 (한글, 영문, 숫자, 언더스코어만 허용)
            code = Regex.Replace(code, @"[^A-Z0-9_가-힣]", "");

            // 길이 제한 (최대 50자)
            if (code.Length > 50)
            {
                code = code.Substring(0, 50);
            }

            return string.IsNullOrEmpty(code) ? "UNKNOWN_PROJECT" : code;
        }

        /// <summary>
        /// Revit 문서에서 프로젝트 정보 추출
        /// </summary>
        private ProjectInfo ExtractProjectInfo(Document document, string projectCode)
        {
            var projectInfo = document.ProjectInformation;

            return new ProjectInfo
            {
                Code = projectCode,
                Name = Path.GetFileNameWithoutExtension(document.PathName),
                RevitFileName = Path.GetFileName(document.PathName),
                RevitFilePath = document.PathName,
                ProjectNumber = projectInfo.Number,
                ClientName = projectInfo.ClientName,
                Address = projectInfo.Address,
                BuildingName = projectInfo.BuildingName,
                CreatedBy = Environment.UserName,
                Metadata = new System.Collections.Generic.Dictionary<string, object>
                {
                    { "author", projectInfo.Author },
                    { "issue_date", projectInfo.IssueDate },
                    { "status", projectInfo.Status },
                    { "extracted_at", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
                }
            };
        }

        /// <summary>
        /// 프로젝트 존재 확인
        /// </summary>
        private async Task<ProjectInfo> CheckProjectExistsAsync(string projectCode)
        {
            var response = await _httpClient.GetAsync<ProjectInfo>(
                $"/api/v1/projects/{projectCode}");

            return response.Success ? response.Data : null;
        }

        /// <summary>
        /// 새 프로젝트 생성
        /// </summary>
        private async Task<ProjectInfo> CreateProjectAsync(ProjectInfo projectInfo)
        {
            var response = await _httpClient.PostAsync<ProjectInfo, ProjectInfo>(
                "/api/v1/projects",
                projectInfo);

            if (!response.Success)
            {
                throw new Exception($"프로젝트 생성 실패: {response.ErrorMessage}");
            }

            return response.Data;
        }

        /// <summary>
        /// 프로젝트 통계 조회
        /// </summary>
        public async Task<ProjectStats> GetProjectStatsAsync(string projectCode)
        {
            var response = await _httpClient.GetAsync<ProjectStats>(
                $"/api/v1/projects/{projectCode}/stats");

            if (!response.Success)
            {
                LoggingService.LogWarning($"프로젝트 통계 조회 실패: {response.ErrorMessage}", "DXrevit");
                return null;
            }

            return response.Data;
        }
    }

    /// <summary>
    /// 프로젝트 정보 모델
    /// </summary>
    public class ProjectInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("code")]
        public string Code { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("revit_file_name")]
        public string RevitFileName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("revit_file_path")]
        public string RevitFilePath { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("project_number")]
        public string ProjectNumber { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("client_name")]
        public string ClientName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("address")]
        public string Address { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("building_name")]
        public string BuildingName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_by")]
        public string CreatedBy { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("metadata")]
        public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; }
    }

    /// <summary>
    /// 프로젝트 통계 모델
    /// </summary>
    public class ProjectStats
    {
        public string ProjectCode { get; set; }
        public int TotalRevisions { get; set; }
        public int RevitRevisions { get; set; }
        public int NavisRevisions { get; set; }
        public int TotalObjects { get; set; }
        public int TotalCategories { get; set; }
        public int TotalActivities { get; set; }
    }
}
