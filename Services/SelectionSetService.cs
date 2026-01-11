using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// Selection Set 자동 생성 서비스
    /// Phase 8: AWP 4D Automation - Selection Set Creation
    /// ADR-002 기반 구현
    /// </summary>
    public class SelectionSetService
    {
        private readonly NavisworksDataExtractor _extractor;

        /// <summary>
        /// 생성된 Selection Set 목록 (SetName → SelectionSet)
        /// </summary>
        private readonly Dictionary<string, SelectionSet> _createdSets;

        /// <summary>
        /// 생성된 폴더 목록 (FolderPath → FolderItem)
        /// </summary>
        private readonly Dictionary<string, FolderItem> _createdFolders;

        /// <summary>
        /// 진행 이벤트
        /// </summary>
        public event EventHandler<SelectionSetProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 상세 로깅
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        public SelectionSetService()
        {
            _extractor = new NavisworksDataExtractor();
            _createdSets = new Dictionary<string, SelectionSet>(StringComparer.OrdinalIgnoreCase);
            _createdFolders = new Dictionary<string, FolderItem>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public void ClearCache()
        {
            _createdSets.Clear();
            _createdFolders.Clear();
        }

        /// <summary>
        /// 스케줄 데이터 기반 계층적 Selection Set 생성
        /// </summary>
        /// <param name="schedules">매칭된 스케줄 데이터 목록</param>
        /// <param name="options">옵션</param>
        /// <returns>생성 결과</returns>
        public SelectionSetResult CreateHierarchicalSets(List<ScheduleData> schedules, AWP4DOptions options)
        {
            var result = new SelectionSetResult();
            var doc = Application.ActiveDocument;

            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            // 매칭된 스케줄만 필터링
            var matchedSchedules = schedules.Where(s =>
                s.MatchStatus == MatchStatus.Matched && s.MatchedObjectId.HasValue).ToList();

            if (matchedSchedules.Count == 0)
            {
                result.FailedSets.Add("매칭된 스케줄 데이터가 없습니다.");
                return result;
            }

            // 루트 폴더 생성/찾기
            var rootFolder = GetOrCreateRootFolder(doc, options.SelectionSetRootFolder);
            result.FolderCount++;

            // 그룹화 전략에 따라 스케줄 그룹화
            var groups = GroupSchedules(matchedSchedules, options.GroupingStrategy);

            int index = 0;
            foreach (var group in groups)
            {
                index++;

                try
                {
                    // 폴더 경로 생성
                    var targetFolder = CreateFolderPath(doc, rootFolder, group.Key, options);

                    // ModelItem 수집
                    var modelItems = new ModelItemCollection();
                    foreach (var schedule in group.Value)
                    {
                        var item = _extractor.FindModelItemById(schedule.MatchedObjectId.Value);
                        if (item != null)
                        {
                            modelItems.Add(item);
                            result.TotalItemCount++;
                        }
                    }

                    // 빈 세트 건너뛰기
                    if (modelItems.Count == 0 && options.SkipEmptySelectionSets)
                    {
                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine($"[SelectionSetService] '{group.Key}' 건너뛰기 (비어있음)");
                        continue;
                    }

                    // Selection Set 생성
                    string setName = GenerateSetName(group.Key, group.Value);
                    var selectionSet = CreateSelectionSet(doc, targetFolder, setName, modelItems);

                    if (selectionSet != null)
                    {
                        _createdSets[setName] = selectionSet;
                        result.SetCount++;
                        result.CreatedSets.Add(setName);

                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine(
                                $"[SelectionSetService] 생성: '{setName}' ({modelItems.Count}개 객체)");
                    }

                    OnProgressChanged(new SelectionSetProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = groups.Count,
                        SetName = setName,
                        ItemCount = modelItems.Count,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    result.FailedSets.Add($"{group.Key}: {ex.Message}");

                    OnProgressChanged(new SelectionSetProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = groups.Count,
                        SetName = group.Key,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            result.FolderCount = _createdFolders.Count + 1; // 루트 폴더 포함

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SelectionSetService] 완료: {result.SetCount}개 세트, {result.FolderCount}개 폴더");
            }

            return result;
        }

        /// <summary>
        /// 루트 폴더 생성/찾기
        /// </summary>
        private FolderItem GetOrCreateRootFolder(Document doc, string folderName)
        {
            var docSets = doc.SelectionSets;

            // 기존 폴더 찾기
            foreach (SavedItem item in docSets.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            // 새 폴더 생성 (AddCopy 패턴)
            var newFolder = new FolderItem();
            newFolder.DisplayName = folderName;
            docSets.AddCopy(newFolder);

            // 추가된 폴더 찾아 반환
            foreach (SavedItem item in docSets.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            throw new InvalidOperationException($"폴더 '{folderName}' 생성 실패");
        }

        /// <summary>
        /// 폴더 경로 생성 (예: "Zone-A/Level-1")
        /// </summary>
        private FolderItem CreateFolderPath(Document doc, FolderItem rootFolder, string path, AWP4DOptions options)
        {
            if (string.IsNullOrEmpty(path))
                return rootFolder;

            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            FolderItem currentFolder = rootFolder;

            foreach (var part in parts)
            {
                string folderPath = currentFolder == rootFolder
                    ? part
                    : $"{GetFolderPath(currentFolder)}/{part}";

                // 캐시 확인
                if (_createdFolders.TryGetValue(folderPath, out FolderItem cached))
                {
                    currentFolder = cached;
                    continue;
                }

                // 기존 하위 폴더 찾기
                FolderItem childFolder = null;
                foreach (SavedItem child in currentFolder.Children)
                {
                    if (child is FolderItem folder && folder.DisplayName == part)
                    {
                        childFolder = folder;
                        break;
                    }
                }

                if (childFolder == null)
                {
                    // 새 폴더 생성
                    childFolder = new FolderItem();
                    childFolder.DisplayName = part;

                    // InsertCopy로 추가
                    doc.SelectionSets.InsertCopy(currentFolder, currentFolder.Children.Count, childFolder);

                    // 추가된 폴더 찾기
                    foreach (SavedItem child in currentFolder.Children)
                    {
                        if (child is FolderItem folder && folder.DisplayName == part)
                        {
                            childFolder = folder;
                            break;
                        }
                    }
                }

                _createdFolders[folderPath] = childFolder;
                currentFolder = childFolder;
            }

            return currentFolder;
        }

        /// <summary>
        /// 폴더 경로 문자열 생성
        /// </summary>
        private string GetFolderPath(FolderItem folder)
        {
            // 간단히 DisplayName 반환 (실제로는 부모 추적 필요)
            return folder.DisplayName;
        }

        /// <summary>
        /// 스케줄 그룹화
        /// </summary>
        private Dictionary<string, List<ScheduleData>> GroupSchedules(List<ScheduleData> schedules, GroupingStrategy strategy)
        {
            switch (strategy)
            {
                case GroupingStrategy.ByParentSet:
                    return schedules
                        .GroupBy(s => s.ParentSet ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByZone:
                    return schedules
                        .GroupBy(s => ExtractZone(s.ParentSet) ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByZoneAndLevel:
                    return schedules
                        .GroupBy(s => ExtractZoneAndLevel(s.ParentSet) ?? "Ungrouped")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByTaskName:
                    return schedules
                        .GroupBy(s => s.TaskName ?? "Unnamed")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByStartWeek:
                    return schedules
                        .Where(s => s.PlannedStartDate.HasValue)
                        .GroupBy(s => GetWeekKey(s.PlannedStartDate.Value))
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.ByTaskType:
                    return schedules
                        .GroupBy(s => s.TaskType ?? "Construct")
                        .ToDictionary(g => g.Key, g => g.ToList());

                case GroupingStrategy.None:
                default:
                    // 개별 세트
                    return schedules.ToDictionary(
                        s => s.SyncID,
                        s => new List<ScheduleData> { s });
            }
        }

        /// <summary>
        /// Zone 추출 (예: "Zone-A/Level-1" → "Zone-A")
        /// </summary>
        private string ExtractZone(string parentSet)
        {
            if (string.IsNullOrEmpty(parentSet))
                return null;

            var parts = parentSet.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// Zone+Level 추출 (예: "Zone-A/Level-1/Structural" → "Zone-A/Level-1")
        /// </summary>
        private string ExtractZoneAndLevel(string parentSet)
        {
            if (string.IsNullOrEmpty(parentSet))
                return null;

            var parts = parentSet.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return $"{parts[0]}/{parts[1]}";
            return parts.Length > 0 ? parts[0] : null;
        }

        /// <summary>
        /// 주 단위 키 생성
        /// </summary>
        private string GetWeekKey(DateTime date)
        {
            var cal = System.Globalization.CultureInfo.CurrentCulture.Calendar;
            int week = cal.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return $"{date.Year}-W{week:D2}";
        }

        /// <summary>
        /// Selection Set 이름 생성
        /// </summary>
        private string GenerateSetName(string groupKey, List<ScheduleData> schedules)
        {
            // 마지막 경로 부분을 이름으로 사용
            var parts = groupKey.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            string baseName = parts.Length > 0 ? parts[parts.Length - 1] : groupKey;

            // 중복 방지
            if (_createdSets.ContainsKey(baseName))
            {
                baseName = $"{baseName}_{schedules.Count}items";
            }

            return baseName;
        }

        /// <summary>
        /// Selection Set 생성
        /// </summary>
        private SelectionSet CreateSelectionSet(Document doc, FolderItem targetFolder, string setName, ModelItemCollection items)
        {
            // SelectionSet 생성
            var selectionSet = new SelectionSet(items);
            selectionSet.DisplayName = setName;

            // 폴더에 추가 (InsertCopy 패턴)
            doc.SelectionSets.InsertCopy(targetFolder, targetFolder.Children.Count, selectionSet);

            // 추가된 SelectionSet 찾아 반환
            foreach (SavedItem child in targetFolder.Children)
            {
                if (child is SelectionSet set && set.DisplayName == setName)
                {
                    return set;
                }
            }

            return null;
        }

        /// <summary>
        /// 이름으로 Selection Set 찾기
        /// </summary>
        public SelectionSet FindSetByName(string setName)
        {
            if (_createdSets.TryGetValue(setName, out SelectionSet cached))
            {
                return cached;
            }

            var doc = Application.ActiveDocument;
            if (doc == null)
                return null;

            return FindSetInCollection(doc.SelectionSets.Value, setName);
        }

        /// <summary>
        /// 재귀적으로 Selection Set 찾기
        /// </summary>
        private SelectionSet FindSetInCollection(SavedItemCollection items, string name)
        {
            foreach (SavedItem item in items)
            {
                if (item is SelectionSet set && set.DisplayName == name)
                    return set;

                if (item is FolderItem folder)
                {
                    var found = FindSetInCollection(folder.Children, name);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }

        /// <summary>
        /// 생성된 모든 Selection Set 반환
        /// </summary>
        public IReadOnlyDictionary<string, SelectionSet> GetCreatedSets()
        {
            return _createdSets;
        }

        /// <summary>
        /// 특정 폴더의 모든 Selection Set 삭제
        /// </summary>
        public int ClearSelectionSets(string rootFolderName)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return 0;

            var docSets = doc.SelectionSets;
            int deletedCount = 0;

            // 루트 폴더 찾기
            FolderItem rootFolder = null;
            int rootIndex = -1;

            for (int i = 0; i < docSets.Value.Count; i++)
            {
                if (docSets.Value[i] is FolderItem folder && folder.DisplayName == rootFolderName)
                {
                    rootFolder = folder;
                    rootIndex = i;
                    break;
                }
            }

            if (rootFolder != null && rootIndex >= 0)
            {
                // 폴더 내 항목 수 카운트
                deletedCount = CountItemsInFolder(rootFolder);

                // 폴더 삭제
                docSets.Value.RemoveAt(rootIndex);

                _createdSets.Clear();
                _createdFolders.Clear();
            }

            return deletedCount;
        }

        /// <summary>
        /// 폴더 내 항목 수 카운트
        /// </summary>
        private int CountItemsInFolder(FolderItem folder)
        {
            int count = 0;
            foreach (SavedItem child in folder.Children)
            {
                if (child is FolderItem subFolder)
                    count += CountItemsInFolder(subFolder);
                else
                    count++;
            }
            return count;
        }

        protected virtual void OnProgressChanged(SelectionSetProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Selection Set 생성 진행 이벤트 인자
    /// </summary>
    public class SelectionSetProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string SetName { get; set; }
        public int ItemCount { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Progress => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;
    }
}
