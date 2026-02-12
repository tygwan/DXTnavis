using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DXTnavis.Models.Geometry;
using DXTnavis.Models.Spatial;

namespace DXTnavis.Services.Spatial
{
    /// <summary>
    /// Phase 17: BBox 기반 공간 인접성 검출 서비스
    /// Brute Force O(n^2) 및 Spatial Hash Grid O(n) 최적화 지원
    /// </summary>
    public class AdjacencyDetector
    {
        #region Events

        public event EventHandler<int> ProgressChanged;
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Category Tolerances

        /// <summary>
        /// 카테고리별 tolerance (미터)
        /// Wang et al. 알고리즘 참조
        /// </summary>
        private static readonly Dictionary<string, double> CategoryTolerances = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pipe"] = 0.05,           // 50mm - 파이프 연결부
            ["Piping"] = 0.05,
            ["Equipment"] = 0.10,      // 100mm - 장비 접근
            ["Structure"] = 0.20,      // 200mm - 구조물
            ["Structural"] = 0.20,
            ["Insulation"] = 0.01,     // 10mm - 보온재
            ["HVAC"] = 0.08,           // 80mm - 공조 덕트
            ["Electrical"] = 0.05,     // 50mm - 전기 배선
            ["Cable Tray"] = 0.05,
            ["Instrument"] = 0.03,     // 30mm - 계측기기
        };

        private const double DefaultTolerance = 0.15; // 150mm

        #endregion

        #region Public Methods

