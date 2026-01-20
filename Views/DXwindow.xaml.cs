using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        /// <summary>
        /// TextBox의 PreviewKeyDown 이벤트 핸들러
        /// Navisworks가 키보드 입력을 가로채는 것을 방지합니다.
        /// 영문/한글 입력이 정상적으로 TextBox에 전달되도록 합니다.
        /// </summary>
        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 특수 키 처리
            switch (e.Key)
            {
                case Key.Enter:
                    // Enter 키: 다음 컨트롤로 포커스 이동
                    textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    e.Handled = true;
                    break;

                case Key.Escape:
                    // Escape 키: 포커스 해제
                    Keyboard.ClearFocus();
                    e.Handled = true;
                    break;

                case Key.Tab:
                    // Tab 키: 기본 동작 허용
                    break;

                default:
                    // Navisworks 단축키 충돌 방지:
                    // 문자, 숫자, 특수문자 키는 모두 TextBox에서 처리되도록
                    // e.Handled = true로 설정하여 Navisworks가 가로채지 못하게 함
                    if (e.Key >= Key.D0 && e.Key <= Key.D9 ||
                        e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9 ||
                        e.Key >= Key.A && e.Key <= Key.Z ||
                        e.Key == Key.Back || e.Key == Key.Delete ||
                        e.Key == Key.Left || e.Key == Key.Right ||
                        e.Key == Key.Home || e.Key == Key.End ||
                        e.Key == Key.OemPeriod || e.Key == Key.OemMinus ||
                        e.Key == Key.Space)
                    {
                        e.Handled = false;  // TextBox가 처리하도록 함
                    }
                    break;
            }
        }

        /// <summary>
        /// TextBox의 GotFocus 이벤트 핸들러
        /// TextBox가 포커스를 받으면 Navisworks 단축키 비활성화
        /// </summary>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                // 전체 텍스트 선택 (편의 기능)
                textBox.SelectAll();
            }
        }
    }
}
