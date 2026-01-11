using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// ComAPI를 사용한 Property Write 서비스
    /// Phase 8: AWP 4D Automation - Property Write
    /// ADR-001 기반 구현
    /// </summary>
    public class PropertyWriteService
    {
        /// <summary>
        /// Property Write 진행 이벤트
        /// </summary>
        public event EventHandler<PropertyWriteProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 재시도 횟수
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 재시도 간격 (ms)
        /// </summary>
        public int RetryDelayMs { get; set; } = 500;

        /// <summary>
        /// 상세 로깅 활성화
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// 단일 ModelItem에 스케줄 속성을 기입합니다.
        /// </summary>
        /// <param name="modelItem">대상 ModelItem</param>
        /// <param name="schedule">스케줄 데이터</param>
        /// <param name="options">옵션</param>
        /// <returns>성공 여부</returns>
        [HandleProcessCorruptedStateExceptions]
        public bool WriteScheduleProperty(ModelItem modelItem, ScheduleData schedule, AWP4DOptions options)
        {
            if (modelItem == null)
                throw new ArgumentNullException(nameof(modelItem));
            if (schedule == null)
                throw new ArgumentNullException(nameof(schedule));

            int attempt = 0;
            while (attempt < RetryCount)
            {
                attempt++;
                try
                {
                    return WritePropertyInternal(modelItem, schedule, options);
                }
                catch (COMException comEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PropertyWrite] COM 오류 (시도 {attempt}/{RetryCount}): {comEx.Message}");

                    if (attempt >= RetryCount)
                        throw;

                    System.Threading.Thread.Sleep(RetryDelayMs * attempt); // 지수 백오프
                }
                catch (AccessViolationException avEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[PropertyWrite] Access Violation (시도 {attempt}/{RetryCount}): {avEx.Message}");

                    if (attempt >= RetryCount)
                        throw;

                    System.Threading.Thread.Sleep(RetryDelayMs * attempt);
                }
            }

            return false;
        }

        /// <summary>
        /// 내부 Property Write 구현
        /// </summary>
        private bool WritePropertyInternal(ModelItem modelItem, ScheduleData schedule, AWP4DOptions options)
        {
            // COM API Bridge 획득
            InwOpState10 comState = ComApiBridge.State;
            if (comState == null)
                throw new InvalidOperationException("COM API에 접근할 수 없습니다.");

            // ModelItem을 COM Path로 변환
            InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
            if (comPath == null)
                throw new InvalidOperationException("ModelItem을 COM Path로 변환할 수 없습니다.");

            try
            {
                // Property Node 획득 (true = create if not exists)
                InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState.GetGUIPropertyNode(comPath, true);
                if (propNode == null)
                    throw new InvalidOperationException("Property Node를 생성할 수 없습니다.");

                // Property Vector 생성
                InwOaPropertyVec propVec = CreatePropertyVector(comState, schedule, options);

                // User Defined Property로 설정
                // index: 0 (새로 추가), displayName, internalName, properties
                propNode.SetUserDefined(
                    0,
                    options.PropertyCategoryName,
                    options.PropertyCategoryInternalName,
                    propVec
                );

                if (VerboseLogging)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PropertyWrite] 성공: {schedule.SyncID} → {modelItem.DisplayName}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PropertyWrite] 실패: {schedule.SyncID} - {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 스케줄 데이터로 Property Vector 생성
        /// </summary>
        private InwOaPropertyVec CreatePropertyVector(InwOpState10 comState, ScheduleData schedule, AWP4DOptions options)
        {
            InwOaPropertyVec propVec = (InwOaPropertyVec)comState.ObjectFactory(
                nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

            // SyncID
            AddProperty(comState, propVec, "SyncID", "SyncID_Internal", schedule.SyncID);

            // TaskName
            if (!string.IsNullOrEmpty(schedule.TaskName))
                AddProperty(comState, propVec, "작업명", "TaskName_Internal", schedule.TaskName);

            // PlannedStartDate
            if (schedule.PlannedStartDate.HasValue)
                AddProperty(comState, propVec, "계획시작일", "PlannedStartDate_Internal",
                    schedule.PlannedStartDate.Value.ToString("yyyy-MM-dd"));

            // PlannedEndDate
            if (schedule.PlannedEndDate.HasValue)
                AddProperty(comState, propVec, "계획종료일", "PlannedEndDate_Internal",
                    schedule.PlannedEndDate.Value.ToString("yyyy-MM-dd"));

            // Duration
            if (schedule.Duration > 0)
                AddProperty(comState, propVec, "기간(일)", "Duration_Internal", schedule.Duration);

            // ActualStartDate
            if (schedule.ActualStartDate.HasValue)
                AddProperty(comState, propVec, "실제시작일", "ActualStartDate_Internal",
                    schedule.ActualStartDate.Value.ToString("yyyy-MM-dd"));

            // ActualEndDate
            if (schedule.ActualEndDate.HasValue)
                AddProperty(comState, propVec, "실제종료일", "ActualEndDate_Internal",
                    schedule.ActualEndDate.Value.ToString("yyyy-MM-dd"));

            // Cost
            if (schedule.Cost.HasValue)
                AddProperty(comState, propVec, "비용", "Cost_Internal", schedule.Cost.Value);

            // Progress
            if (schedule.Progress > 0)
                AddProperty(comState, propVec, "진행률", "Progress_Internal", schedule.Progress);

            // TaskType
            if (!string.IsNullOrEmpty(schedule.TaskType))
                AddProperty(comState, propVec, "작업유형", "TaskType_Internal", schedule.TaskType);

            // SetLevel
            if (!string.IsNullOrEmpty(schedule.SetLevel))
                AddProperty(comState, propVec, "그룹레벨", "SetLevel_Internal", schedule.SetLevel);

            // ParentSet
            if (!string.IsNullOrEmpty(schedule.ParentSet))
                AddProperty(comState, propVec, "상위그룹", "ParentSet_Internal", schedule.ParentSet);

            // Custom Properties
            foreach (var kvp in schedule.CustomProperties)
            {
                AddProperty(comState, propVec, kvp.Key, $"Custom_{kvp.Key}_Internal", kvp.Value);
            }

            return propVec;
        }

        /// <summary>
        /// Property Vector에 속성 추가 (문자열)
        /// </summary>
        private void AddProperty(InwOpState10 comState, InwOaPropertyVec propVec,
            string displayName, string internalName, string value)
        {
            InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
                nwEObjectType.eObjectType_nwOaProperty, null, null);

            prop.name = internalName;
            prop.UserName = displayName;
            prop.value = value ?? string.Empty;

            propVec.Properties().Add(prop);
        }

        /// <summary>
        /// Property Vector에 속성 추가 (정수)
        /// </summary>
        private void AddProperty(InwOpState10 comState, InwOaPropertyVec propVec,
            string displayName, string internalName, int value)
        {
            InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
                nwEObjectType.eObjectType_nwOaProperty, null, null);

            prop.name = internalName;
            prop.UserName = displayName;
            prop.value = value;

            propVec.Properties().Add(prop);
        }

        /// <summary>
        /// Property Vector에 속성 추가 (실수)
        /// </summary>
        private void AddProperty(InwOpState10 comState, InwOaPropertyVec propVec,
            string displayName, string internalName, double value)
        {
            InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
                nwEObjectType.eObjectType_nwOaProperty, null, null);

            prop.name = internalName;
            prop.UserName = displayName;
            prop.value = value;

            propVec.Properties().Add(prop);
        }

        /// <summary>
        /// Property Vector에 속성 추가 (decimal)
        /// </summary>
        private void AddProperty(InwOpState10 comState, InwOaPropertyVec propVec,
            string displayName, string internalName, decimal value)
        {
            InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
                nwEObjectType.eObjectType_nwOaProperty, null, null);

            prop.name = internalName;
            prop.UserName = displayName;
            prop.value = (double)value; // COM API는 decimal을 지원하지 않음

            propVec.Properties().Add(prop);
        }

        /// <summary>
        /// 다수의 ModelItem에 스케줄 속성을 일괄 기입합니다.
        /// </summary>
        /// <param name="schedules">스케줄 데이터 목록 (MatchedObjectId가 설정되어 있어야 함)</param>
        /// <param name="options">옵션</param>
        /// <returns>Property Write 결과</returns>
        public PropertyWriteResult WriteBatch(IEnumerable<ScheduleData> schedules, AWP4DOptions options)
        {
            var result = new PropertyWriteResult();
            var doc = Application.ActiveDocument;

            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            var extractor = new NavisworksDataExtractor();
            int index = 0;

            foreach (var schedule in schedules)
            {
                result.TotalCount++;
                index++;

                // 매칭된 ObjectId가 없으면 건너뛰기
                if (!schedule.MatchedObjectId.HasValue || schedule.MatchStatus != MatchStatus.Matched)
                {
                    result.SkippedCount++;
                    continue;
                }

                try
                {
                    // ModelItem 찾기
                    var modelItem = extractor.FindModelItemById(schedule.MatchedObjectId.Value);
                    if (modelItem == null)
                    {
                        result.FailedCount++;
                        result.FailedItems.Add($"{schedule.SyncID}: ModelItem을 찾을 수 없음");
                        continue;
                    }

                    // Property Write
                    if (WriteScheduleProperty(modelItem, schedule, options))
                    {
                        result.SuccessCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.FailedItems.Add($"{schedule.SyncID}: Write 실패");
                    }

                    // 진행 이벤트
                    OnProgressChanged(new PropertyWriteProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = result.TotalCount,
                        CurrentSyncId = schedule.SyncID,
                        Success = true,
                        Progress = (double)index / result.TotalCount * 100
                    });
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.FailedItems.Add($"{schedule.SyncID}: {ex.Message}");

                    if (!options.ContinueOnError)
                        throw;

                    OnProgressChanged(new PropertyWriteProgressEventArgs
                    {
                        CurrentIndex = index,
                        TotalCount = result.TotalCount,
                        CurrentSyncId = schedule.SyncID,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// 기존 Custom Property 삭제
        /// Note: SetUserDefined가 동일 카테고리명에 대해 자동으로 업데이트하므로
        /// 명시적 삭제 기능은 향후 필요시 구현
        /// </summary>
        /// <param name="modelItem">대상 ModelItem</param>
        /// <param name="categoryInternalName">삭제할 카테고리 내부 이름</param>
        public bool RemoveCustomProperty(ModelItem modelItem, string categoryInternalName)
        {
            if (modelItem == null || string.IsNullOrEmpty(categoryInternalName))
                return false;

            // SetUserDefined는 기존 동일 카테고리를 자동으로 덮어씁니다.
            // 명시적 삭제가 필요한 경우 향후 구현
            System.Diagnostics.Debug.WriteLine(
                $"[PropertyWrite] RemoveCustomProperty: 기능 비활성화 (자동 덮어쓰기 사용)");
            return true;
        }

        protected virtual void OnProgressChanged(PropertyWriteProgressEventArgs e)
        {
            ProgressChanged?.Invoke(this, e);
        }
    }

    /// <summary>
    /// Property Write 진행 이벤트 인자
    /// </summary>
    public class PropertyWriteProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string CurrentSyncId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Progress { get; set; }
    }
}
