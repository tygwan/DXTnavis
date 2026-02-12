using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using DXTnavis.Models.Spatial;

namespace DXTnavis.Services.Spatial
{
    /// <summary>
    /// Phase 17: 공간 관계 데이터를 CSV/TTL 파일로 출력하는 서비스
    /// </summary>
    public class SpatialRelationshipWriter
    {
        #region Events

        public event EventHandler<string> StatusChanged;

        #endregion

        #region CSV Writers

        /// <summary>
        /// 인접 관계를 adjacency.csv로 출력
        /// </summary>
        public string WriteAdjacencyCsv(List<AdjacencyRecord> records, string outputDir)
        {
            if (records == null || records.Count == 0) return null;

            var filePath = Path.Combine(outputDir, "adjacency.csv");
            EnsureDirectory(outputDir);

            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine(AdjacencyRecord.CsvHeader);
                foreach (var record in records)
                {
                    sw.WriteLine(record.ToCsvRow());
                }
            }

            OnStatusChanged(string.Format("adjacency.csv 저장: {0:N0}개 관계 → {1}", records.Count, filePath));
            return filePath;
        }

        /// <summary>
        /// 연결 그룹을 connected_groups.csv로 출력
        /// </summary>
        public string WriteGroupsCsv(List<ConnectedGroup> groups, string outputDir)
        {
            if (groups == null || groups.Count == 0) return null;

            var filePath = Path.Combine(outputDir, "connected_groups.csv");
            EnsureDirectory(outputDir);

            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine(ConnectedGroup.CsvHeader);
                foreach (var group in groups)
                {
                    sw.WriteLine(group.ToCsvRow());
                }
            }

            OnStatusChanged(string.Format("connected_groups.csv 저장: {0:N0}개 그룹 → {1}", groups.Count, filePath));
            return filePath;
        }

        #endregion

        #region TTL Writer

        /// <summary>
        /// 인접 관계 + 연결 그룹을 spatial_relationships.ttl로 출력
        /// </summary>
        public string WriteTtl(
            List<AdjacencyRecord> adjacencies,
            List<ConnectedGroup> groups,
            string outputDir)
        {
            var filePath = Path.Combine(outputDir, "spatial_relationships.ttl");
            EnsureDirectory(outputDir);

            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(false)))
            {
                // Prefixes
                sw.WriteLine("@prefix spatial: <http://example.org/bim-ontology/spatial#> .");
                sw.WriteLine("@prefix inst: <http://example.org/bim-ontology/instance#> .");
                sw.WriteLine("@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .");
                sw.WriteLine("@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .");
                sw.WriteLine();

                // Metadata
                sw.WriteLine("# DXTnavis Phase 17: Spatial Relationships");
                sw.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "# Generated: {0}", DateTime.UtcNow.ToString("o")));
                sw.WriteLine(string.Format("# Adjacency edges: {0}", adjacencies?.Count ?? 0));
                sw.WriteLine(string.Format("# Connected groups: {0}", groups?.Count ?? 0));
                sw.WriteLine();

                // Adjacency triples
                if (adjacencies != null && adjacencies.Count > 0)
                {
                    sw.WriteLine("# === Adjacency Relations ===");
                    sw.WriteLine();
                    foreach (var adj in adjacencies)
                    {
                        sw.Write(adj.ToTtl());
                    }
                    sw.WriteLine();
                }

                // Group triples
                if (groups != null && groups.Count > 0)
                {
                    sw.WriteLine("# === Connected Groups ===");
                    sw.WriteLine();
                    foreach (var group in groups)
                    {
                        sw.Write(group.ToTtl("spatial"));
                        sw.WriteLine();
                    }
                }
            }

            int totalTriples = (adjacencies?.Count ?? 0) * 2 + (groups?.Count ?? 0) * 3;
            OnStatusChanged(string.Format("spatial_relationships.ttl 저장: ~{0:N0}개 트리플 → {1}", totalTriples, filePath));
            return filePath;
        }

        #endregion

        #region Summary Report

        /// <summary>
        /// 분석 결과 요약 보고서 생성
        /// </summary>
        public string WriteSummary(
            List<AdjacencyRecord> adjacencies,
            List<ConnectedGroup> groups,
            string outputDir,
            double elapsedSeconds)
        {
            var filePath = Path.Combine(outputDir, "spatial_summary.txt");
            EnsureDirectory(outputDir);

            using (var sw = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                sw.WriteLine("=== DXTnavis Phase 17: Spatial Analysis Summary ===");
                sw.WriteLine(string.Format("Generated: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                sw.WriteLine(string.Format("Processing time: {0:F1}s", elapsedSeconds));
                sw.WriteLine();

                // Adjacency stats
                sw.WriteLine("--- Adjacency Relations ---");
                sw.WriteLine(string.Format("Total relations: {0:N0}", adjacencies?.Count ?? 0));
                if (adjacencies != null && adjacencies.Count > 0)
                {
                    int overlaps = 0, touches = 0, nearTouches = 0;
                    foreach (var a in adjacencies)
                    {
                        switch (a.RelationType)
                        {
                            case SpatialRelationType.Overlap: overlaps++; break;
                            case SpatialRelationType.Touch: touches++; break;
                            case SpatialRelationType.NearTouch: nearTouches++; break;
                        }
                    }
                    sw.WriteLine(string.Format("  Overlap: {0:N0}", overlaps));
                    sw.WriteLine(string.Format("  Touch: {0:N0}", touches));
                    sw.WriteLine(string.Format("  NearTouch: {0:N0}", nearTouches));
                }
                sw.WriteLine();

                // Group stats
                sw.WriteLine("--- Connected Groups ---");
                sw.WriteLine(string.Format("Total groups: {0:N0}", groups?.Count ?? 0));
                if (groups != null && groups.Count > 0)
                {
                    int totalElements = 0;
                    int maxSize = 0;
                    int singletons = 0;
                    foreach (var g in groups)
                    {
                        totalElements += g.ElementCount;
                        if (g.ElementCount > maxSize) maxSize = g.ElementCount;
                        if (g.ElementCount == 1) singletons++;
                    }
                    sw.WriteLine(string.Format("  Total elements: {0:N0}", totalElements));
                    sw.WriteLine(string.Format("  Largest group: {0:N0} elements", maxSize));
                    sw.WriteLine(string.Format("  Singleton groups: {0:N0}", singletons));
                    sw.WriteLine(string.Format("  Multi-element groups: {0:N0}", groups.Count - singletons));
                }

                sw.WriteLine();
                sw.WriteLine("--- Output Files ---");
                sw.WriteLine("  adjacency.csv");
                sw.WriteLine("  connected_groups.csv");
                sw.WriteLine("  spatial_relationships.ttl");
                sw.WriteLine("  spatial_summary.txt");
            }

            OnStatusChanged(string.Format("요약 보고서 저장 → {0}", filePath));
            return filePath;
        }

        #endregion

        #region Private Methods

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine("[SpatialRelationshipWriter] " + message);
        }

        #endregion
    }
}
