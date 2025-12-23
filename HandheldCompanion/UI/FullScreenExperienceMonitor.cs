using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace HandheldCompanion.UI
{
    public sealed class FullScreenExperienceMonitor : IDisposable
    {
        public sealed class FseStateChangedEventArgs : EventArgs
        {
            public bool IsActive { get; }
            public FseStateChangedEventArgs(bool isActive) => IsActive = isActive;
        }

        /// <summary>Raised when FSE active state changes (true = active, false = inactive).</summary>
        public event EventHandler<FseStateChangedEventArgs>? FseStateChanged;

        /// <summary>Last known state. Valid after Start().</summary>
        public bool IsActive { get; private set; }

        /// <summary>True if the API set exists and the monitor is using the native notification API.</summary>
        public bool IsSupported { get; private set; }

        private readonly SynchronizationContext? _syncContext;
        private readonly bool _marshalToSyncContext;

        private IntPtr _module = IntPtr.Zero;
        private IntPtr _registrationHandle = IntPtr.Zero;

        // Keep delegates alive (GC safety).
        private RegisterNotificationDelegate? _register;
        private UnregisterNotificationDelegate? _unregister;
        private IsActiveDelegate? _isActive;
        private FseChangedCallback? _callback;

        private bool _started;

        public FullScreenExperienceMonitor(bool marshalToSyncContext = true)
        {
            _marshalToSyncContext = marshalToSyncContext;
            _syncContext = SynchronizationContext.Current;
        }

        /// <summary>
        /// Starts monitoring. If unsupported, IsSupported will be false and no notifications will be raised.
        /// </summary>
        public void Start()
        {
            if (_started) return;
            _started = true;

            // Official guidance: gate on IsApiSetImplemented("api-ms-win-gaming-experience-l1-1-0"). :contentReference[oaicite:1]{index=1}
            if (!IsApiSetImplemented("api-ms-win-gaming-experience-l1-1-0"))
            {
                IsSupported = false;
                return;
            }

            // Load the API set DLL and bind exports dynamically.
            if (!NativeLibrary.TryLoad("api-ms-win-gaming-experience-l1-1-0", out _module) || _module == IntPtr.Zero)
            {
                IsSupported = false;
                return;
            }

            _isActive = GetDelegate<IsActiveDelegate>(_module, "IsGamingFullScreenExperienceActive");
            _register = GetDelegate<RegisterNotificationDelegate>(_module, "RegisterGamingFullScreenExperienceChangeNotification");
            _unregister = GetDelegate<UnregisterNotificationDelegate>(_module, "UnregisterGamingFullScreenExperienceChangeNotification");

            if (_isActive is null || _register is null || _unregister is null)
            {
                IsSupported = false;
                CleanupNative();
                return;
            }

            IsSupported = true;

            // Initialize state.
            SetStateAndMaybeRaise(_isActive(), raiseIfChanged: false);

            // Register callback. Microsoft doc sample: callback takes a single PVOID context. :contentReference[oaicite:2]{index=2}
            _callback = OnFseChangedNative;

            int hr = _register(_callback, IntPtr.Zero, out _registrationHandle);
            if (hr < 0 || _registrationHandle == IntPtr.Zero)
            {
                IsSupported = false;
                CleanupNative();
            }
        }

        public void Stop()
        {
            if (!_started) return;
            _started = false;

            if (IsSupported && _unregister is not null && _registrationHandle != IntPtr.Zero)
            {
                try { _unregister(_registrationHandle); }
                catch { /* swallow: stop must be safe */ }
            }

            CleanupNative();
            IsSupported = false;
        }

        public void Dispose()
        {
            Stop();
            GC.SuppressFinalize(this);
        }

        private void OnFseChangedNative(IntPtr context)
        {
            // Guidance: on notification, query current state (don’t assume). :contentReference[oaicite:3]{index=3}
            bool now = false;
            try { if (_isActive is not null) now = _isActive(); }
            catch { return; }

            SetStateAndMaybeRaise(now, raiseIfChanged: true);
        }

        private void SetStateAndMaybeRaise(bool now, bool raiseIfChanged)
        {
            bool changed = now != IsActive;
            IsActive = now;

            if (!raiseIfChanged || !changed)
                return;

            void Raise()
            {
                FseStateChanged?.Invoke(this, new FseStateChangedEventArgs(IsActive));
            }

            if (_marshalToSyncContext && _syncContext is not null)
                _syncContext.Post(_ => Raise(), null);
            else
                Raise();
        }

        private void CleanupNative()
        {
            _registrationHandle = IntPtr.Zero;
            _callback = null;

            _isActive = null;
            _register = null;
            _unregister = null;

            if (_module != IntPtr.Zero)
            {
                try { NativeLibrary.Free(_module); }
                catch { /* ignore */ }
                _module = IntPtr.Zero;
            }
        }

        private static T? GetDelegate<T>(IntPtr module, string exportName) where T : class
        {
            try
            {
                IntPtr p = NativeLibrary.GetExport(module, exportName);
                return Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T;
            }
            catch
            {
                return null;
            }
        }

        // ---- Native signatures (best-effort based on Microsoft sample) ----

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate bool IsActiveDelegate();

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int RegisterNotificationDelegate(
            FseChangedCallback callback,
            IntPtr context,
            out IntPtr registrationHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void UnregisterNotificationDelegate(IntPtr registrationHandle);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void FseChangedCallback(IntPtr context);

        // IsApiSetImplemented is documented in apiquery2.h and used in the Microsoft guidance. :contentReference[oaicite:4]{index=4}
        [DllImport("kernelbase.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern bool IsApiSetImplemented(string contract);
    }
}
