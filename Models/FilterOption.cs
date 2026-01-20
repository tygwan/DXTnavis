using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// Phase 12: 필터 옵션 모델 (체크박스 기반)
    /// Level, Category 등 필터 항목을 체크박스로 표시
    /// </summary>
    public class FilterOption : INotifyPropertyChanged
    {
        #region Private Fields

        private bool _isChecked;
        private int _count;
        private bool _isEnabled;

        #endregion

        #region Properties

        /// <summary>
        /// 필터 옵션 이름 (예: "L0", "Item", "Dimensions")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 필터 옵션 값 (Name과 다를 경우 사용, 예: Level=0)
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 해당 항목 개수 (UI에 표시)
        /// </summary>
        public int Count
        {
            get => _count;
            set
            {
                if (_count != value)
                {
                    _count = value;
                    OnPropertyChanged(nameof(Count));
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        /// <summary>
        /// 체크 상태
        /// </summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(nameof(IsChecked));
                }
            }
        }

        /// <summary>
        /// 활성화 상태 (Count > 0일 때만 활성화)
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged(nameof(IsEnabled));
                }
            }
        }

        #endregion

        #region Computed Properties

        /// <summary>
        /// UI 표시용 텍스트 (이름 + 개수)
        /// </summary>
        public string DisplayText => $"{Name} ({Count:N0})";

        /// <summary>
        /// 툴팁 텍스트
        /// </summary>
        public string ToolTip => $"{Name}: {Count:N0} items";

        #endregion

        #region Constructor

        public FilterOption()
        {
            IsChecked = true;  // 기본값: 체크됨 (모든 항목 표시)
            IsEnabled = true;
            Count = 0;
        }

        public FilterOption(string name, int count = 0, bool isChecked = true)
            : this()
        {
            Name = name;
            Value = name;
            Count = count;
            IsChecked = isChecked;
            IsEnabled = count > 0;
        }

        public FilterOption(string name, object value, int count = 0, bool isChecked = true)
            : this()
        {
            Name = name;
            Value = value;
            Count = count;
            IsChecked = isChecked;
            IsEnabled = count > 0;
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
