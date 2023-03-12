using ControllerCommon.Actions;
using System.Windows.Controls;

namespace HandheldCompanion.Controls
{
    public class IMapping : UserControl
    {
        protected object Value;
        protected object prevValue;

        protected IActions Actions;
        protected IActions prevActions;

        protected object updateLock = new();

        #region events
        public event DeletedEventHandler Deleted;
        public delegate void DeletedEventHandler(object sender);
        public event UpdatedEventHandler Updated;
        public delegate void UpdatedEventHandler(object sender, IActions action);
        #endregion

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
            this.Actions = null;
            this.prevActions = null;

            Deleted?.Invoke(Value);
        }

        protected void SetIActions(IActions actions)
        {
            // update mapping IActions
            this.Actions = actions;
            this.prevActions = actions.Clone() as IActions;
        }
    }
}
