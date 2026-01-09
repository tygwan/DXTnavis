using System;
using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// 계층 구조 정보를 포함하는 속성 데이터 레코드
    /// ParentId와 Level을 통해 부모-자식 관계 및 깊이를 표현합니다.
    /// </summary>
    public class HierarchicalPropertyRecord : INotifyPropertyChanged
    {
        private bool _isSelected;
        /// <summary>
        /// 객체의 고유 ID
        /// </summary>
        public Guid ObjectId { get; set; }

        /// <summary>
        /// 부모 객체의 고유 ID (최상위 객체는 Guid.Empty)
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// 선택된 객체로부터의 깊이 (0부터 시작)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 객체의 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 시스템 경로 (계층 구조 전체 경로, 예: "Project > Building > Level 1 > Wall")
        /// </summary>
        public string SysPath { get; set; }

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
        /// v0.4.2: DisplayString 파싱 결과
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// 파싱된 원본 값 (타입 접두사 제거됨)
        /// v0.4.2: DisplayString 파싱 결과
        /// </summary>
        public string RawValue { get; set; }

        /// <summary>
        /// 숫자 값 (파싱 가능한 경우)
        /// v0.4.2: DisplayString 파싱 결과
        /// </summary>
        public double? NumericValue { get; set; }

        /// <summary>
        /// 단위 (예: "m", "mm", "sq m")
        /// 단위가 없으면 빈 문자열
        /// v0.4.2: DisplayString 파싱 결과
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// 속성 권한 상태 (PRD v7: 읽기/쓰기 권한 표시)
        /// </summary>
        public string ReadWriteStatus { get; set; }

        /// <summary>
        /// 체크박스 선택 여부 (SearchSet 생성용)
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public HierarchicalPropertyRecord()
        {
            IsSelected = false;
            DataType = string.Empty;
            RawValue = string.Empty;
            NumericValue = null;
            Unit = string.Empty;
        }

        /// <summary>
        /// 전체 파라미터 생성자
        /// </summary>
        public HierarchicalPropertyRecord(
            Guid objectId,
            Guid parentId,
            int level,
            string displayName,
            string category,
            string propertyName,
            string propertyValue,
            string readWriteStatus = "알 수 없음",
            string sysPath = "",
            string dataType = "",
            string rawValue = "",
            double? numericValue = null,
            string unit = "")
        {
            ObjectId = objectId;
            ParentId = parentId;
            Level = level;
            DisplayName = displayName;
            SysPath = sysPath;
            Category = category;
            PropertyName = propertyName;
            PropertyValue = propertyValue;
            ReadWriteStatus = readWriteStatus;
            DataType = dataType;
            RawValue = rawValue;
            NumericValue = numericValue;
            Unit = unit;
        }
    }
}
