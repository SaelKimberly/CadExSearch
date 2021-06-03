using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace CEOCC.Helper.Commons
{
    public delegate object ConvertHandler(object value, Type targetType, object parameter, CultureInfo culture, object extraparam = null);
    
    public class Recast : MarkupExtension, IValueConverter
    {
        public Recast(Recast ch, object parameter1 = null, object parameter2 = null)
        {
            ConvertHandler = ch;
            Parameter1 = parameter1;
            Parameter2 = parameter2;
        }
        public Recast(Recast ch, object parameter) : this(ch, parameter, null) { }
        public Recast(Recast ch) : this(ch, null, null) { }

        public ConvertHandler ConvertHandler { get; private set; }
        public Recast ConvertBackHandler { internal get; set; }
        internal object Parameter1 { set; private get; }
        internal object Parameter2 { set; private get; }

        public Recast NextCast { set; private get; }
        internal object UpCast(object value, object p1 = null, object p2 = null)
        {
            try
            {
                if (value == default) return default;
                return ConvertHandler?.Invoke(value, value.GetType(), Parameter1 ?? p1, default, Parameter2 ?? p2) ?? value;
            }
            catch
            {
                return value;
            }
        }
        internal object DnCast(object value, object p1 = null, object p2 = null)
        {
            try
            {
                return ((ConvertHandler)ConvertBackHandler)?.Invoke(value, value.GetType(), Parameter1 ?? p1, default, Parameter2 ?? p2) ?? value;
            }
            catch
            {
                return value;
            }
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var s1 = UpCast(value, parameter);
                return NextCast?.UpCast(s1) ?? s1;
            }
            catch
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DnCast(value, parameter);

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        protected Recast() { }
        public static Recast Of(Func<object, object> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f(v) };
        }
        public static Recast Of(Func<object, object, object> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f(v, p) };
        }
        public static Recast Of(Func<object, object, object, object> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f(v, p, e) };
        }
        public static Recast Of<T>(Func<T, object> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f((T)v) };
        }
        public static Recast Of<T, R>(Func<T, R> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f((T)v) };
        }
        public static Recast Of<T, P1, R>(Func<T, P1, R> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f((T)v, (P1)p) };
        }
        public static Recast Of<T, P1, P2, R>(Func<T, P1, P2, R> f)
        {
            return new() { ConvertHandler = (v, t, p, c, e) => f((T)v, (P1)p, (P2)e) };
        }
        public static implicit operator Recast(ConvertHandler ch)
        {
            return new() { ConvertHandler = ch };
        }
        public static implicit operator ConvertHandler(Recast ccc)
        {
            return ccc.ConvertHandler;
        }
        public static implicit operator Predicate<object>(Recast ccc) => o =>
        {
            return ccc.Convert(o, o.GetType(), null, default) switch
            {
                null => false,
                bool b => b,
                _ => true
            };
        };
    }
}
