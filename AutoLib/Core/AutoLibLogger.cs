using System;

namespace AutoLib.Core
{
    public static class AutoLibLogger
    {
        // Puste akcje domyślne, aby uniknąć NullReferenceException
        public static Action<string> Info { get; set; } = _ => { };
        public static Action<string> Warn { get; set; } = _ => { };
        public static Action<string, Exception> Error { get; set; } = (_, __) => { };
    }
}