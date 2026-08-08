using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AutoLib.Core;
using AutoLib.Models;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class AutomationsTab : UserControl
    {
        public AutomationsTab()
        {
            InitializeComponent();
            this.Loaded += AutomationsTab_Loaded;
        }

        private void AutomationsTab_Loaded(object sender, RoutedEventArgs e)
        {
            LoadScriptsFromDisk();
        }

        public void AddAutomationItem(AutomationItemControl item)
        {
            AutomationsList.Children.Add(item);
        }

        private void LoadScriptsFromDisk()
        {
            AutomationsList.Children.Clear();
            string baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Automations");
            
            if (!Directory.Exists(baseDir))
            {
                if (NoResultsText != null) NoResultsText.Visibility = Visibility.Visible;
                return;
            }

            string[] scriptFiles;
            try
            {
                scriptFiles = Directory.GetFiles(baseDir, "*_script.json", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                Logger.Err(this, "Błąd odczytu folderu ze skryptami.", ex);
                return;
            }
            
            if (scriptFiles.Length == 0)
            {
                if (NoResultsText != null) NoResultsText.Visibility = Visibility.Visible;
                return;
            }

            if (NoResultsText != null) NoResultsText.Visibility = Visibility.Collapsed;
            var converter = new BrushConverter();

            foreach (var file in scriptFiles)
            {
                try
                {
                    AutomationScript script = ScriptSerializer.Load(file);
                    if (script != null)
                    {
                        var item = new AutomationItemControl();
                        int stepsCount = script.Steps?.Count ?? 0;
                        
                        item.Setup(
                            icon: "▶️",
                            title: script.ScriptName ?? "Nienazwany skrypt",
                            desc: $"Kroki: {stepsCount} | Zapisano na dysku",
                            status: "● Ready",
                            statusFg: (Brush)FindResource("SuccessColor"),
                            statusBg: (Brush)FindResource("SuccessBg"),
                            actionText: "Run Automation",
                            actionStyle: (Style)FindResource("PrimaryButton"),
                            logText: "> System gotowy. Skrypt załadowany poprawnie.\n> Oczekuje na start...",
                            logFg: (Brush)FindResource("TextSecondary"),
                            logBg: (Brush)converter.ConvertFrom("#050508")!,
                            logBorder: (Brush)FindResource("BorderSubtle"),
                            isExpanded: false
                        );
                        
                        item.ActionButton.Click += (s, ev) => HandleRunStopClick(item, file);
                        item.EditButton.Click += (s, ev) => HandleEditClick(item, file);
                        item.DeleteButton.Click += (s, ev) => HandleDeleteClick(item, file);
                        
                        AddAutomationItem(item);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Err(this, $"Nie udało się poprawnie załadować skryptu z pliku: {file}", ex);
                }
            }
        }

        private async void HandleRunStopClick(AutomationItemControl item, string filePath)
        {
            if (item.ActionButton.Tag is CancellationTokenSource existingCts)
            {
                item.AppendLog(">>> Zainicjowano procedurę zatrzymania awaryjnego...");
                item.ActionButton.IsEnabled = false;
                item.ActionButton.Content = "Stopping...";
                existingCts.Cancel(); 
                return;
            }

            item.ClearLog();
            item.AppendLog($">>> Przygotowywanie środowiska do uruchomienia skryptu...");
            item.SetRunningState(true);

            var cts = new CancellationTokenSource();
            item.ActionButton.Tag = cts; 

            try
            {
                var script = ScriptSerializer.Load(filePath);
                item.AppendLog($">>> Pomyślnie załadowano JSON: {script.ScriptName}");

                var player = new AutomationPlayer
                {
                    CancellationToken = cts.Token,
                    OnValueResolve = ResolveKeePassTag, 
                    OnProgressUpdate = (msg) => item.AppendLog(msg) 
                };

                bool success = await Task.Run(() => player.Play(script), cts.Token);

                if (cts.Token.IsCancellationRequested)
                {
                    item.AppendLog(">>> [STOP] Skrypt został przerwany przez użytkownika.");
                    item.SetStatus("● Stopped", (Brush)FindResource("TextSecondary"), (Brush)FindResource("BgInput"));
                }
                else if (success)
                {
                    item.AppendLog(">>> [SUKCES] Wykonano wszystkie kroki operacji.");
                    item.SetStatus("● Success", (Brush)FindResource("SuccessColor"), (Brush)FindResource("SuccessBg"));
                }
                else
                {
                    item.AppendLog(">>> [BŁĄD] Skrypt zatrzymał się z powodu problemów.");
                    item.SetStatus("● Failed", (Brush)FindResource("DangerColor"), (Brush)FindResource("DangerBg"));
                }
            }
            catch (Exception ex)
            {
                Logger.Err(this, "Wystąpił błąd podczas odtwarzania automatyzacji.", ex);
                item.AppendLog($">>> BŁĄD KRYTYCZNY: {ex.Message}");
                item.SetStatus("● Error", (Brush)FindResource("DangerColor"), (Brush)FindResource("DangerBg"));
            }
            finally
            {
                item.SetRunningState(false);
                item.ActionButton.IsEnabled = true;
                item.ActionButton.Tag = null; 
                cts.Dispose();
            }
        }

        private void HandleEditClick(AutomationItemControl item, string filePath)
        {
            if (item.ActionButton.Tag != null)
            {
                MessageBox.Show("Nie można edytować uruchomionego skryptu. Zatrzymaj go najpierw.", "Operacja niedozwolona", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var script = ScriptSerializer.Load(filePath);
                if (script == null) return;
                
                var editWindow = new ScriptEditWindow(script, filePath)
                {
                    Owner = Window.GetWindow(this)
                };
                
                if (editWindow.ShowDialog() == true)
                {
                    LoadScriptsFromDisk(); 
                }
            }
            catch (Exception ex)
            {
                Logger.Err(this, $"Błąd podczas otwierania edytora dla skryptu: {filePath}", ex);
                MessageBox.Show("Wystąpił błąd podczas ładowania edytora skryptu.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HandleDeleteClick(AutomationItemControl item, string filePath)
        {
            if (item.ActionButton.Tag != null)
            {
                MessageBox.Show("Nie można usunąć uruchomionego skryptu. Zatrzymaj go najpierw.", "Operacja niedozwolona", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Czy na pewno chcesz trwale usunąć ten skrypt automatyzacji?", "Potwierdzenie usunięcia", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (File.Exists(filePath)) File.Delete(filePath);
                    
                    if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                    {
                        Directory.Delete(dir);
                    }
                    
                    Logger.Info(this, $"Usunięto skrypt i wyczyszczono jego katalog: {filePath}");
                    LoadScriptsFromDisk(); 
                }
                catch (Exception ex)
                {
                    Logger.Err(this, $"Błąd podczas usuwania skryptu {filePath}", ex);
                    MessageBox.Show("Nie udało się całkowicie usunąć skryptu. Sprawdź logi aplikacji.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string ResolveKeePassTag(string val)
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
                    catch (Exception ex)
                    {
                        Logger.Err(this, $"Nie udało się rozwiązać tagu KeePass: {val}", ex);
                        return string.Empty; 
                    }
                }
            }
            return val;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchBox == null || AutomationsList == null) return;
            string query = SearchBox.Text.Trim();
            if (query == "Search automations...") query = string.Empty;
            
            int visibleCount = 0;
            foreach (var child in AutomationsList.Children)
            {
                if (child is AutomationItemControl item)
                {
                    if (item.MatchesSearch(query))
                    {
                        item.Visibility = Visibility.Visible;
                        visibleCount++;
                    }
                    else item.Visibility = Visibility.Collapsed;
                }
            }
            if (NoResultsText != null) NoResultsText.Visibility = (visibleCount == 0) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UnselectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var child in AutomationsList.Children)
            {
                if (child is AutomationItemControl item) item.IsSelected = false;
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Odświeżono listę automatyzacji z poziomu przycisku UI.");
            LoadScriptsFromDisk();
            
            if (SearchBox != null && SearchBox.Text != "Search automations...")
            {
                SearchBox_TextChanged(this, null);
            }
        }
    }
}