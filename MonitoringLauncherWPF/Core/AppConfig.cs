using System.Text.Json.Serialization;

namespace MonitoringLauncherWPF.Core
{
    public class AppConfig
    {
        // --- KEEPASS ---
        [JsonPropertyName("keepass_database_path")]
        public string KeePassDatabasePath { get; set; } = string.Empty;

        // Przechowujemy tylko zaszyfrowany string (Base64)
        [JsonPropertyName("encrypted_keepass_password")]
        public string EncryptedKeePassPassword { get; set; } = string.Empty;

        // --- LOGI ---
        [JsonPropertyName("log_retention_days")]
        public int LogRetentionDays { get; set; } = 7;

        // --- AUTOMATYZACJA ---
        [JsonPropertyName("default_step_delay_ms")]
        public int DefaultStepDelayMs { get; set; } = 500;

        [JsonPropertyName("app_launch_delay_ms")]
        public int AppLaunchDelayMs { get; set; } = 2000;

        [JsonPropertyName("save_macro_logs")]
        public bool SaveMacroLogs { get; set; } = true;
    }
}