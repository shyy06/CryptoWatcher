using System;
using System.Diagnostics;
using System.Windows;
using CryptoWatcher.Services;

namespace CryptoWatcher
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局兜底：任何未处理异常都写日志 + 提示，而不是静默闪退
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
                LogFatal(args.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                LogFatal(args.Exception);
                args.SetObserved();
            };

            DispatcherUnhandledException += (sender, args) =>
            {
                LogFatal(args.Exception);
                try
                {
                    MessageBox.Show(
                        "发生未处理的错误：\n\n" + args.Exception.Message
                        + "\n\n详细信息已写入：\n" + AppPaths.ErrorLogFile,
                        "CryptoWatcher",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[App] 显示错误提示失败: " + ex.Message);
                }
                args.Handled = true;
            };
        }

        private static void LogFatal(Exception ex)
        {
            if (ex == null) return;

            try
            {
                AppPaths.EnsureDataDir();
                string text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] "
                              + ex + Environment.NewLine + Environment.NewLine;
                File.AppendAllText(AppPaths.ErrorLogFile, text);
            }
            catch (Exception logEx)
            {
                Debug.WriteLine("[App] 写错误日志失败: " + logEx.Message);
            }

            Debug.WriteLine("[App] 未处理异常: " + ex);
        }
    }
}
