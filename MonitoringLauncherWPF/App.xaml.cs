using System;
using System.Windows;
using System.Windows.Threading;
using MonitoringLauncherWPF.Core; // Umożliwia dostęp do Twojego Loggera

namespace MonitoringLauncherWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
        {
            // Podłączenie logów z oddzielnej biblioteki AutoLib do naszego głównego systemu logowania
            AutoLib.Core.AutoLibLogger.Info = msg => Logger.Info("AutoLib", msg);
            AutoLib.Core.AutoLibLogger.Warn = msg => Logger.Warn("AutoLib", msg);
            AutoLib.Core.AutoLibLogger.Error = (msg, ex) => Logger.Err("AutoLib", msg, ex);
            
            // 1. Przechwytywanie błędów z wątków w tle (np. Taski, operacje asynchroniczne)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            // 2. Przechwytywanie błędów z głównego wątku interfejsu (WPF / XAML)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Ten event wywoła się, gdy błąd wystąpi poza głównym wątkiem UI.
            // Zazwyczaj po tym błędzie system i tak wymusi zamknięcie aplikacji, ale zdążymy go zapisać.
            if (e.ExceptionObject is Exception ex)
            {
                Logger.Err(this, "Krytyczny błąd aplikacji w tle (UnhandledException).", ex);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Ten event wywoła się, gdy błąd wystąpi w interfejsie graficznym (np. problem z bindowaniem, stylem XAML).
            Logger.Err(this, "Krytyczny błąd interfejsu graficznego (DispatcherUnhandledException).", e.Exception);
            
            // Opcjonalnie: Odkomentowanie poniższej linijki zapobiegnie wyłączeniu się aplikacji!
            // WPF zignoruje błąd i spróbuje działać dalej. Używaj ostrożnie, bo UI może być w niestabilnym stanie.
            
            e.Handled = true; 
        }
}

