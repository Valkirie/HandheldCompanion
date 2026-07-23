using HandheldCompanion.Misc;
using HandheldCompanion.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace HandheldCompanion.Managers
{
    public class CollectionManager : IManager
    {
        #region events
        public delegate void CollectionAddedEventHandler(GameCollection collection);
        public event CollectionAddedEventHandler? CollectionAdded;

        public delegate void CollectionRemovedEventHandler(GameCollection collection);
        public event CollectionRemovedEventHandler? CollectionRemoved;

        public delegate void CollectionUpdatedEventHandler(GameCollection collection);
        public event CollectionUpdatedEventHandler? CollectionUpdated;
        #endregion

        private readonly string _filePath;
        private List<GameCollection> _collections = [];
        private readonly object _lock = new();

        public CollectionManager()
        {
            ManagerPath = Path.Combine(App.SettingsPath, "collections");
            if (!Directory.Exists(ManagerPath))
                Directory.CreateDirectory(ManagerPath);

            _filePath = Path.Combine(ManagerPath, "collections.json");
        }

        public override void Start()
        {
            if (Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized))
                return;

            base.PrepareStart();

            if (File.Exists(_filePath))
            {
                try
                {
                    string json = File.ReadAllText(_filePath);
                    _collections = JsonConvert.DeserializeObject<List<GameCollection>>(json) ?? [];
                }
                catch
                {
                    _collections = [];
                }
            }

            base.Start();
        }

        public override void Stop()
        {
            if (Status.HasFlag(ManagerStatus.Halting) || Status.HasFlag(ManagerStatus.Halted))
                return;

            base.PrepareStop();
            base.Stop();
        }

        public IReadOnlyList<GameCollection> GetCollections()
        {
            if (!Monitor.TryEnter(_lock, TimeSpan.FromSeconds(2)))
                return [];

            try
            {
                return [.. _collections];
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        public GameCollection? GetCollection(Guid id)
        {
            if (!Monitor.TryEnter(_lock, TimeSpan.FromSeconds(2)))
                return null;

            try
            {
                return _collections.FirstOrDefault(c => c.Id == id);
            }
            finally
            {
                Monitor.Exit(_lock);
            }
        }

        public GameCollection CreateCollection(string name)
        {
            GameCollection collection = new() { Name = name };
            lock (_lock)
                _collections.Add(collection);

            Save();
            CollectionAdded?.Invoke(collection);
            LogManager.LogInformation("Created collection: {0}", name);
            return collection;
        }

        public bool DeleteCollection(Guid id)
        {
            GameCollection? collection;
            lock (_lock)
            {
                collection = _collections.FirstOrDefault(c => c.Id == id);
                if (collection is null || collection.IsBuiltIn)
                    return false;

                _collections.Remove(collection);
            }

            Save();
            ManagerFactory.profileManager.RemoveCollectionFromProfiles(id);
            CollectionRemoved?.Invoke(collection);
            LogManager.LogInformation("Deleted collection: {0}", collection.Name);
            return true;
        }

        public bool RenameCollection(Guid id, string newName)
        {
            GameCollection? collection;
            lock (_lock)
            {
                collection = _collections.FirstOrDefault(c => c.Id == id);
                if (collection is null || collection.IsBuiltIn)
                    return false;

                collection.Name = newName;
            }

            Save();
            CollectionUpdated?.Invoke(collection);
            return true;
        }

        private void Save()
        {
            try
            {
                List<GameCollection> snapshot;
                lock (_lock)
                    snapshot = [.. _collections];

                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                File.WriteAllText(_filePath, json);
            }
            catch { }
        }
    }
}
