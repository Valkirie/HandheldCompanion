using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace HandheldCompanion.Utils
{
    public class RegistryChangedEventArgs : EventArgs
    {
        public readonly object? Data;

        public RegistryChangedEventArgs(object? data)
        {
            Data = data;
        }
    }

    public enum WatchedRegistry
    {
        CurrentUser,
        LocalMachine,
    }

    public sealed class RegistryWatcher : IDisposable
    {
        public event EventHandler<RegistryChangedEventArgs>? RegistryChanged;

        private readonly RegistryKey _registry;
        private readonly string _key;
        private readonly string _value;
        private ManagementEventWatcher? _eventWatcher;
        private readonly WqlEventQuery _query;

        public RegistryWatcher(WatchedRegistry watchedRegistry, string key, string value)
        {
            _key = key;
            _value = value;

            var currentUser = WindowsIdentity.GetCurrent();
            var keyPath = _key.Replace("\\", "\\\\");
            var queryString = watchedRegistry switch
            {
                WatchedRegistry.LocalMachine =>
                    $"SELECT * FROM RegistryValueChangeEvent " +
                    $"WHERE Hive='HKEY_LOCAL_MACHINE' AND " +
                    $"KeyPath='{keyPath}' AND " +
                    $"ValueName='{_value}'",
                WatchedRegistry.CurrentUser =>
                    "SELECT * FROM RegistryValueChangeEvent " +
                    "WHERE Hive='HKEY_USERS' AND " +
                    $"KeyPath='{currentUser.User!.Value}\\\\{keyPath}' AND " +
                    $"ValueName='{_value}'",
                _ => throw new ArgumentOutOfRangeException(nameof(watchedRegistry), watchedRegistry, "This part of registry is not implemented")
            };
            _query = new WqlEventQuery(queryString);
            _registry = watchedRegistry switch
            {
                WatchedRegistry.LocalMachine => Registry.LocalMachine,
                WatchedRegistry.CurrentUser => Registry.CurrentUser,
                _ => throw new ArgumentException("This part of registry is not implemented")
            };
        }

        public void StartWatching()
        {
            var scope = new ManagementScope("\\\\.\\root\\default");
            _eventWatcher = new ManagementEventWatcher(scope, _query);
            _eventWatcher.EventArrived += KeyWatcherOnEventArrived;
            _eventWatcher.Start();

            SendData();
        }

        public void StopWatching()
        {
            if (_eventWatcher == null)
            {
                return;
            }
            _eventWatcher.EventArrived -= KeyWatcherOnEventArrived;
            _eventWatcher.Stop();
            _eventWatcher.Dispose();
            _eventWatcher = null;
        }

        private void KeyWatcherOnEventArrived(object sender, EventArrivedEventArgs e)
        {
            SendData();
        }

        private void SendData()
        {
            using var key = _registry.OpenSubKey(_key);
            var data = key?.GetValue(_value);
            RegistryChanged?.Invoke(this, new RegistryChangedEventArgs(data));
        }

        public void Dispose()
        {
            StopWatching();
        }
    }
}
