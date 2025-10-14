using System;

namespace DXrevit.Utils
{
    /// <summary>
    /// 진행률 보고 인터페이스
    /// </summary>
    public interface IProgressReporter
    {
        /// <summary>
        /// 진행률 업데이트 (0-100)
        /// </summary>
        void ReportProgress(int percentage, string message);
    }

    /// <summary>
    /// 진행률 보고 구현
    /// </summary>
    public class ProgressReporter : IProgressReporter
    {
        private readonly Action<int, string> _progressCallback;

        public ProgressReporter(Action<int, string> progressCallback)
        {
            _progressCallback = progressCallback ?? throw new ArgumentNullException(nameof(progressCallback));
        }

        public void ReportProgress(int percentage, string message)
        {
            // UI 스레드로 마샬링
            DispatcherHelper.BeginInvokeOnUIThread(() =>
            {
                _progressCallback(percentage, message);
            });
        }
    }
}
