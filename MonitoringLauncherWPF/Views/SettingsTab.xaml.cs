using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class SettingsTab : UserControl
    {
        public SettingsTab()
        {
            InitializeComponent();
            this.Loaded += SettingsTab_Loaded;
            this.Unloaded += SettingsTab_Unloaded;
        }

        private void SettingsTab_Loaded(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Załadowano widok ustawień (SettingsTab).");
            LoadCurrentSettings();
            
            // Subskrypcja na odświeżenia timera lub inne ręczne przeładowania
            KeyPassMgr.OnCacheUpdated += KeyPassMgr_OnCacheUpdated;
        }

        private void SettingsTab_Unloaded(object sender, RoutedEventArgs e)
        {
            // Zwalniamy event zapobiegając wyciekom pamięci w WPF
            KeyPassMgr.OnCacheUpdated -= KeyPassMgr_OnCacheUpdated;
        }

        private void KeyPassMgr_OnCacheUpdated()
        {
            // Update musi się zadziać na wątku UI (Dispatcher)
            Dispatcher.Invoke(() => UpdateKeePassUI());
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var config = ConfigManager.Current;

                UpdateKeePassUI();

                LaunchDelayInput.Text = config.AppLaunchDelayMs.ToString();
                StepDelayInput.Text = config.DefaultStepDelayMs.ToString();

                SaveMacroLogsToggle.IsChecked = config.SaveMacroLogs;
                LogRetentionInput.Text = config.LogRetentionDays.ToString();
                
                SaveStatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.Err(this, "Wystąpił błąd podczas ładowania danych do kontrolek ustawień.", ex);
            }
        }

        private void UpdateKeePassUI()
        {
            var config = ConfigManager.Current;

            if (string.IsNullOrEmpty(config.KeePassDatabasePath))
            {
                KeePassDbPathText.Text = "Database: Not configured";
                KeePassPwdStatusText.Text = "Password: Not saved";
                
                KeePassDbInfoText.Visibility = Visibility.Collapsed;
                KeePassLastReloadText.Visibility = Visibility.Collapsed;
                ManualReloadBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                KeePassDbPathText.Text = $"Database: {config.KeePassDatabasePath}";
                KeePassPwdStatusText.Text = string.IsNullOrEmpty(config.EncryptedKeePassPassword) 
                    ? "Password: Not saved" 
                    : "Password: Saved & Encrypted";

                try
                {
                    if (KeyPassMgr.IsDBLoaded)
                    {
                        int entryCount = KeyPassMgr.GetTotalEntriesCount();
                        DateTime lastModified = KeyPassMgr.GetLastModifiedDate(config.KeePassDatabasePath);
                        
                        KeePassDbInfoText.Text = $"Entries: {entryCount}  •  File modified: {lastModified:yyyy-MM-dd HH:mm}";
                        KeePassDbInfoText.Visibility = Visibility.Visible;

                        string reloadTime = KeyPassMgr.LastRefreshTime != DateTime.MinValue 
                            ? KeyPassMgr.LastRefreshTime.ToString("HH:mm:ss") 
                            : "Unknown";
                            
                        KeePassLastReloadText.Text = $"Last Cache Reload: {reloadTime}";
                        KeePassLastReloadText.Visibility = Visibility.Visible;

                        ManualReloadBtn.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        KeePassDbInfoText.Visibility = Visibility.Collapsed;
                        KeePassLastReloadText.Visibility = Visibility.Collapsed;
                        ManualReloadBtn.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(this, "Nie udało się pobrać statystyk bazy KeePass do widoku.", ex);
                    KeePassDbInfoText.Visibility = Visibility.Collapsed;
                    KeePassLastReloadText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ConfigureKeePass_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Użytkownik wywołał okno konfiguracji KeePass z poziomu ustawień.");
            
            var parentWindow = Window.GetWindow(this);
            var setupWindow = new KeePassSetupWindow 
            { 
                Owner = parentWindow 
            };
            
            if (setupWindow.ShowDialog() == true)
            {
                Logger.Info(this, "KeePass został pomyślnie zrekonfigurowany.");
                UpdateKeePassUI();
                ShowTemporaryStatus("KeePass configured!");
            }
            else
            {
                Logger.Warn(this, "Konfiguracja KeePass została anulowana lub nie powiodła się.");
            }
        }

        private void ManualReloadBtn_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Użytkownik kliknął przycisk ręcznego przeładowania KeePass z poziomu interfejsu (Settings).");
            
            ManualReloadBtn.IsEnabled = false;
            ManualReloadBtn.Content = "Reloading...";
            
            // Uruchomienie w tle, aby UI pozostało w pełni responsywne
            Task.Run(() => 
            {
                try
                {
                    KeyPassMgr.ForceRefresh();
                    
                    Dispatcher.Invoke(() => {
                        ShowTemporaryStatus("KeePass cache reloaded!");
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        MessageBox.Show($"Błąd podczas odświeżania bazy:\n{ex.Message}", "Błąd odświeżania", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    Dispatcher.Invoke(() => {
                        ManualReloadBtn.IsEnabled = true;
                        ManualReloadBtn.Content = "Manual Reload";
                    });
                }
            });
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            Logger.Info(this, "Próba zapisu ustawień...");
            var config = ConfigManager.Current;

            config.SaveMacroLogs = SaveMacroLogsToggle.IsChecked ?? false;

            if (int.TryParse(LaunchDelayInput.Text, out int launchDelay))
            {
                config.AppLaunchDelayMs = launchDelay;
            }
            else
            {
                Logger.Warn(this, $"Nieprawidłowa wartość AppLaunchDelay: '{LaunchDelayInput.Text}'. Pominięto.");
            }

            if (int.TryParse(StepDelayInput.Text, out int stepDelay))
            {
                config.DefaultStepDelayMs = stepDelay;
            }
            else
            {
                Logger.Warn(this, $"Nieprawidłowa wartość DefaultStepDelay: '{StepDelayInput.Text}'. Pominięto.");
            }

            if (int.TryParse(LogRetentionInput.Text, out int logDays))
            {
                config.LogRetentionDays = logDays;
            }
            else
            {
                Logger.Warn(this, $"Nieprawidłowa wartość LogRetentionDays: '{LogRetentionInput.Text}'. Pominięto.");
            }

            ConfigManager.Save();
            
            Logger.Info(this, "Ustawienia zostały pomyślnie zapisane.");
            ShowTemporaryStatus("Settings saved successfully!");
        }

        private async void ShowTemporaryStatus(string message)
        {
            SaveStatusText.Text = message;
            SaveStatusText.Visibility = Visibility.Visible;
            
            await Task.Delay(3000);
            
            SaveStatusText.Visibility = Visibility.Collapsed;
        }
    }
}