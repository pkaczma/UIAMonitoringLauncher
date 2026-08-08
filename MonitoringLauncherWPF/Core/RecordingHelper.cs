using System;
using Microsoft.VisualBasic;
using AutoLib.Core;
using MonitoringLauncherWPF.Views;
using System.Windows;

namespace MonitoringLauncherWPF.Core
{
    public static class InputCaptureHelper
    {
        public const int VK_CONTROL = 0x11;
        public const int VK_SHIFT = 0x10;
        
        // Kody klawiszy alfanumerycznych (1, 2, 3, 4)
        public const int VK_1 = 0x31;
        public const int VK_2 = 0x32;
        public const int VK_3 = 0x33;
        public const int VK_4 = 0x34;
        
        public const int VK_ESCAPE = 0x1B;

        public static bool IsShortcutPressed(int mainKey)
        {
            bool ctrl = (NativeMethods.GetAsyncKeyState(VK_CONTROL) & 0x8000) > 0;
            bool shift = (NativeMethods.GetAsyncKeyState(VK_SHIFT) & 0x8000) > 0;
            bool key = (NativeMethods.GetAsyncKeyState(mainKey) & 0x8000) > 0;
            
            return ctrl && shift && key;
        }

        public static bool IsKeyPressed(int vkCode)
        {
            return (NativeMethods.GetAsyncKeyState(vkCode) & 0x8000) > 0;
        }
    }

    public static class DialogHelper
    {
        public static string PromptForInput(string prompt, string title, string defaultText = "", bool allowKeePass = false)
        {
            Logger.Info(typeof(DialogHelper), $"Wywołano QuickInputWindow: Tytuł='{title}', KeePass={allowKeePass}");
            
            string result = string.Empty;

            // Wywołujemy z kontekstu UI Thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new QuickInputWindow(prompt, title, defaultText, allowKeePass);
                
                if (window.ShowDialog() == true)
                {
                    result = window.InputText;
                }
            });
            
            if (string.IsNullOrEmpty(result) && !string.IsNullOrEmpty(defaultText))
            {
                Logger.Warn(typeof(DialogHelper), $"Użytkownik anulował wprowadzanie lub zwrócił puste pole dla okna: {title}");
            }
            
            return result;
        }
    }
}