using System;

namespace GregModMoreModules
{
    // Detects at runtime whether gregCore is present (pure type-name lookup).
    // Methods touching gregCore types must ONLY be called
    // when HasCore is true (otherwise JIT TypeLoad when the DLL is missing).
    public static class GregHost
    {
        private const string ProbeType = "gregCore.UI.GregNotificationManager, gregCore";
        private static bool? _hasCore;

        public static bool HasCore
        {
            get
            {
                if (_hasCore == null)
                {
                    try { _hasCore = Type.GetType(ProbeType) != null; }
                    catch { _hasCore = false; }
                }
                return _hasCore.Value;
            }
        }
    }
}
