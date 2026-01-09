using System;
using System.ComponentModel;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// ListView에 표시될 속성 항목의 ViewModel
    /// 중간 패널에서 선택된 노드의 속성을 표시하고, 오른쪽 패널에서 검색 세트 생성 시 사용됩니다.
    /// </summary>
    public class PropertyItemViewModel : INotifyPropertyChanged
    {
        private string _category;
        private string _name;
        private string _value;
        private string _unit;

        /// <summary>
        /// 속성 카테고리 (예: "재료", "Element", "Identity Data")
        /// </summary>
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    OnPropertyChanged(nameof(Category));
                }
            }
        }

        /// <summary>
        /// 속성 이름 (예: "이름", "Type Name", "Level")
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// 속성 값 (예: "콘크리트", "Basic Wall", "Level 1")
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        /// <summary>
        /// 단위 (v0.4.2)
        /// </summary>
        public string Unit
        {
            get => _unit;
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    OnPropertyChanged(nameof(Unit));
                }
            }
        }

        public PropertyItemViewModel()
        {
        }

        public PropertyItemViewModel(string category, string name, string value, string unit = "")
        {
            Category = category;
            Name = name;
            Value = value;
            Unit = unit;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Category} | {Name} | {Value}";
        }
    }
}
