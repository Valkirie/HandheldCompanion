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
            // Generic overload avoids boxing the ValueTuple state.
            ThreadPool.UnsafeQueueUserWorkItem(
                static s =>
                {
                    for (int i = 0; i < s.inv.Length; i++)
                    {
                        var h = (InputsUpdatedEventHandler)s.inv[i];
                        try { h(s.st, s.mapped); } catch { /* swallow or metric */ }
                    }
                },
                (inv: invocation, st: state, mapped: isMapped),
                preferLocal: true);
        }
    }

}
