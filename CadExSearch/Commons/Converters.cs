using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using SaelSharp.Helpers;

namespace CadExSearch.Commons
{
    // ReSharper disable InconsistentNaming
    public static class Extensions
    {
        public static bool IsMatch(this string value, string pattern, bool fullmatch = false)
        {
            var reg = new Regex(pattern);
            return fullmatch ? reg.Match(value).Value == value : reg.IsMatch(value);
        }

        public static bool IsWcMatch(this string value, string pattern)
        {
            return value.IsMatch(WCtoR(pattern));
        }

        internal static string WCtoR(string wildc_exp)
        {
            return $"^{Regex.Escape(wildc_exp).Replace("\\?", ".").Replace("\\*", ".*")}$";
        }
    }

    internal struct BVW
    {
        public bool? Value { get; set; }

        public static implicit operator BVW(bool value)
        {
            return new() {Value = value};
        }

        public static implicit operator BVW(bool? value)
        {
            return new() {Value = value};
        }

        public static implicit operator Visibility(BVW value)
        {
            return value.Value == null ? Visibility.Collapsed :
                value.Value ?? false ? Visibility.Visible : Visibility.Hidden;
        }

        public static implicit operator bool(BVW value)
        {
            return value.Value ?? false;
        }

        public static implicit operator bool?(BVW value)
        {
            return value.Value;
        }
    }


    public static class Converters
    {
        public static Recast RepresentPair { get; } =
            Recast.Of<(string, string), string>(o => o == default ? "" : o.Item2);

        public static Recast CanBeCast { get; } = Recast.Of<string, bool>(s =>
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            try
            {
                var a = Regex.IsMatch("qwer", s);
            }
            catch
            {
                return false;
            }

            return true;
        });
        //public static Recast CollectionToString { get; } = Recast.OfMany<object, string>(os =>
        //{
        //    try
        //    {
        //        var cc = (os.First() as IEnumerable<object>).Select(o => o.ToString());
        //        return string.Join("\t\n", cc);
        //    }
        //    catch
        //    {
        //        return "";
        //    }
        //});

        public static Recast Inv { get; } = Recast.Of<bool, bool>(b => !b);

        public static Recast Dir_BoolToVisibility { get; } = Recast.Of<bool, Visibility>(b => (BVW) b);
        public static Recast Inv_BoolToVisibility { get; } = Recast.Of<bool, Visibility>(b => (BVW) (!b));

        public static Recast CollapseIfNullOrWhitespace { get; } = Recast.Of<string, Visibility>(s =>
            string.IsNullOrWhiteSpace(s) ? Visibility.Collapsed : Visibility.Visible);

        public static Recast HideIfNullOrWhitespace { get; } = Recast.Of<string, Visibility>(s =>
            string.IsNullOrWhiteSpace(s) ? Visibility.Hidden : Visibility.Visible);

        #region Control Specific

        public static Recast Get_Tag { get; } = Recast.Of<Control>(c => c.Tag);

        #endregion

        #region BooleanConverters

        public static Recast Dir_IsValueDefault { get; } = Recast.Of(o => o == default);
        public static Recast Inv_IsValueDefault { get; } = Recast.Of(o => o != default);
        public static Recast Dir_IsValueEqual { get; } = Recast.Of((o1, o2) => o1 == o2);
        public static Recast Inv_IsValueEqual { get; } = Recast.Of((o1, o2) => o1 != o2);

        #endregion

        #region CollectionSpecific

        public static Recast ItemsCount { get; } = Recast.Of<ICollection>(ie => ie?.Count ?? 0);

        public static Recast Dir_IsCollectionEmpty { get; } = Recast.Of<ICollection>(ie => (ie?.Count ?? 0) == 0);
        public static Recast Inv_IsCollectionEmpty { get; } = Recast.Of<ICollection>(ie => (ie?.Count ?? 0) != 0);

