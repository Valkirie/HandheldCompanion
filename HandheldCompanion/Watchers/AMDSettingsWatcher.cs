using HandheldCompanion.GraphicsProcessingUnit;
using HandheldCompanion.Managers;
using HandheldCompanion.Notifications;
using HandheldCompanion.Utils;

namespace HandheldCompanion.Watchers
{
    public class AMDSettingsWatcher : ISpaceWatcher
    {
        private AMDIntegerScalingNotification AMDIntegerScalingNotification = new AMDIntegerScalingNotification();

        public AMDSettingsWatcher()
        { }

        public override void Start()
        {
            // manage events
            ManagerFactory.gpuManager.Hooked += GpuManager_Hooked;
            base.Start();
        }

        public override void Stop()
        {
            // manage events
            ManagerFactory.gpuManager.Hooked -= GpuManager_Hooked;
            base.Stop();
        }

        private void GpuManager_Hooked(GPU GPU)
        {
            if (GPU is AMDGPU aMDGPU)
            {
                // read OS specific values
                int EmbeddedIntegerScalingSupport = RegistryUtils.GetInt(@"SYSTEM\ControlSet001\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DalEmbeddedIntegerScalingSupport");
                switch (EmbeddedIntegerScalingSupport)
                {
                    default:
                    case 0:
                        ManagerFactory.notificationManager.Add(AMDIntegerScalingNotification);
                        break;
                    case 1:
                        ManagerFactory.notificationManager.Discard(AMDIntegerScalingNotification);
                        break;
                }
            }
        }
    }
}
