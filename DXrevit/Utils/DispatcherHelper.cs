using System;
using System.Windows.Threading;

namespace DXrevit.Utils
{
    /// <summary>
    /// WPF Dispatcher 헬퍼
    /// </summary>
    public static class DispatcherHelper
    {
        private static Dispatcher _dispatcher;

        /// <summary>
        /// Dispatcher 초기화
        /// </summary>
        public static void Initialize(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        /// <summary>
        /// UI 스레드에서 동기 실행
        /// </summary>
        public static void InvokeOnUIThread(Action action)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// UI 스레드에서 비동기 실행
        /// </summary>
        public static void BeginInvokeOnUIThread(Action action)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }

        /// <summary>
        /// UI 스레드에서 반환값이 있는 동기 실행
        /// </summary>
        public static T InvokeOnUIThread<T>(Func<T> func)
        {
            if (_dispatcher == null || _dispatcher.CheckAccess())
            {
                return func();
            }
            else
            {
                return _dispatcher.Invoke(func);
            }
        }
    }
}
