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
        Failed = 32,
    }

    public class IManager
    {
        #region events
        public delegate void InitializedEventHandler();
        public event InitializedEventHandler Initialized;

        public delegate void HaltedEventHandler();
        public event HaltedEventHandler Halted;

        public delegate void StatusChangedEventHandler(ManagerStatus status);
        public event StatusChangedEventHandler StatusChanged;
        #endregion

        private ManagerStatus _Status = ManagerStatus.None;
        public ManagerStatus Status
        {
            get => _Status;
            set
            {
                if (_Status != value)
                {
                    _Status = value;
                    StatusChanged?.Invoke(value);
                }
            }
        }
        protected string ManagerPath = string.Empty;

        public bool IsRunning => Status.HasFlag(ManagerStatus.Initializing) || Status.HasFlag(ManagerStatus.Initialized);
        public bool IsBusy => Status.HasFlag(ManagerStatus.Busy);
        public bool IsReady => Status.HasFlag(ManagerStatus.Initialized);

        public bool SuspendWithOS = false;

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

        public virtual void Resume()
        {
            Start();
        }

        public virtual void Suspend()
        {
            Stop();
        }

        protected virtual void AddStatus(ManagerStatus status, params object[] args)
        {
            Status |= status;
        }

        protected virtual void RemoveStatus(ManagerStatus status, params object[] args)
        {
            Status &= ~status;
        }
    }
}
