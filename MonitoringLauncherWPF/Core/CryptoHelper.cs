using System;
using System.Security.Cryptography;
using System.Text;

namespace MonitoringLauncherWPF.Core
{
    public static class CryptoHelper
    {
        // Opcjonalna sól (entropy) dodająca dodatkową warstwę zabezpieczeń. 
        // Zmień te liczby na losowe w swoim docelowym kodzie i ich nie gub!
        private static readonly byte[] Entropy = { 9, 4, 3, 7, 1, 6, 2, 8, 5 };

        /// <summary>
        /// Szyfruje czysty tekst i zwraca go jako ciąg Base64.
        /// </summary>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                
                // DataProtectionScope.CurrentUser upewnia się, że tylko ten użytkownik Windows może to odszyfrować
                byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (Exception ex)
            {
                Logger.Err(typeof(CryptoHelper), "Błąd podczas szyfrowania tekstu.", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Odszyfrowuje ciąg Base64 i zwraca czysty tekst.
        /// </summary>
        public static string DecryptString(string encryptedBase64)
        {
            if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Logger.Err(typeof(CryptoHelper), "Błąd podczas odszyfrowywania tekstu. Zwracam puste hasło.", ex);
                return string.Empty;
            }
        }
    }
}