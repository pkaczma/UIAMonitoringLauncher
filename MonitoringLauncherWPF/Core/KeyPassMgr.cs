using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace MonitoringLauncherWPF.Core
{
    public static class KeyPassMgr
    {
        private class CachedEntry
        {
            public string Path { get; set; }
            public string Title { get; set; }
            public string Url { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private static List<CachedEntry> _cachedEntries = new List<CachedEntry>();
        private static string _currentDbPath = string.Empty;
        private static string _currentDbPassword = string.Empty;
        private static Timer _refreshTimer;
        
        private static readonly object _lockObj = new object();

        public static DateTime LastRefreshTime { get; private set; } = DateTime.MinValue;
        public static event Action OnCacheUpdated;

        public static bool IsDBLoaded
        {
            get
            {
                lock (_lockObj) { return _cachedEntries.Count > 0; }
            }
        }

        public static void ValidateDatabasePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"Plik bazy danych \"{path}\" nie istnieje!");
            }
            if (Path.GetExtension(path).Trim().ToLower() != ".kdbx")
            {
                throw new KeePassException("Plik bazy danych musi mieć rozszerzenie .kdbx!");
            }
        }

        public static void LoadDatabase(string path, string password)
        {
            ValidateDatabasePath(path);

            lock (_lockObj)
            {
                _currentDbPath = path;
                _currentDbPassword = password;
            }
            
            PerformDatabaseLoad();

            lock (_lockObj)
            {
                if (_refreshTimer == null)
                {
                    _refreshTimer = new Timer(RefreshCallback, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
                }
            }
        }

        public static void ForceRefresh()
        {
            Logger.Info(typeof(KeyPassMgr), "Zażądano ręcznego odświeżenia bazy KeePass z dysku.");
            
            string path, pwd;
            lock (_lockObj)
            {
                path = _currentDbPath;
                pwd = _currentDbPassword;
            }

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(pwd))
            {
                throw new KeePassException("Brak skonfigurowanej bazy KeePass do odświeżenia.");
            }

            PerformDatabaseLoad();
        }

        private static void RefreshCallback(object state)
        {
            Logger.Info(typeof(KeyPassMgr), "Rozpoczynam cykliczne odświeżanie bazy KeePass z dysku (interwał 1h).");
            try
            {
                lock (_lockObj)
                {
                    if (string.IsNullOrEmpty(_currentDbPath) || string.IsNullOrEmpty(_currentDbPassword)) return;
                }
                
                PerformDatabaseLoad();
            }
            catch (Exception ex)
            {
                Logger.Warn(typeof(KeyPassMgr), "Cykliczne odświeżanie w tle nie powiodło się, używany będzie stary cache.", ex);
            }
        }

        private static void PerformDatabaseLoad()
        {
            string path, pwd;
            lock (_lockObj)
            {
                path = _currentDbPath;
                pwd = _currentDbPassword;
            }

            var keyPassConnection = new IOConnectionInfo
            {
                Path = path,
                CredProtMode = IOCredProtMode.None,
                CredSaveMode = IOCredSaveMode.NoSave
            };

            var keyPassKey = new CompositeKey();
            keyPassKey.AddUserKey(new KcpPassword(pwd));
            var tempDb = new PwDatabase();
            
            try
            {
                tempDb.Open(keyPassConnection, keyPassKey, null);
                
                var newCache = new List<CachedEntry>();
                var entries = tempDb.RootGroup.GetEntries(true);

                foreach (var entry in entries)
                {
                    string title = entry.Strings.ReadSafe(PwDefs.TitleField);
                    if (string.IsNullOrEmpty(title)) continue;

                    string groupName = entry.ParentGroup?.Name ?? "Root";
                    
                    newCache.Add(new CachedEntry
                    {
                        Path = $"{groupName}/{title}",
                        Title = title,
                        Url = entry.Strings.ReadSafe(PwDefs.UrlField),
                        Username = entry.Strings.ReadSafe(PwDefs.UserNameField),
                        Password = entry.Strings.ReadSafe(PwDefs.PasswordField)
                    });
                }
                
                lock (_lockObj)
                {
                    _cachedEntries = newCache;
                    LastRefreshTime = DateTime.Now;
                }
                
                Logger.Info(typeof(KeyPassMgr), $"Pomyślnie wczytano/odświeżono bazę KeePass. Wpisów w cache: {newCache.Count}");
                
                // Wywołanie eventu aktualizacji UI (bezpieczne wywołanie dla subskrybentów)
                OnCacheUpdated?.Invoke();
            }
            catch (Exception e)
            {
                Logger.Err(typeof(KeyPassMgr), "Wystąpił błąd podczas próby odświeżenia/otwarcia bazy KeePass.", e);
                
                lock (_lockObj)
                {
                    if (_cachedEntries.Count == 0)
                    {
                        throw new KeePassException("Nie udało się odczytać bazy. Sprawdź poprawność hasła i pliku.", e);
                    }
                }
                // Jeśli wymuszono ręcznie (ForceRefresh), wyjątek poleci wyżej, by UI mogło zareagować komunikatem błędu.
                throw;
            }
            finally
            {
                if (tempDb.IsOpen)
                {
                    tempDb.Close(); 
                }
            }
        }

        public static string GetEntryPassword(string keyPassEntryPath) => _GetField(keyPassEntryPath, e => e.Password);
        public static string GetEntryUsername(string keyPassEntryPath) => _GetField(keyPassEntryPath, e => e.Username);
        public static string GetEntryUrl(string keyPassEntryPath) => _GetField(keyPassEntryPath, e => e.Url);

        private static string _GetField(string keyPassEntryPath, Func<CachedEntry, string> selector)
        {
            lock (_lockObj)
            {
                if (!IsDBLoaded) throw new KeePassException("Baza KeePass nie jest załadowana!");
                
                var entry = _cachedEntries.FirstOrDefault(e => e.Path == keyPassEntryPath);
                
                if (entry == null)
                {
                    string title = keyPassEntryPath.Split('/').LastOrDefault() ?? keyPassEntryPath;
                    entry = _cachedEntries.FirstOrDefault(e => e.Title == title);
                    
                    if (entry == null)
                    {
                        Logger.Warn(typeof(KeyPassMgr), $"Nie znaleziono wpisu o ścieżce/tytule: {keyPassEntryPath}");
                        throw new KeePassException($"Wpis \"{keyPassEntryPath}\" nie został znaleziony w bazie.");
                    }
                }

                return selector(entry);
            }
        }

        public static KeyValuePair<string, string>[] GetAllEntriesWithUrls()
        {
            lock (_lockObj)
            {
                if (!IsDBLoaded) throw new KeePassException("Baza KeePass nie jest załadowana!");

                return _cachedEntries
                    .Where(e => !string.IsNullOrEmpty(e.Url))
                    .Select(e => new KeyValuePair<string, string>(e.Path, e.Url))
                    .ToArray();
            }
        }

        public static string[] GetAllEntryPaths()
        {
            lock (_lockObj)
            {
                if (!IsDBLoaded) return Array.Empty<string>();
                return _cachedEntries.Select(e => e.Path).ToArray();
            }
        }

        public static int GetTotalEntriesCount()
        {
            lock (_lockObj)
            {
                if (!IsDBLoaded) return 0;
                return _cachedEntries.Count;
            }
        }

        public static DateTime GetLastModifiedDate(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) 
                    return DateTime.MinValue;

                return File.GetLastWriteTime(path);
            }
            catch (Exception ex)
            {
                Logger.Err(typeof(KeyPassMgr), $"Błąd podczas odczytu daty modyfikacji pliku: {path}", ex);
                return DateTime.MinValue;
            }
        }
    }
}