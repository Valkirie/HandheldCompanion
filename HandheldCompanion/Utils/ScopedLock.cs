using System;

namespace HandheldCompanion.Utils
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
            lockRef = lockVariable;
            lockVariable.locked = true;
        }

        public void Dispose()
        {
            lockRef.locked = false;
        }
    }
}