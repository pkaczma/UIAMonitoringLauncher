using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public enum KeePassFieldType
    {
        Url = 0,
        Username = 1,
        Password = 2
    }

    public partial class KeePassSearchWindow : Window
    {
        private string[] _allPaths = Array.Empty<string>();

        public string SelectedPath { get; private set; }
        public string SelectedValue { get; private set; }
        public string SelectedTag { get; private set; } // Zmienna na dynamiczny TAG

        public KeePassSearchWindow(KeePassFieldType defaultMode)
        {
            InitializeComponent();
            FieldTypeComboBox.SelectedIndex = (int)defaultMode;
            LoadEntries();
        }

        private void LoadEntries()
        {
            if (!KeyPassMgr.IsDBLoaded)
            {
                MessageBox.Show("Baza KeePass nie jest załadowana.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Używamy nowej metody zwracającej formę "Grupa/Tytuł"
                _allPaths = KeyPassMgr.GetAllEntryPaths();
                FilterList();
                SearchTextBox.Focus();
            }
            catch (Exception ex)
            {
                Logger.Err(this, "Błąd podczas ładowania wpisów KeePass do wyszukiwarki.", ex);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => FilterList();

        private void FilterList()
        {
            var query = SearchTextBox.Text.ToLower();
            var filtered = string.IsNullOrWhiteSpace(query)
                ? _allPaths
                : _allPaths.Where(t => t.ToLower().Contains(query)).ToArray();

            EntriesListBox.ItemsSource = filtered;
        }

        private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e) => ConfirmSelection();

        private void EntriesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (EntriesListBox.SelectedItem != null) ConfirmSelection();
        }

        private void EntriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void FieldTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void PreviewCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (PreviewCheckBox == null || PreviewTextBlock == null) return;

            if (PreviewCheckBox.IsChecked != true)
            {
                PreviewTextBlock.Visibility = Visibility.Collapsed;
                return;
            }

            PreviewTextBlock.Visibility = Visibility.Visible;

            if (EntriesListBox.SelectedItem is not string path)
            {
                PreviewTextBlock.Text = "Wybierz wpis z listy...";
                PreviewTextBlock.Foreground = (Brush)FindResource("TextSecondary");
                return;
            }

            try
            {
                var mode = (KeePassFieldType)FieldTypeComboBox.SelectedIndex;
                string val = mode switch
                {
                    KeePassFieldType.Url => KeyPassMgr.GetEntryUrl(path),
                    KeePassFieldType.Username => KeyPassMgr.GetEntryUsername(path),
                    KeePassFieldType.Password => KeyPassMgr.GetEntryPassword(path),
                    _ => string.Empty
                };

                PreviewTextBlock.Text = string.IsNullOrEmpty(val) ? "<puste>" : val;
                PreviewTextBlock.Foreground = (Brush)FindResource("SuccessColor");
            }
            catch (Exception)
            {
                PreviewTextBlock.Text = "<błąd odczytu>";
                PreviewTextBlock.Foreground = (Brush)FindResource("DangerColor");
            }
        }

        private void ConfirmSelection()
        {
            if (EntriesListBox.SelectedItem is not string path)
            {
                MessageBox.Show("Wybierz wpis z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SelectedPath = path;
            var mode = (KeePassFieldType)FieldTypeComboBox.SelectedIndex;

            // ZBUDOWANIE TAGU do wykorzystania w nagrywarce i odtwarzaczu
            SelectedTag = $"KEEPASS:{SelectedPath}:{mode}";

            try
            {
                // Pobieramy prawdziwą wartość (tylko w celu wstrzyknięcia gotowego URL w polu Target URL)
                SelectedValue = mode switch
                {
                    KeePassFieldType.Url => KeyPassMgr.GetEntryUrl(SelectedPath),
                    KeePassFieldType.Username => KeyPassMgr.GetEntryUsername(SelectedPath),
                    KeePassFieldType.Password => KeyPassMgr.GetEntryPassword(SelectedPath),
                    _ => string.Empty
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Err(this, $"Błąd podczas pobierania wartości KeePass dla wpisu: {path}", ex);
                MessageBox.Show("Nie udało się pobrać wybranej wartości ze wskazanego wpisu.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}