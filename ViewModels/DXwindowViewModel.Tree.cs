using System;
using System.Collections.Generic;
using Autodesk.Navisworks.Api;
using DXTnavis.Helpers;
using DXTnavis.Models;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Tree Expand/Collapse 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Tree Expand/Collapse Methods

        /// <summary>
        /// 지정된 레벨까지 트리 확장
        /// </summary>
        private void ExpandTreeToLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandToLevel(targetLevel);
                }
                StatusMessage = $"Tree expanded to Level {targetLevel}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 축소
        /// </summary>
        private void CollapseAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.CollapseAll();
                }
                StatusMessage = "Tree collapsed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 확장
        /// </summary>
        private void ExpandAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandAll();
                }
                StatusMessage = "Tree fully expanded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 트리 확장/축소 명령의 CanExecute 갱신
        /// </summary>
        private void RefreshTreeCommands()
        {
            ((RelayCommand)ExpandToLevelCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)CollapseAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandLevelCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 특정 레벨까지 정확히 확장 (P1 Feature)
        /// 클릭한 레벨까지 확장하고, 그 이후 레벨은 축소
        /// </summary>
        /// <param name="targetLevel">확장할 레벨 (0부터 시작)</param>
        private void ExpandToSpecificLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandExactlyToLevel(targetLevel);
                }
                StatusMessage = $"Expanded to L{targetLevel} (children collapsed)";
                SelectedExpandLevel = targetLevel; // ComboBox도 동기화
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// v0.4.1: ModelItem에서 직접 TreeNodeModel 트리를 재귀적으로 구축
        /// 속성 유무와 관계없이 모든 노드를 포함하여 완전한 계층 구조 생성
        /// </summary>
        private TreeNodeModel BuildTreeFromModelItem(ModelItem item, int level, List<TreeNodeModel> allNodes)
        {
            if (item == null || item.IsHidden)
                return null;

            // 현재 노드 생성
            var node = new TreeNodeModel
            {
                ObjectId = item.InstanceGuid,
                DisplayName = GetDisplayNameFromModelItem(item),
                Level = level,
                HasGeometry = item.HasGeometry
            };

            allNodes.Add(node);

            // 재귀적으로 모든 자식 노드 추가 (속성 유무 무관)
            foreach (ModelItem child in item.Children)
            {
                var childNode = BuildTreeFromModelItem(child, level + 1, allNodes);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }

            return node;
        }

        /// <summary>
        /// ModelItem에서 표시 이름 추출
        /// </summary>
        private string GetDisplayNameFromModelItem(ModelItem item)
        {
            try
            {
                // 먼저 DisplayName 속성 시도
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                // "Item" 카테고리의 "Name" 속성 찾기
                foreach (var category in item.PropertyCategories)
                {
                    if (category?.DisplayName == "Item")
                    {
                        try
                        {
                            var properties = category.Properties;
                            foreach (DataProperty property in properties)
                            {
                                if (property?.DisplayName == "Name")
                                {
                                    return property.Value?.ToString() ?? item.InstanceGuid.ToString();
                                }
                            }
                        }
                        catch { continue; }
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

        #endregion
    }
}
