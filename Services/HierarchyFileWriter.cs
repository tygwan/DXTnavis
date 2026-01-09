using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// 계층 구조 데이터를 파일로 저장하는 서비스
    /// CSV 및 JSON 형식을 지원합니다.
    /// </summary>
    public class HierarchyFileWriter
    {
        /// <summary>
        /// 계층 구조 데이터를 CSV 파일로 저장
        /// ParentId와 Level 정보를 포함하여 부모-자식 관계를 표현합니다.
        /// v0.4.2: Unit 컬럼 추가
        /// </summary>
        public void WriteToCsv(string filePath, List<HierarchicalPropertyRecord> records, bool includeUnit = true)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            if (records == null || records.Count == 0)
                throw new ArgumentException("저장할 데이터가 없습니다.", nameof(records));

            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // CSV 헤더 작성 (v0.4.2: Unit 컬럼 추가)
                if (includeUnit)
                {
                    writer.WriteLine("ObjectId,ParentId,Level,DisplayName,Category,PropertyName,PropertyValue,DataType,Unit");
                }
                else
                {
                    writer.WriteLine("ObjectId,ParentId,Level,DisplayName,Category,PropertyName,PropertyValue");
                }

                // 데이터 작성
                foreach (var record in records)
                {
                    if (includeUnit)
                    {
                        var line = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                            record.ObjectId,
                            record.ParentId,
                            record.Level,
                            EscapeCsvField(record.DisplayName),
                            EscapeCsvField(record.Category),
                            EscapeCsvField(record.PropertyName),
                            EscapeCsvField(record.PropertyValue),
                            EscapeCsvField(record.DataType ?? string.Empty),
                            EscapeCsvField(record.Unit ?? string.Empty));

                        writer.WriteLine(line);
                    }
                    else
                    {
                        var line = string.Format("{0},{1},{2},{3},{4},{5},{6}",
                            record.ObjectId,
                            record.ParentId,
                            record.Level,
                            EscapeCsvField(record.DisplayName),
                            EscapeCsvField(record.Category),
                            EscapeCsvField(record.PropertyName),
                            EscapeCsvField(record.PropertyValue));

                        writer.WriteLine(line);
                    }
                }
            }
        }

        /// <summary>
        /// 계층 구조 데이터를 JSON 파일로 저장 (Flat 구조)
        /// </summary>
        public void WriteToJsonFlat(string filePath, List<HierarchicalPropertyRecord> records)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            if (records == null || records.Count == 0)
                throw new ArgumentException("저장할 데이터가 없습니다.", nameof(records));

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                StringEscapeHandling = StringEscapeHandling.Default
            };

            var json = JsonConvert.SerializeObject(records, settings);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 계층 구조 데이터를 중첩 JSON 트리 구조로 저장
        /// </summary>
        public void WriteToJsonTree(string filePath, List<HierarchicalPropertyRecord> records)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            if (records == null || records.Count == 0)
                throw new ArgumentException("저장할 데이터가 없습니다.", nameof(records));

            // ObjectId별로 그룹화
            var groupedByObject = records
                .GroupBy(r => r.ObjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 트리 구조 생성
            var rootNodes = BuildTree(records, Guid.Empty, groupedByObject);

            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                StringEscapeHandling = StringEscapeHandling.Default,
                NullValueHandling = NullValueHandling.Ignore
            };

            var json = JsonConvert.SerializeObject(rootNodes, settings);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// 재귀적으로 트리 구조를 생성하는 헬퍼 메서드
        /// </summary>
        private List<TreeNode> BuildTree(
            List<HierarchicalPropertyRecord> allRecords,
            Guid parentId,
            Dictionary<Guid, List<HierarchicalPropertyRecord>> groupedByObject)
        {
            var nodes = new List<TreeNode>();

            // 현재 parentId를 가진 모든 고유 객체 찾기
            var childObjectIds = allRecords
                .Where(r => r.ParentId == parentId)
                .Select(r => r.ObjectId)
                .Distinct();

            foreach (var objectId in childObjectIds)
            {
                if (!groupedByObject.ContainsKey(objectId))
                    continue;

                var objectRecords = groupedByObject[objectId];
                var firstRecord = objectRecords.First();

                var node = new TreeNode
                {
                    ObjectId = objectId.ToString(),
                    DisplayName = firstRecord.DisplayName,
                    Level = firstRecord.Level,
                    Properties = objectRecords.Select(r => new PropertyData
                    {
                        Category = r.Category,
                        Name = r.PropertyName,
                        Value = r.PropertyValue,
                        DataType = r.DataType ?? string.Empty,
                        Unit = r.Unit ?? string.Empty
                    }).ToList(),
                    Children = BuildTree(allRecords, objectId, groupedByObject)
                };

                nodes.Add(node);
            }

            return nodes;
        }

        /// <summary>
        /// CSV 필드 이스케이프 처리
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }

        /// <summary>
        /// JSON 트리 구조를 위한 노드 클래스
        /// </summary>
        private class TreeNode
        {
            public string ObjectId { get; set; }
            public string DisplayName { get; set; }
            public int Level { get; set; }
            public List<PropertyData> Properties { get; set; }
            public List<TreeNode> Children { get; set; }
        }

        /// <summary>
        /// 속성 데이터 클래스
        /// v0.4.2: Unit 필드 추가
        /// </summary>
        private class PropertyData
        {
            public string Category { get; set; }
            public string Name { get; set; }
            public string Value { get; set; }
            public string DataType { get; set; }
            public string Unit { get; set; }
        }
    }
}
