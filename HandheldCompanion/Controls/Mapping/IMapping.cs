using System.Windows.Controls;
using ControllerCommon.Actions;

namespace HandheldCompanion.Controls;

public class IMapping : UserControl
{
    protected readonly object updateLock = new();

    protected IActions Actions;
    protected IActions prevActions;
    protected object prevValue;
    protected object Value;

    protected void Update()
    {
        // update axis mapping
        if (Value != prevValue || Actions != prevActions)
            Updated?.Invoke(Value, Actions);

        prevValue = Value;
        prevActions = Actions.Clone() as IActions;
    }

    protected void Delete()
    {
        Actions = null;
        prevActions = null;

        Deleted?.Invoke(Value);
    }

    protected void SetIActions(IActions actions)
    {
        // update mapping IActions
        Actions = actions;
        prevActions = actions.Clone() as IActions;
    }

    #region events

    public event DeletedEventHandler Deleted;

    public delegate void DeletedEventHandler(object sender);

    public event UpdatedEventHandler Updated;

    public delegate void UpdatedEventHandler(object sender, IActions action);

    #endregion
}