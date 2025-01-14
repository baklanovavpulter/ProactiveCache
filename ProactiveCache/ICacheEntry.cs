﻿using System.Threading.Tasks;

namespace ProactiveCache
{
    public interface ICacheEntry<Tval>
    {
        bool IsCompleted { get; }
        ValueTask<Tval> GetValue();
    }
}
