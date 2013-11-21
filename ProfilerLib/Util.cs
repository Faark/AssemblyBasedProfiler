using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ProfilerLib
{
    #region Dictionary Stuff
    public static class DictionaryExpands
    {
        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
                    where TValue : new()
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = self[key] = new TValue();
            }
            return value;
        }
        public static TValue GetOrSet<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = self[key] = default(TValue);
            }
            return value;
        }
        public static TValue GetOrSet<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, TValue defVal)
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = self[key] = defVal;
            }
            return value;
        }
        public static TValue GetOrSet<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TValue> factory)
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = self[key] = factory();
            }
            return value;
        }
        public static TValue GetOrSet<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TKey, TValue> factory)
        {
            TValue value;
            if (!self.TryGetValue(key, out value))
            {
                value = self[key] = factory(key);
            }
            return value;
        }
        public static TValue TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, TValue defVal)
        {
            TValue value;
            if (self.TryGetValue(key, out value))
            {
                return value;
            }
            return defVal;
        }
        public static TValue TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key)
        {
            return self.TryGetValue(key, default(TValue));
        }
        public static TValue TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TValue> factory)
        {
            TValue value;
            if (self.TryGetValue(key, out value))
            {
                return value;
            }
            return factory();
        }
        public static TValue TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TKey, TValue> factory)
        {
            TValue value;
            if (self.TryGetValue(key, out value))
            {
                return value;
            }
            return factory(key);
        }
    }
    #endregion
}
