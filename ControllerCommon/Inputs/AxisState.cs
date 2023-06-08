using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ControllerCommon.Inputs;

[Serializable]
public class AxisState : ICloneable
{
    public ConcurrentDictionary<AxisFlags, short> State = new(Environment.ProcessorCount * 2, (int)AxisFlags.Max);

    public AxisState(ConcurrentDictionary<AxisFlags, short> axisState)
    {
        foreach (var state in axisState)
            this[state.Key] = state.Value;
    }

    public AxisState()
    {
        foreach (AxisFlags flags in Enum.GetValues(typeof(AxisFlags)))
            State[flags] = 0;
    }

    public short this[AxisFlags axis]
    {
        get => !State.ContainsKey(axis) ? (short)0 : State[axis];

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

        return axisState.State.Where(a => a.Value is not 0).All(state => this[state.Key] == state.Value);
    }

    public void AddRange(AxisState axisState)
    {
        foreach (var state in axisState.State)
            this[state.Key] = state.Value;
    }

    public override bool Equals(object obj)
    {
        if (obj is AxisState axisState)
            return EqualsWithValues(State, axisState.State);

        return false;
    }

    public static bool EqualsWithValues(ConcurrentDictionary<AxisFlags, short> obj1,
        ConcurrentDictionary<AxisFlags, short> obj2)
    {
        if (obj1.Count != obj2.Count) return false;
        {
            foreach (var item in obj1)
            {
                if (!obj2.TryGetValue(item.Key, out var value)) return false;
                if (!value.Equals(item.Value)) return false;
            }

            return true;
        }
    }
}