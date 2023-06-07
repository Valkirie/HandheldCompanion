using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ControllerCommon.Inputs;

[Serializable]
public class AxisState : ICloneable
{
    public ConcurrentDictionary<AxisFlags, short> State = new();

    public AxisState(ConcurrentDictionary<AxisFlags, short> axisState)
    {
        foreach (var state in axisState)
            this[state.Key] = state.Value;
    }

    public AxisState()
    {
    }

    public short this[AxisFlags axis]
    {
        get
        {
            if (!State.ContainsKey(axis)) return 0;

            return State[axis];
        }

        set => State[axis] = value;
    }

    [JsonIgnore] public IEnumerable<AxisFlags> Axis => State.Where(a => a.Value != 0).Select(a => a.Key).ToList();

    public object Clone()
    {
        return new AxisState(State);
    }

    public bool IsEmpty()
    {
        return !Axis.Any();
    }

    public void Clear()
    {
        State.Clear();
    }

    public bool Contains(AxisState axisState)
    {
        foreach (var state in axisState.State)
            if (this[state.Key] != state.Value)
                return false;

        return true;
    }

    public bool ContainsTrue(AxisState axisState)
    {
        if (IsEmpty() || axisState.IsEmpty())
            return false;

        foreach (var state in axisState.State.Where(a => a.Value is not 0))
            if (this[state.Key] != state.Value)
                return false;

        return true;
    }

    public void AddRange(AxisState axisState)
    {
        foreach (var state in axisState.State)
            this[state.Key] = state.Value;
    }

    public override bool Equals(object obj)
    {
        var axisState = obj as AxisState;
        if (axisState != null)
            return EqualsWithValues(State, axisState.State);

        return false;
    }

    public static bool EqualsWithValues(ConcurrentDictionary<AxisFlags, short> obj1,
        ConcurrentDictionary<AxisFlags, short> obj2)
    {
        var result = false;
        if (obj1.Count == obj2.Count)
        {
            result = true;
            {
                foreach (var item in obj1)
                {
                    if (obj2.TryGetValue(item.Key, out var value))
                    {
                        if (!value.Equals(item.Value)) return false;

                        continue;
                    }

                    return false;
                }

                return result;
            }
        }

        return result;
    }
}