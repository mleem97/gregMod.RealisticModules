namespace GregModMoreModules
{
    internal static class CompatibilityMatrix
    {
        internal static bool CanAccept(ModuleRegistry.Entry entry, int portSfpType, CompatibilityMode mode)
        {
            // Current Data Center exposes the port compatibility value as sfpType,
            // but not a stable public form-factor enum. Until that API is exposed,
            // strict mode can safely enforce the observed host type only.
            return portSfpType == entry.ModuleSfpType;
        }
    }
}
