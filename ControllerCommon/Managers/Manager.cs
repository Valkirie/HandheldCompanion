namespace ControllerCommon.Managers;

public abstract class Manager
{
    public delegate void InitializedEventHandler();

    public bool IsEnabled { get; set; }
    public bool IsInitialized { get; set; }
    protected string InstallPath { get; set; }

    public event InitializedEventHandler Initialized;

    public virtual void Start()
    {
        IsInitialized = true;
        Initialized?.Invoke();

        LogManager.LogInformation("{0} has started", ToString());
    }

    public virtual void Stop()
    {
        IsInitialized = false;

        LogManager.LogInformation("{0} has stopped", ToString());
    }
}