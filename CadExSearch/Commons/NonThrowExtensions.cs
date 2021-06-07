using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace CadExSearch.Commons
{
    public static class NonThrowExtensions
    {
        /// <summary>
        ///     Similar as
        ///     <code><paramref name="val" /> = default; try { <paramref name="val" /> = <paramref name="f" />(<paramref
        ///         name="input" />); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeLet<T>(this T input, in Func<T, T> f, [MaybeNullWhen(false)] out T val)
        {
            val = default;
            if (f == default) return false;
            try
            {
                val = f(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="val" /> = default; try { if (<paramref name="whenThrow" />(<paramref name="input" />)) return false; <paramref
        ///         name="val" /> = <paramref name="f" />(<paramref name="input" />); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="whenThrow"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeLet<T>(this T input, in Func<T, T> f, in Predicate<T> whenThrow,
            [MaybeNullWhen(false)] out T val)
        {
            val = default;
            if (f == default) return false;
            try
            {
                if (whenThrow is not null && whenThrow(input)) return false;
                val = f(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="val" /> = default; try { <paramref name="val" /> = <paramref name="f" />(<paramref
        ///         name="input" />); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeLet<T1, T2>(this T1 input, in Func<T1, T2> f, [MaybeNullWhen(false)] out T2 val)
        {
            val = default;
            if (f == default) return false;
            try
            {
                val = f(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="val" /> = default; try { if (<paramref name="whenThrow" />(<paramref name="input" />)) return false; <paramref
        ///         name="val" /> = <paramref name="f" />(<paramref name="input" />); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="whenThrow"></param>
        /// <param name="val"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SafeLet<T1, T2>(this T1 input, in Func<T1, T2> f, in Predicate<T1> whenThrow,
            [MaybeNullWhen(false)] out T2 val)
        {
            val = default;
            if (f == default) return false;
            try
            {
                if (whenThrow is not null && whenThrow(input)) return false;
                val = f(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="f" />(<paramref name="input" />); } catch { return <paramref name="defval" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SafeLet<T>(this T input, in Func<T, T> f, T defval)
        {
            return input.SafeLet<T>(f, out var v) ? v : defval;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="f" />(<paramref name="input" />); } catch { return <paramref name="input" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SafeLet<T>(this T input, in Func<T, T> f)
        {
            return input.SafeLet<T>(f, out var v) ? v : input;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="whenThrow" />(<paramref name="input" />) ? <paramref name="defval" /> : <paramref
        ///         name="f" />(<paramref name="input" />);  } catch { return <paramref name="defval" /> }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="whenThrow"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SafeLet<T>(this T input, in Func<T, T> f, in Predicate<T> whenThrow, T defval)
        {
            return input.SafeLet<T>(f, whenThrow, out var v) ? v : defval;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="whenThrow" />(<paramref name="input" />) ? <paramref name="input" /> : <paramref
        ///         name="f" />(<paramref name="input" />);  } catch { return <paramref name="input" /> }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="whenThrow"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T SafeLet<T>(this T input, in Func<T, T> f, in Predicate<T> whenThrow)
        {
            return input.SafeLet<T>(f, whenThrow, out var v) ? v : input;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="f" />(<paramref name="input" />); } catch { return <paramref name="defval" />; }</code>
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T2 SafeLet<T1, T2>(this T1 input, in Func<T1, T2> f, T2 defval = default)
        {
            return input.SafeLet(f, out var v) ? v : defval;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="whenThrow" />(<paramref name="input" />) ? <paramref name="defval" /> : <paramref
        ///         name="f" />(<paramref name="input" />);  } catch { return <paramref name="defval" /> }</code>
        /// </summary>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <param name="input"></param>
        /// <param name="f"></param>
        /// <param name="whenThrow"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T2 SafeLet<T1, T2>(this T1 input, in Func<T1, T2> f, in Predicate<T1> whenThrow, T2 defval = default)
        {
            return input.SafeLet(f, whenThrow, out var v) ? v : defval;
        }
    }
}