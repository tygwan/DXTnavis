using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Timeliner;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// TimeLiner Task 자동 생성 서비스
    /// Phase 8: AWP 4D Automation - TimeLiner Integration
    /// ADR-002 기반 구현
    /// </summary>
    public class TimeLinerService
    {
        private readonly SelectionSetService _selectionSetService;

        /// <summary>
        /// 생성된 Task 목록 (TaskName → TimelinerTask)
        /// </summary>
        private readonly Dictionary<string, TimelinerTask> _createdTasks;

        /// <summary>
        /// 진행 이벤트
        /// </summary>
        public event EventHandler<TimeLinerProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 상세 로깅
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        public TimeLinerService(SelectionSetService selectionSetService = null)
        {
            _selectionSetService = selectionSetService ?? new SelectionSetService();
            _createdTasks = new Dictionary<string, TimelinerTask>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public void ClearCache()
        {
            _createdTasks.Clear();
        }

        /// <summary>
        /// 스케줄 데이터 기반 TimeLiner Task 생성
        /// </summary>
        /// <param name="schedules">매칭된 스케줄 데이터 목록</param>
        /// <param name="syncIdToSetName">SyncID → SelectionSet 이름 매핑</param>
        /// <param name="options">옵션</param>
        /// <returns>생성 결과</returns>
        public TimeLinerResult CreateTasks(
            List<ScheduleData> schedules,
            Dictionary<string, string> syncIdToSetName,
            AWP4DOptions options)
        {
            var result = new TimeLinerResult();
            var doc = Application.ActiveDocument;

            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            // TimeLiner 획득
            var timeliner = GetDocumentTimeliner(doc);
            if (timeliner == null)
                throw new InvalidOperationException("TimeLiner를 사용할 수 없습니다.");

            // 매칭된 스케줄만 필터링
            var validSchedules = schedules.Where(s =>
                s.MatchStatus == MatchStatus.Matched &&
                s.PlannedStartDate.HasValue).ToList();

            if (validSchedules.Count == 0)
            {
                result.FailedTasks.Add("유효한 스케줄 데이터가 없습니다.");
                return result;
            }

            // 그룹화된 Task 구조 생성
            if (options.CreateHierarchicalTasks)
            {
                CreateHierarchicalTasks(timeliner, validSchedules, syncIdToSetName, options, result);
            }
            else
            {
                CreateFlatTasks(timeliner, validSchedules, syncIdToSetName, options, result);
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TimeLinerService] 완료: {result.TaskCount}개 Task, {result.LinkedCount}개 연결됨");
            }

            return result;
        }

        /// <summary>
        /// DocumentTimeliner 획득
        /// </summary>
        private DocumentTimeliner GetDocumentTimeliner(Document doc)
        {
            try
            {
                var timeliner = doc.GetTimeliner();
                return timeliner as DocumentTimeliner;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TimeLinerService] TimeLiner 획득 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 계층적 Task 생성
        /// </summary>
        private void CreateHierarchicalTasks(
            DocumentTimeliner timeliner,
            List<ScheduleData> schedules,
            Dictionary<string, string> syncIdToSetName,
            AWP4DOptions options,
            TimeLinerResult result)
        {
            // ParentSet 기준으로 그룹화
            var groups = schedules
                .GroupBy(s => s.ParentSet ?? "")
                .OrderBy(g => g.Key)
                .ToList();

            // 루트 복사본 생성 (Read-Only 우회 패턴)
            var rootCopy = timeliner.TasksRoot.CreateCopy() as GroupItem;
            if (rootCopy == null)
                throw new InvalidOperationException("TasksRoot 복사 실패");

            // 루트 폴더 생성
            var rootFolder = CreateTaskFolder(rootCopy, options.TimeLinerRootFolder);
            result.FolderCount++;

            // 폴더 캐시
            var folderCache = new Dictionary<string, GroupItem>(StringComparer.OrdinalIgnoreCase);
            folderCache[""] = rootFolder;

            int index = 0;
            foreach (var group in groups)
            {
                // 그룹별 폴더 생성
                GroupItem targetFolder = rootFolder;
                if (!string.IsNullOrEmpty(group.Key))
                {
                    targetFolder = GetOrCreateTaskFolderPath(rootFolder, group.Key, folderCache);
                    result.FolderCount = folderCache.Count;
                }

                // 그룹 내 스케줄별 Task 생성
                foreach (var schedule in group)
                {
                    index++;

                    try
                    {
                        var task = CreateSingleTask(schedule, syncIdToSetName, options);
                        if (task != null)
                        {
                            targetFolder.Children.Add(task);
                            _createdTasks[schedule.SyncID] = task;
                            result.TaskCount++;
                            result.CreatedTasks.Add(schedule.TaskName ?? schedule.SyncID);

                            // Selection 연결 여부 확인
                            if (task.Selection.HasSelectionSources || task.Selection.HasExplicitSelection)
                            {
                                result.LinkedCount++;
                            }
                            else
                            {
                                result.UnlinkedCount++;
                            }
                        }

                        OnProgressChanged(new TimeLinerProgressEventArgs
                        {
                            CurrentIndex = index,
                            TotalCount = schedules.Count,
                            TaskName = schedule.TaskName ?? schedule.SyncID,
                            Success = true
                        });
                    }
                    catch (Exception ex)
                    {
                        result.FailedTasks.Add($"{schedule.SyncID}: {ex.Message}");

                        OnProgressChanged(new TimeLinerProgressEventArgs
                        {
                            CurrentIndex = index,
                            TotalCount = schedules.Count,
                            TaskName = schedule.TaskName ?? schedule.SyncID,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }
            }

            // TimeLiner에 적용 (TasksCopyFrom 패턴)
            timeliner.TasksCopyFrom(rootCopy.Children);
        }

        /// <summary>
        /// Flat Task 생성 (계층 없음)
        /// </summary>
        private void CreateFlatTasks(
            DocumentTimeliner timeliner,
            List<ScheduleData> schedules,
            Dictionary<string, string> syncIdToSetName,
            AWP4DOptions options,
            TimeLinerResult result)
        {
            var rootCopy = timeliner.TasksRoot.CreateCopy() as GroupItem;
            if (rootCopy == null)
                throw new InvalidOperationException("TasksRoot 복사 실패");

            // 루트 폴더 생성
            var rootFolder = CreateTaskFolder(rootCopy, options.TimeLinerRootFolder);
            result.FolderCount = 1;

            int index = 0;
            foreach (var schedule in schedules)
            {
                index++;

                try
                {
                    var task = CreateSingleTask(schedule, syncIdToSetName, options);
                    if (task != null)
                    {
                        rootFolder.Children.Add(task);
                        _createdTasks[schedule.SyncID] = task;
                        result.TaskCount++;
                        result.CreatedTasks.Add(schedule.TaskName ?? schedule.SyncID);

                        if (task.Selection.HasSelectionSources || task.Selection.HasExplicitSelection)
                            result.LinkedCount++;
                        else
                            result.UnlinkedCount++;
                    }

                    OnProgressChanged(new TimeLinerProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = schedules.Count,
                        TaskName = schedule.TaskName ?? schedule.SyncID,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    result.FailedTasks.Add($"{schedule.SyncID}: {ex.Message}");

                    OnProgressChanged(new TimeLinerProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = schedules.Count,
                        TaskName = schedule.TaskName ?? schedule.SyncID,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            timeliner.TasksCopyFrom(rootCopy.Children);
        }

        /// <summary>
        /// Task 폴더 생성
        /// </summary>
        private GroupItem CreateTaskFolder(GroupItem parent, string folderName)
        {
            var folder = new TimelinerTask();
            folder.DisplayName = folderName;
            parent.Children.Add(folder);

            // GroupItem으로 반환 (TimelinerTask는 GroupItem 상속)
            return folder;
        }

        /// <summary>
        /// 폴더 경로 생성
        /// </summary>
        private GroupItem GetOrCreateTaskFolderPath(
            GroupItem rootFolder,
            string path,
            Dictionary<string, GroupItem> cache)
        {
            if (cache.TryGetValue(path, out GroupItem cached))
                return cached;

            var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            GroupItem current = rootFolder;
            string currentPath = "";

            foreach (var part in parts)
            {
                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";

                if (cache.TryGetValue(currentPath, out GroupItem existing))
                {
                    current = existing;
                    continue;
                }

                // 새 폴더 생성
                var newFolder = new TimelinerTask();
                newFolder.DisplayName = part;
                current.Children.Add(newFolder);

                cache[currentPath] = newFolder;
                current = newFolder;
            }

            return current;
        }

        /// <summary>
        /// 단일 Task 생성
        /// </summary>
        private TimelinerTask CreateSingleTask(
            ScheduleData schedule,
            Dictionary<string, string> syncIdToSetName,
            AWP4DOptions options)
        {
            var task = new TimelinerTask();

            // 기본 정보 설정
            task.DisplayName = schedule.TaskName ?? schedule.SyncID;
            task.SynchronizationId = schedule.SyncID;

            // 날짜 설정
            if (schedule.PlannedStartDate.HasValue)
                task.PlannedStartDate = schedule.PlannedStartDate.Value;

            if (schedule.PlannedEndDate.HasValue)
                task.PlannedEndDate = schedule.PlannedEndDate.Value;

            if (schedule.ActualStartDate.HasValue)
                task.ActualStartDate = schedule.ActualStartDate.Value;

            if (schedule.ActualEndDate.HasValue)
                task.ActualEndDate = schedule.ActualEndDate.Value;

            // Task Type 설정
            task.SimulationTaskTypeName = ParseSimulationTaskType(schedule.TaskType ?? options.DefaultTaskType);

            // Selection 연결
            LinkSelectionToTask(task, schedule, syncIdToSetName, options);

            return task;
        }

        /// <summary>
        /// Selection을 Task에 연결
        /// </summary>
        private void LinkSelectionToTask(
            TimelinerTask task,
            ScheduleData schedule,
            Dictionary<string, string> syncIdToSetName,
            AWP4DOptions options)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return;

            try
            {
                switch (options.TaskSelectionMode)
                {
                    case TaskSelectionMode.SelectionSource:
                        // Selection Set을 통한 연결
                        if (syncIdToSetName.TryGetValue(schedule.SyncID, out string setName))
                        {
                            var selectionSet = _selectionSetService.FindSetByName(setName);
                            if (selectionSet != null)
                            {
                                LinkSelectionSet(task, selectionSet);
                            }
                        }
                        break;

                    case TaskSelectionMode.Explicit:
                        // ModelItem 직접 연결
                        if (schedule.MatchedObjectId.HasValue)
                        {
                            var extractor = new NavisworksDataExtractor();
                            var modelItem = extractor.FindModelItemById(schedule.MatchedObjectId.Value);
                            if (modelItem != null)
                            {
                                var items = new ModelItemCollection();
                                items.Add(modelItem);
                                task.Selection.CopyFrom(items);
                            }
                        }
                        break;

                    case TaskSelectionMode.Search:
                        // Search 조건 연결 (간단 구현)
                        // 향후 확장 가능
                        break;
                }
            }
            catch (Exception ex)
            {
                if (VerboseLogging)
                    System.Diagnostics.Debug.WriteLine(
                        $"[TimeLinerService] Selection 연결 실패 ({schedule.SyncID}): {ex.Message}");
            }
        }

        /// <summary>
        /// SelectionSet을 Task에 연결
        /// ExplicitModelItems를 사용하여 Task Selection에 직접 적용
        /// </summary>
        private void LinkSelectionSet(TimelinerTask task, SelectionSet selectionSet)
        {
            try
            {
                // SelectionSet의 ExplicitModelItems를 Task Selection에 적용
                var items = new ModelItemCollection();
                foreach (var item in selectionSet.ExplicitModelItems)
                {
                    items.Add(item);
                }

                if (items.Count > 0)
                {
                    task.Selection.CopyFrom(items);

                    if (VerboseLogging)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[TimeLinerService] Selection 연결 성공: {task.DisplayName} ← {selectionSet.DisplayName} ({items.Count} items)");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[TimeLinerService] Selection 연결 경고: {selectionSet.DisplayName}에 항목이 없습니다.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TimeLinerService] Selection 연결 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// SimulationTaskTypeName 파싱
        /// Phase 13: 한글 TaskType 지원 강화
        /// </summary>
        private string ParseSimulationTaskType(string taskType)
        {
            if (string.IsNullOrEmpty(taskType))
                return "Construct";

            // 정확한 매칭 먼저 시도 (Phase 13)
            var taskTypeLower = taskType.ToLowerInvariant();
            var taskTypeTrimmed = taskType.Trim();

            // 정확한 한글 매칭
            if (taskTypeTrimmed == "구성") return "Construct";
            if (taskTypeTrimmed == "철거") return "Demolish";
            if (taskTypeTrimmed == "임시") return "Temporary";

            // 정확한 영문 매칭
            if (taskTypeLower == "construct") return "Construct";
            if (taskTypeLower == "demolish") return "Demolish";
            if (taskTypeLower == "temporary") return "Temporary";

            // 키워드 기반 매칭 (하위 호환성)
            if (taskTypeLower.Contains("demolish") || taskTypeLower.Contains("remove") ||
                taskTypeTrimmed.Contains("철거") || taskTypeTrimmed.Contains("해체"))
                return "Demolish";

            if (taskTypeLower.Contains("temp") || taskTypeTrimmed.Contains("임시") || taskTypeTrimmed.Contains("가설"))
                return "Temporary";

            return "Construct";
        }

        /// <summary>
        /// 기존 AWP Task 삭제
        /// </summary>
        public int ClearTasks(string rootFolderName)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                return 0;

            var timeliner = GetDocumentTimeliner(doc);
            if (timeliner == null)
                return 0;

            int deletedCount = 0;

            try
            {
                var rootCopy = timeliner.TasksRoot.CreateCopy() as GroupItem;
                if (rootCopy == null)
                    return 0;

                // 루트 폴더 찾기 및 삭제
                for (int i = rootCopy.Children.Count - 1; i >= 0; i--)
                {
                    var child = rootCopy.Children[i];
                    if (child is TimelinerTask task && task.DisplayName == rootFolderName)
                    {
                        deletedCount = CountTasksInGroup(task);
                        rootCopy.Children.RemoveAt(i);
                        break;
                    }
                }

                if (deletedCount > 0)
                {
                    timeliner.TasksCopyFrom(rootCopy.Children);
                    _createdTasks.Clear();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TimeLinerService] Task 삭제 실패: {ex.Message}");
            }

            return deletedCount;
        }

        /// <summary>
        /// 그룹 내 Task 수 카운트
        /// </summary>
        private int CountTasksInGroup(GroupItem group)
        {
            int count = 0;
            foreach (var child in group.Children)
            {
                if (child is GroupItem subGroup)
                    count += CountTasksInGroup(subGroup);
                else
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 생성된 Task 목록 반환
        /// </summary>
        public IReadOnlyDictionary<string, TimelinerTask> GetCreatedTasks()
        {
            return _createdTasks;
        }

        #region TimeLiner Connection Status API

        /// <summary>
        /// TimeLiner 사용 가능 여부 확인
        /// </summary>
        public bool IsTimeLinerAvailable()
        {
            var doc = Application.ActiveDocument;
            if (doc == null) return false;

            var timeliner = GetDocumentTimeliner(doc);
            return timeliner != null;
        }

        /// <summary>
        /// 모든 TimeLiner Task 조회 (계층 구조 평탄화)
        /// </summary>
        public List<TimeLinerTaskInfo> GetAllTasksInfo()
        {
            var result = new List<TimeLinerTaskInfo>();
            var doc = Application.ActiveDocument;
            if (doc == null) return result;

            var timeliner = GetDocumentTimeliner(doc);
            if (timeliner == null) return result;

            CollectTasksRecursively(timeliner.TasksRoot, result, "", doc);
            return result;
        }

        /// <summary>
        /// 재귀적 Task 수집
        /// </summary>
        private void CollectTasksRecursively(GroupItem parent, List<TimeLinerTaskInfo> result, string parentPath, Document doc)
        {
            foreach (var child in parent.Children)
            {
                if (child is TimelinerTask task)
                {
                    var currentPath = string.IsNullOrEmpty(parentPath)
                        ? task.DisplayName
                        : $"{parentPath}/{task.DisplayName}";

                    // Task 정보 수집
                    var info = new TimeLinerTaskInfo
                    {
                        DisplayName = task.DisplayName,
                        SyncId = task.SynchronizationId,
                        FullPath = currentPath,
                        HasSelection = task.Selection.HasSelectionSources || task.Selection.HasExplicitSelection,
                        HasSelectionSources = task.Selection.HasSelectionSources,
                        HasExplicitSelection = task.Selection.HasExplicitSelection,
                        PlannedStart = task.PlannedStartDate,
                        PlannedEnd = task.PlannedEndDate,
                        TaskType = task.SimulationTaskTypeName
                    };

                    // 연결된 ModelItem 수 조회
                    try
                    {
                        var selectedItems = task.Selection.GetSelectedItems(doc);
                        info.LinkedItemCount = selectedItems?.Count ?? 0;
                    }
                    catch
                    {
                        info.LinkedItemCount = 0;
                    }

                    result.Add(info);

                    // 하위 Task 재귀 처리
                    if (task.Children.Count > 0)
                    {
                        CollectTasksRecursively(task, result, currentPath, doc);
                    }
                }
            }
        }

        /// <summary>
        /// 특정 SyncID로 Task 연결 상태 확인
        /// </summary>
        public TimeLinerTaskInfo GetTaskInfoBySyncId(string syncId)
        {
            if (string.IsNullOrEmpty(syncId)) return null;

            var allTasks = GetAllTasksInfo();
            return allTasks.FirstOrDefault(t =>
                !string.IsNullOrEmpty(t.SyncId) &&
                t.SyncId.Equals(syncId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 연결된 Task 수 조회
        /// </summary>
        public (int total, int linked, int unlinked) GetTaskConnectionSummary()
        {
            var allTasks = GetAllTasksInfo();
            int total = allTasks.Count;
            int linked = allTasks.Count(t => t.HasSelection);
            int unlinked = total - linked;
            return (total, linked, unlinked);
        }

        /// <summary>
        /// 특정 Task의 연결된 ModelItem 목록 조회
        /// </summary>
        public ModelItemCollection GetLinkedModelItems(string syncId)
        {
            var doc = Application.ActiveDocument;
            if (doc == null) return null;

            var timeliner = GetDocumentTimeliner(doc);
            if (timeliner == null) return null;

            var task = FindTaskBySyncId(timeliner.TasksRoot, syncId);
            if (task == null) return null;

            try
            {
                return task.Selection.GetSelectedItems(doc);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// SyncID로 Task 찾기 (재귀)
        /// </summary>
        private TimelinerTask FindTaskBySyncId(GroupItem parent, string syncId)
        {
            foreach (var child in parent.Children)
            {
                if (child is TimelinerTask task)
                {
                    if (!string.IsNullOrEmpty(task.SynchronizationId) &&
                        task.SynchronizationId.Equals(syncId, StringComparison.OrdinalIgnoreCase))
                    {
                        return task;
                    }

                    // 하위 Task 재귀 검색
                    var found = FindTaskBySyncId(task, syncId);
                    if (found != null) return found;
                }
            }
            return null;
        }

        #endregion

        protected virtual void OnProgressChanged(TimeLinerProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// TimeLiner 진행 이벤트 인자
    /// </summary>
    public class TimeLinerProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string TaskName { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Progress => TotalCount > 0 ? (double)CurrentIndex / TotalCount * 100 : 0;
    }

    /// <summary>
    /// TimeLiner Task 연결 상태 정보
    /// </summary>
    public class TimeLinerTaskInfo
    {
        /// <summary>
        /// Task 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 동기화 ID
        /// </summary>
        public string SyncId { get; set; }

        /// <summary>
        /// 전체 경로 (계층 구조)
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Selection 연결 여부 (HasSelectionSources || HasExplicitSelection)
        /// </summary>
        public bool HasSelection { get; set; }

        /// <summary>
        /// SelectionSource 연결 여부
        /// </summary>
        public bool HasSelectionSources { get; set; }

        /// <summary>
        /// 명시적 Selection 연결 여부
        /// </summary>
        public bool HasExplicitSelection { get; set; }

        /// <summary>
        /// 연결된 ModelItem 수
        /// </summary>
        public int LinkedItemCount { get; set; }

        /// <summary>
        /// 계획 시작일
        /// </summary>
        public DateTime? PlannedStart { get; set; }

        /// <summary>
        /// 계획 종료일
        /// </summary>
        public DateTime? PlannedEnd { get; set; }

        /// <summary>
        /// Task 유형 (Construct, Demolish, Temporary)
        /// </summary>
        public string TaskType { get; set; }

        /// <summary>
        /// 연결 상태 텍스트
        /// </summary>
        public string ConnectionStatus => HasSelection
            ? $"✓ 연결됨 ({LinkedItemCount} items)"
            : "✗ 미연결";

        /// <summary>
        /// 연결 상태 아이콘
        /// </summary>
        public string ConnectionIcon => HasSelection ? "✓" : "✗";
    }
}
