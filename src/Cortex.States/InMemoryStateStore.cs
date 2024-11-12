﻿using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Cortex.States
{
    public class InMemoryStateStore<TKey, TValue> : IStateStore<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _store = new ConcurrentDictionary<TKey, TValue>();

        public TValue Get(TKey key)
        {
            _store.TryGetValue(key, out var value);
            return value;
        }

        public void Put(TKey key, TValue value)
        {
            _store[key] = value;
        }

        public bool ContainsKey(TKey key)
        {
            return _store.ContainsKey(key);
        }

        public void Remove(TKey key)
        {
            _store.TryRemove(key, out _);
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> GetAll()
        {
            return _store;
        }
    }
}
