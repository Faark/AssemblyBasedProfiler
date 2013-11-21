using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyBasedProfiller
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

    #region Tuples
    public sealed class Tuple<T1> : IEquatable<Tuple<T1>>
    {
        public Tuple() { }
        public Tuple(T1 item)
        {
            Item = item;
        }
        public T1 Item { get; set; }


        #region IEquatable<StateReporter<T>> Implementation
        public override bool Equals(Object obj)
        {
            return obj is Tuple<T1> && this == (Tuple<T1>)obj;
        }
        public override int GetHashCode()
        {
            return Item.GetHashCode();
        }
        public bool Equals(Tuple<T1> other)
        {
            return other == this;
        }
        public static bool operator ==(Tuple<T1> x, Tuple<T1> y)
        {
            if (System.Object.ReferenceEquals(x, null))
            {
                return System.Object.ReferenceEquals(y, null);
            }
            return !System.Object.ReferenceEquals(y, null) && EqualityComparer<T1>.Default.Equals(x.Item, y.Item);
        }
        public static bool operator !=(Tuple<T1> x, Tuple<T1> y)
        {
            return !(x == y);
        }
        #endregion
    }
    public sealed class Tuple<T1, T2> : IEquatable<Tuple<T1, T2>>
    {
        public Tuple() { }
        public Tuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }


        #region IEquatable<StateReporter<T1, T2>> Implementation
        public override bool Equals(Object obj)
        {
            return obj is Tuple<T1, T2> && this == (Tuple<T1, T2>)obj;
        }
        public override int GetHashCode()
        {
            return Item1.GetHashCode() ^ Item2.GetHashCode();
        }
        public bool Equals(Tuple<T1, T2> other)
        {
            return other == this;
        }
        public static bool operator ==(Tuple<T1, T2> x, Tuple<T1, T2> y)
        {
            if (System.Object.ReferenceEquals(x, null))
            {
                return System.Object.ReferenceEquals(y, null);
            }
            return !System.Object.ReferenceEquals(y, null) && EqualityComparer<T1>.Default.Equals(x.Item1, y.Item1) && EqualityComparer<T2>.Default.Equals(x.Item2, y.Item2);
        }
        public static bool operator !=(Tuple<T1, T2> x, Tuple<T1, T2> y)
        {
            return !(x == y);
        }
        #endregion
    }
    public sealed class Tuple<T1, T2, T3> : IEquatable<Tuple<T1, T2, T3>>
    {
        public Tuple() { }
        public Tuple(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }

        public T1 Item1 { get; set; }
        public T2 Item2 { get; set; }
        public T3 Item3 { get; set; }


        #region IEquatable<StateReporter<T1, T2>> Implementation
        public override bool Equals(Object obj)
        {
            return obj is Tuple<T1, T2, T3> && (this == (Tuple<T1, T2, T3>)obj);
        }
        public override int GetHashCode()
        {
            return Item1.GetHashCode() ^ Item2.GetHashCode() ^ Item3.GetHashCode();
        }
        public bool Equals(Tuple<T1, T2, T3> other)
        {
            return other == this;
        }
        public static bool operator ==(Tuple<T1, T2, T3> x, Tuple<T1, T2, T3> y)
        {
            if (System.Object.ReferenceEquals(x, null))
            {
                return System.Object.ReferenceEquals(y, null);
            }
            return !System.Object.ReferenceEquals(y, null) && EqualityComparer<T1>.Default.Equals(x.Item1, y.Item1) && EqualityComparer<T2>.Default.Equals(x.Item2, y.Item2) && EqualityComparer<T3>.Default.Equals(x.Item3, y.Item3);
        }
        public static bool operator !=(Tuple<T1, T2, T3> x, Tuple<T1, T2, T3> y)
        {
            return !(x == y);
        }
        #endregion
    }

    public static class Tuple
    {
        public static Tuple<T1> Create<T1>(T1 item1)
        {
            return new Tuple<T1>(item1);
        }

        public static Tuple<T1, T2> Create<T1, T2>(T1 item1, T2 item2)
        {
            return new Tuple<T1, T2>(item1, item2);
        }

        public static Tuple<T1, T2, T3> Create<T1, T2, T3>(T1 item1, T2 item2, T3 item3)
        {
            return new Tuple<T1, T2, T3>(item1, item2, item3);
        }

        public static Tuple<T1, T2> ToTuple<T1, T2>(this KeyValuePair<T1, T2> self)
        {
            return Tuple.Create(self.Key, self.Value);
        }
    }
    #endregion
}
