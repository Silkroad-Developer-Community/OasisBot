using Python.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RSBot.Python.Runtime
{
    public class PythonExecutor
    {
        private readonly SemaphoreSlim _limiter = new(1, 1);

        public async Task ExecuteAsync(Action action)
        {
            await _limiter.WaitAsync().ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    using (Py.GIL())
                        action();
                }).ConfigureAwait(false);
            }
            finally
            {
                _limiter.Release();
            }
        }

    }
}
