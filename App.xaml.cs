using System;
using System.IO;
using System.Windows;

namespace LogViewer
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LogException(e.Exception);
            e.Handled = true; // Prevent crash if possible
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogException(e.ExceptionObject as Exception);
        }

        private void LogException(Exception ex)
        {
            if (ex == null) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{DateTime.Now}: {ex.Message}");
                sb.AppendLine(ex.StackTrace);
                
                var inner = ex.InnerException;
                while (inner != null)
                {
                    sb.AppendLine("--- Inner Exception ---");
                    sb.AppendLine(inner.Message);
                    sb.AppendLine(inner.StackTrace);
                    inner = inner.InnerException;
                }
                sb.AppendLine();
                sb.AppendLine();

                File.AppendAllText("crash.log", sb.ToString());
                MessageBox.Show($"Application Error: {ex.Message}\nCheck crash.log for details.", "Error");
            }
            catch { }
        }
    }
}
