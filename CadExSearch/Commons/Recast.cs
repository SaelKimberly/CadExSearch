using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CadExSearch.Commons
{
    public delegate object ConvertHandler(object value, Type targetType, object parameter, CultureInfo culture,
        object extraparam = null);

    /* USAGE:
     *
     * public static class Converters
     * {
     *      public static Recast HideIf { get; } = Recast.Of<bool,Visibility>((b) => b ? Visibility.Hidden : Visibility.Visible);
     *      public static Recast CollapseIf { get; } = Recast.Of<bool,Visibility>((b) => b ? Visibility.Collapsed : Visibility.Visible);
     *
     *      public static Recast IsZero { get; } = Recast.Of<long, bool>((i) => i == 0);
     * }
     *
     * In WPF code:
     * LastMax is Int32 value.
     * local Namespace was declared as [xmlns:local="clr-namespace:SaelSharp.Helpers"]
     *
     * <StackPanel Visibility="{Binding LastMax, Converter={local:Recast {x:Static local:Converters.IsZero}, NextCast={x:Static local:Converters.HideIf}}, Mode=OneWay}">
     */

    public class Recast : MarkupExtension, IValueConverter
    {
        public Recast(Recast ch, object parameter1 = null, object parameter2 = null)
        {
            ConvertHandler = ch;
            Parameter1 = parameter1;
            Parameter2 = parameter2;
        }

        public Recast(Recast ch, object parameter) : this(ch, parameter, null)
        {
        }

        public Recast(Recast ch) : this(ch, null, null)
        {
        }

        protected Recast()
        {
        }

        public ConvertHandler ConvertHandler { get; private init; }
        public Recast ConvertBackHandler { internal get; set; }
        internal object Parameter1 { set; private get; }
        internal object Parameter2 { set; private get; }

        public Recast NextCast { set; private get; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return UpCast(value, parameter, null, out var ret)
                ? NextCast is null ? ret :
                NextCast.UpCast(ret, null, null, out ret) ? ret : value
                : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DnCast(value, parameter, null, out var ret) ? ret : value;
        }

        internal bool UpCast(object value, object p1, object p2, out object result)
        {
            result = value;
            if (value is null) return false;
            try
            {
                result = ConvertHandler?.Invoke(value, value.GetType(),
                    Parameter1 ?? p1,
                    default,
                    Parameter2 ?? p2);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool DnCast(object value, object p1, object p2, out object result)
        {
            result = value;
            if (value is null) return false;
            try
            {
                result = ((ConvertHandler) ConvertBackHandler)?.Invoke(value, value.GetType(),
                    Parameter1 ?? p1,
                    default,
                    Parameter2 ?? p2);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public static Recast Of(Func<object, object> f)
        {
            return new() {ConvertHandler = (v, _, _, _, _) => f(v)};
        }

        public static Recast Of(Func<object, object, object> f)
        {
            return new() {ConvertHandler = (v, _, p, _, _) => f(v, p)};
        }

        public static Recast Of(Func<object, object, object, object> f)
        {
            return new() {ConvertHandler = (v, _, p, _, e) => f(v, p, e)};
        }

        public static Recast Of<T>(Func<T, object> f)
        {
            return new() {ConvertHandler = (v, _, _, _, _) => f((T) v)};
        } // ReSharper disable InconsistentNaming
        public static Recast Of<T, R>(Func<T, R> f)
        {
            return new() {ConvertHandler = (v, _, _, _, _) => f((T) v)};
        }

        public static Recast Of<T, P1, R>(Func<T, P1, R> f)
        {
            return new() {ConvertHandler = (v, _, p, _, _) => f((T) v, (P1) p)};
        }

        public static Recast Of<T, P1, P2, R>(Func<T, P1, P2, R> f)
        {
            return new() {ConvertHandler = (v, _, p, _, e) => f((T) v, (P1) p, (P2) e)};
        }

        public static implicit operator Recast(ConvertHandler ch)
        {
            return new() {ConvertHandler = ch};
        }

        public static implicit operator ConvertHandler(Recast ccc)
        {
            return ccc.ConvertHandler;
        }

        public static implicit operator Predicate<object>(Recast ccc)
        {
            return o =>
            {
                var _r = ccc.Convert(o, o.GetType(), null, default);
                return _r switch
                {
                    null => false,
                    bool b => b,
                    _ => true
                };
            };
        }
    }
}