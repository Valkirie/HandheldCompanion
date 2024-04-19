using System;
using System.Threading;

namespace HandheldCompanion.Utils
{
    /// <summary>
    /// Provides a cross-thread locking mechanism using a SemaphoreSlim.
    /// This class is intended to be used in scenarios where a lock is needed across multiple threads,
    /// and it supports both blocking and non-blocking acquisition of the lock.
    /// </summary>
    public class CrossThreadLock : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _isEntered;

        /// <summary>
        /// Initializes a new instance of the CrossThreadLock class.
        /// </summary>
        public CrossThreadLock(int initialCount = 1, int maxCount = 1)
        {
            // Initialize the semaphore with a capacity of 1, allowing only one thread to enter at a time.
            _semaphore = new SemaphoreSlim(initialCount, maxCount);
        }

        /// <summary>
        /// Attempts to enter the lock without blocking more than specified timeout. Returns a value indicating whether the lock was successfully entered.
        /// </summary>
        /// <returns>True if the lock was successfully acquired; otherwise, false.</returns>
        public bool TryEnter(int millisecondsTimeout = 0)
        {
            // Attempt to enter the semaphore without blocking. If successful, set _isEntered to true.
            _isEntered = _semaphore.Wait(millisecondsTimeout);
            return _isEntered;
        }

        /// <summary>
        /// Enters the lock, blocking the calling thread until the lock can be entered.
        /// </summary>
        public void Enter()
        {
            // Block until the semaphore can be entered, then set _isEntered to true.
            _semaphore.Wait();
            _isEntered = true;
        }

        /// <summary>
        /// Returns current lock status.
        /// </summary>
        /// <returns>True if the lock is set; otherwise, false.</returns>
        public bool IsLocked()
        {
            return _isEntered;
        }

        /// <summary>
        /// Releases the lock if it has been entered.
        /// </summary>
        public void Exit()
        {
            // If the lock has been entered, release the semaphore and reset _isEntered to false.
            _semaphore.Release();
            _isEntered = false;
        }

        /// <summary>
        /// Releases all resources used by the CrossThreadLock.
        /// </summary>
        public void Dispose()
        {
            // Ensure the lock is released if it has been entered, then dispose of the semaphore.
            Exit();
            _semaphore.Dispose();
            GC.SuppressFinalize(this);
        }

        public bool IsEntered()
        {
            return _isEntered;
        }
    }
}
