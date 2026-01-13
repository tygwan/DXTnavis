using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// Load Hierarchy 최적화 서비스 (Phase 10)
    /// - 비동기 로딩
    /// - 진행률 보고
    /// - 취소 지원
    /// - 단일 순회 최적화
    /// </summary>
    public class LoadHierarchyService
    {
        private readonly DisplayStringParser _displayStringParser;
        private const int PROGRESS_REPORT_INTERVAL = 50; // 50개마다 진행률 보고

        public LoadHierarchyService()
        {
            _displayStringParser = new DisplayStringParser();
        }

        /// <summary>
        /// 통합 추출 결과
        /// </summary>
        public class HierarchyExtractionResult
        {
            public List<TreeNodeModel> TreeNodes { get; set; } = new List<TreeNodeModel>();
            public List<HierarchicalPropertyRecord> Properties { get; set; } = new List<HierarchicalPropertyRecord>();
            public int TotalNodeCount { get; set; }
            public int TotalPropertyCount => Properties.Count;
            public bool IsCancelled { get; set; }
            public string ErrorMessage { get; set; }
        }

        /// <summary>
        /// 모델 전체 계층 구조를 비동기로 로드 (최적화된 단일 순회)
        /// </summary>
        /// <param name="progress">진행률 보고 인터페이스</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>추출 결과</returns>
        public async Task<HierarchyExtractionResult> LoadHierarchyAsync(
            IProgress<LoadProgress> progress,
            CancellationToken cancellationToken)
        {
            var result = new HierarchyExtractionResult();

            try
            {
                var doc = Application.ActiveDocument;

                if (doc == null)
                {
                    result.ErrorMessage = "활성 문서가 없습니다.";
                    return result;
                }

                // 1단계: 노드 수 카운트 (UI 스레드에서 동기 실행 - Navisworks API 제약)
                progress?.Report(new LoadProgress(0, 0, "Counting...", LoadPhase.Counting));

                // UI 응답성을 위해 yield
                await Task.Yield();

                int totalNodes = CountAllNodes(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    result.IsCancelled = true;
                    progress?.Report(new LoadProgress(0, totalNodes, "", LoadPhase.Cancelled));
                    return result;
                }

                progress?.Report(new LoadProgress(0, totalNodes, "Starting extraction...", LoadPhase.ExtractingTree));

                // UI 응답성을 위해 yield
                await Task.Yield();

                // 2단계: 데이터 추출 (단일 순회) - UI 스레드에서 실행 (Navisworks API 제약)
                int processed = 0;

                foreach (var model in doc.Models)
                {
                    if (model?.RootItem == null) continue;

                    cancellationToken.ThrowIfCancellationRequested();

                    var rootTreeNode = await TraverseUnifiedAsync(
                        model.RootItem,
                        Guid.Empty,
                        0,
                        string.Empty,
                        result,
                        processed,
                        totalNodes,
                        progress,
                        cancellationToken);

                    processed = rootTreeNode.Item2;

                    if (rootTreeNode.Item1 != null)
                    {
                        result.TreeNodes.Add(rootTreeNode.Item1);
                    }
                }

                result.TotalNodeCount = processed;

                // 3단계: 완료
                progress?.Report(new LoadProgress(processed, totalNodes, "Complete", LoadPhase.Complete));

                return result;
            }
            catch (OperationCanceledException)
            {
                result.IsCancelled = true;
                progress?.Report(new LoadProgress(0, 0, "", LoadPhase.Cancelled));
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[LoadHierarchyService] Error: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// 전체 노드 수 카운트 (빠른 순회)
        /// </summary>
        private int CountAllNodes(CancellationToken ct)
        {
            int count = 0;
            var doc = Application.ActiveDocument;

            if (doc == null) return 0;

            foreach (var model in doc.Models)
            {
                if (model?.RootItem == null) continue;
                count += CountNodesRecursive(model.RootItem, ct);
            }

            return count;
        }

        /// <summary>
        /// 재귀적 노드 카운트
        /// </summary>
        private int CountNodesRecursive(ModelItem item, CancellationToken ct)
        {
            if (item == null || item.IsHidden) return 0;
            ct.ThrowIfCancellationRequested();

            int count = 1;
            foreach (var child in item.Children)
            {
                count += CountNodesRecursive(child, ct);
            }
            return count;
        }

        /// <summary>
        /// 비동기 통합 순회 (TreeNodeModel + HierarchicalPropertyRecord 동시 생성)
        /// UI 스레드에서 실행하되, Task.Yield()로 UI 응답성 유지
        /// </summary>
        private async Task<Tuple<TreeNodeModel, int>> TraverseUnifiedAsync(
            ModelItem item,
            Guid parentId,
            int level,
            string parentPath,
            HierarchyExtractionResult result,
            int processed,
            int total,
            IProgress<LoadProgress> progress,
            CancellationToken ct)
        {
            if (item == null || item.IsHidden)
                return Tuple.Create<TreeNodeModel, int>(null, processed);

            ct.ThrowIfCancellationRequested();

            // 표시 이름 추출
            string displayName = GetDisplayName(item);

            // 현재 경로 계산
            string currentPath = string.IsNullOrEmpty(parentPath)
                ? displayName
                : $"{parentPath} > {displayName}";

            // 현재 객체 ID
            Guid currentId = item.InstanceGuid;

            // TreeNodeModel 생성
            var treeNode = new TreeNodeModel
            {
                ObjectId = currentId,
                DisplayName = displayName,
                Level = level,
                HasGeometry = item.HasGeometry
            };

            // HierarchicalPropertyRecord 추출 (속성이 있는 경우)
            ExtractProperties(item, currentId, parentId, level, displayName, currentPath, result);

            // 진행률 보고
            processed++;
            if (processed % PROGRESS_REPORT_INTERVAL == 0 || processed == total)
            {
                progress?.Report(new LoadProgress(processed, total, displayName, LoadPhase.ExtractingTree));
                // UI 응답성을 위해 yield (진행률 보고 시점에만)
                await Task.Yield();
            }

            // 자식 재귀 처리
            Guid childParentId = (currentId == Guid.Empty) ? parentId : currentId;

            foreach (ModelItem child in item.Children)
            {
                ct.ThrowIfCancellationRequested();

                var childResult = await TraverseUnifiedAsync(
                    child, childParentId, level + 1, currentPath,
                    result, processed, total, progress, ct);

                processed = childResult.Item2;

                if (childResult.Item1 != null)
                {
                    treeNode.Children.Add(childResult.Item1);
                }
            }

            return Tuple.Create(treeNode, processed);
        }

        /// <summary>
        /// ModelItem에서 속성 추출
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
        private void ExtractProperties(
            ModelItem item,
            Guid currentId,
            Guid parentId,
            int level,
            string displayName,
            string currentPath,
            HierarchyExtractionResult result)
        {
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
                            string categoryName = string.Empty;
                            string propertyName = string.Empty;
                            string propertyValue = string.Empty;
                            string readWriteStatus = "알 수 없음";
                            string dataType = string.Empty;
                            string rawValue = string.Empty;
                            double? numericValue = null;
                            string unit = string.Empty;

                            try { categoryName = category.DisplayName ?? string.Empty; }
                            catch (System.AccessViolationException) { categoryName = "Unknown Category"; }

                            try { propertyName = property.DisplayName ?? string.Empty; }
                            catch (System.AccessViolationException) { propertyName = "Unknown Property"; }

                            try
                            {
                                var value = property.Value;
                                if (value != null)
                                {
                                    propertyValue = value.ToString();
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
                            }
                            catch
                            {
                                propertyValue = "[Error]";
                            }

                            try
                            {
                                readWriteStatus = property.IsReadOnly ? "읽기 전용" : "쓰기 가능";
                            }
                            catch (System.AccessViolationException)
                            {
                                readWriteStatus = "알 수 없음";
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

                            result.Properties.Add(record);
                        }
                        catch (System.AccessViolationException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"AccessViolationException in property loop: {ex.Message}");
                            continue;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error processing property: {ex.Message}");
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ExtractProperties error: {ex.Message}");
            }
        }

        /// <summary>
        /// ModelItem에서 표시 이름 추출
        /// </summary>
        private string GetDisplayName(ModelItem item)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                foreach (var category in item.PropertyCategories)
                {
                    if (category == null) continue;

                    if (category.DisplayName == "Item")
                    {
                        DataPropertyCollection properties = null;
                        try
                        {
                            properties = category.Properties;
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

                return item.InstanceGuid.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
