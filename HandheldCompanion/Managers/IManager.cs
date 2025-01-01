using HandheldCompanion.Shared;

namespace HandheldCompanion.Managers
{
    public enum ManagerStatus
    {
        None = 0,
        Initializing = 1,
        Initialized = 2,
        Halting = 4,
        Halted = 8,
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
        public bool IsRunning => Status == ManagerStatus.Initializing || Status == ManagerStatus.Initialized;

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
    }
}
