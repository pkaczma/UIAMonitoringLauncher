using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MonitoringLauncherWPF.Core;
using MonitoringLauncherWPF.Views;

namespace MonitoringLauncherWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Dodaj to zdarzenie (pamiętaj o dopisaniu Loaded="Window_Loaded" w MainWindow.xaml)
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        InitializeApplication();
    }

    private void InitializeApplication()
    {
        bool isKeePassLoaded = false;

        // 1. Sprawdzamy, czy mamy już zapisane ustawienia w config.json
        if (!string.IsNullOrEmpty(ConfigManager.Current.KeePassDatabasePath) && 
            !string.IsNullOrEmpty(ConfigManager.Current.EncryptedKeePassPassword))
        {
            try
            {
                // Próbujemy odszyfrować hasło i załadować bazę
                string decryptedPassword = CryptoHelper.DecryptString(ConfigManager.Current.EncryptedKeePassPassword);
                KeyPassMgr.LoadDatabase(ConfigManager.Current.KeePassDatabasePath, decryptedPassword);
                
                isKeePassLoaded = true;
                Logger.Info(this, "Automatyczne logowanie do KeePass zakończone sukcesem.");
            }
            catch (Exception ex)
            {
                // Wyłapujemy błędy KeePassa (np. zmienione hasło, usunięty plik, zepsuty plik)
                Logger.Warn(this, "Nie udało się automatycznie załadować bazy KeePass. Wymagane ponowne logowanie.", ex);
            }
        }

        // 2. Jeśli automatyczne logowanie się nie powiodło (lub to pierwsze uruchomienie), pokaż prompt
        if (!isKeePassLoaded)
        {
            ShowKeePassPrompt();
        }
    }

    private void ShowKeePassPrompt()
    {
        var setupWindow = new KeePassSetupWindow();
        
        // Ustawiamy główne okno jako właściciela, żeby prompt wycentrował się względem niego 
        // i zablokował możliwość klikania z tyłu.
        setupWindow.Owner = this; 
        
        // ShowDialog zawiesza wykonywanie kodu w tym miejscu, dopóki okno się nie zamknie
        bool? result = setupWindow.ShowDialog();
        
        if (result != true)
        {
            // Jeśli użytkownik kliknął "Cancel" w KeePassSetupWindow (zwrócono false)
            Logger.Warn(this, "Użytkownik anulował logowanie do KeePass. Zamykanie aplikacji.");
            
            // Opcjonalnie możesz wyświetlić systemowy komunikat:
            // MessageBox.Show("Do działania aplikacji wymagane jest połączenie z bazą KeePass.", "Błąd logowania", MessageBoxButton.OK, MessageBoxImage.Error);
            
            Application.Current.Shutdown();
        }
    }

    private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
    
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        RecordingWindow rec = new();
        rec.ShowDialog();
    }
}