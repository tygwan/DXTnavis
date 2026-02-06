using System;
using System.Globalization;

namespace DXTnavis.Models.Geometry
{
    /// <summary>
    /// 3D 공간의 점을 나타내는 구조체
    /// Navisworks world coordinates 사용
    /// </summary>
    public struct Point3D : IEquatable<Point3D>
    {
        /// <summary>X 좌표 (동-서)</summary>
        public double X { get; set; }

        /// <summary>Y 좌표 (남-북)</summary>
        public double Y { get; set; }

        /// <summary>Z 좌표 (상-하, 고도)</summary>
        public double Z { get; set; }

        /// <summary>
        /// Point3D 생성자
        /// </summary>
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 원점 (0, 0, 0)
        /// </summary>
        public static Point3D Origin => new Point3D(0, 0, 0);

        /// <summary>
        /// 두 점이 같은지 비교 (epsilon = 1e-9)
        /// </summary>
        public bool Equals(Point3D other)
        {
            const double epsilon = 1e-9;
            return Math.Abs(X - other.X) < epsilon &&
                   Math.Abs(Y - other.Y) < epsilon &&
                   Math.Abs(Z - other.Z) < epsilon;
        }

        public override bool Equals(object obj) => obj is Point3D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// 디버깅용 문자열 표현 (6자리 소수점)
        /// </summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                "({0:F6}, {1:F6}, {2:F6})", X, Y, Z);

        /// <summary>
        /// JSON 직렬화용 문자열
        /// </summary>
        public string ToJson() =>
            string.Format(CultureInfo.InvariantCulture,
                "{{ \"x\": {0}, \"y\": {1}, \"z\": {2} }}", X, Y, Z);

        #region 연산자 오버로딩

        public static Point3D operator +(Point3D a, Point3D b) =>
            new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Point3D operator -(Point3D a, Point3D b) =>
            new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Point3D operator *(Point3D p, double scalar) =>
            new Point3D(p.X * scalar, p.Y * scalar, p.Z * scalar);

        public static Point3D operator /(Point3D p, double scalar) =>
            new Point3D(p.X / scalar, p.Y / scalar, p.Z / scalar);

        public static bool operator ==(Point3D a, Point3D b) => a.Equals(b);
        public static bool operator !=(Point3D a, Point3D b) => !a.Equals(b);

        #endregion

        #region 유틸리티 메서드

        /// <summary>
        /// 두 점 사이의 유클리드 거리
        /// </summary>
        public double DistanceTo(Point3D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// 원점으로부터의 거리 (벡터 크기)
        /// </summary>
        public double Magnitude => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// 정규화된 단위 벡터 반환
        /// </summary>
        public Point3D Normalized
        {
            get
            {
                var mag = Magnitude;
                if (mag < 1e-10) return Origin;
                return this / mag;
            }
        }

        /// <summary>
        /// 두 점 사이의 중점
        /// </summary>
        public static Point3D Midpoint(Point3D a, Point3D b) =>
            new Point3D(
                (a.X + b.X) * 0.5,
                (a.Y + b.Y) * 0.5,
                (a.Z + b.Z) * 0.5);

        /// <summary>
        /// 선형 보간 (t = 0이면 a, t = 1이면 b)
        /// </summary>
        public static Point3D Lerp(Point3D a, Point3D b, double t) =>
            new Point3D(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t);

        #endregion
    }
}
