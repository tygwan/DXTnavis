using Autodesk.Revit.DB;
using DXBase.Models;
using DXBase.Services;
using DXBase.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXrevit.Services
{
    /// <summary>
    /// Revit 데이터 추출 서비스
    /// </summary>
    public class DataExtractor
    {
        private readonly Document _document;

        public DataExtractor(Document document)
        {
            _document = document;
        }

        /// <summary>
        /// 전체 데이터 추출 (메타데이터, 객체, 관계)
        /// </summary>
        public ExtractedData ExtractAll(string modelVersion, string createdBy, string description)
        {
            LoggingService.LogInfo("데이터 추출 시작", "DXrevit");

            var extractedData = new ExtractedData
            {
                Metadata = ExtractMetadata(modelVersion, createdBy, description),
                Objects = new List<ObjectRecord>(),
                Relationships = new List<RelationshipRecord>()
            };

            // 모든 Element 수집 (ViewSpecific 제외)
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            int totalCount = collector.GetElementCount();
            int currentCount = 0;

            LoggingService.LogInfo($"총 {totalCount}개 객체 추출 시작", "DXrevit");

            foreach (Element element in collector)
            {
                currentCount++;

                // 진행률 보고 (10%마다)
                if (currentCount % Math.Max(totalCount / 10, 1) == 0)
                {
                    LoggingService.LogInfo($"진행률: {currentCount}/{totalCount}", "DXrevit");
                }

                // 객체 데이터 추출
                var objectRecord = ExtractObjectData(element, modelVersion);
                if (objectRecord != null)
                {
                    extractedData.Objects.Add(objectRecord);
                }

                // 관계 데이터 추출
                var relationships = ExtractRelationships(element, modelVersion);
                extractedData.Relationships.AddRange(relationships);
            }

            extractedData.Metadata.TotalObjectCount = extractedData.Objects.Count;

            LoggingService.LogInfo($"데이터 추출 완료: {extractedData.Objects.Count}개 객체, {extractedData.Relationships.Count}개 관계", "DXrevit");

            return extractedData;
        }

        /// <summary>
        /// 메타데이터 추출
        /// </summary>
        private MetadataRecord ExtractMetadata(string modelVersion, string createdBy, string description)
        {
            return new MetadataRecord
            {
                ModelVersion = modelVersion,
                Timestamp = DateTime.UtcNow,
                ProjectName = _document.ProjectInformation.Name ?? "Unknown",
                CreatedBy = createdBy,
                Description = description,
                RevitFilePath = _document.PathName,
                TotalObjectCount = 0 // 나중에 업데이트됨
            };
        }

        /// <summary>
        /// 객체 데이터 추출
        /// </summary>
        private ObjectRecord ExtractObjectData(Element element, string modelVersion)
        {
            try
            {
                // 카테고리 필터링 (불필요한 카테고리 제외)
                if (element.Category == null || !IsValidCategory(element.Category.Name))
                {
                    return null;
                }

                // InstanceGuid 가져오기 (없으면 경로 기반 해시 생성)
                string instanceGuid = element.UniqueId;
                string objectId = IdGenerator.GenerateObjectId(
                    instanceGuid,
                    element.Category.Name,
                    GetFamilyName(element),
                    GetTypeName(element));

                // ActivityId (공정 ID) 가져오기
                string activityId = GetParameterValue(element, "ActivityId");

                // 모든 매개변수를 JSON으로 변환
                var properties = ExtractProperties(element);
                string propertiesJson = JsonHelper.Serialize(properties);

                // 바운딩 박스 추출
                string boundingBoxJson = ExtractBoundingBox(element);

                return new ObjectRecord
                {
                    ModelVersion = modelVersion,
                    ObjectId = objectId,
                    ElementId = (int)element.Id.Value,
                    Category = element.Category.Name,
                    Family = GetFamilyName(element),
                    Type = GetTypeName(element),
                    ActivityId = activityId,
                    Properties = propertiesJson,
                    BoundingBox = boundingBoxJson
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"객체 추출 실패: ElementId={element.Id}", "DXrevit", ex);
                return null;
            }
        }

        /// <summary>
        /// 관계 데이터 추출 (호스트 관계)
        /// </summary>
        private List<RelationshipRecord> ExtractRelationships(Element element, string modelVersion)
        {
            var relationships = new List<RelationshipRecord>();

            try
            {
                // FamilyInstance인 경우 호스트 관계 확인
                if (element is FamilyInstance familyInstance && familyInstance.Host != null)
                {
                    string sourceId = IdGenerator.GenerateObjectId(
                        familyInstance.Host.UniqueId,
                        familyInstance.Host.Category?.Name ?? "Unknown",
                        GetFamilyName(familyInstance.Host),
                        GetTypeName(familyInstance.Host));

                    string targetId = IdGenerator.GenerateObjectId(
                        element.UniqueId,
                        element.Category.Name,
                        GetFamilyName(element),
                        GetTypeName(element));

                    relationships.Add(new RelationshipRecord
                    {
                        ModelVersion = modelVersion,
                        SourceObjectId = sourceId,
                        TargetObjectId = targetId,
                        RelationType = "HostedBy",
                        IsDirected = true
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"관계 추출 실패: ElementId={element.Id}", "DXrevit", ex);
            }

            return relationships;
        }

        /// <summary>
        /// 모든 매개변수 추출
        /// </summary>
        private Dictionary<string, object> ExtractProperties(Element element)
        {
            var properties = new Dictionary<string, object>();

            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    if (!param.HasValue)
                        continue;

                    string paramName = param.Definition.Name;
                    object paramValue = GetParameterValueAsObject(param);

                    if (paramValue != null)
                    {
                        properties[paramName] = paramValue;
                    }
                }
                catch
                {
                    // 매개변수 읽기 실패 무시
                }
            }

            return properties;
        }

        /// <summary>
        /// 바운딩 박스 추출
        /// </summary>
        private string ExtractBoundingBox(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    var bboxData = new
                    {
                        MinX = bbox.Min.X,
                        MinY = bbox.Min.Y,
                        MinZ = bbox.Min.Z,
                        MaxX = bbox.Max.X,
                        MaxY = bbox.Max.Y,
                        MaxZ = bbox.Max.Z
                    };
                    return JsonHelper.Serialize(bboxData);
                }
            }
            catch
            {
                // 바운딩 박스 없음
            }

            return null;
        }

        /// <summary>
        /// 유효한 카테고리 판별
        /// </summary>
        private bool IsValidCategory(string categoryName)
        {
            // 제외할 카테고리 목록
            var excludedCategories = new HashSet<string>
            {
                "Views",
                "Sheets",
                "Schedules",
                "Legends",
                "RVT Links",
                "Imports in Families"
            };

            return !excludedCategories.Contains(categoryName);
        }

        /// <summary>
        /// 패밀리 이름 가져오기
        /// </summary>
        private string GetFamilyName(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                return familyInstance.Symbol?.FamilyName ?? "Unknown";
            }
            return element.Name ?? "Unknown";
        }

        /// <summary>
        /// 타입 이름 가져오기
        /// </summary>
        private string GetTypeName(Element element)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                ElementType elementType = _document.GetElement(typeId) as ElementType;
                return elementType?.Name ?? "Unknown";
            }
            return "Unknown";
        }

        /// <summary>
        /// 매개변수 값 가져오기 (문자열)
        /// </summary>
        private string GetParameterValue(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                return param.AsValueString() ?? param.AsString();
            }
            return null;
        }

        /// <summary>
        /// 매개변수 값 가져오기 (객체)
        /// </summary>
        private object GetParameterValueAsObject(Parameter param)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();
                case StorageType.Integer:
                    return param.AsInteger();
                case StorageType.Double:
                    return param.AsDouble();
                case StorageType.ElementId:
                    return (int)param.AsElementId().Value;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 추출된 전체 데이터를 담는 컨테이너
    /// </summary>
    public class ExtractedData
    {
        public MetadataRecord Metadata { get; set; }
        public List<ObjectRecord> Objects { get; set; }
        public List<RelationshipRecord> Relationships { get; set; }
    }
}
