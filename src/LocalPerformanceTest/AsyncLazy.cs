using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTest
{
    public class AsyncLazy<T> : Lazy<Task<T>>
    {
        public AsyncLazy(Func<Task<T>> valueFactory) : base(() => Task.Factory.StartNew(() => valueFactory()).Unwrap())
        {
        }

        public TaskAwaiter<T> GetAwaiter() { return Value.GetAwaiter(); }
    }
}
