using System;
using System.Threading.Tasks;

namespace HandheldCompanion.Helpers
{
    public static class EventHelper
    {
        public static void RaiseAsync<TDelegate>(TDelegate? eventDelegate, params object[] args)
            where TDelegate : Delegate
        {
            Delegate[]? handlers = eventDelegate?.GetInvocationList();
            if (handlers == null) return;

            foreach (TDelegate handler in handlers)
            {
                Task.Run(() =>
                {
                    try { handler.DynamicInvoke(args); }
                    catch { /* swallow or count errors as discussed */ }
                });
            }
        }
    }
}
