using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ControllerCommon.Inputs;

[Serializable]
public class ButtonState : ICloneable
{
    public ConcurrentDictionary<ButtonFlags, bool> State = new(Environment.ProcessorCount * 2, (int)ButtonFlags.Max);

    public ButtonState(ConcurrentDictionary<ButtonFlags, bool> buttonState)
    {
        foreach (var state in buttonState)
            this[state.Key] = state.Value;
    }

    public ButtonState()
    {
        foreach (ButtonFlags flags in Enum.GetValues(typeof(ButtonFlags)))
            State[flags] = false;
    }

    public bool this[ButtonFlags button]
    {
        get => State.ContainsKey(button) && State[button];

        set => State[button] = value;
    }

    [JsonIgnore]
    public IEnumerable<ButtonFlags> Buttons => State.Where(a => a.Value).Select(a => a.Key).ToList();

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
        foreach (var state in buttonState.State.Where(a => a.Value))
            this[state.Key] = state.Value;
    }

    public override bool Equals(object obj)
    {
        if (obj is ButtonState buttonState)
            return buttonState.Buttons.SequenceEqual(Buttons);

        return false;
    }
}