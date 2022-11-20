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
        }

        public virtual void Stop()
        {
            IsInitialized = false;
        }
    }
}
