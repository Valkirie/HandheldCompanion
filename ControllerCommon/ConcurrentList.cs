using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ControllerCommon;

public class ConcurrentList<T> : IList<T>, IDisposable
{
    private T[] _arr;
    private int _count;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public ConcurrentList(int initialCapacity)
    {
        _arr = new T[initialCapacity];
    }

    public ConcurrentList() : this(4)
    {
    }

    public ConcurrentList(IEnumerable<T> items)
    {
        _arr = items.ToArray();
        _count = _arr.Length;
    }

    public int InternalArrayLength
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _arr.Length;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            var newCount = _count + 1;
            EnsureCapacity(newCount);
            _arr[_count] = item;
            _count = newCount;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Remove(T item)
    {
        _lock.EnterUpgradeableReadLock();

        try
        {
            var i = IndexOfInternal(item);

            if (i == -1)
                return false;

            _lock.EnterWriteLock();
            try
            {
                RemoveAtInternal(i);
                return true;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        _lock.EnterReadLock();

        try
        {
            for (var i = 0; i < _count; i++)
                // deadlocking potential mitigated by lock recursion enforcement
                yield return _arr[i];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int IndexOf(T item)
    {
        _lock.EnterReadLock();
        try
        {
            return IndexOfInternal(item);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Insert(int index, T item)
    {
        _lock.EnterUpgradeableReadLock();

        try
        {
            if (index > _count)
                throw new ArgumentOutOfRangeException("index");

            _lock.EnterWriteLock();
            try
            {
                var newCount = _count + 1;
                EnsureCapacity(newCount);

                // shift everything right by one, starting at index
                Array.Copy(_arr, index, _arr, index + 1, _count - index);

                // insert
                _arr[index] = item;
                _count = newCount;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public void RemoveAt(int index)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (index >= _count)
                throw new ArgumentOutOfRangeException("index");

            _lock.EnterWriteLock();
            try
            {
                RemoveAtInternal(index);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            Array.Clear(_arr, 0, _count);
            _count = 0;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Contains(T item)
    {
        _lock.EnterReadLock();
        try
        {
            return IndexOfInternal(item) != -1;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _lock.EnterReadLock();
        try
        {
            if (_count > array.Length - arrayIndex)
                throw new ArgumentException("Destination array was not long enough.");

            Array.Copy(_arr, 0, array, arrayIndex, _count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool IsReadOnly => false;

    public T this[int index]
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                if (index >= _count)
                    throw new ArgumentOutOfRangeException("index");

                return _arr[index];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        set
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (index >= _count)
                    throw new ArgumentOutOfRangeException("index");

                _lock.EnterWriteLock();
                try
                {
                    _arr[index] = value;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }
    }

    public void AddRange(IEnumerable<T> items)
    {
        if (items == null)
            throw new ArgumentNullException("items");

        _lock.EnterWriteLock();

        try
        {
            var arr = items as T[] ?? items.ToArray();
            var newCount = _count + arr.Length;
            EnsureCapacity(newCount);
            Array.Copy(arr, 0, _arr, _count, arr.Length);
            _count = newCount;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    private void EnsureCapacity(int capacity)
    {
        if (_arr.Length >= capacity)
            return;

        int doubled;
        checked
        {
            try
            {
                doubled = _arr.Length * 2;
            }
            catch (OverflowException)
            {
                doubled = int.MaxValue;
            }
        }

        var newLength = Math.Max(doubled, capacity);
        Array.Resize(ref _arr, newLength);
    }

    private int IndexOfInternal(T item)
    {
        return Array.FindIndex(_arr, 0, _count, x => x.Equals(item));
    }

    private void RemoveAtInternal(int index)
    {
        Array.Copy(_arr, index + 1, _arr, index, _count - index - 1);
        _count--;

        // release last element
        Array.Clear(_arr, _count, 1);
    }

    public void DoSync(Action<ConcurrentList<T>> action)
    {
        GetSync(l =>
        {
            action(l);
            return 0;
        });
    }

    public TResult GetSync<TResult>(Func<ConcurrentList<T>, TResult> func)
    {
        _lock.EnterWriteLock();
        try
        {
            return func(this);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}