using System;
using System.Windows;
using DXTnavis.ViewModels;

namespace DXTnavis.Views
{
    /// <summary>
    /// DXwindow.xaml에 대한 상호 작용 논리
    /// WPF 창으로서 속성 목록을 표시하고 사용자 입력을 처리합니다.
    /// </summary>
    public partial class DXwindow : Window
    {
        private readonly DXwindowViewModel _viewModel;

        public DXwindow()
        {
            // XAML UI 초기화 (필수!)
            InitializeComponent();

            // ViewModel 초기화 및 DataContext 설정
            _viewModel = new DXwindowViewModel();
            DataContext = _viewModel;

            // 창이 로드될 때 Navisworks 이벤트 구독 시작
            Loaded += OnWindowLoaded;

            // 창이 닫힐 때 리소스 정리
            Closing += OnWindowClosing;
        }

        /// <summary>
        /// 창이 로드될 때 호출되는 이벤트 핸들러
        /// Navisworks 선택 변경 이벤트 구독을 시작합니다.
        /// </summary>
        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.StartMonitoring();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Navisworks 모니터링 시작 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 창이 닫힐 때 호출되는 이벤트 핸들러
        /// 메모리 누수 방지를 위해 이벤트 구독을 취소합니다.
        /// </summary>
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _viewModel.StopMonitoring();
                _viewModel.Dispose();
            }
            catch (Exception ex)
            {
                // 정리 중 오류가 발생해도 창은 닫히도록 함
                System.Diagnostics.Debug.WriteLine($"정리 중 오류: {ex.Message}");
            }
        }
    }
}
