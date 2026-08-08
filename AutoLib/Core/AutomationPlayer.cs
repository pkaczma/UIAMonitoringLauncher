using System;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AutoLib.Models;

namespace AutoLib.Core
{
    public class AutomationPlayer
    {
        public Func<string, string> OnValueResolve;
        
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        public Action<string> OnProgressUpdate { get; set; }

        private void LogProgress(string msg, bool isError = false)
        {
            if (isError) AutoLibLogger.Error(msg, null);
            else AutoLibLogger.Info(msg);
            
            OnProgressUpdate?.Invoke(msg);
        }

        public bool Play(AutomationScript script)
        {
            AutomationElement currentWindow = null;
            LogProgress($"Rozpoczęto odtwarzanie skryptu: {script.ScriptName}");

            foreach (var step in script.Steps)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    LogProgress("Zatrzymano: Otrzymano żądanie przerwania od użytkownika.", true);
                    return false;
                }

                if (!ExecuteStepWithFallback(step, ref currentWindow))
                {
                    LogProgress($"Przerwano odtwarzanie. Krok {step.Type} ({step.ElementName}) zawiódł.", true);
                    return false;
                }
            }
            
            LogProgress($"Skrypt '{script.ScriptName}' zakończył się pełnym sukcesem.");
            return true;
        }

        public bool ExecuteSingleStep(AutomationStep step, ref AutomationElement currentWindow)
        {
            return ExecuteStepWithFallback(step, ref currentWindow);
        }

        private bool ExecuteStepWithFallback(AutomationStep step, ref AutomationElement currentWindow)
        {
            bool success = ExecuteCoreStep(step, ref currentWindow);
            
            if (!success && step.FallbackSteps != null && step.FallbackSteps.Count > 0)
            {
                LogProgress($"Krok ({step.Type}) zawiódł. Uruchamianie procedury ratunkowej Fallback...");
                foreach (var fallbackStep in step.FallbackSteps)
                {
                    if (CancellationToken.IsCancellationRequested) return false;

                    if (!ExecuteCoreStep(fallbackStep, ref currentWindow))
                    {
                        LogProgress("Krok z procedury ratunkowej Fallback również zawiódł.", true);
                        return false; 
                    }
                }
                return true; 
            }
            return success;
        }

        private bool ExecuteCoreStep(AutomationStep step, ref AutomationElement currentWindow)
        {
            try
            {
                if (step.DelayBeforeMs > 0)
                {
                    LogProgress($"Oczekiwanie {step.DelayBeforeMs}ms...");
                    if (CancellationToken.WaitHandle.WaitOne(step.DelayBeforeMs)) return false; 
                }
        
                return step.Type switch
                {
                    ActionType.StartProcess => HandleStartProcess(step, ref currentWindow),
                    ActionType.WindowOperation => HandleWindowOperation(step, ref currentWindow),
                    ActionType.Click => HandleClick(step, currentWindow),
                    ActionType.TypeText => HandleTypeText(step, currentWindow),
                    ActionType.VerifyElementExists => FindElement(currentWindow, step) != null,
                    _ => false
                };
            }
            catch (Exception ex)
            {
                LogProgress($"Nieobsłużony błąd podczas wykonywania kroku '{step.Type}': {ex.Message}", true);
                return false;
            }
        }

        private bool HandleStartProcess(AutomationStep step, ref AutomationElement currentWindow)
        {
            string resolvedArgs = step.Arguments ?? "";

            if (OnValueResolve != null && resolvedArgs.Contains("KEEPASS:"))
            {
                resolvedArgs = Regex.Replace(
                    resolvedArgs,
                    @"KEEPASS:.*?:(?:Url|Username|Password)",
                    match => OnValueResolve(match.Value)
                );
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = step.AppPath,
                    Arguments = resolvedArgs,
                    UseShellExecute = true
                }
            };
            
            LogProgress($"Uruchamianie aplikacji: {step.AppPath}");
            process.Start();

            int? startedProcessId = null;
            try { startedProcessId = process.Id; } catch { }
            
            LogProgress("Oczekiwanie na identyfikację przypisanego okna głównego...");
            IntPtr targetWindowHandle = IntPtr.Zero;
            string expectedProcessName = Path.GetFileNameWithoutExtension(step.AppPath);

            for (int i = 0; i < 30; i++) 
            {
                if (CancellationToken.WaitHandle.WaitOne(500)) return false; 

                IntPtr activeHandle = NativeMethods.GetForegroundWindow();
                if (activeHandle != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(activeHandle, out uint pid);
                    try
                    {
                        var fgProcess = Process.GetProcessById((int)pid);
                        bool isMatch = false;

                        if (startedProcessId.HasValue && fgProcess.Id == startedProcessId.Value) isMatch = true;
                        else if (fgProcess.ProcessName.Equals(expectedProcessName, StringComparison.OrdinalIgnoreCase)) isMatch = true;

                        if (isMatch)
                        {
                            targetWindowHandle = activeHandle;
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (targetWindowHandle != IntPtr.Zero)
            {
                currentWindow = AutomationElement.FromHandle(targetWindowHandle);
                if (step.WindowWidth.HasValue && step.WindowHeight.HasValue)
                {
                    NativeMethods.ShowWindow(targetWindowHandle, NativeMethods.SW_RESTORE);
                    NativeMethods.SetWindowPos(targetWindowHandle, IntPtr.Zero, 
                        step.WindowX ?? 0, step.WindowY ?? 0, 
                        step.WindowWidth.Value, step.WindowHeight.Value, NativeMethods.SWP_NOZORDER);
                    
                    LogProgress("Przeskalowano i ustawiono pozycję okna aplikacji.");
                }
            }
            else
            {
                LogProgress("Nie udało się bezbłędnie zidentyfikować przypisanego okna. Skrypt będzie kontynuowany ryzykując brak precyzji.", true);
            }
            return true;
        }

        private bool HandleWindowOperation(AutomationStep step, ref AutomationElement currentWindow)
        {
            LogProgress($"Poszukiwanie okna: '{step.ElementName}' (Klasa: '{step.WindowClassName}')...");

            int timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 5000;
            DateTime endTime = DateTime.Now.AddMilliseconds(timeoutMs);
            IntPtr foundHwnd = IntPtr.Zero;

            while (DateTime.Now < endTime && foundHwnd == IntPtr.Zero)
            {
                if (CancellationToken.IsCancellationRequested) return false;

                foundHwnd = FindMatchingWindowHandle(step);

                if (foundHwnd == IntPtr.Zero)
                {
                    if (CancellationToken.WaitHandle.WaitOne(500)) return false;
                }
            }

            if (foundHwnd == IntPtr.Zero)
            {
                LogProgress($"Nie odnaleziono pasującego okna dla wzorca '{step.ElementName}' w czasie {timeoutMs}ms.", true);
                return false;
            }

            try
            {
                NativeMethods.ShowWindow(foundHwnd, NativeMethods.SW_RESTORE);

                if (step.WindowWidth.HasValue && step.WindowHeight.HasValue)
                {
                    NativeMethods.SetWindowPos(
                        foundHwnd,
                        IntPtr.Zero,
                        step.WindowX ?? 0,
                        step.WindowY ?? 0,
                        step.WindowWidth.Value,
                        step.WindowHeight.Value,
                        NativeMethods.SWP_NOZORDER
                    );
                    LogProgress($"Pomyślnie zmieniono pozycję i rozmiar okna (Handle: {foundHwnd}).");
                }

                currentWindow = AutomationElement.FromHandle(foundHwnd);
                return true;
            }
            catch (Exception ex)
            {
                LogProgress($"Błąd podczas manipulacji oknem: {ex.Message}", true);
                return false;
            }
        }

        private IntPtr FindMatchingWindowHandle(AutomationStep step)
        {
            IntPtr matchedHwnd = IntPtr.Zero;

            NativeMethods.EnumWindows((hWnd, lParam) =>
            {
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;

                int length = NativeMethods.GetWindowTextLength(hWnd);
                var sbTitle = new StringBuilder(length + 1);
                if (length > 0) NativeMethods.GetWindowText(hWnd, sbTitle, sbTitle.Capacity);
                string winTitle = sbTitle.ToString();

                var sbClass = new StringBuilder(256);
                NativeMethods.GetClassName(hWnd, sbClass, sbClass.Capacity);
                string winClass = sbClass.ToString();

                string winProcessName = string.Empty;
                NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid > 0)
                {
                    try { winProcessName = Process.GetProcessById((int)pid).ProcessName; } catch { }
                }

                if (!string.IsNullOrEmpty(step.ProcessName) && !IsMatchWithWildcard(winProcessName, step.ProcessName)) return true;
                if (!string.IsNullOrEmpty(step.WindowClassName) && !IsMatchWithWildcard(winClass, step.WindowClassName)) return true;
                if (!string.IsNullOrEmpty(step.ElementName) && !IsMatchWithWildcard(winTitle, step.ElementName)) return true;

                if (!string.IsNullOrEmpty(step.RequiredWindowContent))
                {
                    try
                    {
                        var winElement = AutomationElement.FromHandle(hWnd);
                        var condition = new PropertyCondition(AutomationElement.NameProperty, step.RequiredWindowContent);
                        var foundInner = winElement.FindFirst(TreeScope.Descendants, condition);
                        
                        if (foundInner == null)
                        {
                            return true; 
                        }
                    }
                    catch
                    {
                        return true; 
                    }
                }

                matchedHwnd = hWnd;
                return false; 
            }, IntPtr.Zero);

            return matchedHwnd;
        }

        private static bool IsMatchWithWildcard(string input, string pattern)
        {
            if (string.IsNullOrEmpty(input) && string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(input)) return false;

            if (pattern.StartsWith("REGEX:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string actualRegex = pattern.Substring(6);
                    return Regex.IsMatch(input, actualRegex, RegexOptions.IgnoreCase);
                }
                catch
                {
                    return false; 
                }
            }

            if (pattern.Contains("*") || pattern.Contains("?"))
            {
                string regexPattern = "^" + Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";
                return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
            }

            return input.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private bool HandleClick(AutomationStep step, AutomationElement currentWindow)
        {
            LogProgress($"Szukanie elementu do kliknięcia: '{step.ElementName}'...");
            var clickTarget = FindElement(currentWindow, step);
            if (clickTarget == null) return false;

            // --- THE MAGIC FIX: Kaskadowe Szukanie Wzorców (Pattern Bubbling) ---
            // Jeżeli odnajdziemy Etykietę wewnątrz przycisku, nie klikniemy jej programowo.
            // Sprawdzamy więc samego rodzica i dziadka - to tam znajdują się metody Execute (Invoke).
            AutomationElement currentObj = clickTarget;
            int depth = 0;

            while (currentObj != null && depth <= 3)
            {
                if (currentObj.TryGetCurrentPattern(InvokePattern.Pattern, out object invokeObj))
                {
                    try { ((InvokePattern)invokeObj).Invoke(); LogProgress($"Kliknięto element: '{step.ElementName}' (Programowo - Invoke, Poziom: {depth})"); return true; } catch { }
                }
                if (currentObj.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object selObj))
                {
                    try { ((SelectionItemPattern)selObj).Select(); LogProgress($"Kliknięto element: '{step.ElementName}' (Programowo - Select, Poziom: {depth})"); return true; } catch { }
                }
                if (currentObj.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out object expObj))
                {
                    try { 
                        var exp = (ExpandCollapsePattern)expObj;
                        if (exp.Current.ExpandCollapseState == ExpandCollapseState.Collapsed) exp.Expand();
                        else exp.Collapse();
                        LogProgress($"Kliknięto element: '{step.ElementName}' (Programowo - Expand/Collapse, Poziom: {depth})"); 
                        return true; 
                    } catch { }
                }
                if (currentObj.TryGetCurrentPattern(TogglePattern.Pattern, out object togObj))
                {
                    try { ((TogglePattern)togObj).Toggle(); LogProgress($"Kliknięto element: '{step.ElementName}' (Programowo - Toggle, Poziom: {depth})"); return true; } catch { }
                }

                // Bezpieczne pobieranie rodzica (wchodzenie wyżej w drzewo)
                try 
                {
                    currentObj = TreeWalker.ControlViewWalker.GetParent(currentObj);
                    depth++;
                }
                catch 
                {
                    break;
                }
            }

            // Jeśli wszystkie programowe wciśnięcia zawiodły (rzadkość przy kaskadzie), używamy fizycznej myszy
            ForcePhysicalClick(clickTarget);
            LogProgress($"Kliknięto element: '{step.ElementName}' (Fizycznie - Fallback)");
            return true;
        }

        private bool HandleTypeText(AutomationStep step, AutomationElement currentWindow)
        {
            LogProgress($"Szukanie elementu dla tekstu: '{step.ElementName}'...");
            var typeTarget = FindElement(currentWindow, step);
            if (typeTarget == null) return false;
        
            string textToType = OnValueResolve != null ? OnValueResolve(step.Value) : step.Value;
        
            try { typeTarget.SetFocus(); } catch { }

            if (typeTarget.TryGetCurrentPattern(ValuePattern.Pattern, out object valueObj))
            {
                try
                {
                    var valPattern = (ValuePattern)valueObj;
                    if (!valPattern.Current.IsReadOnly)
                    {
                        valPattern.SetValue(textToType);
                        LogProgress($"Wpisano tekst (Programowo).");
                        return true;
                    }
                }
                catch { } 
            }
        
            ForcePhysicalClick(typeTarget);
            if (CancellationToken.WaitHandle.WaitOne(300)) return false; 
            
            System.Windows.Forms.SendKeys.SendWait(textToType);
            LogProgress($"Wpisano tekst z użyciem klawiatury fizycznej.");
            return true;
        }

        private void ForcePhysicalClick(AutomationElement element)
        {
            try
            {
                var rect = element.Current.BoundingRectangle;
                if (rect.IsEmpty) return;

                int targetX = (int)(rect.Left + (rect.Width / 2));
                int targetY = (int)(rect.Top + (rect.Height / 2));

                NativeMethods.SetCursorPos(targetX, targetY);

                // Krótki ruch Wiggle (budzi niektóre kontrolki w Win11)
                NativeMethods.SetCursorPos(targetX + 1, targetY + 1);
                if (CancellationToken.WaitHandle.WaitOne(20)) return;
                NativeMethods.SetCursorPos(targetX, targetY);

                if (CancellationToken.WaitHandle.WaitOne(150)) return;

                NativeMethods.mouse_event(0x02, 0, 0, 0, 0); // DOWN
                if (CancellationToken.WaitHandle.WaitOne(50)) return;
                NativeMethods.mouse_event(0x04, 0, 0, 0, 0); // UP
            }
            catch (Exception ex)
            {
                LogProgress($"Nie udało się wymusić ruchu kursora. ({ex.Message})", true);
            }
        }

        private AutomationElement FindElement(AutomationElement window, AutomationStep step)
        {
            if (window == null) window = AutomationElement.RootElement;
            AutomationElement foundElement = null;

            var conditions = new List<Condition>();

            if (!string.IsNullOrEmpty(step.AutomationId))
                conditions.Add(new PropertyCondition(AutomationElement.AutomationIdProperty, step.AutomationId));

            if (!string.IsNullOrEmpty(step.ControlType))
            {
                try
                {
                    string typeName = step.ControlType.Replace("ControlType.", "");
                    var field = typeof(ControlType).GetField(typeName, BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                    
                    if (field != null && field.GetValue(null) is ControlType actualType)
                    {
                        conditions.Add(new PropertyCondition(AutomationElement.ControlTypeProperty, actualType));
                    }
                }
                catch { } 
            }

            conditions.Add(new PropertyCondition(AutomationElement.IsOffscreenProperty, false));

            bool hasWildcard = !string.IsNullOrEmpty(step.ElementName) && 
                (step.ElementName.Contains("*") || step.ElementName.Contains("?") || step.ElementName.StartsWith("REGEX:", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(step.ElementName) && !hasWildcard)
            {
                conditions.Add(new PropertyCondition(AutomationElement.NameProperty, step.ElementName));
            }

            Condition baseCondition;
            if (conditions.Count > 1)
                baseCondition = new AndCondition(conditions.ToArray());
            else if (conditions.Count == 1)
                baseCondition = conditions[0];
            else
                baseCondition = Condition.TrueCondition;

            int timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : 5000;
            DateTime startTime = DateTime.Now;
            DateTime endTime = startTime.AddMilliseconds(timeoutMs); 
            bool switchedToRoot = false;

            while (DateTime.Now < endTime && foundElement == null)
            {
                if (CancellationToken.IsCancellationRequested) return null;

                if (!hasWildcard)
                {
                    foundElement = window.FindFirst(TreeScope.Descendants, baseCondition);
                }
                else
                {
                    AutomationElementCollection matches = window.FindAll(TreeScope.Descendants, baseCondition);
                    foreach (AutomationElement el in matches)
                    {
                        try
                        {
                            string elName = el.Current.Name ?? string.Empty;
                            if (IsMatchWithWildcard(elName, step.ElementName))
                            {
                                foundElement = el;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (foundElement == null && step.TreePath != null && step.TreePath.Count > 0)
                {
                    foundElement = FindElementByPath(window, step.TreePath);
                }

                if (foundElement == null && !switchedToRoot)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > (timeoutMs / 2.0))
                    {
                        window = AutomationElement.RootElement;
                        switchedToRoot = true;
                        LogProgress($"[Proaktywność] Zwiększono zakres poszukiwań do całego pulpitu (RootElement).");
                    }
                }

                if (foundElement == null) 
                {
                    if (CancellationToken.WaitHandle.WaitOne(250)) return null; 
                }
            }

            if (foundElement == null)
            {
                LogProgress($"Nie odnaleziono elementu: '{step.ElementName}' (Timeout: {timeoutMs}ms)", true);
            }
            
            return foundElement;
        }

        private AutomationElement FindElementByPath(AutomationElement rootWindow, List<int> path)
        {
            var current = rootWindow;
            var walker = TreeWalker.ControlViewWalker;

            foreach (var index in path)
            {
                var child = walker.GetFirstChild(current);
                for (int i = 0; i < index && child != null; i++)
                {
                    child = walker.GetNextSibling(child);
                }
                if (child == null) return null; 
                current = child;
            }
            return current;
        }
    }
}