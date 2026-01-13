using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// Phase 5: 속성 데이터 유효성 검증 서비스
    /// v0.7.0: Initial implementation
    /// </summary>
    public class ValidationService
    {
        #region Validation Rules Configuration

        /// <summary>
        /// 필수 속성 목록 (Category -> PropertyNames)
        /// </summary>
        private readonly Dictionary<string, List<string>> _requiredProperties = new Dictionary<string, List<string>>
        {
            { "Item", new List<string> { "Name", "Type" } },
            { "항목", new List<string> { "이름", "유형" } }
        };

        /// <summary>
        /// 단위 패턴 정규식
        /// </summary>
        private static readonly Dictionary<string, Regex> UnitPatterns = new Dictionary<string, Regex>
        {
            { "length_mm", new Regex(@"(\d+(?:\.\d+)?)\s*(mm|밀리미터)", RegexOptions.IgnoreCase) },
            { "length_m", new Regex(@"(\d+(?:\.\d+)?)\s*(m|미터)(?!m)", RegexOptions.IgnoreCase) },
            { "length_ft", new Regex(@"(\d+(?:\.\d+)?)\s*(ft|feet|피트)", RegexOptions.IgnoreCase) },
            { "length_in", new Regex(@"(\d+(?:\.\d+)?)\s*(in|inch|인치)", RegexOptions.IgnoreCase) },
            { "weight_kg", new Regex(@"(\d+(?:\.\d+)?)\s*(kg|킬로그램)", RegexOptions.IgnoreCase) },
            { "weight_lbm", new Regex(@"(\d+(?:\.\d+)?)\s*(lbm|lb|파운드)", RegexOptions.IgnoreCase) }
        };

        /// <summary>
        /// 데이터 타입별 패턴
        /// </summary>
        private static readonly Dictionary<string, Regex> DataTypePatterns = new Dictionary<string, Regex>
        {
            { "Double", new Regex(@"^Double:(-?\d+(?:\.\d+)?(?:E[+-]?\d+)?)$", RegexOptions.IgnoreCase) },
            { "Int32", new Regex(@"^Int32:(-?\d+)$", RegexOptions.IgnoreCase) },
            { "Boolean", new Regex(@"^Boolean:(True|False)$", RegexOptions.IgnoreCase) },
            { "DisplayString", new Regex(@"^DisplayString:(.*)$", RegexOptions.IgnoreCase) }
        };

        #endregion

        #region Main Validation Methods

        /// <summary>
        /// 전체 유효성 검증 수행
        /// </summary>
        public ValidationReport ValidateAll(IEnumerable<HierarchicalPropertyRecord> properties)
        {
            var report = new ValidationReport
            {
                ValidationDate = DateTime.Now,
                Issues = new List<ValidationIssue>()
            };

            var propertyList = properties.ToList();
            report.TotalProperties = propertyList.Count;

            // 객체별로 그룹화
            var objectGroups = propertyList.GroupBy(p => p.ObjectId);
            report.TotalObjects = objectGroups.Count();

            foreach (var objectGroup in objectGroups)
            {
                var objectId = objectGroup.Key;
                var objectProperties = objectGroup.ToList();
                var displayName = objectProperties.FirstOrDefault()?.DisplayName ?? "Unknown";

                // FR-501: 단위 불일치 감지
                var unitIssues = CheckUnitConsistency(objectId, displayName, objectProperties);
                report.Issues.AddRange(unitIssues);

                // FR-502: 필수 속성 누락 확인
                var missingIssues = CheckRequiredProperties(objectId, displayName, objectProperties);
                report.Issues.AddRange(missingIssues);

                // FR-503: 데이터 타입 불일치 감지
                var typeIssues = CheckDataTypeConsistency(objectId, displayName, objectProperties);
                report.Issues.AddRange(typeIssues);
            }

            // 통계 계산
            report.WarningCount = report.Issues.Count(i => i.Severity == ValidationSeverity.Warning);
            report.ErrorCount = report.Issues.Count(i => i.Severity == ValidationSeverity.Error);
            report.ValidObjects = report.TotalObjects - report.Issues.Select(i => i.ObjectId).Distinct().Count();

            return report;
        }

        #endregion

        #region FR-501: Unit Consistency Check

        /// <summary>
        /// 단위 일관성 검사
        /// </summary>
        private List<ValidationIssue> CheckUnitConsistency(Guid objectId, string displayName, List<HierarchicalPropertyRecord> properties)
        {
            var issues = new List<ValidationIssue>();

            // 카테고리별로 그룹화하여 단위 확인
            var categoryGroups = properties.GroupBy(p => p.Category);

            foreach (var categoryGroup in categoryGroups)
            {
                var unitsFound = new Dictionary<string, List<string>>();

                foreach (var prop in categoryGroup)
                {
                    if (string.IsNullOrEmpty(prop.PropertyValue)) continue;

                    foreach (var unitPattern in UnitPatterns)
                    {
                        if (unitPattern.Value.IsMatch(prop.PropertyValue))
                        {
                            var unitType = unitPattern.Key.Split('_')[0]; // e.g., "length", "weight"
                            if (!unitsFound.ContainsKey(unitType))
                                unitsFound[unitType] = new List<string>();

                            var unitName = unitPattern.Key.Split('_')[1]; // e.g., "mm", "m", "ft"
                            if (!unitsFound[unitType].Contains(unitName))
                                unitsFound[unitType].Add(unitName);
                        }
                    }
                }

                // 동일 유형에 여러 단위가 혼용되면 경고
                foreach (var unitType in unitsFound)
                {
                    if (unitType.Value.Count > 1)
                    {
                        issues.Add(new ValidationIssue
                        {
                            ObjectId = objectId,
                            ObjectName = displayName,
                            IssueType = ValidationIssueType.UnitMismatch,
                            Severity = ValidationSeverity.Warning,
                            Category = categoryGroup.Key,
                            Details = $"{unitType.Key} 단위 혼용: {string.Join(", ", unitType.Value)}",
                            Suggestion = $"단위를 {unitType.Value.First()}로 통일하세요"
                        });
                    }
                }
            }

            return issues;
        }

        #endregion

        #region FR-502: Required Properties Check

        /// <summary>
        /// 필수 속성 존재 여부 확인
        /// </summary>
        private List<ValidationIssue> CheckRequiredProperties(Guid objectId, string displayName, List<HierarchicalPropertyRecord> properties)
        {
            var issues = new List<ValidationIssue>();

            foreach (var requiredCategory in _requiredProperties)
            {
                var categoryProps = properties.Where(p => p.Category == requiredCategory.Key).ToList();

                if (!categoryProps.Any())
                {
                    // 카테고리 자체가 없으면 건너뜀 (다른 유형의 객체일 수 있음)
                    continue;
                }

                foreach (var requiredProp in requiredCategory.Value)
                {
                    var found = categoryProps.Any(p =>
                        p.PropertyName?.Equals(requiredProp, StringComparison.OrdinalIgnoreCase) == true &&
                        !string.IsNullOrWhiteSpace(p.PropertyValue));

                    if (!found)
                    {
                        issues.Add(new ValidationIssue
                        {
                            ObjectId = objectId,
                            ObjectName = displayName,
                            IssueType = ValidationIssueType.MissingProperty,
                            Severity = ValidationSeverity.Error,
                            Category = requiredCategory.Key,
                            PropertyName = requiredProp,
                            Details = $"필수 속성 '{requiredProp}' 누락",
                            Suggestion = $"'{requiredCategory.Key}' 카테고리에 '{requiredProp}' 속성을 추가하세요"
                        });
                    }
                }
            }

            return issues;
        }

        #endregion

        #region FR-503: Data Type Consistency Check

        /// <summary>
        /// 데이터 타입 일관성 검사
        /// </summary>
        private List<ValidationIssue> CheckDataTypeConsistency(Guid objectId, string displayName, List<HierarchicalPropertyRecord> properties)
        {
            var issues = new List<ValidationIssue>();

            foreach (var prop in properties)
            {
                if (string.IsNullOrEmpty(prop.DataType) || string.IsNullOrEmpty(prop.PropertyValue))
                    continue;

                // 데이터 타입과 값 형식이 일치하는지 확인
                if (DataTypePatterns.TryGetValue(prop.DataType, out var pattern))
                {
                    if (!pattern.IsMatch(prop.PropertyValue))
                    {
                        issues.Add(new ValidationIssue
                        {
                            ObjectId = objectId,
                            ObjectName = displayName,
                            IssueType = ValidationIssueType.TypeMismatch,
                            Severity = ValidationSeverity.Warning,
                            Category = prop.Category,
                            PropertyName = prop.PropertyName,
                            Details = $"데이터 타입 불일치: 선언={prop.DataType}, 값={prop.PropertyValue}",
                            Suggestion = $"값을 {prop.DataType} 형식에 맞게 수정하세요"
                        });
                    }
                }
            }

            return issues;
        }

        #endregion

        #region FR-504: Report Generation

        /// <summary>
        /// 검증 리포트를 JSON 형식 문자열로 변환
        /// </summary>
        public string GenerateReportJson(ValidationReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"summary\": {");
            sb.AppendLine($"    \"validationDate\": \"{report.ValidationDate:yyyy-MM-dd HH:mm:ss}\",");
            sb.AppendLine($"    \"totalObjects\": {report.TotalObjects},");
            sb.AppendLine($"    \"totalProperties\": {report.TotalProperties},");
            sb.AppendLine($"    \"validObjects\": {report.ValidObjects},");
            sb.AppendLine($"    \"warningCount\": {report.WarningCount},");
            sb.AppendLine($"    \"errorCount\": {report.ErrorCount}");
            sb.AppendLine("  },");
            sb.AppendLine("  \"issues\": [");

            for (int i = 0; i < report.Issues.Count; i++)
            {
                var issue = report.Issues[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"objectId\": \"{issue.ObjectId}\",");
                sb.AppendLine($"      \"objectName\": \"{EscapeJson(issue.ObjectName)}\",");
                sb.AppendLine($"      \"type\": \"{issue.IssueType}\",");
                sb.AppendLine($"      \"severity\": \"{issue.Severity}\",");
                sb.AppendLine($"      \"category\": \"{EscapeJson(issue.Category)}\",");
                sb.AppendLine($"      \"propertyName\": \"{EscapeJson(issue.PropertyName)}\",");
                sb.AppendLine($"      \"details\": \"{EscapeJson(issue.Details)}\",");
                sb.AppendLine($"      \"suggestion\": \"{EscapeJson(issue.Suggestion)}\"");
                sb.Append("    }");
                if (i < report.Issues.Count - 1) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// 검증 리포트를 요약 텍스트로 변환
        /// </summary>
        public string GenerateReportSummary(ValidationReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine("                    VALIDATION REPORT                       ");
            sb.AppendLine("═══════════════════════════════════════════════════════════");
            sb.AppendLine($"검증 일시: {report.ValidationDate:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"총 객체 수: {report.TotalObjects}");
            sb.AppendLine($"총 속성 수: {report.TotalProperties}");
            sb.AppendLine($"유효 객체: {report.ValidObjects}");
            sb.AppendLine($"경고: {report.WarningCount}개");
            sb.AppendLine($"오류: {report.ErrorCount}개");
            sb.AppendLine("───────────────────────────────────────────────────────────");

            if (report.Issues.Any())
            {
                // 오류 먼저 표시
                var errors = report.Issues.Where(i => i.Severity == ValidationSeverity.Error).ToList();
                if (errors.Any())
                {
                    sb.AppendLine("\n[오류]");
                    foreach (var error in errors)
                    {
                        sb.AppendLine($"  ❌ {error.ObjectName}: {error.Details}");
                        sb.AppendLine($"     → {error.Suggestion}");
                    }
                }

                // 경고 표시
                var warnings = report.Issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList();
                if (warnings.Any())
                {
                    sb.AppendLine("\n[경고]");
                    foreach (var warning in warnings)
                    {
                        sb.AppendLine($"  ⚠️ {warning.ObjectName}: {warning.Details}");
                        sb.AppendLine($"     → {warning.Suggestion}");
                    }
                }
            }
            else
            {
                sb.AppendLine("\n✅ 모든 검증을 통과했습니다!");
            }

            sb.AppendLine("\n═══════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 필수 속성 규칙 추가
        /// </summary>
        public void AddRequiredProperty(string category, string propertyName)
        {
            if (!_requiredProperties.ContainsKey(category))
                _requiredProperties[category] = new List<string>();

            if (!_requiredProperties[category].Contains(propertyName))
                _requiredProperties[category].Add(propertyName);
        }

        /// <summary>
        /// 필수 속성 규칙 제거
        /// </summary>
        public void RemoveRequiredProperty(string category, string propertyName)
        {
            if (_requiredProperties.ContainsKey(category))
                _requiredProperties[category].Remove(propertyName);
        }

        /// <summary>
        /// 현재 필수 속성 규칙 조회
        /// </summary>
        public Dictionary<string, List<string>> GetRequiredProperties()
        {
            return new Dictionary<string, List<string>>(_requiredProperties);
        }

        #endregion
    }

    #region Data Models

    /// <summary>
    /// 검증 리포트
    /// </summary>
    public class ValidationReport
    {
        public DateTime ValidationDate { get; set; }
        public int TotalObjects { get; set; }
        public int TotalProperties { get; set; }
        public int ValidObjects { get; set; }
        public int WarningCount { get; set; }
        public int ErrorCount { get; set; }
        public List<ValidationIssue> Issues { get; set; }
    }

    /// <summary>
    /// 검증 이슈
    /// </summary>
    public class ValidationIssue
    {
        public Guid ObjectId { get; set; }
        public string ObjectName { get; set; }
        public ValidationIssueType IssueType { get; set; }
        public ValidationSeverity Severity { get; set; }
        public string Category { get; set; }
        public string PropertyName { get; set; }
        public string Details { get; set; }
        public string Suggestion { get; set; }
    }

    /// <summary>
    /// 이슈 유형
    /// </summary>
    public enum ValidationIssueType
    {
        UnitMismatch,       // FR-501: 단위 불일치
        MissingProperty,    // FR-502: 필수 속성 누락
        TypeMismatch        // FR-503: 데이터 타입 불일치
    }

    /// <summary>
    /// 심각도
    /// </summary>
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    #endregion
}
