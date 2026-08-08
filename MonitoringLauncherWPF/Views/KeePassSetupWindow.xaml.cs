using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using MonitoringLauncherWPF.Core;

namespace MonitoringLauncherWPF.Views
{
    public partial class KeePassSetupWindow : Window
    {
        public KeePassSetupWindow()
        {
            InitializeComponent();
        }

        // Metoda do wyświetlania błędu pod polem hasła
        public void ShowWarning(string message)
        {
            WarningTextBlock.Text = message;
            WarningTextBlock.Visibility = Visibility.Visible;
        }

        // Metoda do ukrywania błędu
        public void HideWarning()
        {
            WarningTextBlock.Visibility = Visibility.Collapsed;
            WarningTextBlock.Text = string.Empty;
        }

        // Obsługa przesuwania okna
        private void HeaderBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) 
                DragMove();
        }

        // Otwieranie dialogu do wyboru pliku bazy .kdbx
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // Opcjonalnie: ukryj błąd po kliknięciu Browse
            HideWarning();

            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "KeePass Databases (*.kdbx)|*.kdbx|All files (*.*)|*.*",
                Title = "Wybierz plik bazy danych KeePass"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                KdbxFilePath.Text = openFileDialog.FileName;
            }
        }

        // Obsługa logowania
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            HideWarning(); // Ukrywamy stary komunikat

            try
            {
                // 1. Próbujemy otworzyć bazę. Jeśli hasło/plik są złe, rzuci wyjątek!
                KeyPassMgr.LoadDatabase(KdbxFilePath.Text, MasterPasswordBox.Password);

                // 2. Skoro tu dotarliśmy, wszystko jest ok. Zapisujemy config.
                ConfigManager.Current.KeePassDatabasePath = KdbxFilePath.Text;
                ConfigManager.Current.EncryptedKeePassPassword = CryptoHelper.EncryptString(MasterPasswordBox.Password);
                ConfigManager.Save();

                DialogResult = true; // Zamyka okno sukcesem
            }
            catch (KeePassException ex)
            {
                // Błąd rzucony przez naszą klasę (np. złe hasło, nie ma pliku, zły wpis)
                ShowWarning(ex.Message);
            }
            catch (Exception ex)
            {
                // Jakiś inny, krytyczny błąd systemu
                Logger.Err(this, "Nieoczekiwany błąd w oknie logowania.", ex);
                ShowWarning("Wystąpił błąd krytyczny. Sprawdź logi aplikacji.");
            }
        }

        // Zwrócenie false i anulowanie
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}