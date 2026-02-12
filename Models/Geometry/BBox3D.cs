using System;

namespace DXTnavis.Models.Geometry
{
    /// <summary>
    /// 3D 공간의 축 정렬 경계 상자 (Axis-Aligned Bounding Box)
    /// Navisworks ModelItem.BoundingBox()에서 추출
    /// </summary>
    public class BBox3D
    {
        /// <summary>최소 좌표 (하단 좌측 뒤)</summary>
        public Point3D Min { get; set; }

        /// <summary>최대 좌표 (상단 우측 앞)</summary>
        public Point3D Max { get; set; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public BBox3D() { }

        /// <summary>
        /// Min/Max 좌표로 BBox 생성
        /// </summary>
        public BBox3D(Point3D min, Point3D max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// 6개 좌표값으로 BBox 생성
        /// </summary>
        public BBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        {
            Min = new Point3D(minX, minY, minZ);
            Max = new Point3D(maxX, maxY, maxZ);
        }

        #region 계산 속성

        /// <summary>
        /// 경계 상자의 중심점 (Centroid)
        /// </summary>
        public Point3D GetCentroid()
        {
            return new Point3D(
                (Min.X + Max.X) * 0.5,
                (Min.Y + Max.Y) * 0.5,
                (Min.Z + Max.Z) * 0.5);
        }

        /// <summary>
        /// 경계 상자의 크기 (Width, Depth, Height)
        /// </summary>
        public Point3D GetSize()
        {
            return new Point3D(
                Max.X - Min.X,
                Max.Y - Min.Y,
                Max.Z - Min.Z);
        }

        /// <summary>
        /// 경계 상자의 부피
        /// </summary>
        public double GetVolume()
        {
            var size = GetSize();
            return Math.Abs(size.X * size.Y * size.Z);
        }

        /// <summary>
        /// 경계 상자의 표면적
        /// </summary>
        public double GetSurfaceArea()
        {
            var size = GetSize();
            return 2.0 * (size.X * size.Y + size.Y * size.Z + size.Z * size.X);
        }

        /// <summary>
        /// 대각선 길이 (가장 먼 두 꼭지점 사이 거리)
        /// </summary>
        public double GetDiagonalLength()
        {
            return Min.DistanceTo(Max);
        }

        /// <summary>
        /// 경계 상자가 유효한지 (Min < Max)
        /// </summary>
        public bool IsValid =>
            Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;

        /// <summary>
        /// 경계 상자가 비어있는지 (크기 = 0)
        /// </summary>
        public bool IsEmpty =>
            GetVolume() < 1e-10;

        #endregion

        #region 공간 쿼리

        /// <summary>
        /// 점이 경계 상자 내부에 있는지 확인
        /// </summary>
        public bool Contains(Point3D point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        /// <summary>
        /// 다른 경계 상자와 교차하는지 확인
        /// </summary>
        public bool Intersects(BBox3D other)
        {
            if (other == null) return false;

            return !(other.Max.X < Min.X || other.Min.X > Max.X ||
                     other.Max.Y < Min.Y || other.Min.Y > Max.Y ||
                     other.Max.Z < Min.Z || other.Min.Z > Max.Z);
        }

        /// <summary>
        /// 다른 경계 상자를 완전히 포함하는지 확인
        /// </summary>
        public bool Contains(BBox3D other)
        {
            if (other == null) return false;

            return Contains(other.Min) && Contains(other.Max);
        }

        #endregion

        #region 조합 연산

        /// <summary>
        /// 두 경계 상자의 합집합 (둘 다 포함하는 최소 상자)
        /// </summary>
        public BBox3D Union(BBox3D other)
        {
            if (other == null) return this;

            return new BBox3D(
                new Point3D(
                    Math.Min(Min.X, other.Min.X),
                    Math.Min(Min.Y, other.Min.Y),
                    Math.Min(Min.Z, other.Min.Z)),
                new Point3D(
                    Math.Max(Max.X, other.Max.X),
                    Math.Max(Max.Y, other.Max.Y),
                    Math.Max(Max.Z, other.Max.Z)));
        }

        /// <summary>
        /// 두 경계 상자의 교집합 (겹치는 영역)
        /// </summary>
        public BBox3D Intersection(BBox3D other)
        {
            if (other == null || !Intersects(other)) return null;

            return new BBox3D(
                new Point3D(
                    Math.Max(Min.X, other.Min.X),
                    Math.Max(Min.Y, other.Min.Y),
                    Math.Max(Min.Z, other.Min.Z)),
                new Point3D(
                    Math.Min(Max.X, other.Max.X),
                    Math.Min(Max.Y, other.Max.Y),
                    Math.Min(Max.Z, other.Max.Z)));
        }

        /// <summary>
        /// 경계 상자를 주어진 양만큼 확장
        /// </summary>
        public BBox3D Expand(double amount)
        {
            return new BBox3D(
                new Point3D(Min.X - amount, Min.Y - amount, Min.Z - amount),
                new Point3D(Max.X + amount, Max.Y + amount, Max.Z + amount));
        }

        /// <summary>
        /// 점을 포함하도록 경계 상자 확장
        /// </summary>
        public BBox3D ExpandToInclude(Point3D point)
        {
            return new BBox3D(
                new Point3D(
                    Math.Min(Min.X, point.X),
                    Math.Min(Min.Y, point.Y),
                    Math.Min(Min.Z, point.Z)),
                new Point3D(
                    Math.Max(Max.X, point.X),
                    Math.Max(Max.Y, point.Y),
                    Math.Max(Max.Z, point.Z)));
        }

        #endregion

        #region Phase 17: 인접성 검출 지원

        /// <summary>
        /// 두 BBox 간 최소 거리 (겹치면 0 반환)
        /// </summary>
        public double DistanceTo(BBox3D other)
        {
            if (other == null) return double.MaxValue;

            // 각 축에서 갭 계산 (겹치면 0)
            double dx = Math.Max(0, Math.Max(Min.X - other.Max.X, other.Min.X - Max.X));
            double dy = Math.Max(0, Math.Max(Min.Y - other.Max.Y, other.Min.Y - Max.Y));
            double dz = Math.Max(0, Math.Max(Min.Z - other.Max.Z, other.Min.Z - Max.Z));

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// tolerance 이내로 인접한지 판정
        /// </summary>
        public bool IsAdjacentTo(BBox3D other, double tolerance)
        {
            if (other == null) return false;
            return DistanceTo(other) <= tolerance;
        }

        /// <summary>
        /// 겹침 체적 반환 (겹치지 않으면 0)
        /// </summary>
        public double OverlapVolume(BBox3D other)
        {
            if (other == null || !Intersects(other)) return 0;

            double dx = Math.Min(Max.X, other.Max.X) - Math.Max(Min.X, other.Min.X);
            double dy = Math.Min(Max.Y, other.Max.Y) - Math.Max(Min.Y, other.Min.Y);
            double dz = Math.Min(Max.Z, other.Max.Z) - Math.Max(Min.Z, other.Min.Z);

            if (dx <= 0 || dy <= 0 || dz <= 0) return 0;
            return dx * dy * dz;
        }

        #endregion

        #region 직렬화

        /// <summary>
        /// JSON 형식 문자열로 변환
        /// </summary>
        public string ToJson()
        {
            return $"{{ \"min\": {Min.ToJson()}, \"max\": {Max.ToJson()} }}";
        }

        public override string ToString() => $"BBox[{Min} → {Max}]";

        #endregion
    }
}
