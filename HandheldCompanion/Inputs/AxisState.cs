using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace HandheldCompanion.Inputs;

[Serializable]
public partial class AxisState : ICloneable, IDisposable
{
    public static readonly AxisFlags[] AllAxis = Enum.GetValues<AxisFlags>();
    public static readonly AxisLayoutFlags[] AllAxisLayoutFlags = Enum.GetValues<AxisLayoutFlags>();

    // Runtime storage (no locks, no dictionaries)
    [JsonIgnore]
    private short[] _values = new short[MaxValue];
    private const int MaxValue = (int)AxisFlags.Max;

    // Kept only so existing JSON (de)serialization does not break.
    public Dictionary<AxisFlags, short> State = new();

    private bool _disposed = false;

    public AxisState(Dictionary<AxisFlags, short> state)
    {
        _values = new short[MaxValue];
        if (state != null)
        {
            for (int i = 0; i < MaxValue; i++)
                _values[i] = state.TryGetValue((AxisFlags)i, out var v) ? v : (short)0;
        }
    }

    public AxisState()
    {
        _values = new short[MaxValue];
    }

    ~AxisState() => Dispose(false);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOf(AxisFlags axis) => (int)axis;

    public short this[AxisFlags axis]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            int idx = (int)axis;
            return (idx < _values.Length && idx >= 0) ? _values[idx] : (short)0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            int idx = (int)axis;
            if (idx < _values.Length && idx >= 0)
                _values[idx] = value;
        }
    }

    [JsonIgnore]
    public IEnumerable<AxisFlags> Axis
    {
        get
        {
            List<AxisFlags> list = new List<AxisFlags>();
            for (int i = 0; i < MaxValue; i++)
                if (_values[i] != 0) list.Add((AxisFlags)i);
            return list;
        }
    }

    public object Clone()
    {
        var clone = new AxisState();
        Array.Copy(_values, clone._values, _values.Length);
        return clone;
    }

    public bool IsEmpty()
    {
        for (int i = 0; i < _values.Length; i++)
            if (_values[i] != 0) return false;
        return true;
    }

    public void Clear()
    {
        Array.Clear(_values, 0, _values.Length);
    }

    public bool Contains(AxisState other)
    {
        if (other is null) return false;
        for (int i = 0; i < _values.Length; i++)
            if (_values[i] != other._values[i]) return false;
        return true;
    }

    public bool ContainsTrue(AxisState other)
    {
        if (other is null) return false;

        bool emptyThis = true, emptyOther = true;
        for (int i = 0; i < _values.Length; i++)
        {
            if (_values[i] != 0) emptyThis = false;
            if (other._values[i] != 0) emptyOther = false;
        }
        if (emptyThis || emptyOther) return false;

        // every non-zero in other must match in this
        for (int i = 0; i < _values.Length; i++)
            if (other._values[i] != 0 && _values[i] != other._values[i]) return false;

        return true;
    }

    public void AddRange(AxisState other)
    {
        if (other is null) return;
        for (int i = 0; i < _values.Length; i++)
            _values[i] = other._values[i];
    }

    public override bool Equals(object obj)
    {
        if (obj is not AxisState other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (other._values.Length != _values.Length) return false;

        for (int i = 0; i < _values.Length; i++)
            if (_values[i] != other._values[i]) return false;

        return true;
    }

    public override int GetHashCode()
    {
        int h = 17;
        for (int i = 0; i < _values.Length; i++)
            if (_values[i] != 0) h = (h * 31) ^ (_values[i] + i + 1);
        return h;
    }

    public static bool EqualsWithValues(AxisState a, AxisState b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        return a.Equals(b);
    }

    public static bool EqualsWithValues(Dictionary<AxisFlags, short> obj1, Dictionary<AxisFlags, short> obj2)
    {
        if (ReferenceEquals(obj1, obj2)) return true;
        if (obj1 is null || obj2 is null) return false;

        // Snapshot and compare
        var a = obj1.ToArray();
        var b = obj2.ToArray();
        if (a.Length != b.Length) return false;

        var set = new Dictionary<AxisFlags, short>(b.Length);
        foreach (var kv in b) set[kv.Key] = kv.Value;

        foreach (var kv in a)
            if (!set.TryGetValue(kv.Key, out var v) || v != kv.Value)
                return false;

        return true;
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
            Array.Clear(_values, 0, _values.Length);
            _values = Array.Empty<short>();
            State?.Clear(); // kept for JSON compatibility only
        }
        _disposed = true;
    }

    // ---- JSON glue: keep State only for (de)serialization compatibility ----

    [OnSerializing]
    private void OnSerializing(StreamingContext _)
    {
        State = new Dictionary<AxisFlags, short>(MaxValue);
        for (int i = 0; i < MaxValue; i++)
            State[(AxisFlags)i] = _values[i];
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext _)
    {
        if (_values == null || _values.Length != MaxValue)
            _values = new short[MaxValue];

        if (State == null || State.Count == 0)
        {
            Array.Clear(_values, 0, _values.Length);
            return;
        }

        for (int i = 0; i < MaxValue; i++)
            _values[i] = State.TryGetValue((AxisFlags)i, out var v) ? v : (short)0;
    }
}
