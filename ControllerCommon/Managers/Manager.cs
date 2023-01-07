namespace ControllerCommon.Managers
{
    public abstract class Manager
    {
        public bool IsEnabled { get; set; }
        public bool IsInitialized { get; set; }
        protected string Path { get; set; }

        public event InitializedEventHandler Initialized;
        public delegate void InitializedEventHandler();

        public virtual void Start()
        {
            IsInitialized = true;
            Initialized?.Invoke();

            LogManager.LogInformation("{0} has started", this.ToString());
        }

        public virtual void Stop()
        {
            IsInitialized = false;

            LogManager.LogInformation("{0} has stopped", this.ToString());
        }
    }
}
