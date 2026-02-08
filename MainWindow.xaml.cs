using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Text;
using System.Windows.Input;

namespace LogViewer
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private LogReader _logReader;
        private string _filterText;
        private string[] _excludeTerms;
        private ICollectionView _filteredLogsView;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Set Icon safely
            try
            {
                var iconUri = new Uri("pack://application:,,,/icon.png", UriKind.RelativeOrAbsolute);
                this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
            }

            // CHANGE THIS PATH TO YOUR ACTUAL LOG FILE PATH FOR TESTING
            string logPath = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of Honor II\BepInEx\LogOutput.log";
            
            // Fallback for testing if file doesn't exist
            if (!System.IO.File.Exists(logPath))
            {
                 // Create a dummy file for testing if the real one isn exists
                 string dummyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_log.txt");
                 if(!System.IO.File.Exists(dummyPath)) System.IO.File.WriteAllText(dummyPath, "Log Viewer Started\n");
                 logPath = dummyPath;
            }

            _logReader = new LogReader(logPath);
            FilteredLogs = CollectionViewSource.GetDefaultView(_logReader.LogEntries);
            FilteredLogs.Filter = FilterLog;
            
            // Auto scroll
            _logReader.LogEntries.CollectionChanged += (s, e) => 
            {
                if (e.NewItems != null && e.NewItems.Count > 0)
                {
                    LogListBox.ScrollIntoView(e.NewItems[e.NewItems.Count - 1]);
                }
            };

            LoadSettings();
        }

        public ICollectionView FilteredLogs
        {
            get { return _filteredLogsView; }
            set { _filteredLogsView = value; OnPropertyChanged(); }
        }

        private bool FilterLog(object item)
        {
            var log = item as LogEntry;
            if (log == null) return false;

            // Exclude filter (Check this first)
            if (_excludeTerms != null && _excludeTerms.Length > 0)
            {
                foreach (var term in _excludeTerms)
                {
                    if (!string.IsNullOrWhiteSpace(term) && log.Content.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return false; // Found an excluded term
                    }
                }
            }

            // Include filter
            if (string.IsNullOrEmpty(_filterText)) return true;
            
            return log.Content.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void FilterTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            _filterText = FilterTextBox.Text;
            FilteredLogs?.Refresh();
            SaveSettings();
        }

        private void ExcludeTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            string text = ExcludeTextBox.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _excludeTerms = null;
            }
            else
            {
                _excludeTerms = text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .Where(t => !string.IsNullOrEmpty(t))
                                    .ToArray();
            }
            FilteredLogs?.Refresh();
            SaveSettings();
        }

        private void StripPrefixTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // Just refresh to trigger the converter update
            // Check for null because this event can fire during InitializeComponent before LogListBox is created
            if (LogListBox != null)
            {
                LogListBox.Items.Refresh();
            }
            SaveSettings();
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            SaveSettings();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            try
            {
                string settingsPath = GetSettingsPath();
                if (System.IO.File.Exists(settingsPath))
                {
                    string json = System.IO.File.ReadAllText(settingsPath);
                    var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        FilterTextBox.Text = settings.FilterText;
                        ExcludeTextBox.Text = settings.ExcludeText;
                        StripPrefixTextBox.Text = settings.StripPrefix;

                        if (settings.WindowWidth > 0) this.Width = settings.WindowWidth;
                        if (settings.WindowHeight > 0) this.Height = settings.WindowHeight;
                        this.Top = settings.WindowTop;
                        this.Left = settings.WindowLeft;
                        this.WindowState = settings.IsMaximized ? WindowState.Maximized : WindowState.Normal;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            // Only save if initialized and window is not minimized
            if (FilterTextBox == null || ExcludeTextBox == null || StripPrefixTextBox == null || this.WindowState == WindowState.Minimized) return;

            try
            {
                var settings = new AppSettings
                {
                    FilterText = FilterTextBox.Text,
                    ExcludeText = ExcludeTextBox.Text,
                    StripPrefix = StripPrefixTextBox.Text,
                    WindowTop = this.WindowState == WindowState.Maximized ? this.RestoreBounds.Top : this.Top,
                    WindowLeft = this.WindowState == WindowState.Maximized ? this.RestoreBounds.Left : this.Left,
                    WindowWidth = this.WindowState == WindowState.Maximized ? this.RestoreBounds.Width : this.Width,
                    WindowHeight = this.WindowState == WindowState.Maximized ? this.RestoreBounds.Height : this.Height,
                    IsMaximized = this.WindowState == WindowState.Maximized
                };
                string json = System.Text.Json.JsonSerializer.Serialize(settings);
                System.IO.File.WriteAllText(GetSettingsPath(), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
            }
        }

        private string GetSettingsPath()
        {
            return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        public class AppSettings
        {
            public string FilterText { get; set; } = string.Empty;
            public string ExcludeText { get; set; } = string.Empty;
            public string StripPrefix { get; set; } = string.Empty;
            public double WindowTop { get; set; } = 100;
            public double WindowLeft { get; set; } = 100;
            public double WindowWidth { get; set; } = 800;
            public double WindowHeight { get; set; } = 600;
            public bool IsMaximized { get; set; } = false;
        }

        private void LogListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (LogListBox.SelectedItems.Count > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    var selectedOrdered = LogListBox.SelectedItems.Cast<LogEntry>()
                                           .OrderBy(item => LogListBox.Items.IndexOf(item));

                    foreach (var logEntry in selectedOrdered)
                    {
                        sb.AppendLine(logEntry.Content);
                    }
                    
                    try
                    {
                        Clipboard.SetText(sb.ToString().TrimEnd('\r', '\n'));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}