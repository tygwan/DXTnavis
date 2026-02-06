using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;
using DXTnavis.Models.Geometry;
using DXTnavis.Services.Geometry;

namespace DXTnavis.Services
{
    /// <summary>
    /// 통합 CSV 내보내기 서비스
    /// Phase 16: Unified CSV Export for Ontology Conversion
    ///
    /// Hierarchy + Geometry + Manifest 데이터를 단일 CSV로 병합
    /// - 객체별 1행 (속성은 PropertiesJson으로 집계)
    /// - ObjectId 통합 (NavisworksDataExtractor와 동일한 ID 생성 로직 사용)
    /// - bim-ontology 프로젝트 연동용
    /// </summary>
    public class UnifiedCsvExporter
    {
        #region Events

        /// <summary>진행률 변경 이벤트 (0-100%)</summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>상태 메시지 이벤트</summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Fields

        private readonly NavisworksDataExtractor _hierarchyExtractor;
        private readonly GeometryExtractor _geometryExtractor;

        #endregion

        #region Constructor

        public UnifiedCsvExporter()
        {
            _hierarchyExtractor = new NavisworksDataExtractor();
            _geometryExtractor = new GeometryExtractor();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 전체 문서에서 통합 CSV 내보내기
        /// </summary>
        /// <param name="filePath">출력 파일 경로</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>내보낸 객체 수</returns>
        public int ExportFromDocument(string filePath, CancellationToken cancellationToken = default)
        {
            var doc = Application.ActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("활성 문서가 없습니다.");

            OnStatusChanged("Hierarchy 데이터 추출 중...");
            OnProgressChanged(5);

            // 1. Hierarchy 데이터 추출
            var hierarchyRecords = _hierarchyExtractor.ExtractAllHierarchicalRecords();
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"Hierarchy 추출 완료: {hierarchyRecords.Count:N0}개 속성");
            OnProgressChanged(30);

            // 2. Geometry 데이터 추출
            OnStatusChanged("Geometry 데이터 추출 중...");
            var geometryRecords = _geometryExtractor.ExtractFromDocument(doc, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"Geometry 추출 완료: {geometryRecords.Count:N0}개 객체");
            OnProgressChanged(60);

            // 3. 데이터 병합
            OnStatusChanged("데이터 병합 중...");
            var unifiedRecords = MergeData(hierarchyRecords, geometryRecords);
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"병합 완료: {unifiedRecords.Count:N0}개 통합 객체");
            OnProgressChanged(80);

            // 4. CSV 파일 작성
            OnStatusChanged("CSV 파일 작성 중...");
            WriteCsv(filePath, unifiedRecords);

            OnProgressChanged(100);
            OnStatusChanged($"내보내기 완료: {filePath}");

            return unifiedRecords.Count;
        }

        /// <summary>
        /// 선택된 객체에서 통합 CSV 내보내기
        /// </summary>
        /// <param name="selectedItems">선택된 ModelItem 컬렉션</param>
        /// <param name="filePath">출력 파일 경로</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>내보낸 객체 수</returns>
        public int ExportFromSelection(
            ModelItemCollection selectedItems,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            if (selectedItems == null || selectedItems.Count == 0)
                throw new ArgumentException("선택된 객체가 없습니다.");

            OnStatusChanged("선택 객체 Hierarchy 추출 중...");
            OnProgressChanged(5);

            // 1. Hierarchy 데이터 추출 (선택 객체)
            var hierarchyRecords = _hierarchyExtractor.ExtractHierarchicalRecordsFromSelection(selectedItems);
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"Hierarchy 추출 완료: {hierarchyRecords.Count:N0}개 속성");
            OnProgressChanged(30);