        /// <summary>
        /// 모든 요소 쌍에 대해 인접성 검사 (Brute Force O(n^2))
        /// 소규모 모델용 (요소 < 1,000)
        /// </summary>
        public List<AdjacencyRecord> DetectAll(
            Dictionary<Guid, GeometryRecord> geometries,
            double? globalTolerance = null,
            CancellationToken cancellationToken = default)
        {
            if (geometries == null || geometries.Count < 2)
                return new List<AdjacencyRecord>();

            var result = new List<AdjacencyRecord>();
            var entries = geometries.ToList();
            int total = entries.Count;
            int pairCount = total * (total - 1) / 2;
            int processed = 0;

            OnStatusChanged(string.Format("Adjacency 검출 시작 (Brute Force): {0:N0}개 객체, {1:N0} 쌍", total, pairCount));
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < total; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var a = entries[i];
                if (a.Value.BBox == null) continue;

                for (int j = i + 1; j < total; j++)
                {
                    var b = entries[j];
                    if (b.Value.BBox == null) continue;

                    double tolerance = globalTolerance ?? GetPairTolerance(a.Value.Category, b.Value.Category);
                    double distance = a.Value.BBox.DistanceTo(b.Value.BBox);

                    if (distance <= tolerance)
                    {
                        double overlap = a.Value.BBox.OverlapVolume(b.Value.BBox);
                        var relType = ClassifyRelation(distance, overlap);

                        result.Add(new AdjacencyRecord
                        {
                            SourceObjectId = a.Key,
                            TargetObjectId = b.Key,
                            SourceName = a.Value.DisplayName,
                            TargetName = b.Value.DisplayName,
                            Distance = distance,
                            OverlapVolume = overlap,
                            RelationType = relType,
                            SourceCategory = a.Value.Category,
                            TargetCategory = b.Value.Category,
                            Tolerance = tolerance
                        });
                    }

                    processed++;
                }

                // 100개마다 진행률 보고
                if (i % 100 == 0)
                {
                    int pct = total > 1 ? (int)(100.0 * i / total) : 100;
                    OnProgressChanged(pct);
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged(string.Format("Adjacency 검출 완료: {0:N0}개 관계 ({1:F1}초)", result.Count, sw.Elapsed.TotalSeconds));

            return result;
        }

        /// <summary>
        /// Spatial Hash Grid 기반 최적화 인접성 검사 O(n)
        /// 대규모 모델용 (요소 >= 1,000)
        /// </summary>
        public List<AdjacencyRecord> DetectAllOptimized(
            Dictionary<Guid, GeometryRecord> geometries,
            double cellSize = 0.5,
            double? globalTolerance = null,
            CancellationToken cancellationToken = default)
        {
            if (geometries == null || geometries.Count < 2)
                return new List<AdjacencyRecord>();

            var result = new List<AdjacencyRecord>();
            var entries = geometries.ToList();
            int total = entries.Count;

            OnStatusChanged(string.Format("Adjacency 검출 시작 (Spatial Hash Grid, cell={0:F2}m): {1:N0}개 객체", cellSize, total));
            var sw = Stopwatch.StartNew();

            // 1) Spatial Hash Grid 구성
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < total; i++)
            {
                var bbox = entries[i].Value.BBox;
                if (bbox == null) continue;

                // BBox가 차지하는 모든 셀에 등록
                int minCx = (int)Math.Floor(bbox.Min.X / cellSize);
                int minCy = (int)Math.Floor(bbox.Min.Y / cellSize);
                int minCz = (int)Math.Floor(bbox.Min.Z / cellSize);
                int maxCx = (int)Math.Floor(bbox.Max.X / cellSize);
                int maxCy = (int)Math.Floor(bbox.Max.Y / cellSize);
                int maxCz = (int)Math.Floor(bbox.Max.Z / cellSize);

                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cy = minCy; cy <= maxCy; cy++)
                    {
                        for (int cz = minCz; cz <= maxCz; cz++)
                        {
                            long key = HashCell(cx, cy, cz);
                            if (!grid.ContainsKey(key))
                                grid[key] = new List<int>();
                            grid[key].Add(i);
                        }
                    }
                }
            }

            // 2) 셀 내 이웃 후보 쌍만 검사
            var checkedPairs = new HashSet<long>();
            int cellsProcessed = 0;
            int totalCells = grid.Count;

            foreach (var cell in grid.Values)
            {
                if (cancellationToken.IsCancellationRequested) break;

                for (int a = 0; a < cell.Count; a++)
                {
                    for (int b = a + 1; b < cell.Count; b++)
                    {
                        int i = cell[a];
                        int j = cell[b];

                        // 중복 쌍 제거
                        long pairKey = i < j ? ((long)i << 32) | (long)j : ((long)j << 32) | (long)i;
                        if (checkedPairs.Contains(pairKey)) continue;
                        checkedPairs.Add(pairKey);

                        var ea = entries[i];
                        var eb = entries[j];
                        if (ea.Value.BBox == null || eb.Value.BBox == null) continue;

                        double tolerance = globalTolerance ?? GetPairTolerance(ea.Value.Category, eb.Value.Category);
                        double distance = ea.Value.BBox.DistanceTo(eb.Value.BBox);

                        if (distance <= tolerance)
                        {
                            double overlap = ea.Value.BBox.OverlapVolume(eb.Value.BBox);
                            var relType = ClassifyRelation(distance, overlap);

                            result.Add(new AdjacencyRecord
                            {
                                SourceObjectId = ea.Key,
                                TargetObjectId = eb.Key,
                                SourceName = ea.Value.DisplayName,
                                TargetName = eb.Value.DisplayName,
                                Distance = distance,
                                OverlapVolume = overlap,
                                RelationType = relType,
                                SourceCategory = ea.Value.Category,
                                TargetCategory = eb.Value.Category,
                                Tolerance = tolerance
                            });
                        }
                    }
                }

                cellsProcessed++;
                if (cellsProcessed % 500 == 0)
                {
                    int pct = (int)(100.0 * cellsProcessed / totalCells);
                    OnProgressChanged(pct);
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged(string.Format("Adjacency 검출 완료: {0:N0}개 관계, {1:N0}개 쌍 검사, {2:N0}개 셀 ({3:F1}초)",
                result.Count, checkedPairs.Count, totalCells, sw.Elapsed.TotalSeconds));

            return result;
        }

        /// <summary>
        /// 자동으로 적절한 알고리즘 선택
        /// </summary>
        public List<AdjacencyRecord> Detect(
            Dictionary<Guid, GeometryRecord> geometries,
            double? globalTolerance = null,
            CancellationToken cancellationToken = default)
        {
            if (geometries == null) return new List<AdjacencyRecord>();

            // 1,000개 미만이면 Brute Force, 이상이면 Spatial Hash Grid
            if (geometries.Count < 1000)
            {
                return DetectAll(geometries, globalTolerance, cancellationToken);
            }
            else
            {
                double maxTolerance = globalTolerance ?? DefaultTolerance;
                double cellSize = Math.Max(maxTolerance * 2, 0.5);
                return DetectAllOptimized(geometries, cellSize, globalTolerance, cancellationToken);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 두 카테고리 쌍에 대한 tolerance 결정 (큰 값 사용)
        /// </summary>
        private double GetPairTolerance(string categoryA, string categoryB)
        {
            double tolA = GetCategoryTolerance(categoryA);
            double tolB = GetCategoryTolerance(categoryB);
            return Math.Max(tolA, tolB);
        }

        private double GetCategoryTolerance(string category)
        {
            if (string.IsNullOrEmpty(category)) return DefaultTolerance;

            double tolerance;
            if (CategoryTolerances.TryGetValue(category, out tolerance))
                return tolerance;

            // 부분 매칭
            foreach (var kv in CategoryTolerances)
            {
                if (category.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            }

            return DefaultTolerance;
        }

        /// <summary>
        /// 거리와 겹침 체적으로 관계 유형 분류
        /// </summary>
        private static SpatialRelationType ClassifyRelation(double distance, double overlapVolume)
        {
            if (overlapVolume > 1e-9)
                return SpatialRelationType.Overlap;

            if (distance < 1e-4) // 0.1mm 미만
                return SpatialRelationType.Touch;

            return SpatialRelationType.NearTouch;
        }

        /// <summary>
        /// 3D 셀 좌표를 단일 long 해시로 변환
        /// </summary>
        private static long HashCell(int cx, int cy, int cz)
        {
            unchecked
            {
                long hash = 17;
                hash = hash * 73856093 + cx;
                hash = hash * 19349663 + cy;
                hash = hash * 83492791 + cz;
                return hash;
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
            Debug.WriteLine("[AdjacencyDetector] " + message);
        }

        #endregion
    }
}
