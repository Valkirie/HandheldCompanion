using System;
using System.Threading;
using System.Threading.Tasks;

namespace HandheldCompanion.Misc.Threading.Tasks
{
    /**
     * Source: https://github.com/Gentlee/SerialQueue
     *
     * C# implementation of a lightweight FIFO (First-In-First-Out) serial queue
     * It is designed to provide synchronization for concurrent access to a shared resource by ensuring
     * that actions or functions are executed in the order they are enqueued.
     *
     * When an action or function is enqueued, the `SerialQueue` acquires the spin lock to ensure exclusive access.
     * It checks the reference to the last executed task to determine if there is an existing task.
     *
     * - If there is, it sets up a continuation using `ContinueWith` to execute the enqueued action or function after the last task completes.
     *      The `TaskContinuationOptions.ExecuteSynchronously` option ensures that the continuation runs on the same thread.
     * - If there is no last task, a new task is created using `Task.Run` to execute the action or function.
     * 
     * After setting up the task, the `SerialQueue` updates the reference to the last task using the weak reference.
     * Finally, it releases the spin lock before returning the task.
     *
     * This implementation allows for efficient synchronization without blocking the caller's thread.
     * It leverages the power of the Task Parallel Library (TPL) to execute actions and functions asynchronously.
     */
    public class SerialQueue
    {
        // To Manage thread synchronization
        SpinLock _spinLock = new(false);

        // Maintains a weak reference to the last executed Task
        readonly WeakReference<Task?> _lastTask = new(null);

        /**
         * Enqueues an action to be executed.
         * Returns a `Task` representing the asynchronous operation.
         */
        public Task Enqueue(Action action)
        {
            bool gotLock = false;
            try
            {
                Task? lastTask;
                Task resultTask;

                _spinLock.Enter(ref gotLock);

                if (_lastTask.TryGetTarget(out lastTask))
                {
                    resultTask = lastTask.ContinueWith(_ => action(), TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    resultTask = Task.Run(action);
                }

                _lastTask.SetTarget(resultTask);

                return resultTask;
            }
            finally
            {
                if (gotLock) _spinLock.Exit(false);
            }
        }

        /**
         * Enqueues a function to be executed.
         * Returns a `Task<T>` representing the asynchronous operation.
         */
        public Task<T> Enqueue<T>(Func<T> function)
        {
            bool gotLock = false;
            try
            {
                Task? lastTask;
                Task<T> resultTask;

                _spinLock.Enter(ref gotLock);

                if (_lastTask.TryGetTarget(out lastTask))
                {
                    resultTask = lastTask.ContinueWith(_ => function(), TaskContinuationOptions.ExecuteSynchronously);
                }
                else
                {
                    resultTask = Task.Run(function);
                }

                _lastTask.SetTarget(resultTask);

                return resultTask;
            }
            finally
            {
                if (gotLock) _spinLock.Exit(false);
            }
        }

        /**
         * Enqueues an asynchronous action (a function that returns a `Task`) to be executed.
         * Returns a `Task` representing the asynchronous operation.
         */
        public Task Enqueue(Func<Task> asyncAction)
        {
            bool gotLock = false;
            try
            {
                Task? lastTask;
                Task resultTask;

                _spinLock.Enter(ref gotLock);

                if (_lastTask.TryGetTarget(out lastTask))
                {
                    resultTask = lastTask.ContinueWith(_ => asyncAction(), TaskContinuationOptions.ExecuteSynchronously).Unwrap();
                }
                else
                {
                    resultTask = Task.Run(asyncAction);
                }

                _lastTask.SetTarget(resultTask);

                return resultTask;
            }
            finally
            {
                if (gotLock) _spinLock.Exit(false);
            }
        }

        /**
         * Enqueues an asynchronous function (a function that returns a `Task<T>`) to be executed.
         * Returns a `Task<T>` representing the asynchronous operation.
         */
        public Task<T> Enqueue<T>(Func<Task<T>> asyncFunction)
        {
            bool gotLock = false;
            try
            {
                Task? lastTask;
                Task<T> resultTask;

                _spinLock.Enter(ref gotLock);

                if (_lastTask.TryGetTarget(out lastTask))
                {
                    resultTask = lastTask.ContinueWith(_ => asyncFunction(), TaskContinuationOptions.ExecuteSynchronously).Unwrap();
                }
                else
                {
                    resultTask = Task.Run(asyncFunction);
                }

                _lastTask.SetTarget(resultTask);

                return resultTask;
            }
            finally
            {
                if (gotLock) _spinLock.Exit(false);
            }
        }
    }
}