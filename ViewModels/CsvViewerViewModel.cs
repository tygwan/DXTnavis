using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using DXTnavis.Helpers;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// CSV Viewer ViewModel
    /// v0.5.0: CSV 파일 로드 및 표시 기능
    /// </summary>
    public class CsvViewerViewModel : INotifyPropertyChanged
    {
        #region Fields

        private DataTable _csvData;
        private string _loadedFilePath;
        private string _statusMessage;
        private string _filterText;
        private DataView _filteredView;
        private bool _isLoading;

        #endregion

        #region Properties

        /// <summary>
        /// CSV 데이터 테이블
        /// </summary>
        public DataTable CsvData
        {
            get => _csvData;
            set
            {
                _csvData = value;
                OnPropertyChanged(nameof(CsvData));
                OnPropertyChanged(nameof(HasData));
                UpdateFilteredView();
            }
        }

        /// <summary>
        /// 필터링된 DataView
        /// </summary>
        public DataView FilteredView
        {
            get => _filteredView;
            set
            {
                _filteredView = value;
                OnPropertyChanged(nameof(FilteredView));
            }
        }

        /// <summary>
        /// 로드된 파일 경로
        /// </summary>
        public string LoadedFilePath
        {
            get => _loadedFilePath;
            set
            {
                _loadedFilePath = value;
                OnPropertyChanged(nameof(LoadedFilePath));
                OnPropertyChanged(nameof(FileName));
            }
        }

        /// <summary>
        /// 파일 이름만 표시
        /// </summary>
        public string FileName => string.IsNullOrEmpty(LoadedFilePath)
            ? "(No file loaded)"
            : Path.GetFileName(LoadedFilePath);

        /// <summary>
        /// 상태 메시지
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        /// <summary>
        /// 필터 텍스트
        /// </summary>
        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                UpdateFilteredView();
            }
        }

        /// <summary>
        /// 데이터 로드 여부
        /// </summary>
        public bool HasData => CsvData != null && CsvData.Rows.Count > 0;

        /// <summary>
        /// 로딩 중 여부
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        /// <summary>
        /// 컬럼 목록 (필터 드롭다운용)
        /// </summary>
        public ObservableCollection<string> ColumnNames { get; } = new ObservableCollection<string>();

        /// <summary>
        /// 선택된 필터 컬럼
        /// </summary>
        private string _selectedFilterColumn;
        public string SelectedFilterColumn
        {
            get => _selectedFilterColumn;
            set
            {
                _selectedFilterColumn = value;
                OnPropertyChanged(nameof(SelectedFilterColumn));
                UpdateFilteredView();
            }
        }

        #endregion

        #region Commands

        public ICommand LoadCsvCommand { get; }
        public ICommand ClearDataCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ExportFilteredCommand { get; }

        #endregion

        #region Constructor

        public CsvViewerViewModel()
        {
            LoadCsvCommand = new RelayCommand(LoadCsvFile);
            ClearDataCommand = new RelayCommand(ClearData, () => HasData);
            ClearFilterCommand = new RelayCommand(ClearFilter);
            ExportFilteredCommand = new RelayCommand(ExportFiltered, () => HasData);

            StatusMessage = "Click 'Load CSV' to open a file";
        }

        #endregion

        #region Methods

        /// <summary>
        /// CSV 파일 로드
        /// </summary>
        private void LoadCsvFile()
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "CSV 파일|*.csv|모든 파일|*.*",
                DefaultExt = "csv",
                Title = "Select CSV file to load"
            };

            if (openDialog.ShowDialog() != true) return;

            try
            {
                IsLoading = true;
                StatusMessage = "Loading CSV file...";

                var dataTable = ParseCsvFile(openDialog.FileName);

                CsvData = dataTable;
                LoadedFilePath = openDialog.FileName;

                // 컬럼 목록 업데이트
                ColumnNames.Clear();
                ColumnNames.Add("(All Columns)");
                foreach (DataColumn col in dataTable.Columns)
                {
                    ColumnNames.Add(col.ColumnName);
                }
                SelectedFilterColumn = "(All Columns)";

                StatusMessage = $"Loaded: {dataTable.Rows.Count} rows, {dataTable.Columns.Count} columns";

                ((RelayCommand)ClearDataCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExportFilteredCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Failed to load CSV file:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// CSV 파일 파싱
        /// </summary>
        private DataTable ParseCsvFile(string filePath)
        {
            var dataTable = new DataTable();

            // 인코딩 자동 감지
            Encoding encoding = DetectEncoding(filePath);

            using (var reader = new StreamReader(filePath, encoding))
            {
                bool isFirstLine = true;
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // CSV 파싱 (쉼표로 분리, 따옴표 처리)
                    var values = ParseCsvLine(line);

                    if (isFirstLine)
                    {
                        // 헤더 행으로 컬럼 생성
                        foreach (var header in values)
                        {
                            string columnName = string.IsNullOrWhiteSpace(header) ? $"Column{dataTable.Columns.Count + 1}" : header;
                            // 중복 컬럼명 처리
                            if (dataTable.Columns.Contains(columnName))
                            {
                                int suffix = 1;
                                while (dataTable.Columns.Contains($"{columnName}_{suffix}"))
                                    suffix++;
                                columnName = $"{columnName}_{suffix}";
                            }
                            dataTable.Columns.Add(columnName);
                        }
                        isFirstLine = false;
                    }
                    else
                    {
                        // 데이터 행 추가
                        var row = dataTable.NewRow();
                        for (int i = 0; i < Math.Min(values.Length, dataTable.Columns.Count); i++)
                        {
                            row[i] = values[i];
                        }
                        dataTable.Rows.Add(row);
                    }
                }
            }

            return dataTable;
        }

        /// <summary>
        /// CSV 라인 파싱 (따옴표 처리)
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentValue = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 이스케이프된 따옴표
                        currentValue.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            result.Add(currentValue.ToString().Trim());
            return result.ToArray();
        }

        /// <summary>
        /// 파일 인코딩 감지
        /// </summary>
        private Encoding DetectEncoding(string filePath)
        {
            // BOM 확인
            byte[] bom = new byte[4];
            using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                file.Read(bom, 0, 4);
            }

            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
                return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe)
                return Encoding.Unicode;
            if (bom[0] == 0xfe && bom[1] == 0xff)
                return Encoding.BigEndianUnicode;

            // BOM이 없으면 UTF-8 시도, 실패 시 EUC-KR
            try
            {
                using (var reader = new StreamReader(filePath, Encoding.UTF8, true))
                {
                    reader.ReadToEnd();
                    return Encoding.UTF8;
                }
            }
            catch
            {
                return Encoding.GetEncoding("euc-kr");
            }
        }

        /// <summary>
        /// 필터링된 뷰 업데이트
        /// </summary>
        private void UpdateFilteredView()
        {
            if (CsvData == null)
            {
                FilteredView = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(FilterText))
            {
                FilteredView = CsvData.DefaultView;
                StatusMessage = $"Showing all {CsvData.Rows.Count} rows";
                return;
            }

            try
            {
                string filterExpression;
                string searchText = FilterText.Replace("'", "''"); // SQL injection 방지

                if (SelectedFilterColumn == "(All Columns)" || string.IsNullOrEmpty(SelectedFilterColumn))
                {
                    // 모든 컬럼에서 검색
                    var conditions = new List<string>();
                    foreach (DataColumn col in CsvData.Columns)
                    {
                        conditions.Add($"CONVERT([{col.ColumnName}], System.String) LIKE '%{searchText}%'");
                    }
                    filterExpression = string.Join(" OR ", conditions);
                }
                else
                {
                    // 특정 컬럼에서만 검색
                    filterExpression = $"CONVERT([{SelectedFilterColumn}], System.String) LIKE '%{searchText}%'";
                }

                CsvData.DefaultView.RowFilter = filterExpression;
                FilteredView = CsvData.DefaultView;
                StatusMessage = $"Filtered: {FilteredView.Count} / {CsvData.Rows.Count} rows";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Filter error: {ex.Message}";
                FilteredView = CsvData.DefaultView;
            }
        }

        /// <summary>
        /// 데이터 초기화
        /// </summary>
        private void ClearData()
        {
            CsvData = null;
            LoadedFilePath = null;
            FilterText = string.Empty;
            ColumnNames.Clear();
            StatusMessage = "Data cleared. Click 'Load CSV' to open a new file";

            ((RelayCommand)ClearDataCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ExportFilteredCommand).RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 필터 초기화
        /// </summary>
        private void ClearFilter()
        {
            FilterText = string.Empty;
            SelectedFilterColumn = "(All Columns)";
        }

        /// <summary>
        /// 필터링된 데이터 내보내기
        /// </summary>
        private void ExportFiltered()
        {
            if (FilteredView == null || FilteredView.Count == 0)
            {
                System.Windows.MessageBox.Show("No data to export.", "Export",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV 파일|*.csv",
                DefaultExt = "csv",
                FileName = $"Filtered_{Path.GetFileNameWithoutExtension(LoadedFilePath)}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                using (var writer = new StreamWriter(saveDialog.FileName, false, Encoding.UTF8))
                {
                    // 헤더
                    var headers = new List<string>();
                    foreach (DataColumn col in CsvData.Columns)
                    {
                        headers.Add($"\"{col.ColumnName}\"");
                    }
                    writer.WriteLine(string.Join(",", headers));

                    // 데이터
                    foreach (DataRowView row in FilteredView)
                    {
                        var values = new List<string>();
                        foreach (var item in row.Row.ItemArray)
                        {
                            string value = item?.ToString() ?? "";
                            values.Add($"\"{value.Replace("\"", "\"\"")}\"");
                        }
                        writer.WriteLine(string.Join(",", values));
                    }
                }

                StatusMessage = $"Exported {FilteredView.Count} rows to {Path.GetFileName(saveDialog.FileName)}";
                System.Windows.MessageBox.Show(
                    $"Exported successfully!\n\n{FilteredView.Count} rows saved.",
                    "Export Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export error: {ex.Message}";
                System.Windows.MessageBox.Show(
                    $"Export failed:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
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
