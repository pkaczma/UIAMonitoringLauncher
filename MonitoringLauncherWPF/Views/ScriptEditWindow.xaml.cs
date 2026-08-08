using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using AutoLib.Core;
using AutoLib.Models;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class ScriptEditWindow : Window
    {
        private AutomationScript _script;
        private string _originalFilePath;

        public ObservableCollection<AutomationStep> Steps { get; set; }

        public ScriptEditWindow(AutomationScript script, string filePath)
        {
            InitializeComponent();
            
            _script = script;
            _originalFilePath = filePath;
            
            ScriptNameTextBox.Text = script.ScriptName;
            
            Steps = new ObservableCollection<AutomationStep>(script.Steps ?? new List<AutomationStep>());
            StepsListBox.ItemsSource = Steps;

            var firstStep = Steps.FirstOrDefault();
            if (firstStep != null && firstStep.Type == ActionType.StartProcess)
            {
                AppPathTextBox.Text = firstStep.AppPath;
                ArgumentsTextBox.Text = firstStep.Arguments;
            }
            else
            {
                AppPathTextBox.Text = "Brak przypisanego StartProcess. Nie zmieniaj jeżeli nie musisz.";
                AppPathTextBox.IsEnabled = false;
                ArgumentsTextBox.IsEnabled = false;
            }
            
            DataContext = this;
        }

        private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Wybierz plik wykonywalny aplikacji"
            };

            if (openFileDialog.ShowDialog() == true) AppPathTextBox.Text = openFileDialog.FileName;
        }

        private void BrowseKeePass_Click(object sender, RoutedEventArgs e)
        {
            if (!KeyPassMgr.IsDBLoaded)
            {
                MessageBox.Show("Baza KeePass nie jest otwarta!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var searchWindow = new KeePassSearchWindow(KeePassFieldType.Url) { Owner = this };
            if (searchWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(searchWindow.SelectedTag))
            {
                ArgumentsTextBox.Text += $" {searchWindow.SelectedTag}"; 
                ArgumentsTextBox.Text = ArgumentsTextBox.Text.Trim();
            }
        }

        // Czysta edycja używająca StepEditWindow bez uciążliwej kaskady
        private void EditStep_Click(object sender, RoutedEventArgs e)
        {
            if (StepsListBox.SelectedItem is not AutomationStep selectedStep)
            {
                MessageBox.Show("Wybierz najpierw krok z listy do edycji.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editWindow = new StepEditWindow(selectedStep) { Owner = this };
            
            if (editWindow.ShowDialog() == true && editWindow.IsSaved)
            {
                int index = StepsListBox.SelectedIndex;
                Steps.RemoveAt(index);
                Steps.Insert(index, editWindow.Step);
                StepsListBox.SelectedIndex = index;
            }
        }

        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (StepsListBox.SelectedItem is AutomationStep selectedStep)
            {
                Steps.Remove(selectedStep);
            }
            else
            {
                MessageBox.Show("Wybierz krok z listy do usunięcia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = ScriptNameTextBox.Text.Trim();
            
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Nazwa skryptu nie może być pusta.", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _script.ScriptName = newName;
            _script.Steps = new List<AutomationStep>(Steps);

            var firstStep = _script.Steps.FirstOrDefault();
            if (firstStep != null && firstStep.Type == ActionType.StartProcess && AppPathTextBox.IsEnabled)
            {
                firstStep.AppPath = AppPathTextBox.Text;
                firstStep.Arguments = ArgumentsTextBox.Text;
            }

            try
            {
                string safeName = newName.Replace(" ", "_");
                string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations");
                string newDir = Path.Combine(baseDir, safeName);
                string newFileName = $"{safeName}_script.json";
                string newFilePath = Path.Combine(newDir, newFileName);
                
                string oldDir = Path.GetDirectoryName(_originalFilePath);

                if (!string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(newDir))
                    {
                        MessageBox.Show("Skrypt o takiej nazwie już istnieje! Wybierz inną nazwę.", "Konflikt nazw", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    Directory.Move(oldDir, newDir);
                    
                    string oldFileInsideNewDir = Path.Combine(newDir, Path.GetFileName(_originalFilePath));
                    if (File.Exists(oldFileInsideNewDir)) File.Move(oldFileInsideNewDir, newFilePath);
                }
                else if (!string.Equals(_originalFilePath, newFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(_originalFilePath, newFilePath);
                }

                ScriptSerializer.Save(_script, newFilePath);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Logger.Err(this, "Błąd podczas zapisywania struktury edytowanego skryptu.", ex);
                MessageBox.Show("Nie udało się zapisać skryptu. Sprawdź logi.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}