        public static Recast Dir_IsItemsCountEq { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) == c);

        public static Recast Inv_IsItemsCountEq { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) != c);

        public static Recast Dir_IsItemsCountLt { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) < c);

        public static Recast Inv_IsItemsCountLt { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) >= c);

        public static Recast Dir_IsItemsCountGt { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) > c);

        public static Recast Inv_IsItemsCountGt { get; } =
            Recast.Of<ICollection, int, bool>((ie, c) => (ie?.Count ?? 0) <= c);

        public static Recast Get_Item { get; } =
            Recast.Of<IEnumerable<object>, int, object>((ie, c) => (ie?.Count() ?? 0) > c ? ie?.ElementAt(c) : null);

        public static Recast Get_FirstItem { get; } = Recast.Of<IEnumerable<object>>(ie => ie?.FirstOrDefault());
        public static Recast Get_LastItem { get; } = Recast.Of<IEnumerable<object>>(ie => ie?.LastOrDefault());

        public static Recast Dir_IsCollectionStartsWith { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => ie?.FirstOrDefault() == o);

        public static Recast Inv_IsCollectionStartsWith { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => ie?.FirstOrDefault() != o);

        public static Recast Dir_IsCollectionEndsWith { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => ie?.LastOrDefault() == o);

        public static Recast Inv_IsCollectionEndsWith { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => ie?.LastOrDefault() != o);

        public static Recast Dir_IsCollectionContains { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => ie?.Contains(o) ?? false);

        public static Recast Inv_IsCollectionContains { get; } =
            Recast.Of<IEnumerable<object>, object, bool>((ie, o) => !(ie?.Contains(o) ?? false));

        #endregion

        #region StringSpecific

        public static Recast Dir_IsStartsWith { get; } = Recast.Of<string, string, bool>((s, ss) => s.StartsWith(ss));
        public static Recast Inv_IsStartsWith { get; } = Recast.Of<string, string, bool>((s, ss) => !s.StartsWith(ss));
        public static Recast Dir_IsEndsWith { get; } = Recast.Of<string, string, bool>((s, ss) => s.EndsWith(ss));
        public static Recast Inv_IsEndsWith { get; } = Recast.Of<string, string, bool>((s, ss) => !s.EndsWith(ss));
        public static Recast Dir_IsContains { get; } = Recast.Of<string, string, bool>((s, ss) => s.Contains(ss));
        public static Recast Inv_IsContains { get; } = Recast.Of<string, string, bool>((s, ss) => !s.Contains(ss));

        public static Recast Dir_IsMatch { get; } = Recast.Of<string, string, bool>((s, exp) => s.IsMatch(exp));
        public static Recast Inv_IsMatch { get; } = Recast.Of<string, string, bool>((s, exp) => !s.IsMatch(exp));
        public static Recast Dir_IsWcMatch { get; } = Recast.Of<string, string, bool>((s, exp) => s.IsWcMatch(exp));
        public static Recast Inv_IsWcMatch { get; } = Recast.Of<string, string, bool>((s, exp) => !s.IsWcMatch(exp));

        public static Recast Get_FirstMatch { get; } = Recast.Of<string, string, string>((s, exp) =>
        {
            try
            {
                var m = Regex.Match(s, exp);
                return m.Success ? m.Groups[0].Value : "";
            }
            catch
            {
                return "";
            }
        });

        public static Recast Get_LastMatch { get; } = Recast.Of<string, string, string>((s, exp) =>
        {
            try
            {
                var m = Regex.Match(s, exp, RegexOptions.RightToLeft);
                return m.Success ? m.Groups[0].Value : "";
            }
            catch
            {
                return "";
            }
        });

        public static Recast Get_Match { get; } = Recast.Of<string, string, int, string>((s, exp, n) =>
        {
            try
            {
                var m = Regex.Matches(s, exp);
                return m.Any() && m.Count > n ? m[n].Groups[0].Value : "";
            }
            catch
            {
                return "";
            }
        });

        public static Recast Get_MatchGroup { get; } = Recast.Of<string, string, string, string>((s, exp, gn) =>
        {
            try
            {
                var m = Regex.Match(s, exp);
                return m.Success ? m.Groups[gn].Value : "";
            }
            catch
            {
                return "";
            }
        });

        public static Recast Dir_IsEmpty { get; } = Recast.Of<string, bool>(string.IsNullOrWhiteSpace);
        public static Recast Inv_IsEmpty { get; } = Recast.Of<string, bool>(s => !string.IsNullOrWhiteSpace(s));

        public static Recast Get_Upper { get; } = Recast.Of<string>(s => s.ToUpper());
        public static Recast Get_Lower { get; } = Recast.Of<string>(s => s.ToLower());

        public static Recast Get_Char { get; } = Recast.Of<string, string, char>((s, _i) =>
        {
            return int.TryParse(_i, out var i)
                ? i < 0 ? s.Length >= -i ? s.Reverse().Skip(-i - 1).First() : default :
                i < s.Length ? s[i] : default
                : default;
        });

        public static Recast GetBase64 { get; } = Recast.Of<byte[], string>(Convert.ToBase64String);

        public static Recast GetBase16 { get; } =
            Recast.Of<byte[], string>(b => BitConverter.ToString(b).Replace("-", "").ToLower());

        #endregion

        #region Integer Specific

        public static Recast Get_Inc { get; } = Recast.Of<int, int, int>((a, b) => a + b);
        public static Recast Get_Dec { get; } = Recast.Of<int, int, int>((a, b) => a - b);
        public static Recast Dir_IsZero { get; } = Recast.Of<long, bool>(i => i == 0);
        public static Recast Inv_IsZero { get; } = Recast.Of<long, bool>(i => i != 0);

        #endregion
    }
}