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
        private bool _isEntered;    // indicates if the lock is currently held
        private bool _disposed;     // tracks whether Dispose has been called

        /// <summary>
        /// Initializes a new instance of the CrossThreadLock class.
        /// </summary>
        /// <param name="initialCount">Initial count for the semaphore (default is 1).</param>
        /// <param name="maxCount">Maximum count for the semaphore (default is 1).</param>
        public CrossThreadLock(int initialCount = 1, int maxCount = 1)
        {
            _semaphore = new SemaphoreSlim(initialCount, maxCount);
        }

        /// <summary>
        /// Finalizer to clean up resources if Dispose was not called.
        /// </summary>
        ~CrossThreadLock()
        {
            Dispose(false);
        }

        /// <summary>
        /// Attempts to enter the lock without blocking more than the specified timeout.
        /// Returns a value indicating whether the lock was successfully entered.
        /// </summary>
        /// <param name="millisecondsTimeout">Timeout in milliseconds.</param>
        /// <returns>True if the lock was acquired; otherwise, false.</returns>
        public bool TryEnter(int millisecondsTimeout = 0)
        {
            ThrowIfDisposed();

            bool acquired = _semaphore.Wait(millisecondsTimeout);
            if (acquired)
            {
                _isEntered = true;
            }
            return acquired;
        }

        /// <summary>
        /// Enters the lock, blocking the calling thread until the lock can be entered.
        /// </summary>
        public void Enter()
        {
            ThrowIfDisposed();

            _semaphore.Wait();
            _isEntered = true;
        }

        /// <summary>
        /// Returns the current lock status.
        /// </summary>
        /// <returns>True if the lock is held; otherwise, false.</returns>
        public bool IsEntered()
        {
            return _isEntered;
        }

        /// <summary>
        /// Releases the lock if it has been entered.
        /// </summary>
        public void Exit()
        {
            // Only call Release if the lock was acquired.
            if (_isEntered)
            {
                _semaphore.Release();
                _isEntered = false;
            }
        }

        /// <summary>
        /// Releases all resources used by the CrossThreadLock.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose to free both managed and unmanaged resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose; false if called from the finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Ensure that if the lock was held, we release it.
                    Exit();

                    // Dispose of the semaphore.
                    _semaphore.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the object has already been disposed.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CrossThreadLock));
            }
        }
    }
}
