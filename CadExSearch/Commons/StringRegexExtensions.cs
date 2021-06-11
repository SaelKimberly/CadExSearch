using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace CadExSearch.Commons
{
    public static class StringRegexExtensions
    {
        /// <summary>
        ///     Extension version of <see cref="string.IsNullOrWhiteSpace" />
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhitespace(this string input)
        {
            return string.IsNullOrWhiteSpace(input);
        }

        /// <summary>
        ///     Extension version of <see cref="string.IsNullOrEmpty" />
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty(this string input)
        {
            return string.IsNullOrEmpty(input);
        }

        public static bool TryToRegex(this string input, [MaybeNullWhen(false)] out Regex regex)
        {
            regex = null;
            try
            {
                regex = new Regex(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="match" /> = default; try { <paramref name="match" /> = <see cref="Regex" />.Match(<paramref
        ///         name="input" />, <paramref name="pattern" />); } catch { }</code>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="match"></param>
        /// <returns></returns>
        public static bool TryMatch(this string input, string pattern, [MaybeNullWhen(false)] out Match match)
        {
            match = default;
            try
            {
                match = (input, p: pattern) switch
                {
                    var (v, p) when !Regex.IsMatch(v, p) => throw new ArgumentException(""),
                    _ => Regex.Match(input, pattern)
                };
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" /> = default; try { <paramref name="mc" /> = <see cref="Regex" />.Matches(<paramref
        ///         name="input" />, <paramref name="pattern" />); } catch { }</code>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="mc"></param>
        /// <returns></returns>
        public static bool TryMatches(this string input, string pattern, [MaybeNullWhen(false)] out MatchCollection mc)
        {
            mc = default;
            try
            {
                mc = (i: input, p: pattern) switch
                {
                    var (v, p) when !Regex.IsMatch(v, p) => throw new ArgumentException(""),
                    _ => Regex.Matches(input, pattern)
                };
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="match" /> = default; try { <paramref name="match" /> = <see cref="Regex" />.Match(<paramref
        ///         name="input" />, <paramref name="pattern" />, <paramref name="options" />); } catch { }</code>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="options"></param>
        /// <param name="match"></param>
        /// <returns></returns>
        public static bool TryMatch(this string input, string pattern, RegexOptions options,
            [MaybeNullWhen(false)] out Match match)
        {
            match = default;
            try
            {
                match = (input, p: pattern) switch
                {
                    var (v, p) when !Regex.IsMatch(v, p, options) => throw new ArgumentException(""),
                    _ => Regex.Match(input, pattern, options)
                };
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" /> = default; try { <paramref name="mc" /> = <see cref="Regex" />.Matches(<paramref
        ///         name="input" />, <paramref name="pattern" />, <paramref name="options" />); } catch { }</code>
        /// </summary>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="options"></param>
        /// <param name="mc"></param>
        /// <returns></returns>
        public static bool TryMatches(this string input, string pattern, RegexOptions options,
            [MaybeNullWhen(false)] out MatchCollection mc)
        {
            mc = default;
            try
            {
                mc = (i: input, p: pattern) switch
                {
                    var (v, p) when !Regex.IsMatch(v, p, options) => throw new ArgumentException(""),
                    _ => Regex.Matches(input, pattern, options)
                };
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="matchProcessor" />(<see cref="Regex" />.Match(<paramref name="input" />, <paramref
        ///         name="pattern" />)); } catch { return <paramref name="defval" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="matchProcessor"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        public static T TryMatch<T>(this string input, string pattern, Func<Match, T> matchProcessor,
            T defval = default)
        {
            return input.TryMatch(pattern, out var m) && matchProcessor is not null ? matchProcessor(m) : defval;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <see cref="Regex" />.Matches(<paramref name="input" />, <paramref name="pattern" />).Select(<paramref
        ///         name="matchProcessor" />); } catch { return <see cref="ArraySegment{T}.Empty" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="matchProcessor"></param>
        /// <returns></returns>
        public static IEnumerable<T> TryMatches<T>(this string input, string pattern, Func<Match, T> matchProcessor)
        {
            return input.TryMatches(pattern, out var mc) && matchProcessor is not null
                ? from m in mc select matchProcessor(m)
                : ArraySegment<T>.Empty;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="matchProcessor" />(<see cref="Regex" />.Match(<paramref name="input" />, <paramref
        ///         name="pattern" />, <paramref name="options" />)); } catch { return <paramref name="defval" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="options"></param>
        /// <param name="matchProcessor"></param>
        /// <param name="defval"></param>
        /// <returns></returns>
        public static T TryMatch<T>(this string input, string pattern, RegexOptions options,
            Func<Match, T> matchProcessor, T defval = default)
        {
            return input.TryMatch(pattern, options, out var m) && matchProcessor is not null
                ? matchProcessor(m)
                : defval;
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <see cref="Regex" />.Matches(<paramref name="input" />, <paramref name="pattern" />, <paramref
        ///         name="options" />).Select(<paramref name="matchProcessor" />); } catch { return <see
        ///         cref="ArraySegment{T}.Empty" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input"></param>
        /// <param name="pattern"></param>
        /// <param name="options"></param>
        /// <param name="matchProcessor"></param>
        /// <returns></returns>
        public static IEnumerable<T> TryMatches<T>(this string input, string pattern, RegexOptions options,
            Func<Match, T> matchProcessor)
        {
            return input.TryMatches(pattern, options, out var mc) && matchProcessor is not null
                ? from m in mc select matchProcessor(m)
                : ArraySegment<T>.Empty;
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value))</code>
        ///     Throws exception when <paramref name="selector" /> is null or <paramref name="group" /> is null or whitespace
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(this IEnumerable<Match> mc, string group, Func<string, T> selector)
        {
            ICollection<Match> matches = mc switch {MatchCollection v => v, _ => mc.ToArray()};
            return (mc: matches, group, selector) switch
            {
                (_, _, null) => throw new ArgumentNullException(nameof(selector)),
                var (_, g, _) when g.IsNullOrWhitespace() => throw new ArgumentNullException(nameof(group)),
                var (mm, g, c) => from m in mm where m.Groups.ContainsKey(g) select c(m.Groups[g].Value)
            };
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value))</code>
        ///     Throws exception when <paramref name="selector" /> is null or <paramref name="group" /> is null or whitespace
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(this MatchCollection mc, string group, Func<string, T> selector)
        {
            return mc.OfType<Match>().Select(group, selector);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value)); } catch { return <see
        ///         cref="ArraySegment{T}.Empty" />;}</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> TrySelect<T>(this IEnumerable<Match> mc, string group, Func<string, T> selector)
        {
            try
            {
                return mc.Select(group, selector);
            }
            catch { return ArraySegment<T>.Empty; }
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value)); } catch { return <see
        ///         cref="ArraySegment{T}.Empty" />;}</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> TrySelect<T>(this MatchCollection mc, string group, Func<string, T> selector)
        {
            try
            {
                return mc.Select(group, selector);
            }
            catch { return ArraySegment<T>.Empty; }
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value));</code>
        ///     Throws exception when <paramref name="selector" /> is <c>null</c> or <paramref name="group" /> less than zero or
        ///     greater than max group number for all matches.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(this IEnumerable<Match> mc, int group, Func<string, T> selector)
        {
            ICollection<Match> matches = mc switch {MatchCollection v => v, _ => mc.ToArray()};
            return (mc: matches, group, selector, mg: matches.Max(m => m.Groups.Count)) switch
            {
                (_, _, null, _) => throw new ArgumentNullException(nameof(selector)),
                var (mm, _, _, _) when mm is null || !mm.Any() => ArraySegment<T>.Empty,
                var (_, g, _, mg) when g < 0 || g >= mg => throw new IndexOutOfRangeException(
                    $"group value must be between 0 and {mg} for this Match Collection!"),
                var (mm, g, c, _) => from m in mm where m.Groups.Count > g select c(m.Groups[g].Value)
            };
        }

        /// <summary>
        ///     Similar as
        ///     <code><paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value));</code>
        ///     Throws exception when <paramref name="selector" /> is <c>null</c> or <paramref name="group" /> less than zero or
        ///     greater than max group number for all matches.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> Select<T>(this MatchCollection mc, int group, Func<string, T> selector)
        {
            return mc.OfType<Match>().Select(group, selector);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value); } catch { return <see
        ///         cref="ArraySegment{T}.Empty" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> TrySelect<T>(this IEnumerable<Match> mc, int group, Func<string, T> selector)
        {
            try
            {
                return mc.Select(group, selector);
            }
            catch { return ArraySegment<T>.Empty; }
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => <paramref name="selector" />(m.Groups[<paramref name="group" />].Value); } catch { return <see
        ///         cref="ArraySegment{T}.Empty" />; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<T> TrySelect<T>(this MatchCollection mc, int group, Func<string, T> selector)
        {
            try
            {
                return mc.Select(group, selector);
            }
            catch { return ArraySegment<T>.Empty; }
        }

        /// <summary>
        ///     Similar as <code><paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value);</code>
        ///     Throws exception when <paramref name="group" /> is null or whitespace
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> Select(this IEnumerable<Match> mc, string group)
        {
            return mc.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value); } catch { return ArraySegment&lt;<see
        ///         cref="string" />&gt;.Empty; }</code>
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> TrySelect(this IEnumerable<Match> mc, string group)
        {
            return mc.TrySelect(group, s => s);
        }

        /// <summary>
        ///     Similar as <code><paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value);</code>
        ///     Throws exception when <paramref name="group" /> less than zero or greater than max group number for all matches.
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> Select(this IEnumerable<Match> mc, int group)
        {
            return mc.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value); } catch { return ArraySegment&lt;<see
        ///         cref="string" />&gt;.Empty; }</code>
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> TrySelect(this IEnumerable<Match> mc, int group)
        {
            return mc.TrySelect(group, s => s);
        }

        /// <summary>
        ///     Similar as <code><paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value);</code>
        ///     Throws exception when <paramref name="group" /> is null or whitespace
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> Select(this MatchCollection mc, string group)
        {
            return mc.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value); } catch { return ArraySegment&lt;<see
        ///         cref="string" />&gt;.Empty; }</code>
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> TrySelect(this MatchCollection mc, string group)
        {
            return mc.TrySelect(group, s => s);
        }

        /// <summary>
        ///     Similar as <code><paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value);</code>
        ///     Throws exception when <paramref name="group" /> less than zero or greater than max group number for all matches.
        /// </summary>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> Select(this MatchCollection mc, int group)
        {
            return mc.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="mc" />.Select(m => m.Groups[<paramref name="group" />].Value); } catch { return ArraySegment&lt;<see
        ///         cref="string" />&gt;.Empty; }</code>
        /// </summary>
        /// <param name="mc"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static IEnumerable<string> TrySelect(this MatchCollection mc, int group)
        {
            return mc.TrySelect(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>return <paramref name="converter" />(<paramref name="m" />.Groups[<paramref name="group" />].Value)</code>
        ///     Throws exception when <paramref name="m" /> is <c>null</c>, or <paramref name="group" /> is <c>null</c> or
        ///     whitespace, or <paramref name="converter" /> is <c>null</c>
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <param name="converter"></param>
        /// <returns></returns>
        public static T Select<T>(this Match m, string group, Func<string, T> converter)
        {
            return (group, m, converter) switch
            {
                (_, null, _) => throw new ArgumentNullException(nameof(m)),
                (_, _, null) => throw new ArgumentNullException(nameof(converter)),
                var (g, _, _) when g.IsNullOrWhitespace() =>
                    throw new ArgumentNullException(nameof(group)),
                _ => converter(m.Groups[group].Value)
            };
        }

        /// <summary>
        ///     Similar as
        ///     <code>value = default; try { value = <paramref name="selector" />(<paramref name="m" />.Groups[<paramref
        ///         name="group" />].Value); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TrySelect<T>(this Match m, string group, Func<string, T> selector,
            [MaybeNullWhen(false)] out T value)
        {
            value = default;
            try
            {
                value = m.Select(group, selector);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as
        ///     <code>return <paramref name="converter" />(<paramref name="m" />.Groups[<paramref name="group" />].Value)</code>
        ///     Throws exception when <paramref name="m" /> is <c>null</c>, or <paramref name="converter" /> is <c>null</c>, or
        ///     <paramref name="group" /> is less than zero or greater than max group number for all matches.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <param name="converter"></param>
        /// <returns></returns>
        public static T Select<T>(this Match m, int group, Func<string, T> converter)
        {
            return (group, m, converter, mg: m.Groups.Count) switch
            {
                (_, null, _, _) => throw new ArgumentNullException(nameof(m)),
                (_, _, null, _) => throw new ArgumentNullException(nameof(converter)),
                var (g, _, _, mg) when g < 0 || g >= mg => throw new IndexOutOfRangeException(
                    $"group value must be between 0 and {mg} for this match!"),
                _ => converter(m.Groups[group].Value)
            };
        }

        /// <summary>
        ///     Similar as
        ///     <code>value = default; try { value = <paramref name="selector" />(<paramref name="m" />.Groups[<paramref
        ///         name="group" />].Value); return true; } catch { return false; }</code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <param name="selector"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TrySelect<T>(this Match m, int group, Func<string, T> selector,
            [MaybeNullWhen(false)] out T value)
        {
            value = default;
            try
            {
                value = m.Select(group, selector);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        ///     Similar as <code>return <paramref name="m" />.Groups[<paramref name="group" />].Value</code>
        ///     Throws exception when <paramref name="m" /> is <c>null</c>, or <paramref name="group" /> is <c>null</c> or
        ///     whitespace
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string Select(this Match m, string group)
        {
            return m.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="m" />.Groups[<paramref name="group" />].Value; } catch { return null; }</code>
        /// </summary>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string TrySelect(this Match m, int group)
        {
            return m.TrySelect(group, s => s, out var ss) ? ss : string.Empty;
        }

        /// <summary>
        ///     Similar as <code>return <paramref name="m" />.Groups[<paramref name="group" />].Value</code>
        ///     Throws exception when <paramref name="m" /> is <c>null</c>, or <paramref name="group" /> is less than zero or
        ///     greater than group max index for this match.
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="IndexOutOfRangeException"></exception>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string Select(this Match m, int group)
        {
            return m.Select(group, s => s);
        }

        /// <summary>
        ///     Similar as
        ///     <code>try { return <paramref name="m" />.Groups[<paramref name="group" />].Value; } catch { return null; }</code>
        /// </summary>
        /// <param name="m"></param>
        /// <param name="group"></param>
        /// <returns></returns>
        public static string TrySelect(this Match m, string group)
        {
            return m.TrySelect(group, s => s, out var ss) ? ss : string.Empty;
        }
    }
}