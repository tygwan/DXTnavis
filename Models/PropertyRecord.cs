using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// Phase 12: 속성 레코드 (간소화)
    /// 객체 레벨 정보 없이 순수 속성 데이터만 보관
    /// ObjectGroupModel 하위에서 사용
    /// </summary>
    public class PropertyRecord : INotifyPropertyChanged
    {
        #region Private Fields

        private bool _isSelected;

        #endregion

        #region Properties

        /// <summary>
        /// 속성 카테고리
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 속성 이름
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 속성 값 (원본 DisplayString)
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// 데이터 타입 (예: "Double", "Int32", "DisplayLength")
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 파싱된 원본 값 (타입 접두사 제거됨)
        /// </summary>
        public string RawValue { get; set; }

        /// <summary>
        /// 숫자 값 (파싱 가능한 경우)
        /// </summary>
        public double? NumericValue { get; set; }

        /// <summary>
        /// 단위 (예: "m", "mm", "sq m")
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 속성 권한 상태 (읽기/쓰기)
        /// </summary>
        public string ReadWriteStatus { get; set; }

        /// <summary>
        /// 개별 선택 상태 (선택적 사용)
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        #endregion

        #region Computed Properties

        /// <summary>
        /// UI 표시용 요약 텍스트
        /// </summary>
        public string DisplayText => $"{Category}: {PropertyName} = {PropertyValue}";

        /// <summary>
        /// 단축 표시 (값만)
        /// </summary>
        public string ShortDisplay => $"{PropertyName}: {PropertyValue}";

        #endregion

        #region Constructor

        public PropertyRecord()
        {
            DataType = string.Empty;
            RawValue = string.Empty;
            Unit = string.Empty;
            ReadWriteStatus = "알 수 없음";
            IsSelected = false;
        }

        public PropertyRecord(string category, string propertyName, string propertyValue)
            : this()
        {
            Category = category;
            PropertyName = propertyName;
            PropertyValue = propertyValue;
        }

        public PropertyRecord(
            string category,
            string propertyName,
            string propertyValue,
            string dataType,
            string rawValue,
            double? numericValue,
            string unit,
            string readWriteStatus = "알 수 없음")
            : this()
        {
            Category = category;
            PropertyName = propertyName;
            PropertyValue = propertyValue;
            DataType = dataType;
            RawValue = rawValue;
            NumericValue = numericValue;
            Unit = unit;
            ReadWriteStatus = readWriteStatus;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
