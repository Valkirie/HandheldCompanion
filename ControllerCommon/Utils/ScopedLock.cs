using System;
using System.Diagnostics;

namespace ControllerCommon.Utils
{
    public class LockObject
    {
        public bool locked = false;
        public LockObject() { }

        public static implicit operator bool(LockObject o)
        {
            return o.locked;
        }
    }

    public class ScopedLock : IDisposable
    {
        private LockObject lockRef;

        public ScopedLock(LockObject lockVariable)
        {
            // not re-entrant, at least for now
            Debug.Assert(lockVariable == false);

            lockRef = lockVariable;
            lockVariable.locked = true;
        }

        public void Dispose()
        {
            lockRef.locked = false;
        }
    }
}