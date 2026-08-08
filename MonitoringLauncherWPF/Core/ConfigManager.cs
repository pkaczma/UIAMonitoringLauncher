using System;
using System.IO;
using System.Text.Json;

namespace MonitoringLauncherWPF.Core
{
    public static class ConfigManager
    {
        private static readonly string ConfigFilePath;
        
        // Opcje serializacji (żeby plik JSON ładnie wyglądał - z wcięciami)
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true 
        };

        // Główny punkt dostępu do ustawień w całej aplikacji
        public static AppConfig Current { get; private set; }

        static ConfigManager()
        {
            ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            
            // Inicjalizujemy od razu przy pierwszym wywołaniu
            Load();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    Current = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
                    Logger.Info(typeof(ConfigManager), "Pomyślnie załadowano plik konfiguracyjny.");
                }
                else
                {
                    Logger.Warn(typeof(ConfigManager), "Plik config.json nie istnieje. Tworzenie nowej, domyślnej konfiguracji.");
                    Current = new AppConfig();
                    Save(); // Tworzy plik z domyślnymi wartościami
                }
            }
            catch (Exception ex)
            {
                Logger.Err(typeof(ConfigManager), "Wystąpił błąd podczas ładowania konfiguracji. Używam ustawień domyślnych.", ex);
                Current = new AppConfig();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(ConfigFilePath, json);
                Logger.Info(typeof(ConfigManager), "Zapisano zmiany w pliku konfiguracyjnym.");
            }
            catch (Exception ex)
            {
                Logger.Err(typeof(ConfigManager), "Nie udało się zapisać pliku konfiguracyjnego.", ex);
            }
        }
    }
}