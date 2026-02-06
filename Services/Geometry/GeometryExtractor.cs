using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Autodesk.Navisworks.Api;
using DXTnavis.Models.Geometry;
// Type alias to avoid ambiguity with Autodesk.Navisworks.Api.Point3D
using DXTPoint3D = DXTnavis.Models.Geometry.Point3D;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Navisworks ModelItem에서 기하학 정보(BoundingBox, Centroid)를 추출하는 서비스
    /// Phase 15: Geometry Export System
    /// </summary>
    public class GeometryExtractor
    {
        #region Events

        /// <summary>
        /// 진행률 변경 이벤트 (0-100%)
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// 상태 메시지 이벤트
        /// </summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// 단일 ModelItem에서 BoundingBox 추출
        /// </summary>
        /// <param name="item">Navisworks ModelItem</param>
        /// <param name="hierarchyPath">계층 경로 (Synthetic ID 생성용)</param>
        /// <returns>GeometryRecord (실패 시 null)</returns>
        public GeometryRecord ExtractBoundingBox(ModelItem item, string hierarchyPath = "")
        {
            if (item == null) return null;

            try
            {
                // Navisworks API: BoundingBox()는 월드 좌표계 반환
                var bbox = item.BoundingBox();

                // BoundingBox가 유효한지 확인
                if (bbox == null || bbox.IsEmpty)
                {
                    Debug.WriteLine($"[GeometryExtractor] Empty BoundingBox for item: {GetDisplayName(item)}");
                    return null;
                }

                // DXTnavis 모델로 변환 (Navisworks Point3D → DXTnavis Point3D)
                var min = new DXTPoint3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z);
                var max = new DXTPoint3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z);
                var bboxModel = new BBox3D(min, max);

                // 안정적인 ObjectId 생성
                var objectId = GetStableObjectId(item, hierarchyPath);

                // GeometryRecord 생성
                var record = new GeometryRecord(objectId, bboxModel)
                {
                    DisplayName = GetDisplayName(item),
                    Category = GetCategory(item)
                };

                return record;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeometryExtractor] Error extracting BBox: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 ModelItem에서 BoundingBox 일괄 추출
        /// </summary>
        /// <param name="items">ModelItem 컬렉션</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractAllBoundingBoxes(
            IEnumerable<ModelItem> items,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, GeometryRecord>();
            var itemList = new List<ModelItem>(items);
            int total = itemList.Count;
            int processed = 0;
            int successful = 0;

            OnStatusChanged($"BoundingBox 추출 시작: {total:N0}개 객체");
            var sw = Stopwatch.StartNew();

            foreach (var item in itemList)
            {
                // 취소 확인
                if (cancellationToken.IsCancellationRequested)
                {
                    OnStatusChanged("추출 취소됨");
                    break;
                }

                // 계층 경로 생성
                var hierarchyPath = BuildHierarchyPath(item);

                // BoundingBox 추출
                var record = ExtractBoundingBox(item, hierarchyPath);
                if (record != null && record.ObjectId != Guid.Empty)
                {
                    result[record.ObjectId] = record;
                    successful++;
                }

                processed++;

                // 100개마다 진행률 보고
                if (processed % 100 == 0 || processed == total)
                {
                    int percentage = (int)(100.0 * processed / total);
                    OnProgressChanged(percentage);
                    OnStatusChanged($"추출 중: {processed:N0}/{total:N0} ({successful:N0}개 성공)");
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged($"추출 완료: {successful:N0}개 BoundingBox ({sw.Elapsed.TotalSeconds:F1}초)");

            return result;
        }

        /// <summary>
        /// 문서의 전체 모델에서 BoundingBox 추출
        /// </summary>
        /// <param name="document">Navisworks Document</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractFromDocument(
            Document document,
            CancellationToken cancellationToken = default)
        {
            if (document?.Models == null || document.Models.Count == 0)
            {
                OnStatusChanged("모델이 없습니다.");
                return new Dictionary<Guid, GeometryRecord>();
            }

            // 모든 ModelItem 수집
            var allItems = new List<ModelItem>();
            CollectAllModelItems(document.Models.RootItems, allItems);

            OnStatusChanged($"총 {allItems.Count:N0}개 객체 발견");

            return ExtractAllBoundingBoxes(allItems, cancellationToken);
        }

        /// <summary>
        /// 선택된 ModelItem에서 BoundingBox 추출
        /// </summary>
        /// <param name="selection">선택된 ModelItem 컬렉션</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → GeometryRecord 딕셔너리</returns>
        public Dictionary<Guid, GeometryRecord> ExtractFromSelection(
            ModelItemCollection selection,
            CancellationToken cancellationToken = default)
        {
            if (selection == null || selection.Count == 0)
            {
                OnStatusChanged("선택된 객체가 없습니다.");
                return new Dictionary<Guid, GeometryRecord>();
            }

            return ExtractAllBoundingBoxes(selection, cancellationToken);
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// 모든 ModelItem을 재귀적으로 수집
        /// </summary>
        private void CollectAllModelItems(ModelItemEnumerableCollection rootItems, List<ModelItem> result)
        {
            foreach (var item in rootItems)
            {
                result.Add(item);

                if (item.Children != null && item.Children.Any())
                {
                    CollectAllModelItems(item.Children, result);
                }
            }
        }

        /// <summary>
        /// 계층 경로 문자열 생성
        /// </summary>
        private string BuildHierarchyPath(ModelItem item)
        {
            if (item == null) return string.Empty;

            var parts = new List<string>();
            var current = item;

            while (current != null)
            {
                var name = GetDisplayName(current);
                if (!string.IsNullOrEmpty(name))
                {
                    parts.Insert(0, name);
                }
                current = current.Parent;
            }

            return string.Join("/", parts);
        }

        /// <summary>
        /// ModelItem의 표시 이름 가져오기
        /// </summary>
        private string GetDisplayName(ModelItem item)
        {
            if (item == null) return string.Empty;

            try
            {
                // DisplayName 우선
                if (!string.IsNullOrEmpty(item.DisplayName))
                    return item.DisplayName;

                // ClassDisplayName 대체
                if (!string.IsNullOrEmpty(item.ClassDisplayName))
                    return item.ClassDisplayName;

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// ModelItem의 카테고리 가져오기
        /// </summary>
        private string GetCategory(ModelItem item)
        {
            if (item == null) return string.Empty;

            try
            {
                return item.ClassDisplayName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion

        #region Stable Object ID (v1.3.0 패턴 재사용)

        /// <summary>
        /// ModelItem에 대한 안정적인 고유 ID 생성
        /// NavisworksDataExtractor.GetStableObjectId() 로직 기반
        /// </summary>
        private Guid GetStableObjectId(ModelItem item, string hierarchyPath)
        {
            if (item == null)
                return Guid.Empty;

            // 1. InstanceGuid가 유효하면 사용
            if (item.InstanceGuid != Guid.Empty)
                return item.InstanceGuid;

            // 2. Item 카테고리의 GUID 속성 확인
            var itemGuid = GetItemPropertyGuid(item);
            if (itemGuid != Guid.Empty)
                return itemGuid;

            // 3. Authoring ID 확인 (Revit Element ID 등)
            var authoringId = GetAuthoringId(item);
            if (!string.IsNullOrEmpty(authoringId))
            {
                var sourceGuid = GetModelSourceGuid(item);
                return CreateDeterministicGuid(sourceGuid.ToString() + "|" + authoringId);
            }

            // 4. 계층 경로 기반 Synthetic ID (최종 폴백)
            var sourceGuid2 = GetModelSourceGuid(item);
            var pathKey = sourceGuid2.ToString() + "|" + hierarchyPath;
            return CreateDeterministicGuid(pathKey);
        }

        /// <summary>
        /// Item 카테고리에서 GUID 속성 추출
        /// </summary>
        private Guid GetItemPropertyGuid(ModelItem item)
        {
            try
            {
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    if (category.DisplayName == "Item" || category.Name == "LcOaNode")
                    {
                        foreach (DataProperty property in category.Properties)
                        {
                            if (property == null) continue;

                            var propName = property.DisplayName ?? property.Name ?? string.Empty;
                            if (propName == "GUID" || propName == "Guid" || propName == "guid")
                            {
                                var value = property.Value?.ToString();
                                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var guid))
                                {
                                    return guid;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return Guid.Empty;
        }

        /// <summary>
        /// Authoring 도구의 고유 ID 추출 (Revit Element ID, AutoCAD Handle 등)
        /// </summary>
        private string GetAuthoringId(ModelItem item)
        {
            try
            {
                var targetProps = new[] { "Element ID", "Id", "ElementId", "Handle", "Object Handle", "GlobalId", "IfcGlobalId" };

                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    foreach (DataProperty property in category.Properties)
                    {
                        if (property == null) continue;

                        var propName = property.DisplayName ?? property.Name ?? string.Empty;
                        foreach (var target in targetProps)
                        {
                            if (propName.Equals(target, StringComparison.OrdinalIgnoreCase))
                            {
                                var value = property.Value?.ToString();
                                if (!string.IsNullOrEmpty(value))
                                    return value;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 모델 파일의 SourceGuid 가져오기
        /// </summary>
        private Guid GetModelSourceGuid(ModelItem item)
        {
            try
            {
                var modelFile = item?.Model;
                if (modelFile != null)
                {
                    return modelFile.SourceGuid;
                }
            }
            catch { }

            return Guid.Empty;
        }

        /// <summary>
        /// 문자열에서 결정적 GUID 생성 (MD5 해시 기반)
        /// </summary>
        private Guid CreateDeterministicGuid(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                return new Guid(hash);
            }
        }

        #endregion

        #region Event Helpers

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine($"[GeometryExtractor] {message}");
        }

        #endregion
    }
}
