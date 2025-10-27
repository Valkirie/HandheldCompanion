using HandheldCompanion.Controllers;
using System;
using System.Threading;
using static HandheldCompanion.Managers.ControllerManager;

namespace HandheldCompanion.Helpers
{
    public static class EventHelper
    {
        public static void RaiseInputsUpdatedAsync(InputsUpdatedEventHandler? handlers, ControllerState state, bool isMapped)
        {
            if (handlers is null) return;

            Delegate[] invocation = handlers.GetInvocationList();

            // Single thread-pool hop; iterate all handlers on that worker.
            ThreadPool.UnsafeQueueUserWorkItem(static s =>
            {
                var (inv, st, mapped) = ((Delegate[], ControllerState, bool))s!;
                for (int i = 0; i < inv.Length; i++)
                {
                    var h = (InputsUpdatedEventHandler)inv[i];
                    try { h(st, mapped); } catch { /* swallow or metric */ }
                }
            }, (invocation, state, isMapped), preferLocal: true);
        }
    }

}
