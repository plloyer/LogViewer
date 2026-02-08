using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;

namespace LogViewer
{
    public class LogEntry
    {
        public string Content { get; set; } = string.Empty;
    }

    public class LogReader
    {
        private string _filePath;
        private long _lastPosition = 0;
        private FileSystemWatcher _watcher;
        private Dispatcher _uiDispatcher;
        private DispatcherTimer _pollTimer;

        public ObservableCollection<LogEntry> LogEntries { get; private set; } = new ObservableCollection<LogEntry>();

        public LogReader(string filePath)
        {
            _filePath = filePath;
            _uiDispatcher = Dispatcher.CurrentDispatcher;

            if (File.Exists(_filePath))
            {
                ReadFile();
            }

            SetupWatcher();
            SetupPolling();
        }

        private void SetupPolling()
        {
            _pollTimer = new DispatcherTimer();
            _pollTimer.Interval = TimeSpan.FromMilliseconds(100);
            _pollTimer.Tick += (s, e) => ReadFile();
            _pollTimer.Start();
        }

        private void SetupWatcher()
        {
            string directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            string fileName = Path.GetFileName(_filePath);

            _watcher = new FileSystemWatcher(directory);
            _watcher.Filter = fileName;
            _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime;
            _watcher.Changed += OnChanged;
            _watcher.Created += OnChanged;
            _watcher.Deleted += OnChanged;
            _watcher.EnableRaisingEvents = true;
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
             ReadFile();
        }

        private void ReadFile()
        {
            if (!lockObj_Acquired()) return;

            try
            {
                if (!File.Exists(_filePath))
                {
                    if (_lastPosition > 0)
                    {
                        Console.WriteLine($"[LogReader] File deleted/missing: {_filePath}");
                        _lastPosition = 0;
                        RunOnUiThread(() => LogEntries.Clear());
                    }
                    return;
                }

                // Use FileShare.ReadWrite to allow other processes to write to the file
                using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.Length < _lastPosition)
                    {
                        Console.WriteLine($"[LogReader] File truncated: OldPos={_lastPosition}, NewLen={fs.Length}");
                        _lastPosition = 0;
                        RunOnUiThread(() => LogEntries.Clear());
                    }

                    if (fs.Length == _lastPosition)
                    {
                        return; // No new data
                    }

                    fs.Seek(_lastPosition, SeekOrigin.Begin);

                    var newEntries = new System.Collections.Generic.List<LogEntry>();
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            if (line != null)
                            {
                                newEntries.Add(new LogEntry { Content = line });
                            }
                        }
                        _lastPosition = fs.Position;
                    }

                    if (newEntries.Count > 0)
                    {
                        RunOnUiThread(() => 
                        {
                            foreach (var entry in newEntries)
                            {
                                LogEntries.Add(entry);
                            }
                        });
                    }
                }
            }
            catch (IOException)
            {
                // File might be used by another process, will retry next poll or event
            }
            catch (Exception ex)
            {
                 Console.WriteLine($"[LogReader] Error reading file: {ex.Message}");
            }
            finally
            {
                System.Threading.Monitor.Exit(_lockObj);
            }
        }

        private bool lockObj_Acquired()
        {
            return System.Threading.Monitor.TryEnter(_lockObj);
        }

        private object _lockObj = new object();

        private void RunOnUiThread(Action action)
        {
            if (_uiDispatcher != null)
            {
                if (_uiDispatcher.CheckAccess())
                    action();
                else
                    _uiDispatcher.BeginInvoke(action, DispatcherPriority.Normal);
            }
            else
            {
                action();
            }
        }
    }
}
