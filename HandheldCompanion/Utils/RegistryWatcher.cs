using System;
using System.Management;
using System.Security.Principal;
using HandheldCompanion.Managers;
using Microsoft.Win32;

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
        private bool _disposed;

        public RegistryWatcher(WatchedRegistry watchedRegistry, string key, string value)
        {
            _key = key;
            _value = value;

            _registry = watchedRegistry switch
            {
                WatchedRegistry.LocalMachine => Registry.LocalMachine,
                WatchedRegistry.CurrentUser => Registry.CurrentUser,
                _ => throw new ArgumentException("This part of registry is not implemented")
            };

            if (!RegistryKeyExists(_registry, _key, _value))
            {
                LogManager.LogError("Registry key '{0}' or value '{1}' does not exist.", key, value);
                return;
            }

            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            string keyPath = _key.Replace("\\", "\\\\");

            string queryString = watchedRegistry switch
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
        }

        private static bool RegistryKeyExists(RegistryKey baseKey, string subKey, string valueName)
        {
            try
            {
                using RegistryKey? key = baseKey.OpenSubKey(subKey);
                return key?.GetValue(valueName) != null;
            }
            catch
            {
                return false;
            }
        }

        public void StartWatching(bool sendData = true)
        {
            if (_query is null)
                return;

            try
            {
                if (_eventWatcher is null)
                {
                    ManagementScope scope = new ManagementScope("\\\\.\\root\\default");
                    _eventWatcher = new ManagementEventWatcher(scope, _query);
                    _eventWatcher.EventArrived += KeyWatcherOnEventArrived;
                    _eventWatcher.Start();
                }

                if (sendData)
                    SendData();
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in RegistryWatcher.StartWatching: {0}", ex.Message);
            }
        }

        public void StopWatching()
        {
            DisposeEventWatcher();
        }

        private void DisposeEventWatcher()
        {
            if (_eventWatcher is null)
                return;

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
            try
            {
                using RegistryKey? key = _registry.OpenSubKey(_key);
                object? data = key?.GetValue(_value);
                RegistryChanged?.Invoke(this, new RegistryChangedEventArgs(data));
            }
            catch (Exception ex)
            {
                LogManager.LogError("Error in RegistryWatcher.SendData: {0}", ex.Message);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisposeEventWatcher();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}