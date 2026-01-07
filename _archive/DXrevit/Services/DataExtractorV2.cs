using Autodesk.Revit.DB;
using DXBase.Services;
using DXBase.Utils;
using DXrevit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXrevit.Services
{
    /// <summary>
    /// Revit 데이터 추출 서비스 v2.0 - 새 스키마 대응
    /// </summary>
    public class DataExtractorV2
    {
        private readonly Document _document;
        private IProgressReporter _progressReporter;

        public DataExtractorV2(Document document)
        {
            _document = document;
        }

        /// <summary>
        /// 진행률 보고자 설정
        /// </summary>
        public void SetProgressReporter(IProgressReporter progressReporter)
        {
            _progressReporter = progressReporter;
        }

        /// <summary>
        /// 모든 객체 데이터 추출 (v2.0 형식)
        /// </summary>
        public List<ObjectData> ExtractAllObjects()
        {
            LoggingService.LogInfo("데이터 추출 시작 (v2.0)", "DXrevit");

            var objects = new List<ObjectData>();

            // 모든 Element 수집 (ViewSpecific 제외)
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            int totalCount = collector.GetElementCount();
            int currentCount = 0;

            LoggingService.LogInfo($"총 {totalCount}개 객체 추출 시작", "DXrevit");
            _progressReporter?.ReportProgress(15, $"객체 추출 중 (0/{totalCount})");

            foreach (Element element in collector)
            {
                currentCount++;

                // 진행률 보고 (10%마다)
                if (currentCount % Math.Max(totalCount / 10, 1) == 0)
                {
                    int progress = 15 + (int)((currentCount / (double)totalCount) * 60); // 15% ~ 75%
                    _progressReporter?.ReportProgress(progress, $"객체 추출 중 ({currentCount}/{totalCount})");
                }

                // 객체 데이터 추출
                var objectData = ExtractObjectData(element);
                if (objectData != null)
                {
                    objects.Add(objectData);
                }
            }

            _progressReporter?.ReportProgress(75, $"데이터 추출 완료: {objects.Count}개 객체");

            LoggingService.LogInfo($"데이터 추출 완료: {objects.Count}개 객체", "DXrevit");

            return objects;
        }

        /// <summary>
        /// 단일 객체 데이터 추출 (v2.0 형식)
        /// </summary>
        private ObjectData ExtractObjectData(Element element)
        {
            try
            {
                // 카테고리 필터링
                if (element.Category == null || !IsValidCategory(element.Category.Name))
                {
                    return null;
                }

                // Revit UniqueId 사용
                string objectId = element.UniqueId;

                // Element ID
                int elementId = (int)element.Id.Value;

                // 표시 이름
                string displayName = element.Name ?? GetFamilyName(element);

                // 카테고리, 패밀리, 타입
                string category = element.Category.Name;
                string family = GetFamilyName(element);
                string type = GetTypeName(element);

                // Activity ID (4D 시뮬레이션용)
                string activityId = GetParameterValue(element, "ActivityId");

                // 모든 매개변수 추출
                var properties = ExtractProperties(element);

                // Bounding Box 추출
                var boundingBox = ExtractBoundingBox(element);

                return new ObjectData
                {
                    object_id = objectId,
                    element_id = elementId,
                    display_name = displayName,
                    category = category,
                    family = family,
                    type = type,
                    activity_id = activityId,
                    properties = properties,
                    bounding_box = boundingBox
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"객체 추출 실패: ElementId={element.Id}", "DXrevit", ex);
                return null;
            }
        }

        /// <summary>
        /// 모든 매개변수 추출
        /// </summary>
        private Dictionary<string, object> ExtractProperties(Element element)
        {
            var properties = new Dictionary<string, object>();

            // 기본 정보
            properties["RevitUniqueId"] = element.UniqueId;
            properties["ElementId"] = (int)element.Id.Value;
            properties["Name"] = element.Name;

            // 모든 매개변수
            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    if (!param.HasValue)
                        continue;

                    string paramName = param.Definition.Name;
                    object paramValue = GetParameterValueAsObject(param);

                    if (paramValue != null && !properties.ContainsKey(paramName))
                    {
                        properties[paramName] = paramValue;
                    }
                }
                catch
                {
                    // 매개변수 읽기 실패 무시
                }
            }

            // Level 정보 추가
            try
            {
                Parameter levelParam = element.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                if (levelParam != null && levelParam.HasValue)
                {
                    ElementId levelId = levelParam.AsElementId();
                    if (levelId != ElementId.InvalidElementId)
                    {
                        Level level = _document.GetElement(levelId) as Level;
                        if (level != null)
                        {
                            properties["LevelName"] = level.Name;
                            properties["LevelElevation"] = level.Elevation;
                        }
                    }
                }
            }
            catch { }

            // Workset 정보 추가
            try
            {
                if (_document.IsWorkshared)
                {
                    WorksetId worksetId = element.WorksetId;
                    if (worksetId != WorksetId.InvalidWorksetId)
                    {
                        Workset workset = _document.GetWorksetTable().GetWorkset(worksetId);
                        properties["WorksetName"] = workset.Name;
                    }
                }
            }
            catch { }

            return properties;
        }

        /// <summary>
        /// Bounding Box 추출 (v2.0 형식)
        /// </summary>
        private BoundingBoxData ExtractBoundingBox(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    return new BoundingBoxData
                    {
                        MinX = bbox.Min.X,
                        MinY = bbox.Min.Y,
                        MinZ = bbox.Min.Z,
                        MaxX = bbox.Max.X,
                        MaxY = bbox.Max.Y,
                        MaxZ = bbox.Max.Z
                    };
                }
            }
            catch
            {
                // Bounding Box 없음
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
                "Views", "Sheets", "Schedules", "Legends",
                "RVT Links", "Imports in Families", "Cameras",
                "Reference Points", "Reference Lines"
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
            try
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
                        ElementId elemId = param.AsElementId();
                        if (elemId != ElementId.InvalidElementId)
                        {
                            Element refElement = _document.GetElement(elemId);
                            if (refElement != null)
                            {
                                return new Dictionary<string, object>
                                {
                                    { "ElementId", (int)elemId.Value },
                                    { "Name", refElement.Name }
                                };
                            }
                        }
                        return (int)elemId.Value;
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
