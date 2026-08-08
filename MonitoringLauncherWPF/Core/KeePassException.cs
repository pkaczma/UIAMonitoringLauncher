using System;

namespace MonitoringLauncherWPF.Core
{
    public class KeePassException : Exception
    {
        public KeePassException(string message) : base(message) { }
        
        public KeePassException(string message, Exception innerException) : base(message, innerException) { }
    }
}