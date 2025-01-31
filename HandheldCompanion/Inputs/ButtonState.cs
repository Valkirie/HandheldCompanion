using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HandheldCompanion.Inputs;

[Serializable]
public partial class ButtonState : ICloneable, IDisposable
{
    public ConcurrentDictionary<ButtonFlags, bool> State = new();
    private bool _disposed = false; // Prevent multiple disposals

    public ButtonState(ConcurrentDictionary<ButtonFlags, bool> State)
    {
        foreach (var state in State)
            this[state.Key] = state.Value;
    }

    public ButtonState()
    {
        foreach (ButtonFlags flags in Enum.GetValues(typeof(ButtonFlags)))
            State[flags] = false;
    }

    ~ButtonState()
    {
        Dispose(false);
    }

    public bool this[ButtonFlags button]
    {
        get => State != null && State.TryGetValue(button, out bool value) && value;

        set => State[button] = value;
    }

    [JsonIgnore]
    public IEnumerable<ButtonFlags> Buttons => State.Where(a => a.Value).Select(a => a.Key);

    public object Clone()
    {
        return new ButtonState(State);
    }

    public bool IsEmpty()
    {
        return !Buttons.Any();
    }

    public void Clear()
    {
        State.Clear();
    }

    public bool Contains(ButtonState buttonState)
    {
        return buttonState.State.All(state => this[state.Key] == state.Value);
    }

    public bool ContainsTrue(ButtonState buttonState)
    {
        if (IsEmpty() || buttonState.IsEmpty())
            return false;

        return buttonState.State.Where(a => a.Value).All(state => this[state.Key] == state.Value);
    }

    public void AddRange(ButtonState buttonState)
    {
        // only add pressed button
        foreach (KeyValuePair<ButtonFlags, bool> state in buttonState.State.Where(a => a.Value))
            this[state.Key] = state.Value;
    }

    public static void Overwrite(ButtonState origin, ButtonState target)
    {
        foreach (KeyValuePair<ButtonFlags, bool> state in origin.State)
            target[state.Key] = origin[state.Key];
    }

    public override bool Equals(object obj)
    {
        if (obj is ButtonState buttonState)
        {
            if (Buttons.Count() != buttonState.Buttons.Count())
                return false;

            // Use a simple sequence comparison if ordering is irrelevant
            foreach (var button in Buttons)
                if (!buttonState.Buttons.Contains(button))
                    return false;

            return true;
        }

        return false;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Free managed resources
            State?.Clear();
            State = null;
        }

        _disposed = true;
    }
}