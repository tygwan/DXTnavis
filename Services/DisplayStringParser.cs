using System;
using System.Text.RegularExpressions;

namespace DXTnavis.Services
{
    /// <summary>
    /// Navisworks VariantData DisplayString 파싱 결과
    /// </summary>
    public class ParsedDisplayString
    {
        /// <summary>
        /// 데이터 타입 (예: "Double", "Int32", "String", "NamedConstant", "DisplayLength")
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// 원본 값 (타입 접두사 제거됨)
        /// </summary>
        public string RawValue { get; set; } = string.Empty;

        /// <summary>
        /// 숫자 값 (파싱 가능한 경우)
        /// </summary>
        public double? NumericValue { get; set; }

        /// <summary>
        /// 단위 (예: "m", "mm", "sq m", "cu m")
        /// </summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// 원본 DisplayString
        /// </summary>
        public string OriginalString { get; set; } = string.Empty;
    }

    /// <summary>
    /// Navisworks VariantData.ToString() 결과인 DisplayString을 파싱하는 서비스
    ///
    /// DisplayString 형식 예시:
    /// - "Double: 5.5"
    /// - "Int32: 42"
    /// - "String: some text"
    /// - "NamedConstant: Yes"
    /// - "DisplayLength: 5.5 m"
    /// - "DisplayArea: 25.5 sq m"
    /// - "DisplayVolume: 100 cu m"
    /// </summary>
    public class DisplayStringParser
    {
        // 타입 접두사 패턴: "Type: value"
        private static readonly Regex TypePrefixPattern = new Regex(
            @"^(\w+):\s*(.*)$",
            RegexOptions.Compiled);

        // 숫자 + 단위 패턴: "5.5 m" or "-3.14 mm"
        private static readonly Regex NumberWithUnitPattern = new Regex(
            @"^(-?\d+\.?\d*(?:E[+-]?\d+)?)\s*(.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// DisplayString을 파싱하여 구조화된 결과 반환
        /// </summary>
        /// <param name="displayString">Navisworks VariantData.ToString() 결과</param>
        /// <returns>파싱된 결과</returns>
        public ParsedDisplayString Parse(string displayString)
        {
            var result = new ParsedDisplayString
            {
                OriginalString = displayString ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(displayString))
                return result;

            // 1. 타입 접두사 추출 (예: "Double: 5.5" → DataType="Double", remaining="5.5")
            var typeMatch = TypePrefixPattern.Match(displayString);
            if (typeMatch.Success)
            {
                result.DataType = typeMatch.Groups[1].Value;
                result.RawValue = typeMatch.Groups[2].Value.Trim();
            }
            else
            {
                // 접두사가 없으면 전체가 값
                result.DataType = "Unknown";
                result.RawValue = displayString;
            }

            // 2. 숫자 값과 단위 추출 시도
            var numberMatch = NumberWithUnitPattern.Match(result.RawValue);
            if (numberMatch.Success)
            {
                string numStr = numberMatch.Groups[1].Value;
                string unitStr = numberMatch.Groups[2].Value.Trim();

                if (double.TryParse(numStr, out double numValue))
                {
                    result.NumericValue = numValue;
                }

                if (!string.IsNullOrEmpty(unitStr))
                {
                    result.Unit = NormalizeUnit(unitStr);
                }
            }
            else if (double.TryParse(result.RawValue, out double plainNum))
            {
                // 단위 없는 순수 숫자
                result.NumericValue = plainNum;
            }

            return result;
        }

        /// <summary>
        /// 단위 문자열 정규화
        /// </summary>
        private string NormalizeUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
                return string.Empty;

            // 일반적인 단위 정규화
            switch (unit.ToLowerInvariant())
            {
                // 길이
                case "m":
                case "meter":
                case "meters":
                    return "m";
                case "mm":
                case "millimeter":
                case "millimeters":
                    return "mm";
                case "cm":
                case "centimeter":
                case "centimeters":
                    return "cm";
                case "ft":
                case "foot":
                case "feet":
                    return "ft";
                case "in":
                case "inch":
                case "inches":
                    return "in";

                // 면적
                case "sq m":
                case "m²":
                case "square meter":
                case "square meters":
                    return "sq m";
                case "sq ft":
                case "ft²":
                case "square foot":
                case "square feet":
                    return "sq ft";
                case "sq mm":
                case "mm²":
                    return "sq mm";

                // 체적
                case "cu m":
                case "m³":
                case "cubic meter":
                case "cubic meters":
                    return "cu m";
                case "cu ft":
                case "ft³":
                case "cubic foot":
                case "cubic feet":
                    return "cu ft";

                // 각도
                case "°":
                case "deg":
                case "degree":
                case "degrees":
                    return "deg";
                case "rad":
                case "radian":
                case "radians":
                    return "rad";

                // 그 외는 원본 반환
                default:
                    return unit;
            }
        }

        /// <summary>
        /// DisplayString이 측정값(숫자+단위) 형식인지 확인
        /// </summary>
        public bool IsMeasurement(string displayString)
        {
            var parsed = Parse(displayString);
            return parsed.NumericValue.HasValue && !string.IsNullOrEmpty(parsed.Unit);
        }

        /// <summary>
        /// DisplayString이 숫자 형식인지 확인
        /// </summary>
        public bool IsNumeric(string displayString)
        {
            var parsed = Parse(displayString);
            return parsed.NumericValue.HasValue;
        }
    }
}
