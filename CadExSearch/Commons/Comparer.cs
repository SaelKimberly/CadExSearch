using System;
using System.Collections;

namespace CadExSearch.Commons
{
    public sealed class SimpleComparer<T> : IComparer
    {
        private SimpleComparer()
        {
        }

        private bool? Inverted { get; init; }
        private Func<T, T, int> CompareFunc { get; init; }

        public int Compare(object x, object y)
        {
            if (Inverted == null) return 0;
            var ret = CompareFunc((T) x, (T) y);
            return ret == 0 ? 0 : Inverted ?? false ? -ret : ret;
        }

        public static SimpleComparer<T> Of(Func<T, T, int> comparer, bool? direction = false)
        {
            return new()
            {
                CompareFunc = comparer ?? throw new ArgumentNullException(nameof(comparer)),
                Inverted = direction
            };
        }
    }
}