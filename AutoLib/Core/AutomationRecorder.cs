using System;
using System.Diagnostics;
using System.Text;
using System.Windows.Automation;
using AutoLib.Models;

namespace AutoLib.Core
{
    public class AutomationRecorder
    {
        public AutomationStep RecordClick(AutomationElement element)
        {
            if (element == null) return null;
            
            var rootWindow = UiaHelper.GetTopLevelWindow(element);
            return new AutomationStep
            {
                Type = ActionType.Click,
                AutomationId = element.Current.AutomationId,
                ElementName = element.Current.Name,
                ControlType = element.Current.ControlType.ProgrammaticName,
                TreePath = UiaHelper.GenerateTreePath(element, rootWindow)
            };
        }

        public AutomationStep RecordTypeText(AutomationElement element, string textToType)
        {
            if (element == null) return null;

            var rootWindow = UiaHelper.GetTopLevelWindow(element);
            return new AutomationStep
            {
                Type = ActionType.TypeText,
                AutomationId = element.Current.AutomationId,
                ElementName = element.Current.Name,
                ControlType = element.Current.ControlType.ProgrammaticName,
                TreePath = UiaHelper.GenerateTreePath(element, rootWindow),
                Value = textToType
            };
        }

        public AutomationStep RecordStartProcess(AutomationElement element)
        {
            if (element == null) return null;

            var rootWindow = UiaHelper.GetTopLevelWindow(element);
            int pid = rootWindow.Current.ProcessId;
            var process = Process.GetProcessById(pid);
            
            var rect = rootWindow.Current.BoundingRectangle;

            return new AutomationStep
            {
                Type = ActionType.StartProcess,
                AppPath = process.MainModule?.FileName,
                WindowX = (int)rect.X,
                WindowY = (int)rect.Y,
                WindowWidth = (int)rect.Width,
                WindowHeight = (int)rect.Height
            };
        }

        public AutomationStep RecordTextClick(string linkText)
        {
            if (string.IsNullOrWhiteSpace(linkText)) return null;

            return new AutomationStep
            {
                Type = ActionType.Click,
                ElementName = linkText, 
                TimeoutMs = 15000 
            };
        }

        public AutomationStep RecordWindowOperation(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;

            try
            {
                var winElem = AutomationElement.FromHandle(hwnd);
                var rect = winElem.Current.BoundingRectangle;

                string title = winElem.Current.Name ?? string.Empty;

                var sbClass = new StringBuilder(256);
                NativeMethods.GetClassName(hwnd, sbClass, sbClass.Capacity);
                string className = sbClass.ToString();

                string procName = string.Empty;
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid > 0)
                {
                    try
                    {
                        procName = Process.GetProcessById((int)pid).ProcessName;
                    }
                    catch { }
                }

                return new AutomationStep
                {
                    Type = ActionType.WindowOperation,
                    ElementName = title,
                    WindowClassName = className,
                    ProcessName = procName,
                    WindowX = (int)rect.X,
                    WindowY = (int)rect.Y,
                    WindowWidth = (int)rect.Width,
                    WindowHeight = (int)rect.Height
                };
            }
            catch (Exception ex)
            {
                AutoLibLogger.Error($"Błąd podczas nagrywania operacji na oknie: {ex.Message}", ex);
                return null;
            }
        }
    }
}