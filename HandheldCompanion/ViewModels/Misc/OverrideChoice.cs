using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace HandheldCompanion.ViewModels.Misc
{
    /// <summary>
    /// One entry of a layered setting combo: Inherit (with the value it resolves to) or one of the setting's own values.
    /// </summary>
    public sealed class OverrideChoice : INotifyPropertyChanged
    {
        public const string InheritKey = "inherit";

        private string _label;
        private bool _isEnabled;

        public OverrideChoice(string key, string label, bool isEnabled = true, bool isStale = false)
        {
            Key = key;
            _label = label;
            _isEnabled = isEnabled;
            IsStale = isStale;
        }

        // value identity, stable across refreshes: combos select through it
        public string Key { get; }

        public bool IsInherit => Key == InheritKey;

        // stored override value the current list doesn't offer, listed so the combo still shows what is saved
        public bool IsStale { get; }

        public string Label
        {
            get => _label;
            set
            {
                if (_label == value)
                    return;

                _label = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public override string ToString() => Label;
    }

    /// <summary>
    /// Choices of one layered setting combo: an optional Inherit entry first, then the setting's own values.
    /// </summary>
    public sealed class OverrideChoiceList
    {
        public IReadOnlyList<OverrideChoice> Items { get; private set; } = [];

        // the entry a combo selects: combos bind SelectedItem, a replaced list hands them new objects to select
        public OverrideChoice? Find(string? key) => Suspended ? null : Items.FirstOrDefault(c => c.Key == key);

        // while set, the selection reads as nothing so a combo lets go of its old entry
        public bool Suspended { get; private set; }

        /// <summary>
        /// Hands a replaced list to its combo: the selection is let go before the list changes and set again after it,
        /// so the combo never holds an entry of the old list. A combo can drop its selection while it swaps the items
        /// (and keep the new entry as its value), so the same is repeated once the UI handled the change.
        /// </summary>
        public void Publish(Action<string> notify, string itemsProperty, string selectionProperty)
        {
            Suspended = true;
            notify(selectionProperty);
            notify(itemsProperty);
            Suspended = false;
            notify(selectionProperty);

            System.Windows.Threading.Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.HasShutdownStarted)
                return;

            dispatcher.BeginInvoke(() =>
            {
                Suspended = true;
                notify(selectionProperty);
                Suspended = false;
                notify(selectionProperty);
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Updates the entries: the stored value when the list doesn't offer it, Inherit (a null label lists none), then the values.
        /// Only listing the stored value writes nothing, picking it again is no change.
        /// Returns true when the list was replaced; same keys keep the list and its entries and only update their labels,
        /// so an open combo keeps its items and gamepad focus.
        /// </summary>
        public bool Update(string? inheritLabel, IEnumerable<(string Key, string Label, bool IsEnabled)> values, string? selectedKey = null, Func<string>? storedLabel = null)
        {
            List<(string Key, string Label, bool IsEnabled, bool IsStale)> wanted = [];
            if (inheritLabel is not null)
                wanted.Add((OverrideChoice.InheritKey, inheritLabel, true, false));
            wanted.AddRange(values.Select(v => (v.Key, v.Label, v.IsEnabled, false)));

            if (!string.IsNullOrEmpty(selectedKey) && storedLabel is not null && !wanted.Any(w => w.Key == selectedKey))
                wanted.Insert(0, (selectedKey, string.Format(Properties.Resources.ProfilesPage_ValueNotAvailable, storedLabel()), true, true));

            if (Items.Select(c => (c.Key, c.IsStale)).SequenceEqual(wanted.Select(w => (w.Key, w.IsStale))))
            {
                for (int i = 0; i < wanted.Count; i++)
                {
                    Items[i].Label = wanted[i].Label;
                    Items[i].IsEnabled = wanted[i].IsEnabled;
                }

                return false;
            }

            Items = wanted.Select(w => new OverrideChoice(w.Key, w.Label, w.IsEnabled, w.IsStale)).ToList();
            return true;
        }
    }
}
