using System.Windows.Controls;
using HandheldCompanion.Actions;

namespace HandheldCompanion.Controls;

public class IMapping : UserControl
{
    protected IActions Actions;
    protected object Value;

    protected void Update()
    {
        // update axis mapping
        Updated?.Invoke(Value, Actions);
    }

    protected void Delete()
    {
        Actions = null;

        Deleted?.Invoke(Value);
    }

    protected void SetIActions(IActions actions)
    {
        // update mapping IActions
        Actions = actions;
    }

    #region events

    public event DeletedEventHandler Deleted;

    public delegate void DeletedEventHandler(object sender);

    public event UpdatedEventHandler Updated;

    public delegate void UpdatedEventHandler(object sender, IActions action);

    #endregion
}