using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DXTnavis.Models.Geometry;
using DXTnavis.Models.Spatial;

namespace DXTnavis.Services.Spatial
{
    /// <summary>
    /// Phase 17: Union-Find 알고리즘으로 연결 컴포넌트를 찾는 서비스
    /// 인접 관계 리스트 → 연결 그룹 목록 변환
    /// </summary>
    public class ConnectedComponentFinder
    {
        #region Union-Find 데이터 구조

        private Dictionary<Guid, Guid> _parent;
        private Dictionary<Guid, int> _rank;

        private Guid Find(Guid x)
        {
            if (!_parent.ContainsKey(x))
            {
                _parent[x] = x;
                _rank[x] = 0;
            }

            // Path compression
            if (_parent[x] != x)
                _parent[x] = Find(_parent[x]);

            return _parent[x];
        }

        private void Union(Guid x, Guid y)
        {
            var rootX = Find(x);
            var rootY = Find(y);

            if (rootX == rootY) return;

            // Union by rank
            if (_rank[rootX] < _rank[rootY])
            {
                _parent[rootX] = rootY;
            }
            else if (_rank[rootX] > _rank[rootY])
            {
                _parent[rootY] = rootX;
            }
            else
            {
                _parent[rootY] = rootX;
                _rank[rootX]++;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 인접 관계 리스트에서 연결 컴포넌트를 찾는다
        /// </summary>
        /// <param name="adjacencies">인접 관계 목록</param>
        /// <param name="allObjectIds">전체 객체 ID (고립 노드 포함 시)</param>
        /// <returns>그룹 ID → 객체 ID 리스트</returns>
        public Dictionary<int, List<Guid>> FindGroups(
            List<AdjacencyRecord> adjacencies,
            IEnumerable<Guid> allObjectIds = null)
        {
            _parent = new Dictionary<Guid, Guid>();
            _rank = new Dictionary<Guid, int>();

            // 모든 노드 초기화
            if (allObjectIds != null)
            {
                foreach (var id in allObjectIds)
                {
                    _parent[id] = id;
                    _rank[id] = 0;
                }
            }

            // 인접 관계로 Union
            if (adjacencies != null)
            {
                foreach (var adj in adjacencies)
                {
                    Union(adj.SourceObjectId, adj.TargetObjectId);
                }
            }

            // 그룹별 분류
            var rootToGroup = new Dictionary<Guid, List<Guid>>();
            foreach (var id in _parent.Keys.ToList())
            {
                var root = Find(id);
                if (!rootToGroup.ContainsKey(root))
                    rootToGroup[root] = new List<Guid>();
                rootToGroup[root].Add(id);
            }

            // 그룹 ID 할당 (크기 내림차순)
            var result = new Dictionary<int, List<Guid>>();
            int groupId = 1;
            foreach (var group in rootToGroup.Values.OrderByDescending(g => g.Count))
            {
                result[groupId++] = group;
            }

            Debug.WriteLine(string.Format("[ConnectedComponentFinder] {0}개 객체 → {1}개 그룹",
                _parent.Count, result.Count));

            return result;
        }

        /// <summary>
        /// 그룹별 통계를 계산하여 ConnectedGroup 객체 목록 반환
        /// </summary>
        public List<ConnectedGroup> ComputeGroupStatistics(
            Dictionary<int, List<Guid>> groups,
            Dictionary<Guid, GeometryRecord> geometries,
            List<AdjacencyRecord> adjacencies = null)
        {
            var result = new List<ConnectedGroup>();

            // 인접 관계를 ObjectId 기준으로 인덱싱
            var edgesByObject = new Dictionary<Guid, int>();
            if (adjacencies != null)
            {
                foreach (var adj in adjacencies)
                {
                    if (!edgesByObject.ContainsKey(adj.SourceObjectId))
                        edgesByObject[adj.SourceObjectId] = 0;
                    edgesByObject[adj.SourceObjectId]++;

                    if (!edgesByObject.ContainsKey(adj.TargetObjectId))
                        edgesByObject[adj.TargetObjectId] = 0;
                    edgesByObject[adj.TargetObjectId]++;
                }
            }

            foreach (var kv in groups)
            {
                var group = new ConnectedGroup
                {
                    GroupId = kv.Key,
                    ElementIds = kv.Value
                };

                // 이름 목록
                if (geometries != null)
                {
                    foreach (var id in kv.Value)
                    {
                        GeometryRecord geo;
                        if (geometries.TryGetValue(id, out geo) && !string.IsNullOrEmpty(geo.DisplayName))
                            group.ElementNames.Add(geo.DisplayName);
                    }
                }

                // 그룹 내 edge 수 계산
                if (adjacencies != null)
                {
                    var groupSet = new HashSet<Guid>(kv.Value);
                    int edges = 0;
                    foreach (var adj in adjacencies)
                    {
                        if (groupSet.Contains(adj.SourceObjectId) && groupSet.Contains(adj.TargetObjectId))
                            edges++;
                    }
                    group.EdgeCount = edges;
                }

                // 통계 계산 (BBox union, volume, category)
                if (geometries != null)
                    group.ComputeStatistics(geometries);

                result.Add(group);
            }

            Debug.WriteLine(string.Format("[ConnectedComponentFinder] {0}개 그룹 통계 계산 완료", result.Count));

            return result;
        }

        /// <summary>
        /// 인접 관계 + GeometryRecord로 전체 파이프라인 실행
        /// </summary>
        public List<ConnectedGroup> FindAndCompute(
            List<AdjacencyRecord> adjacencies,
            Dictionary<Guid, GeometryRecord> geometries)
        {
            var groups = FindGroups(adjacencies, geometries?.Keys);
            return ComputeGroupStatistics(groups, geometries, adjacencies);
        }

        #endregion
    }
}
