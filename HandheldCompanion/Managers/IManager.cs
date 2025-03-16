using HandheldCompanion.Shared;
using System;

namespace HandheldCompanion.Managers
{
    [Flags]
    public enum ManagerStatus
    {
        None = 0,
        Initializing = 1,
        Initialized = 2,
        Halting = 4,
        Halted = 8,
        Busy = 16,
    }

    public class IManager
    {
        #region events
        public delegate void InitializedEventHandler();
        public event InitializedEventHandler Initialized;

        public delegate void HaltedEventHandler();
        public event HaltedEventHandler Halted;
        #endregion

        public ManagerStatus Status = ManagerStatus.None;

        public bool IsRunning => Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized);
        public bool IsBusy => Status.HasFlag(ManagerStatus.Busy);
        public bool IsReady => Status.HasFlag(ManagerStatus.Initialized);

        public virtual void PrepareStart()
        {
            // update status
            Status = ManagerStatus.Initializing;

            LogManager.LogInformation("{0} is {1}", this.GetType().Name, Status);
        }

        public virtual void Start()
        {
            // update status
            Status = ManagerStatus.Initialized;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} is {1}", this.GetType().Name, Status);
        }

        public virtual void PrepareStop()
        {
            // update status
            Status = ManagerStatus.Halting;

            LogManager.LogInformation("{0} is {1}", this.GetType().Name, Status);
        }

        public virtual void Stop()
        {
            Status = ManagerStatus.Halted;
            Halted?.Invoke();

            LogManager.LogInformation("{0} is {1}", this.GetType().Name, Status);
        }

        protected void AddStatus(ManagerStatus status)
        {
            Status |= status;
        }

        protected void RemoveStatus(ManagerStatus status)
        {
            Status &= ~status;
        }
    }
}
