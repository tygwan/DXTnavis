using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using DXTnavis.ViewModels;

namespace DXTnavis.Services
{
    /// <summary>
    /// Navisworks 모델에서 계층 구조 데이터를 추출하는 서비스
    /// 재귀적 트리 순회를 통해 부모-자식 관계를 유지하며 데이터를 수집합니다.
    /// </summary>
    public class NavisworksDataExtractor
    {
        /// <summary>
        /// DisplayString 파서 인스턴스 (v0.4.2)
        /// </summary>
        private readonly DisplayStringParser _displayStringParser = new DisplayStringParser();
        /// <summary>
        /// 선택된 객체부터 시작하여 모든 하위 계층을 재귀적으로 탐색하고 속성을 추출합니다.
        /// </summary>
        /// <param name="currentItem">현재 처리 중인 ModelItem</param>
        /// <param name="parentId">부모 객체의 GUID (최상위 객체는 Guid.Empty)</param>
        /// <param name="level">계층 깊이 (0부터 시작)</param>
        /// <param name="results">결과를 저장할 리스트</param>
        /// <param name="parentPath">부모 경로 (예: "Project > Building")</param>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public void TraverseAndExtractProperties(
            ModelItem currentItem,
            Guid parentId,
            int level,
            List<HierarchicalPropertyRecord> results,
            string parentPath = "")
        {
            if (currentItem == null)
                return;

            // 필터링: 숨겨진 객체는 건너뛰지만, 형상이 없어도 속성이 있으면 포함
            if (currentItem.IsHidden)
            {
                // 자식은 탐색하지 않고 완전히 건너뜀
                return;
            }

            // 형상이 없고 속성도 없는 경우 확인
            bool hasProperties = false;
            foreach (var category in currentItem.PropertyCategories)
            {
                hasProperties = true;
                break;
            }

            // 2. 현재 객체의 표시 이름 추출 (경로 계산에 필요하므로 먼저 추출)
            string displayName = GetDisplayName(currentItem);

            // 3. 현재 경로 계산 (화살표 구분자 사용)
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? displayName
                : $"{parentPath} > {displayName}";

            if (!currentItem.HasGeometry && !hasProperties)
            {
                // 하지만 자식은 계속 탐색 (컨테이너 역할만 하는 객체일 수 있음)
                foreach (ModelItem child in currentItem.Children)
                {
                    TraverseAndExtractProperties(child, parentId, level + 1, results, currentPath);
                }
                return;
            }

            // 4. 현재 객체의 고유 ID 가져오기
            Guid currentId = currentItem.InstanceGuid;

            // PRD v8 Step 2: InstanceGuid 유효성 진단 로깅
            System.Diagnostics.Debug.WriteLine($"[ID 검증] Level={level}, ParentId={parentId}, CurrentId={currentId}, IsEmpty={currentId == Guid.Empty}");

            // 3. 현재 객체의 모든 속성 추출 및 리스트에 추가
            foreach (var category in currentItem.PropertyCategories)
            {
                if (category == null) continue;

                DataPropertyCollection properties = null;
                try
                {
                    // AccessViolationException 방지: Properties 접근을 try-catch로 보호
                    properties = category.Properties;
                }
                catch (System.AccessViolationException)
                {
                    // Navisworks API 내부 오류 - 이 카테고리는 건너뜀
                    System.Diagnostics.Debug.WriteLine($"AccessViolationException in category: {category.DisplayName}");
                    continue;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error accessing properties: {ex.Message}");
                    continue;
                }

                if (properties == null) continue;

                foreach (DataProperty property in properties)
                {
                    if (property == null) continue;

                    try
                    {
                        // 각 속성 값을 안전하게 추출
                        string categoryName = string.Empty;
                        string propertyName = string.Empty;
                        string propertyValue = string.Empty;
                        string readWriteStatus = "알 수 없음";

                        // v0.4.2: 파싱된 값 변수
                        string dataType = string.Empty;
                        string rawValue = string.Empty;
                        double? numericValue = null;
                        string unit = string.Empty;

                        try
                        {
                            categoryName = category.DisplayName ?? string.Empty;
                        }
                        catch (System.AccessViolationException)
                        {
                            categoryName = "Unknown Category";
                        }

                        try
                        {
                            propertyName = property.DisplayName ?? string.Empty;
                        }
                        catch (System.AccessViolationException)
                        {
                            propertyName = "Unknown Property";
                        }

                        try
                        {
                            // property.Value 접근이 가장 위험함
                            var value = property.Value;
                            if (value != null)
                            {
                                propertyValue = value.ToString();

                                // v0.4.2: DisplayString 파싱
                                var parsed = _displayStringParser.Parse(propertyValue);
                                dataType = parsed.DataType;
                                rawValue = parsed.RawValue;
                                numericValue = parsed.NumericValue;
                                unit = parsed.Unit;
                            }
                        }
                        catch (System.AccessViolationException)
                        {
                            propertyValue = "[Access Denied]";
                            System.Diagnostics.Debug.WriteLine($"AccessViolationException accessing property value: {propertyName}");
                        }
                        catch
                        {
                            propertyValue = "[Error]";
                        }

                        // PRD v7: 속성 권한 상태 추출
                        try
                        {
                            readWriteStatus = property.IsReadOnly ? "읽기 전용" : "쓰기 가능";
                        }
                        catch (System.AccessViolationException)
                        {
                            readWriteStatus = "알 수 없음";
                            System.Diagnostics.Debug.WriteLine($"AccessViolationException accessing IsReadOnly: {propertyName}");
                        }
                        catch
                        {
                            readWriteStatus = "알 수 없음";
                        }

                        var record = new HierarchicalPropertyRecord(
                            objectId: currentId,
                            parentId: parentId,
                            level: level,
                            displayName: displayName,
                            category: categoryName,
                            propertyName: propertyName,
                            propertyValue: propertyValue,
                            readWriteStatus: readWriteStatus,
                            sysPath: currentPath,
                            dataType: dataType,
                            rawValue: rawValue,
                            numericValue: numericValue,
                            unit: unit
                        );

                        results.Add(record);
                    }
                    catch (System.AccessViolationException ex)
                    {
                        // 전체 속성 처리 실패
                        System.Diagnostics.Debug.WriteLine($"AccessViolationException in property loop: {ex.Message}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // 기타 오류
                        System.Diagnostics.Debug.WriteLine($"Error processing property: {ex.Message}");
                        continue;
                    }
                }
            }

            // 5. 재귀 호출: 현재 객체의 모든 자식에 대해 이 메서드를 다시 호출
            // PRD v8 수정: currentId가 비어있으면 parentId를 전달 (계층 유지)
            Guid childParentId = (currentId == Guid.Empty) ? parentId : currentId;

            foreach (ModelItem child in currentItem.Children)
            {
                TraverseAndExtractProperties(child, childParentId, level + 1, results, currentPath);
            }
        }

        /// <summary>
        /// ModelItem으로부터 표시 이름을 추출하는 헬퍼 메서드
        /// </summary>
        private string GetDisplayName(ModelItem item)
        {
            try
            {
                // 먼저 DisplayName 속성 시도
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                // "Item" 카테고리의 "Name" 속성 찾기
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    if (category.DisplayName == "Item")
                    {
                        DataPropertyCollection properties = null;
                        try
                        {
                            // AccessViolationException 방지
                            properties = category.Properties;
                        }
                        catch (System.AccessViolationException)
                        {
                            continue;
                        }
                        catch
                        {
                            continue;
                        }

                        if (properties == null) continue;

                        foreach (DataProperty property in properties)
                        {
                            if (property == null) continue;

                            try
                            {
                                if (property.DisplayName == "Name")
                                {
                                    return property.Value?.ToString() ?? item.InstanceGuid.ToString();
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }

                // 이름을 찾지 못하면 GUID 사용
                return item.InstanceGuid.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// 여러 최상위 객체로부터 계층 구조 데이터를 일괄 추출합니다. (HierarchicalPropertyRecord 방식)
        /// </summary>
        /// <param name="selectedItems">사용자가 선택한 ModelItem 컬렉션</param>
        /// <returns>계층 구조 속성 레코드 리스트</returns>
        public List<HierarchicalPropertyRecord> ExtractHierarchicalRecordsFromSelection(ModelItemCollection selectedItems)
        {
            var results = new List<HierarchicalPropertyRecord>();

            if (selectedItems == null || selectedItems.Count == 0)
                return results;

            // 선택된 각 최상위 객체에 대해 재귀 순회 시작
            foreach (ModelItem selectedItem in selectedItems)
            {
                // 최상위 객체이므로 parentId는 Guid.Empty, level은 0
                TraverseAndExtractProperties(selectedItem, Guid.Empty, 0, results);
            }

            // PRD v8 Step 2: 추출 완료 후 통계 로깅
            var uniqueObjectIds = results.Select(r => r.ObjectId).Distinct().ToList();
            var emptyObjectIds = uniqueObjectIds.Count(id => id == Guid.Empty);
            var validObjectIds = uniqueObjectIds.Count - emptyObjectIds;
            var uniqueParentIds = results.Select(r => r.ParentId).Distinct().ToList();
            var emptyParentIds = uniqueParentIds.Count(id => id == Guid.Empty);
            var validParentIds = uniqueParentIds.Count - emptyParentIds;

            System.Diagnostics.Debug.WriteLine($"=== [ID 통계] ===");
            System.Diagnostics.Debug.WriteLine($"총 레코드 수: {results.Count}");
            System.Diagnostics.Debug.WriteLine($"고유 ObjectId 수: {uniqueObjectIds.Count} (유효={validObjectIds}, 빈값={emptyObjectIds})");
            System.Diagnostics.Debug.WriteLine($"고유 ParentId 수: {uniqueParentIds.Count} (유효={validParentIds}, 빈값={emptyParentIds})");
            System.Diagnostics.Debug.WriteLine($"ObjectId가 모두 빈값인가? {emptyObjectIds == uniqueObjectIds.Count}");
            System.Diagnostics.Debug.WriteLine($"ParentId가 모두 빈값인가? {emptyParentIds == uniqueParentIds.Count}");

            return results;
        }

        /// <summary>
        /// ModelItem으로부터 TreeView 바인딩용 HierarchyNodeViewModel 트리를 생성합니다.
        /// PRD v4 Phase 3 - 재귀적 변환 메서드
        /// </summary>
        /// <param name="rootItem">최상위 ModelItem</param>
        /// <returns>HierarchyNodeViewModel의 재귀적 리스트</returns>
        public List<HierarchyNodeViewModel> ExtractHierarchy(ModelItem rootItem)
        {
            var results = new List<HierarchyNodeViewModel>();

            if (rootItem == null)
                return results;

            // 재귀적으로 변환
            var rootNode = ConvertToHierarchyNode(rootItem, 0);
            if (rootNode != null)
            {
                results.Add(rootNode);
            }

            return results;
        }

        /// <summary>
        /// ModelItem을 HierarchyNodeViewModel으로 재귀적으로 변환하는 헬퍼 메서드
        /// </summary>
        private HierarchyNodeViewModel ConvertToHierarchyNode(ModelItem item, int level)
        {
            if (item == null || item.IsHidden)
                return null;

            var node = new HierarchyNodeViewModel
            {
                ObjectId = item.InstanceGuid,
                DisplayName = GetDisplayName(item),
                Level = level,
                HasGeometry = item.HasGeometry
            };

            // 자식 노드 재귀적 변환
            foreach (ModelItem child in item.Children)
            {
                var childNode = ConvertToHierarchyNode(child, level + 1);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }

            return node;
        }

        /// <summary>
        /// 선택된 여러 객체로부터 HierarchyNodeViewModel 리스트를 생성합니다.
        /// </summary>
        public List<HierarchyNodeViewModel> ExtractHierarchyFromSelection(ModelItemCollection selectedItems)
        {
            var results = new List<HierarchyNodeViewModel>();

            if (selectedItems == null || selectedItems.Count == 0)
                return results;

            foreach (ModelItem selectedItem in selectedItems)
            {
                var nodeList = ExtractHierarchy(selectedItem);
                results.AddRange(nodeList);
            }

            return results;
        }

        /// <summary>
        /// 단일 ModelItem으로부터 속성 리스트를 추출합니다.
        /// PRD v4 Phase 3 - 단일 객체 속성 추출 메서드
        /// </summary>
        /// <param name="item">속성을 추출할 ModelItem</param>
        /// <returns>PropertyItemViewModel 리스트</returns>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public List<PropertyItemViewModel> ExtractProperties(ModelItem item)
        {
            var results = new List<PropertyItemViewModel>();

            if (item == null)
                return results;

            try
            {
                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    DataPropertyCollection properties = null;
                    try
                    {
                        // AccessViolationException 방지: Properties 접근을 try-catch로 보호
                        properties = category.Properties;
                    }
                    catch (System.AccessViolationException)
                    {
                        System.Diagnostics.Debug.WriteLine($"AccessViolationException in ExtractProperties: {category.DisplayName}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing properties in ExtractProperties: {ex.Message}");
                        continue;
                    }

                    if (properties == null) continue;

                    foreach (DataProperty property in properties)
                    {
                        if (property == null) continue;

                        try
                        {
                            var valueStr = property.Value?.ToString() ?? string.Empty;

                            // v0.4.2: DisplayString 파싱
                            var parsed = _displayStringParser.Parse(valueStr);

                            var propertyItem = new PropertyItemViewModel(
                                category: category.DisplayName ?? string.Empty,
                                name: property.DisplayName ?? string.Empty,
                                value: valueStr,
                                unit: parsed.Unit
                            );

                            results.Add(propertyItem);
                        }
                        catch
                        {
                            // 개별 속성 추출 실패는 건너뜀
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"속성 추출 중 오류: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// ObjectId로 ModelItem을 찾는 헬퍼 메서드 (전체 모델 순회)
        /// </summary>
        public ModelItem FindModelItemById(Guid objectId)
        {
            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
                return null;

            foreach (var model in doc.Models)
            {
                var found = FindModelItemByIdRecursive(model.RootItem, objectId);
                if (found != null)
                    return found;
            }

            return null;
        }

        private ModelItem FindModelItemByIdRecursive(ModelItem currentItem, Guid targetId)
        {
            if (currentItem == null)
                return null;

            if (currentItem.InstanceGuid == targetId)
                return currentItem;

            foreach (ModelItem child in currentItem.Children)
            {
                var found = FindModelItemByIdRecursive(child, targetId);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 선택된 객체들의 속성을 PropertyInfo 리스트로 추출 (Selection × Properties)
        /// </summary>
        /// <param name="selectedItems">선택된 ModelItem 컬렉션</param>
        /// <returns>PropertyInfo 리스트</returns>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public List<PropertyInfo> ExtractPropertiesFromSelection(ModelItemCollection selectedItems)
        {
            var results = new List<PropertyInfo>();

            if (selectedItems == null || selectedItems.Count == 0)
                return results;

            int objectIndex = 0;
            foreach (ModelItem item in selectedItems)
            {
                if (item == null) continue;

                objectIndex++;
                string objectName = GetDisplayName(item);

                try
                {
                    foreach (var category in item.PropertyCategories)
                    {
                        if (category == null) continue;

                        DataPropertyCollection properties = null;
                        try
                        {
                            properties = category.Properties;
                        }
                        catch (System.AccessViolationException)
                        {
                            continue;
                        }
                        catch
                        {
                            continue;
                        }

                        if (properties == null) continue;

                        foreach (DataProperty property in properties)
                        {
                            if (property == null) continue;

                            try
                            {
                                results.Add(new PropertyInfo
                                {
                                    Category = category.DisplayName ?? string.Empty,
                                    Name = property.DisplayName ?? string.Empty,
                                    Value = property.Value?.ToString() ?? string.Empty,
                                    ObjectId = objectIndex,
                                    ObjectName = objectName
                                });
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Selection Properties 추출 중 오류: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// 전체 모델의 계층 구조를 HierarchicalPropertyRecord 리스트로 추출 (All × Hierarchy)
        /// </summary>
        /// <returns>HierarchicalPropertyRecord 리스트</returns>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        public List<HierarchicalPropertyRecord> ExtractAllHierarchicalRecords()
        {
            var results = new List<HierarchicalPropertyRecord>();

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null)
                return results;

            // 모든 모델의 루트 아이템부터 순회
            foreach (var model in doc.Models)
            {
                if (model?.RootItem == null) continue;

                // 각 모델의 루트에서부터 재귀 순회
                TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, results);
            }

            // 통계 로깅
            var uniqueObjectIds = results.Select(r => r.ObjectId).Distinct().Count();
            System.Diagnostics.Debug.WriteLine($"=== [All Hierarchy 통계] ===");
            System.Diagnostics.Debug.WriteLine($"총 레코드 수: {results.Count}");
            System.Diagnostics.Debug.WriteLine($"고유 객체 수: {uniqueObjectIds}");

            return results;
        }
    }
}
