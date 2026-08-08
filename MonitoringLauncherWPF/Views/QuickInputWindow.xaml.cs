using System;
using System.Windows;
using System.Windows.Input;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class QuickInputWindow : Window
    {
        public string InputText { get; private set; } = string.Empty;

        public QuickInputWindow(string prompt, string title, string defaultText = "", bool allowKeePass = false)
        {
            InitializeComponent();
            
            Title = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultText;

            if (!allowKeePass)
            {
                KeePassBtn.Visibility = Visibility.Collapsed;
                KeePassColumn.Width = new GridLength(0);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Handled = true;
                Ok_Click(this, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(this, new RoutedEventArgs());
            }
        }

        private void KeePassBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!KeyPassMgr.IsDBLoaded)
            {
                MessageBox.Show("Baza KeePass nie jest otwarta!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var searchWindow = new KeePassSearchWindow(KeePassFieldType.Password) 
            {
                Owner = this
            };

            if (searchWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(searchWindow.SelectedTag))
            {
                InputTextBox.Text = searchWindow.SelectedTag;
                InputTextBox.Focus();
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}