            // 2. Geometry 데이터 추출 (선택 객체)
            OnStatusChanged("선택 객체 Geometry 추출 중...");
            var geometryRecords = _geometryExtractor.ExtractFromSelection(selectedItems, cancellationToken);
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"Geometry 추출 완료: {geometryRecords.Count:N0}개 객체");
            OnProgressChanged(60);

            // 3. 데이터 병합
            OnStatusChanged("데이터 병합 중...");
            var unifiedRecords = MergeData(hierarchyRecords, geometryRecords);
            if (cancellationToken.IsCancellationRequested) return 0;

            OnStatusChanged($"병합 완료: {unifiedRecords.Count:N0}개 통합 객체");
            OnProgressChanged(80);

            // 4. CSV 파일 작성
            OnStatusChanged("CSV 파일 작성 중...");
            WriteCsv(filePath, unifiedRecords);

            OnProgressChanged(100);
            OnStatusChanged($"내보내기 완료: {filePath}");

            return unifiedRecords.Count;
        }

        /// <summary>
        /// 이미 추출된 데이터에서 통합 CSV 내보내기
        /// (외부에서 데이터를 제공하는 경우)
        /// </summary>
        public int ExportFromData(
            List<HierarchicalPropertyRecord> hierarchyRecords,
            Dictionary<Guid, GeometryRecord> geometryRecords,
            string filePath)
        {
            OnStatusChanged("데이터 병합 중...");
            OnProgressChanged(50);

            var unifiedRecords = MergeData(hierarchyRecords, geometryRecords);

            OnStatusChanged("CSV 파일 작성 중...");
            OnProgressChanged(80);

            WriteCsv(filePath, unifiedRecords);

            OnProgressChanged(100);
            OnStatusChanged($"내보내기 완료: {unifiedRecords.Count:N0}개 객체");

            return unifiedRecords.Count;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Hierarchy 데이터와 Geometry 데이터를 병합
        /// </summary>
        private List<UnifiedObjectRecord> MergeData(
            List<HierarchicalPropertyRecord> hierarchyRecords,
            Dictionary<Guid, GeometryRecord> geometryRecords)
        {
            var sw = Stopwatch.StartNew();

            // ObjectId별로 Hierarchy 레코드 그룹화
            var hierarchyByObject = hierarchyRecords
                .GroupBy(r => r.ObjectId)
                .ToDictionary(g => g.Key, g => g.ToList());

            Debug.WriteLine($"[UnifiedCsvExporter] Hierarchy 그룹화: {hierarchyByObject.Count:N0}개 객체");

            // 통합 레코드 생성
            var result = new List<UnifiedObjectRecord>();
            var exportTime = DateTime.UtcNow;

            // Hierarchy 기준으로 통합 (Geometry는 Left Join)
            foreach (var kvp in hierarchyByObject)
            {
                var objectId = kvp.Key;
                var properties = kvp.Value;
                var firstProp = properties.First();

                var unified = new UnifiedObjectRecord
                {
                    SchemaVersion = "1.0.0",
                    ExportedAtUtc = exportTime,
                    ObjectId = objectId,
                    ParentId = firstProp.ParentId,
                    Level = firstProp.Level,
                    DisplayName = firstProp.DisplayName,
                    ObjectCategory = ExtractObjectCategory(properties),
                    HierarchyPath = firstProp.SysPath ?? string.Empty
                };

                // 속성 추가
                unified.AddProperties(properties);

                // Geometry 정보 추가 (있는 경우)
                if (geometryRecords.TryGetValue(objectId, out var geometry))
                {
                    unified.SetGeometry(geometry);
                }

                result.Add(unified);
            }

            // Geometry만 있고 Hierarchy에 없는 객체 추가 (Union)
            var hierarchyObjectIds = new HashSet<Guid>(hierarchyByObject.Keys);
            foreach (var kvp in geometryRecords)
            {
                if (!hierarchyObjectIds.Contains(kvp.Key))
                {
                    var geometry = kvp.Value;
                    var unified = new UnifiedObjectRecord
                    {
                        SchemaVersion = "1.0.0",
                        ExportedAtUtc = exportTime,
                        ObjectId = kvp.Key,
                        DisplayName = geometry.DisplayName ?? "Unknown",
                        ObjectCategory = geometry.Category ?? string.Empty,
                        PropertyCount = 0
                    };

                    unified.SetGeometry(geometry);
                    result.Add(unified);
                }
            }

            sw.Stop();
            Debug.WriteLine($"[UnifiedCsvExporter] 병합 완료: {result.Count:N0}개 객체 ({sw.ElapsedMilliseconds}ms)");

            return result;
        }

        /// <summary>
        /// 속성에서 객체 카테고리 추출
        /// </summary>
        private string ExtractObjectCategory(List<HierarchicalPropertyRecord> properties)
        {
            // "Item" 카테고리에서 "Type" 또는 "Category" 속성 찾기
            var itemCategory = properties.FirstOrDefault(p =>
                p.Category == "Item" &&
                (p.PropertyName == "Type" || p.PropertyName == "Category" || p.PropertyName == "유형"));

            if (itemCategory != null)
                return itemCategory.PropertyValue ?? string.Empty;

            // Element 카테고리에서 Category 속성 찾기
            var elementCategory = properties.FirstOrDefault(p =>
                p.Category == "Element" && p.PropertyName == "Category");

            if (elementCategory != null)
                return elementCategory.PropertyValue ?? string.Empty;

            // 첫 번째 속성의 카테고리 반환
            return properties.FirstOrDefault()?.Category ?? string.Empty;
        }

        /// <summary>
        /// 통합 레코드를 CSV 파일로 작성
        /// </summary>
        private void WriteCsv(string filePath, List<UnifiedObjectRecord> records)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 헤더 작성
                writer.WriteLine(UnifiedObjectRecord.GetCsvHeader());

                // 데이터 작성
                foreach (var record in records)
                {
                    writer.WriteLine(record.ToCsvRow());
                }
            }

            Debug.WriteLine($"[UnifiedCsvExporter] CSV 작성 완료: {filePath}");
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
            Debug.WriteLine($"[UnifiedCsvExporter] {message}");
        }

        #endregion
    }
}
