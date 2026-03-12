using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace HandheldCompanion.Processors.AMD
{


    /// <summary>
    /// Global PCI bus mutex used by several low-level tools (ZenStates-Core, HWiNFO, etc.)
    /// to serialize PCI/SMU access across processes.
    /// </summary>
    internal static class PciBusMutex
    {
        private static Mutex? _pciBusMutex;

        public static void Open()
        {
            _pciBusMutex ??= CreateOrOpenExistingMutex(@"Global\Access_PCI");
        }

        public static void Close()
        {
            try { _pciBusMutex?.Close(); }
            catch { /* ignored */ }
            _pciBusMutex = null;
        }

        public static bool Wait(int millisecondsTimeout = 5000) => WaitMutex(_pciBusMutex, millisecondsTimeout);

        public static void Release()
        {
            try { _pciBusMutex?.ReleaseMutex(); }
            catch { /* ignored */ }
        }

        private static Mutex? CreateOrOpenExistingMutex(string name)
        {
            try
            {
                var worldRule = new MutexAccessRule(
                    new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    MutexRights.FullControl,
                    AccessControlType.Allow);

                var mutexSecurity = new MutexSecurity();
                mutexSecurity.AddAccessRule(worldRule);

#if NETFRAMEWORK
                return new Mutex(false, name, out _, mutexSecurity);
#else
                return MutexAcl.Create(false, name, out _, mutexSecurity);
#endif
            }
            catch (UnauthorizedAccessException)
            {
                try { return Mutex.OpenExisting(name); }
                catch { /* ignored */ }
            }

            return null;
        }

        private static bool WaitMutex(Mutex? mutex, int millisecondsTimeout = 5000)
        {
            if (mutex is null)
                return false;

            try
            {
                return mutex.WaitOne(millisecondsTimeout, false);
            }
            catch (AbandonedMutexException)
            {
                // Treat abandoned mutex as acquired, consistent with ZenStates-Core.
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
