using System;
using System.Windows.Automation;
using System.Collections.Generic;

namespace AutoLib.Core
{
    public static class UiaHelper
    {
        public static AutomationElement GetElementUnderCursor()
        {
            if (NativeMethods.GetCursorPos(out POINT point))
            {
                try
                {
                    var sysPoint = new System.Windows.Point(point.X, point.Y);
                    AutomationElement element = AutomationElement.FromPoint(sysPoint);
                    
                    if (element != null)
                    {
                        // Wymuszamy algorytm poszukujący fizycznie najmniejszego elementu
                        // aby uniknąć klikania w niewidzialne panele i nakładające się kontrolki (WinUI 3)
                        element = FindSmallestElementUnderPoint(element, sysPoint);
                    }
                    
                    return element;
                }
                catch (Exception ex) 
                { 
                    AutoLibLogger.Warn($"Nie udało się pobrać elementu UI pod kursorem. ({ex.Message})");
                    return null; 
                }
            }
            return null;
        }

        // --- DEFINITYWNA NAPRAWA: Wyszukiwanie elementu o najmniejszej fizycznej powierzchni ---
        private static AutomationElement FindSmallestElementUnderPoint(AutomationElement startElement, System.Windows.Point pt)
        {
            AutomationElement bestMatch = startElement;
            double minArea = double.MaxValue;

            try
            {
                var rect = startElement.Current.BoundingRectangle;
                if (!rect.IsEmpty && rect.Contains(pt))
                {
                    minArea = rect.Width * rect.Height;
                }
            }
            catch { }

            var walker = TreeWalker.ControlViewWalker;
            var queue = new Queue<AutomationElement>();
            queue.Enqueue(startElement);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                try
                {
                    var child = walker.GetFirstChild(current);
                    while (child != null)
                    {
                        try
                        {
                            if (!child.Current.IsOffscreen)
                            {
                                var rect = child.Current.BoundingRectangle;
                                
                                // Jeśli dziecko zawiera kursor, sprawdzamy jego rozmiar
                                if (!rect.IsEmpty && rect.Contains(pt))
                                {
                                    double area = rect.Width * rect.Height;
                                    
                                    // Mniejsza powierzchnia = dokładniejsze trafienie w przycisk
                                    // Używamy <= aby z dwójki takich samych elementów preferować głębszy (dziecko)
                                    if (area <= minArea)
                                    {
                                        minArea = area;
                                        bestMatch = child;
                                    }
                                    
                                    // Skoro ten element zawiera kursor, szukamy w jego dzieciach
                                    queue.Enqueue(child);
                                }
                            }
                        }
                        catch { } // Ignorujemy martwe/chronione elementy systemu Windows

                        try
                        {
                            child = walker.GetNextSibling(child);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
                catch { }
            }

            return bestMatch;
        }

        public static AutomationElement GetTopLevelWindow(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            var current = element;
            
            try
            {
                var parent = walker.GetParent(current);

                while (parent != null && !parent.Equals(AutomationElement.RootElement))
                {
                    current = parent;
                    parent = walker.GetParent(current);
                }
            } 
            catch { }
            
            return current;
        }

        public static List<int> GenerateTreePath(AutomationElement target, AutomationElement rootWindow)
        {
            var path = new List<int>();
            var current = target;
            var walker = TreeWalker.ControlViewWalker;

            try
            {
                while (current != null && !current.Equals(rootWindow) && !current.Equals(AutomationElement.RootElement))
                {
                    var parent = walker.GetParent(current);
                    if (parent == null) break;

                    int index = 0;
                    var child = walker.GetFirstChild(parent);
                    
                    while (child != null)
                    {
                        if (child.Equals(current)) break;
                        index++;
                        child = walker.GetNextSibling(child);
                    }
                    
                    path.Insert(0, index); 
                    current = parent;
                }
            } 
            catch { }
            
            return path;
        }
    }
}