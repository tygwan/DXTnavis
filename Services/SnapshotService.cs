using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// 3D Snapshot 캡처 및 ViewPoint 저장 서비스
    /// Phase 4: 3D Snapshot 워크플로우
    /// </summary>
    public class SnapshotService
    {
        private readonly NavisworksSelectionService _selectionService;
        private readonly NavisworksDataExtractor _extractor;

        /// <summary>
        /// 스냅샷 캡처 이벤트 (진행률 보고용)
        /// </summary>
        public event EventHandler<SnapshotProgressEventArgs> SnapshotProgress;

        /// <summary>
        /// 기본 저장 경로
        /// </summary>
        public string DefaultOutputPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        /// <summary>
        /// 이미지 형식 (기본: PNG)
        /// </summary>
        public ImageFormat ImageFormat { get; set; } = ImageFormat.Png;

        /// <summary>
        /// 이미지 너비 (기본: 1920)
        /// </summary>
        public int ImageWidth { get; set; } = 1920;

        /// <summary>
        /// 이미지 높이 (기본: 1080)
        /// </summary>
        public int ImageHeight { get; set; } = 1080;

        public SnapshotService()
        {
            _selectionService = new NavisworksSelectionService();
            _extractor = new NavisworksDataExtractor();
        }

        /// <summary>
        /// 현재 뷰포트를 이미지로 캡처합니다.
        /// COM API를 사용하여 Navisworks 뷰를 이미지로 내보냅니다.
        /// </summary>
        /// <param name="outputPath">저장 경로 (null이면 기본 경로 사용)</param>
        /// <param name="fileName">파일명 (확장자 제외)</param>
        /// <returns>저장된 파일 전체 경로</returns>
        public string CaptureCurrentView(string outputPath = null, string fileName = null)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            outputPath = outputPath ?? DefaultOutputPath;
            fileName = fileName ?? $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}";

            // 저장 폴더 확인/생성
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            string extension = GetExtensionForFormat(ImageFormat);
            string fullPath = Path.Combine(outputPath, $"{fileName}{extension}");

            try
            {
                // COM API를 사용한 이미지 내보내기
                ExportImageUsingComApi(fullPath);

                OnSnapshotProgress(new SnapshotProgressEventArgs
                {
                    CurrentItem = fileName,
                    FilePath = fullPath,
                    Status = SnapshotStatus.Completed
                });

                return fullPath;
            }
            catch (Exception ex)
            {
                OnSnapshotProgress(new SnapshotProgressEventArgs
                {
                    CurrentItem = fileName,
                    Status = SnapshotStatus.Failed,
                    ErrorMessage = ex.Message
                });
                throw;
            }
        }

        /// <summary>
        /// COM API를 사용하여 현재 뷰를 이미지로 내보냅니다.
        /// </summary>
        private void ExportImageUsingComApi(string fullPath)
        {
            // COM Bridge를 통해 Navisworks COM API에 접근
            var comState = ComApiBridge.State;
            if (comState == null)
                throw new InvalidOperationException("COM API에 접근할 수 없습니다.");

            // InwOpState10의 DriveImage 메서드를 사용하여 이미지 내보내기
            // 이미지 타입 결정 (1: BMP, 2: JPG, 3: PNG, 4: TIF)
            int imageType = GetComApiImageType(ImageFormat);

            // DriveImage 호출 (width, height, filename, imagetype)
            // Note: DriveImage 메서드가 존재하지 않으면 대체 방법 사용
            try
            {
                // 방법 1: InwOpState10.DriveImage 시도
                dynamic state = comState;
                state.DriveImage(ImageWidth, ImageHeight, fullPath, imageType);
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("RuntimeBinder") ||
                                       ex is MissingMethodException ||
                                       ex is NotImplementedException)
            {
                // 방법 2: 직접 렌더링 대체 방법 - 빈 이미지 생성 (Navisworks GUI에서만 동작)
                throw new NotSupportedException(
                    "이미지 캡처를 위해서는 Navisworks GUI에서 플러그인을 사용해야 합니다.\n" +
                    "또는 File > Export > Image 메뉴를 사용하세요.");
            }
        }

        /// <summary>
        /// ImageFormat을 COM API 이미지 타입으로 변환
        /// </summary>
        private int GetComApiImageType(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Png)) return 3;
            if (format.Equals(ImageFormat.Jpeg)) return 2;
            if (format.Equals(ImageFormat.Bmp)) return 1;
            if (format.Equals(ImageFormat.Tiff)) return 4;
            return 3; // 기본값: PNG
        }

        /// <summary>
        /// 현재 ViewPoint를 저장합니다.
        /// </summary>
        /// <param name="viewpointName">ViewPoint 이름</param>
        /// <param name="folderName">저장할 폴더 이름 (선택)</param>
        /// <returns>생성된 SavedViewpoint</returns>
        public SavedViewpoint SaveCurrentViewPoint(string viewpointName, string folderName = null)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            try
            {
                // 현재 Viewpoint 복사
                var currentViewpoint = doc.CurrentViewpoint.CreateCopy();

                // SavedViewpoint 생성
                var savedViewpoint = new SavedViewpoint(currentViewpoint);
                savedViewpoint.DisplayName = viewpointName;

                // 폴더 찾기 또는 생성
                GroupItem targetFolder = null;
                if (!string.IsNullOrEmpty(folderName))
                {
                    targetFolder = FindOrCreateViewpointFolder(doc, folderName);
                }

                // SavedViewpoints에 추가
                using (var transaction = new Transaction(doc, "Save Viewpoint"))
                {
                    if (targetFolder != null)
                    {
                        targetFolder.Children.Add(savedViewpoint);
                    }
                    else
                    {
                        doc.SavedViewpoints.AddCopy(savedViewpoint);
                    }
                    transaction.Commit();
                }

                OnSnapshotProgress(new SnapshotProgressEventArgs
                {
                    CurrentItem = viewpointName,
                    Status = SnapshotStatus.Completed,
                    Message = $"ViewPoint '{viewpointName}' 저장됨"
                });

                return savedViewpoint;
            }
            catch (Exception ex)
            {
                OnSnapshotProgress(new SnapshotProgressEventArgs
                {
                    CurrentItem = viewpointName,
                    Status = SnapshotStatus.Failed,
                    ErrorMessage = ex.Message
                });
                throw;
            }
        }

        /// <summary>
        /// 특정 객체를 격리(Isolate)하고 캡처합니다.
        /// </summary>
        /// <param name="objectId">대상 객체 ID</param>
        /// <param name="outputPath">저장 경로</param>
        /// <returns>저장된 파일 경로</returns>
        public string IsolateAndCapture(Guid objectId, string outputPath = null)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            var modelItem = _extractor.FindModelItemById(objectId);
            if (modelItem == null)
                throw new ArgumentException($"ObjectId {objectId}에 해당하는 ModelItem을 찾을 수 없습니다.");

            try
            {
                // 1. 다른 모든 객체 숨기기 (격리)
                var allItems = new ModelItemCollection();
                foreach (var model in doc.Models)
                {
                    CollectAllModelItems(model.RootItem, allItems);
                }

                var itemsToHide = new ModelItemCollection();
                foreach (ModelItem item in allItems)
                {
                    if (item.InstanceGuid != objectId)
                    {
                        itemsToHide.Add(item);
                    }
                }

                doc.Models.SetHidden(itemsToHide, true);

                // 2. 대상 객체 선택 및 줌
                doc.CurrentSelection.Clear();
                doc.CurrentSelection.Add(modelItem);
                _selectionService.ZoomToSelection();

                // 3. 캡처
                string fileName = $"{modelItem.DisplayName}_{objectId:N}".Replace(" ", "_");
                string filePath = CaptureCurrentView(outputPath, fileName);

                // 4. 복원 (모든 객체 표시)
                doc.Models.SetHidden(allItems, false);

                return filePath;
            }
            catch (Exception ex)
            {
                // 오류 발생 시에도 복원 시도
                try { _selectionService.ShowAllObjects(); } catch { }
                throw new Exception($"객체 격리 캡처 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 필터링된 객체들을 배치 캡처합니다.
        /// </summary>
        /// <param name="filteredRecords">필터링된 레코드 목록</param>
        /// <param name="outputPath">저장 경로</param>
        /// <param name="isolateEach">각 객체 격리 여부</param>
        /// <returns>캡처된 파일 경로 목록</returns>
        public List<string> BatchCaptureFilteredObjects(
            IEnumerable<HierarchicalPropertyRecord> filteredRecords,
            string outputPath = null,
            bool isolateEach = true)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            outputPath = outputPath ?? Path.Combine(DefaultOutputPath, $"Snapshots_{DateTime.Now:yyyyMMdd_HHmmss}");

            // 저장 폴더 생성
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            // 고유 ObjectId 추출
            var uniqueObjectIds = filteredRecords
                .Select(r => r.ObjectId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var capturedFiles = new List<string>();
            int totalCount = uniqueObjectIds.Count;
            int currentIndex = 0;

            foreach (var objectId in uniqueObjectIds)
            {
                currentIndex++;

                OnSnapshotProgress(new SnapshotProgressEventArgs
                {
                    CurrentIndex = currentIndex,
                    TotalCount = totalCount,
                    CurrentItem = objectId.ToString(),
                    Status = SnapshotStatus.InProgress,
                    Progress = (double)currentIndex / totalCount * 100
                });

                try
                {
                    string filePath;
                    if (isolateEach)
                    {
                        filePath = IsolateAndCapture(objectId, outputPath);
                    }
                    else
                    {
                        // 격리 없이 선택만 하고 캡처
                        var modelItem = _extractor.FindModelItemById(objectId);
                        if (modelItem != null)
                        {
                            doc.CurrentSelection.Clear();
                            doc.CurrentSelection.Add(modelItem);
                            _selectionService.ZoomToSelection();
                            filePath = CaptureCurrentView(outputPath, $"{modelItem.DisplayName}_{objectId:N}".Replace(" ", "_"));
                        }
                        else
                        {
                            continue;
                        }
                    }
                    capturedFiles.Add(filePath);
                }
                catch (Exception ex)
                {
                    OnSnapshotProgress(new SnapshotProgressEventArgs
                    {
                        CurrentIndex = currentIndex,
                        TotalCount = totalCount,
                        CurrentItem = objectId.ToString(),
                        Status = SnapshotStatus.Failed,
                        ErrorMessage = ex.Message
                    });
                }
            }

            // 완료 후 모든 객체 표시
            _selectionService.ShowAllObjects();

            OnSnapshotProgress(new SnapshotProgressEventArgs
            {
                CurrentIndex = totalCount,
                TotalCount = totalCount,
                Status = SnapshotStatus.Completed,
                Message = $"{capturedFiles.Count}/{totalCount} 객체 캡처 완료"
            });

            return capturedFiles;
        }

        /// <summary>
        /// 현재 필터 조건으로 스냅샷과 ViewPoint를 함께 저장합니다.
        /// </summary>
        /// <param name="filterCondition">필터 조건명 (파일명에 사용)</param>
        /// <param name="outputPath">저장 경로</param>
        /// <returns>생성된 파일 정보</returns>
        public SnapshotResult CaptureWithViewPoint(string filterCondition, string outputPath = null)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성화된 Navisworks 문서가 없습니다.");

            outputPath = outputPath ?? DefaultOutputPath;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseName = $"{filterCondition}_{timestamp}";

            var result = new SnapshotResult
            {
                FilterCondition = filterCondition,
                Timestamp = DateTime.Now
            };

            try
            {
                // 이미지 캡처
                result.ImagePath = CaptureCurrentView(outputPath, baseName);

                // ViewPoint 저장
                string viewpointName = baseName;
                result.ViewPointName = viewpointName;
                SaveCurrentViewPoint(viewpointName, "DXTnavis Snapshots");

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #region Helper Methods

        private string GetExtensionForFormat(ImageFormat format)
        {
            if (format.Equals(ImageFormat.Png)) return ".png";
            if (format.Equals(ImageFormat.Jpeg)) return ".jpg";
            if (format.Equals(ImageFormat.Bmp)) return ".bmp";
            if (format.Equals(ImageFormat.Gif)) return ".gif";
            return ".png";
        }

        private GroupItem FindOrCreateViewpointFolder(Document doc, string folderName)
        {
            // 기존 폴더 찾기
            foreach (SavedItem item in doc.SavedViewpoints.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            // 폴더 생성
            var newFolder = new FolderItem();
            newFolder.DisplayName = folderName;

            using (var transaction = new Transaction(doc, "Create Viewpoint Folder"))
            {
                doc.SavedViewpoints.AddCopy(newFolder);
                transaction.Commit();
            }

            // 생성된 폴더 찾아서 반환
            foreach (SavedItem item in doc.SavedViewpoints.Value)
            {
                if (item is FolderItem folder && folder.DisplayName == folderName)
                {
                    return folder;
                }
            }

            return null;
        }

        private void CollectAllModelItems(ModelItem currentItem, ModelItemCollection collection)
        {
            if (currentItem == null) return;

            if (currentItem.HasGeometry)
            {
                collection.Add(currentItem);
            }

            foreach (ModelItem child in currentItem.Children)
            {
                CollectAllModelItems(child, collection);
            }
        }

        protected virtual void OnSnapshotProgress(SnapshotProgressEventArgs e)
        {
            SnapshotProgress?.Invoke(this, e);
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// 스냅샷 진행 상태 이벤트 인자
    /// </summary>
    public class SnapshotProgressEventArgs : EventArgs
    {
        public int CurrentIndex { get; set; }
        public int TotalCount { get; set; }
        public string CurrentItem { get; set; }
        public string FilePath { get; set; }
        public SnapshotStatus Status { get; set; }
        public double Progress { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// 스냅샷 상태
    /// </summary>
    public enum SnapshotStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// 스냅샷 결과
    /// </summary>
    public class SnapshotResult
    {
        public bool Success { get; set; }
        public string FilterCondition { get; set; }
        public DateTime Timestamp { get; set; }
        public string ImagePath { get; set; }
        public string ViewPointName { get; set; }
        public string ErrorMessage { get; set; }
    }

    #endregion
}
