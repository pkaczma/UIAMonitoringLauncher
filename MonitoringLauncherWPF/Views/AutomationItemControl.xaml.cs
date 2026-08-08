using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MonitoringLauncherWPF.Views
{
    public partial class AutomationItemControl : UserControl
    {
        public AutomationItemControl()
        {
            InitializeComponent();
        }

        public bool IsSelected
        {
            get => ItemCheckBox.IsChecked ?? false;
            set => ItemCheckBox.IsChecked = value;
        }

        public bool MatchesSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string title = TitleText.Text ?? string.Empty;
            string desc = DescText.Text ?? string.Empty;
            return title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   desc.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private void ItemCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (MainExpander == null || ItemCheckBox == null) return;
            if (ItemCheckBox.IsChecked == true)
            {
                if (TryFindResource("AccentPrimary") is Brush brush)
                {
                    MainExpander.BorderBrush = brush;
                    MainExpander.BorderThickness = new Thickness(2);
                }
            }
            else
            {
                MainExpander.ClearValue(Expander.BorderBrushProperty);
                MainExpander.ClearValue(Expander.BorderThicknessProperty);
            }
        }

        public void Setup(string icon, string title, string desc, string status, Brush statusFg, Brush statusBg, string actionText, Style actionStyle, string logText, Brush logFg, Brush logBg, Brush logBorder, bool isExpanded = false)
        {
            IconText.Text = icon; TitleText.Text = title; DescText.Text = desc;
            StatusText.Text = status; StatusText.Foreground = statusFg; StatusBadge.Background = statusBg;
            ActionButton.Content = actionText; ActionButton.Style = actionStyle;
            LogTextBox.Text = logText; LogTextBox.Foreground = logFg;
            LogBorder.Background = logBg; LogBorder.BorderBrush = logBorder;
            MainExpander.IsExpanded = isExpanded;
        }

        // --- NOWE METODY DO OBSŁUGI LOGÓW I STATUSU UI ---

        public void AppendLog(string message)
        {
            // Bezpieczne wstrzykiwanie tekstu z wątku w tle do UI
            Dispatcher.InvokeAsync(() => {
                string time = DateTime.Now.ToString("HH:mm:ss");
                LogTextBox.AppendText($"\n[{time}] {message}");
                LogTextBox.ScrollToEnd();
            });
        }

        public void ClearLog()
        {
            Dispatcher.Invoke(() => LogTextBox.Text = "");
        }

        public void SetStatus(string text, Brush fg, Brush bg)
        {
            Dispatcher.Invoke(() => {
                StatusText.Text = text;
                StatusText.Foreground = fg;
                StatusBadge.Background = bg;
            });
        }

        public void SetRunningState(bool isRunning)
        {
            Dispatcher.Invoke(() => {
                if (isRunning)
                {
                    ActionButton.Content = "Stop Execution";
                    ActionButton.Style = (Style)FindResource("DangerButton");
                    MainExpander.IsExpanded = true;
                    SetStatus("● Running...", (Brush)FindResource("RunningColor"), (Brush)FindResource("RunningBg"));
                }
                else
                {
                    ActionButton.Content = "Run Automation";
                    ActionButton.Style = (Style)FindResource("PrimaryButton");
                }
            });
        }
    }
}