namespace ControllerCommon.Managers
{
    public abstract class Manager
    {
        public bool IsEnabled { get; set; }
        public bool IsInitialized { get; set; }

        public virtual void Start()
        {
            IsInitialized = true;
        }

        public virtual void Stop()
        {
        }
    }
}
