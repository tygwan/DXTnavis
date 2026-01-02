using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// Navisworks 3D 객체 선택 및 가시성 제어 서비스
    /// Phase 3: 3D Object Integration
    /// </summary>
    public class NavisworksSelectionService
    {
        private readonly NavisworksDataExtractor _extractor;

        public NavisworksSelectionService()
        {
            _extractor = new NavisworksDataExtractor();
        }

        /// <summary>
        /// 필터링된 레코드에 해당하는 객체들을 Navisworks에서 선택합니다.
        /// </summary>
        /// <param name="filteredRecords">필터링된 HierarchicalPropertyRecord 목록</param>
        /// <returns>선택된 객체 수</returns>
        public int SelectFilteredObjects(IEnumerable<HierarchicalPropertyRecord> filteredRecords)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return 0;

            // 고유 ObjectId 추출
            var uniqueObjectIds = filteredRecords
                .Select(r => r.ObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (uniqueObjectIds.Count == 0)
                return 0;

            // ModelItem 찾기
            var modelItems = new ModelItemCollection();
            foreach (var objectId in uniqueObjectIds)
            {
                var modelItem = _extractor.FindModelItemById(objectId);
                if (modelItem != null)
                {
                    modelItems.Add(modelItem);
                }
            }

            if (modelItems.Count == 0)
                return 0;

            // Navisworks 선택 설정
            doc.CurrentSelection.Clear();
            doc.CurrentSelection.CopyFrom(modelItems);

            return modelItems.Count;
        }

        /// <summary>
        /// 체크박스로 선택된 레코드에 해당하는 객체들을 Navisworks에서 선택합니다.
        /// </summary>
        /// <param name="filteredRecords">필터링된 레코드 목록 (IsSelected가 true인 항목만 처리)</param>
        /// <returns>선택된 객체 수</returns>
        public int SelectCheckedObjects(IEnumerable<HierarchicalPropertyRecord> filteredRecords)
        {
            var checkedRecords = filteredRecords.Where(r => r.IsSelected);
            return SelectFilteredObjects(checkedRecords);
        }

        /// <summary>
        /// 필터링된 객체만 표시하고 나머지는 숨깁니다.
        /// </summary>
        /// <param name="filteredRecords">표시할 객체들의 레코드</param>
        /// <returns>표시된 객체 수</returns>
        public int ShowOnlyFilteredObjects(IEnumerable<HierarchicalPropertyRecord> filteredRecords)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return 0;

            // 고유 ObjectId 추출
            var uniqueObjectIds = filteredRecords
                .Select(r => r.ObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToHashSet();

            if (uniqueObjectIds.Count == 0)
                return 0;

            // 표시할 ModelItem 수집
            var itemsToShow = new ModelItemCollection();
            var allItems = new ModelItemCollection();

            foreach (var model in doc.Models)
            {
                CollectAllModelItems(model.RootItem, allItems);
            }

            foreach (var objectId in uniqueObjectIds)
            {
                var modelItem = _extractor.FindModelItemById(objectId);
                if (modelItem != null)
                {
                    itemsToShow.Add(modelItem);
                }
            }

            if (itemsToShow.Count == 0)
                return 0;

            // 숨김 선택 설정: 표시할 항목만 HiddenSelection에서 제외
            // Navisworks에서는 HiddenSelection에 추가된 항목이 숨겨짐
            doc.CurrentSelection.Clear();
            doc.CurrentSelection.CopyFrom(itemsToShow);

            // 선택 반전 후 숨기기 (선택되지 않은 것들을 숨김)
            // 또는 API를 사용하여 직접 가시성 제어
            try
            {
                // 모든 항목을 가져와서 필터링된 항목이 아닌 것들을 숨김 처리
                var itemsToHide = new ModelItemCollection();
                foreach (ModelItem item in allItems)
                {
                    if (!uniqueObjectIds.Contains(item.InstanceGuid))
                    {
                        itemsToHide.Add(item);
                    }
                }

                // HiddenSelection API 사용 (있는 경우)
                if (itemsToHide.Count > 0)
                {
                    // Navisworks 2025 API: HideItems
                    doc.Models.SetHidden(itemsToHide, true);
                }

                // 표시할 항목은 보이게 설정
                if (itemsToShow.Count > 0)
                {
                    doc.Models.SetHidden(itemsToShow, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"가시성 설정 중 오류: {ex.Message}");
                // 폴백: 선택만 설정하고 가시성은 사용자가 수동 조작
            }

            return itemsToShow.Count;
        }

        /// <summary>
        /// 체크박스로 선택된 객체만 표시하고 나머지는 숨깁니다.
        /// </summary>
        public int ShowOnlyCheckedObjects(IEnumerable<HierarchicalPropertyRecord> filteredRecords)
        {
            var checkedRecords = filteredRecords.Where(r => r.IsSelected);
            return ShowOnlyFilteredObjects(checkedRecords);
        }

        /// <summary>
        /// 모든 객체를 표시합니다 (숨김 해제).
        /// </summary>
        public void ShowAllObjects()
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return;

            try
            {
                var allItems = new ModelItemCollection();
                foreach (var model in doc.Models)
                {
                    CollectAllModelItems(model.RootItem, allItems);
                }

                if (allItems.Count > 0)
                {
                    doc.Models.SetHidden(allItems, false);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"모든 객체 표시 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 현재 선택을 해제합니다.
        /// </summary>
        public void ClearSelection()
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return;

            doc.CurrentSelection.Clear();
        }

        /// <summary>
        /// 선택된 객체로 카메라 줌 (Zoom to Selection)
        /// </summary>
        public void ZoomToSelection()
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return;

            try
            {
                // Navisworks API: Viewpoint 조작으로 선택 항목에 줌
                var viewpoint = doc.CurrentViewpoint.CreateCopy();

                if (doc.CurrentSelection.SelectedItems.Count > 0)
                {
                    // 선택된 항목의 BoundingBox로 줌
                    var boundingBox = doc.CurrentSelection.SelectedItems.BoundingBox();
                    if (boundingBox != null && boundingBox.IsEmpty == false)
                    {
                        // ZoomBox 또는 FitView 사용
                        viewpoint.ZoomBox(boundingBox);
                        doc.CurrentViewpoint.CopyFrom(viewpoint);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"줌 처리 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 필터링된 객체로 카메라 줌
        /// </summary>
        public void ZoomToFilteredObjects(IEnumerable<HierarchicalPropertyRecord> filteredRecords)
        {
            // 먼저 선택 설정
            SelectFilteredObjects(filteredRecords);
            // 그 다음 줌
            ZoomToSelection();
        }

        /// <summary>
        /// 모든 ModelItem을 재귀적으로 수집하는 헬퍼 메서드
        /// </summary>
        private void CollectAllModelItems(ModelItem currentItem, ModelItemCollection collection)
        {
            if (currentItem == null)
                return;

            // 현재 항목 추가 (형상이 있는 경우에만)
            if (currentItem.HasGeometry)
            {
                collection.Add(currentItem);
            }

            // 자식 항목 재귀 수집
            foreach (ModelItem child in currentItem.Children)
            {
                CollectAllModelItems(child, collection);
            }
        }

        /// <summary>
        /// 특정 ObjectId 목록에 해당하는 ModelItem들을 찾아 반환합니다.
        /// </summary>
        /// <param name="objectIds">찾을 ObjectId 목록</param>
        /// <returns>ModelItemCollection</returns>
        public ModelItemCollection FindModelItems(IEnumerable<Guid> objectIds)
        {
            var result = new ModelItemCollection();

            foreach (var objectId in objectIds)
            {
                if (objectId == Guid.Empty)
                    continue;

                var modelItem = _extractor.FindModelItemById(objectId);
                if (modelItem != null)
                {
                    result.Add(modelItem);
                }
            }

            return result;
        }

        /// <summary>
        /// 필터링된 레코드 중 고유 ObjectId 개수를 반환합니다.
        /// </summary>
        public int GetUniqueObjectCount(IEnumerable<HierarchicalPropertyRecord> records)
        {
            return records
                .Select(r => r.ObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Count();
        }

        /// <summary>
        /// 선택된(체크된) 레코드 중 고유 ObjectId 개수를 반환합니다.
        /// </summary>
        public int GetCheckedObjectCount(IEnumerable<HierarchicalPropertyRecord> records)
        {
            return records
                .Where(r => r.IsSelected)
                .Select(r => r.ObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .Count();
        }
    }
}
