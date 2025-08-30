using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace HandheldCompanion.Inputs;

[Serializable]
public partial class ButtonState : ICloneable, IDisposable
{
    public static readonly ButtonFlags[] AllButtons = Enum.GetValues<ButtonFlags>();
    private const int MaxValue = (int)ButtonFlags.Max;
    private const int MaxButton = (int)ButtonFlags.B15;

    [JsonIgnore]
    private bool[] _pressed = new bool[MaxValue];

    // Kept only so existing JSON (de)serialization does not break
    public Dictionary<ButtonFlags, bool> State = new();

    private bool _disposed = false;

    public ButtonState(Dictionary<ButtonFlags, bool> state)
    {
        // initialize array from incoming dictionary
        _pressed = new bool[MaxValue];
        if (state != null)
        {
            for (int i = 0; i < MaxValue; i++)
                _pressed[i] = state.TryGetValue((ButtonFlags)i, out var v) && v;
        }
    }

    public ButtonState()
    {
        _pressed = new bool[MaxValue];
    }

    ~ButtonState() => Dispose(false);

    public bool this[ButtonFlags button]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int idx = (int)button;
            return (idx < _pressed.Length && idx >= 0) ? _pressed[idx] : false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            int idx = (int)button;
            if (idx < _pressed.Length && idx >= 0)
                _pressed[idx] = value;
        }
    }

    [JsonIgnore]
    public IEnumerable<ButtonFlags> Buttons
    {
        get
        {
            // snapshot of currently pressed buttons
            List<ButtonFlags> list = new List<ButtonFlags>();
            for (int i = 0; i < MaxValue; i++)
                if (_pressed[i]) list.Add((ButtonFlags)i);
            return list;
        }
    }

    public object Clone()
    {
        var clone = new ButtonState();
        Array.Copy(_pressed, clone._pressed, _pressed.Length);
        return clone;
    }

    public bool IsEmpty()
    {
        for (int i = 0; i < _pressed.Length; i++)
            if (_pressed[i]) return false;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_pressed, 0, _pressed.Length);
    }

    public bool Contains(ButtonState other)
    {
        if (other is null) return false;
        // Equivalent to: for every button, this[b] == other[b]
        for (int i = 0; i < MaxValue; i++)
            if (_pressed[i] != other._pressed[i]) return false;
        return true;
    }

    public bool ContainsTrue(ButtonState other)
    {
        if (other is null) return false;

        bool emptyThis = true, emptyOther = true;
        for (int i = 0; i < _pressed.Length; i++)
        {
            if (_pressed[i]) emptyThis = false;
            if (other._pressed[i]) emptyOther = false;
        }
        if (emptyThis || emptyOther) return false;

        // every true in other must be true in this
        for (int i = 0; i < _pressed.Length; i++)
            if (other._pressed[i] && !_pressed[i]) return false;

        return true;
    }

    public void AddRange(ButtonState other)
    {
        if (other is null) return;
        for (int i = 0; i < _pressed.Length; i++)
            if (other._pressed[i]) _pressed[i] = true;
    }

    public static void Overwrite(ButtonState origin, ButtonState target)
    {
        if (origin is null || target is null) return;
        if (target._pressed.Length != origin._pressed.Length)
            target._pressed = new bool[origin._pressed.Length];

        Array.Copy(origin._pressed, target._pressed, origin._pressed.Length);
    }

    public static void Inject(ButtonState origin, ButtonState target)
    {
        if (origin is null || target is null) return;
        for (int i = 0; i < origin._pressed.Length; i++)
        {
            if (origin._pressed[i] != target._pressed[i])
                target._pressed[i] = true;
        }
    }

    public override bool Equals(object obj)
    {
        if (obj is not ButtonState other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other._pressed.Length != _pressed.Length) return false;

        for (int i = 0; i < _pressed.Length; i++)
            if (_pressed[i] != other._pressed[i]) return false;

        return true;
    }

    public override int GetHashCode()
    {
        // simple hash on pressed bits
        int h = 17;
        for (int i = 0; i < _pressed.Length; i++)
            if (_pressed[i]) h = (h * 31) ^ (i + 1);
        return h;
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
            Array.Clear(_pressed, 0, _pressed.Length);
            _pressed = Array.Empty<bool>();
            State?.Clear(); // kept for JSON compatibility only
        }
        _disposed = true;
    }

    [OnSerializing]
    private void OnSerializing(StreamingContext _)
    {
        // Populate State from array so JSON stays backward compatible
        // Only populate non hotkey buttons
        State = new Dictionary<ButtonFlags, bool>(MaxButton);
        for (int i = 0; i <= MaxButton; i++)
            State[(ButtonFlags)i] = _pressed[i];
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext _)
    {
        // Populate array from State
        if (_pressed == null || _pressed.Length != MaxValue)
            _pressed = new bool[MaxValue];

        if (State == null || State.Count == 0)
        {
            Array.Clear(_pressed, 0, _pressed.Length);
            return;
        }

        for (int i = 0; i < MaxValue; i++)
            _pressed[i] = State.TryGetValue((ButtonFlags)i, out var v) && v;
    }
}