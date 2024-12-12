using System;

namespace HandheldCompanion.Utils
{
    public class LambdaDisposable : IDisposable
    {
        private readonly Action _action;

        public LambdaDisposable(Action action) => _action = action;

        ~LambdaDisposable()
        {
            Dispose();
        }

        public void Dispose()
        {
            _action();
            GC.SuppressFinalize(this);
        }
    }
}
