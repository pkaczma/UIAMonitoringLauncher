using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using Microsoft.Win32;
using AutoLib.Core;
using AutoLib.Models;
using MonitoringLauncherWPF.Core;
using System.Collections.Generic;

namespace MonitoringLauncherWPF.Views
{
    public partial class RecordingWindow : Window
    {
        private AutomationScript _currentScript;
        private AutomationRecorder _recorder;
        private AutomationPlayer _player;
        private AutomationElement _currentWindow = null;
        private bool _isCancelled = false;
        private bool _isRecording = false;

        public ObservableCollection<AutomationStep> RecordedSteps { get; set; } = new ObservableCollection<AutomationStep>();

        public RecordingWindow()
        {
            InitializeComponent();
            DataContext = this;
            StepsListBox.ItemsSource = RecordedSteps;
            Logger.Info(this, "Zainicjowano okno RecordingWindow.");
        }

        #region Obsługa UI

        private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Anulowano nagrywanie przez użytkownika. Zapis zostanie zablokowany.");
            _isCancelled = true;
            _isRecording = false;
            Close();
        }

        private void AppTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BrowserUrlPanel == null || ExternalAppPanel == null) return;

            bool isBrowser = AppTypeComboBox.SelectedIndex == 0;
            BrowserUrlPanel.Visibility = isBrowser ? Visibility.Visible : Visibility.Collapsed;
            ExternalAppPanel.Visibility = !isBrowser ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BrowseApp_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Wybierz plik wykonywalny aplikacji"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                AppPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void BrowseKeePassUrl_Click(object sender, RoutedEventArgs e)
        {
            if (!KeyPassMgr.IsDBLoaded)
            {
                MessageBox.Show("Baza KeePass nie jest otwarta! Zaloguj się najpierw do bazy głównej.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var searchWindow = new KeePassSearchWindow(KeePassFieldType.Url) { Owner = this };
            if (searchWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(searchWindow.SelectedTag))
            {
                TargetUrlTextBox.Text = searchWindow.SelectedTag; 
            }
        }

        #endregion

        #region Logika Nagrywania

        private async void StartStopRecording_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                bool initSuccess = await TryInitializeRecordingSessionAsync();
                if (!initSuccess) return;

                StartRecordingUI();
                _isRecording = true;
                await RunRecordingLoopAsync();
            }
            else
            {
                _isRecording = false; 
            }
        }

        private async Task<bool> TryInitializeRecordingSessionAsync()
        {
            string scriptName = ScriptNameTextBox.Text.Trim();
            string appPath = "";
            string arguments = "";

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                MessageBox.Show("Podaj nazwę skryptu przed rozpoczęciem nagrywania!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string safeName = scriptName.Replace(" ", "_");
            string expectedScriptDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations", safeName);
            string expectedPath = Path.Combine(expectedScriptDir, $"{safeName}_script.json");

            if (File.Exists(expectedPath))
            {
                MessageBox.Show($"Skrypt o nazwie '{scriptName}' już istnieje.\nProszę podać inną nazwę.", "Konflikt nazw", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            _isCancelled = false;

            if (AppTypeComboBox.SelectedIndex == 0) 
            {
                string url = TargetUrlTextBox.Text;
                appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe");
                if (!File.Exists(appPath)) appPath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";

                var browserArgs = new List<string>();
                if (ChkInPrivate.IsChecked == true) browserArgs.Add("-inprivate");
                if (ChkForceAccessibility.IsChecked == true) browserArgs.Add("--force-renderer-accessibility");
                if (ChkNewWindow.IsChecked == true) browserArgs.Add("--new-window");
                if (!string.IsNullOrWhiteSpace(ArgumentsTextBox.Text)) browserArgs.Add(ArgumentsTextBox.Text.Trim());
                
                browserArgs.Add($"\"{url}\"");
                arguments = string.Join(" ", browserArgs);
            }
            else 
            {
                appPath = AppPathTextBox.Text;
                arguments = ArgumentsTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath))
                {
                    MessageBox.Show("Podaj poprawną ścieżkę do pliku .exe aplikacji!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            _currentScript = new AutomationScript { ScriptName = scriptName };
            _recorder = new AutomationRecorder();
            _player = new AutomationPlayer
            {
                OnValueResolve = (val) => 
                {
                    if (!string.IsNullOrEmpty(val) && val.StartsWith("KEEPASS:"))
                    {
                        int firstColon = val.IndexOf(':'); 
                        int lastColon = val.LastIndexOf(':'); 
                        if (firstColon != -1 && lastColon != -1 && firstColon != lastColon)
                        {
                            string path = val.Substring(firstColon + 1, lastColon - firstColon - 1);
                            string type = val.Substring(lastColon + 1);
                            try
                            {
                                if (type == "Username") return KeyPassMgr.GetEntryUsername(path);
                                if (type == "Password") return KeyPassMgr.GetEntryPassword(path);
                                if (type == "Url") return KeyPassMgr.GetEntryUrl(path);
                            }
                            catch { return string.Empty; }
                        }
                    }
                    return val;
                }
            };

            _currentWindow = null;
            RecordedSteps.Clear();

            var startStep = new AutomationStep
            {
                Type = ActionType.StartProcess,
                AppPath = appPath,
                Arguments = arguments,
                DelayBeforeMs = ConfigManager.Current.DefaultStepDelayMs, 
                ElementName = "Start Application"
            };

            _currentScript.Steps.Add(startStep);
            RecordedSteps.Add(startStep);

            AutomationElement tempWindow = null;
            bool startSuccess = await Task.Run(() => 
            {
                try { return _player.ExecuteSingleStep(startStep, ref tempWindow); }
                catch { return false; }
            });

            _currentWindow = tempWindow;

            if (!startSuccess)
            {
                MessageBox.Show("Nie udało się uruchomić aplikacji startowej!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private void StartRecordingUI()
        {
            StartStopRecordingButton.Content = "Stop & Save Recording";
            StartStopRecordingButton.Style = (Style)FindResource("DangerButton");
            
            ScriptNameTextBox.IsEnabled = false;
            ScriptDescTextBox.IsEnabled = false;
            AppTypeComboBox.IsEnabled = false;
            BrowserUrlPanel.IsEnabled = false;
            ExternalAppPanel.IsEnabled = false;
            ArgumentsTextBox.IsEnabled = false;
            
            ChkInPrivate.IsEnabled = false;
            ChkForceAccessibility.IsEnabled = false;
            ChkNewWindow.IsEnabled = false;
        }

        private async Task RunRecordingLoopAsync()
        {
            try
            {
                while (_isRecording)
                {
                    var element = UiaHelper.GetElementUnderCursor();

                    if (InputCaptureHelper.IsShortcutPressed(InputCaptureHelper.VK_1)) await HandleRecordClickAsync(element);
                    else if (InputCaptureHelper.IsShortcutPressed(InputCaptureHelper.VK_2)) await HandleRecordTypeTextAsync(element);
                    else if (InputCaptureHelper.IsShortcutPressed(InputCaptureHelper.VK_3)) await HandleRecordTextClickAsync();
                    else if (InputCaptureHelper.IsShortcutPressed(InputCaptureHelper.VK_4)) await HandleRecordWindowBoundsAsync();
                    else if (InputCaptureHelper.IsKeyPressed(InputCaptureHelper.VK_ESCAPE)) break;

                    await Task.Delay(50); 
                }
            }
            finally
            {
                FinalizeRecording();
            }
        }
        #endregion

        #region Moduły Akcji Nagrywania

        private async Task HandleRecordClickAsync(AutomationElement element)
        {
            var step = _recorder.RecordClick(element);
            if (step != null)
            {
                step.DelayBeforeMs = ConfigManager.Current.DefaultStepDelayMs;
                _player.ExecuteSingleStep(step, ref _currentWindow);
                AddStepToUI(step);
            }
            await Task.Delay(800); 
        }
        
        private async Task HandleRecordTypeTextAsync(AutomationElement element)
        {
            string textToType = DialogHelper.PromptForInput("Wprowadź tekst (lub wyciągnij wartość z KeePass):", "Wpisz tekst (Ctrl+Shift+2)", "", true);
            if (!string.IsNullOrEmpty(textToType))
            {
                var step = _recorder.RecordTypeText(element, textToType);
                if (step != null)
                {
                    step.DelayBeforeMs = ConfigManager.Current.DefaultStepDelayMs;
                    _player.ExecuteSingleStep(step, ref _currentWindow);
                    AddStepToUI(step);
                }
            }
            await Task.Delay(800);
        }
        
        private async Task HandleRecordTextClickAsync()
        {
            string searchText = DialogHelper.PromptForInput("Podaj dokładny tekst elementu (np. widoczny tekst przycisku):", "Kliknięcie po tekście", "", false);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var step = _recorder.RecordTextClick(searchText);
                if (step != null)
                {
                    step.DelayBeforeMs = ConfigManager.Current.DefaultStepDelayMs;
                    _player.ExecuteSingleStep(step, ref _currentWindow);
                    AddStepToUI(step);
                }
            }
            await Task.Delay(800);
        }
        
        private async Task HandleRecordWindowBoundsAsync()
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                var step = _recorder.RecordWindowOperation(hwnd);
                if (step != null)
                {
                    step.DelayBeforeMs = ConfigManager.Current.DefaultStepDelayMs;
                    _player.ExecuteSingleStep(step, ref _currentWindow);
                    AddStepToUI(step);
                }
            }
            await Task.Delay(800);
        }
        
        private void AddStepToUI(AutomationStep step)
        {
            Dispatcher.Invoke(() => { 
                RecordedSteps.Add(step); 
                StepsListBox.ScrollIntoView(step); 
            });
            _currentScript.Steps.Add(step);
        }
        
        #endregion

        #region Narzędzia i Modyfikacja Kroków

        // NOWA, CZYSTA WERSJA EDYCJI KROKÓW (Problem 2 zażegnany)
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
                RecordedSteps.RemoveAt(index);
                RecordedSteps.Insert(index, editWindow.Step);
                _currentScript.Steps[index] = editWindow.Step; // Bezpośrednia modyfikacja modelu głównego
                StepsListBox.SelectedIndex = index;
                
                Logger.Info(this, $"Krok na indeksie {index} został zaktualizowany.");
            }
        }

        private void DeleteStep_Click(object sender, RoutedEventArgs e)
        {
            if (StepsListBox.SelectedItem is AutomationStep selectedStep)
            {
                RecordedSteps.Remove(selectedStep);
                _currentScript.Steps.Remove(selectedStep);
            }
            else
            {
                MessageBox.Show("Wybierz krok z listy do usunięcia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void FinalizeRecording()
        {
            _isRecording = false;
        
            if (_isCancelled) return; 
        
            if (_currentScript != null && _currentScript.Steps.Count > 1)
            {
                try 
                {
                    string safeName = _currentScript.ScriptName.Replace(" ", "_");
                    string baseAutomationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations");
                    string specificScriptDir = Path.Combine(baseAutomationsDir, safeName);
                    
                    if (!Directory.Exists(specificScriptDir)) Directory.CreateDirectory(specificScriptDir);
                    
                    string savePath = Path.Combine(specificScriptDir, $"{safeName}_script.json");
                    
                    ScriptSerializer.Save(_currentScript, savePath);
                    Dispatcher.Invoke(() => MessageBox.Show($"Nagrywanie zakończone sukcesem!\nZapisano do pliku:\n{savePath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information));
                }
                catch (Exception ex)
                {
                    Logger.Err(this, "Błąd podczas próby zapisu skryptu JSON.", ex);
                    Dispatcher.Invoke(() => MessageBox.Show($"Błąd podczas zapisu pliku konfiguracyjnego skryptu.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error));
                }
            }
        
            Dispatcher.Invoke(() =>
            {
                StartStopRecordingButton.Content = "Start Live Recording";
                StartStopRecordingButton.Style = (Style)FindResource("PrimaryButton");
                ScriptNameTextBox.IsEnabled = true;
                ScriptDescTextBox.IsEnabled = true;
                AppTypeComboBox.IsEnabled = true;
                BrowserUrlPanel.IsEnabled = true;
                ExternalAppPanel.IsEnabled = true;
                ArgumentsTextBox.IsEnabled = true;
                ChkInPrivate.IsEnabled = true;
                ChkForceAccessibility.IsEnabled = true;
                ChkNewWindow.IsEnabled = true;
            });
        }

        #endregion
    }
}