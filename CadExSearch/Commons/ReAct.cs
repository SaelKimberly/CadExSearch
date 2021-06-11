using System;
using System.Windows.Input;
using System.Windows.Markup;

namespace CadExSearch.Commons
{
    public class ReAct : MarkupExtension, ICommand
    {
        protected Func<object, object, bool> CanExecuteFunc { get; }
        protected Action<object, object> ExecuteFunc { get; }

        public ReAct(ReAct act, object param1, object param2)
        {
            ExecuteFunc = act.ExecuteFunc;
            CanExecuteFunc = act.CanExecuteFunc;
            Parameter1 = param1;
            Parameter2 = param2;
        }

        public ReAct(ReAct act, object param1) : this(act, param1, null)
        {
        }

        public ReAct(ReAct act) : this(act, null, null)
        {
        }


        protected ReAct(Action<object, object> action, Func<object, object, bool> can)
        {
            ExecuteFunc = action;
            CanExecuteFunc = can;
        }

        protected ReAct(Action<object, object> action, Func<object, bool> can) : this(action, (o1, _) => can(o1))
        {
        }

        protected ReAct(Action<object, object> action, Func<bool> can) : this(action, (_, _) => can())
        {
        }

        protected ReAct(Action<object, object> action) : this(action, (_, _) => true)
        {
        }

        protected ReAct(Action<object> action, Func<object, object, bool> can) : this((o1, _) => action(o1), can)
        {
        }

        protected ReAct(Action<object> action, Func<object, bool> can) : this((o1, _) => action(o1),
            (o1, _) => can(o1))
        {
        }

        protected ReAct(Action<object> action, Func<bool> can) : this((o1, _) => action(o1), (_, _) => can())
        {
        }

        protected ReAct(Action<object> action) : this((o1, _) => action(o1), (_, _) => true)
        {
        }

        protected ReAct(Action action, Func<object, object, bool> can) : this((_, _) => action(), can)
        {
        }

        protected ReAct(Action action, Func<object, bool> can) : this((_, _) => action(), (o1, _) => can(o1))
        {
        }

        protected ReAct(Action action, Func<bool> can) : this((_, _) => action(), (_, _) => can())
        {
        }

        protected ReAct(Action action) : this((_, _) => action(), (_, _) => true)
        {
        }

        public object Parameter1 { set; private get; }
        public object Parameter2 { set; private get; }
        public ReAct NextReAct { set; private get; }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }


        public bool CanExecute(object parameter = null)
        {
            return (CanExecuteFunc == null || CanExecuteFunc(Parameter1 ?? parameter, Parameter2))
                   && (NextReAct == null || NextReAct.CanExecuteFunc(Parameter1 ?? parameter, Parameter2));
        }

        public void Execute(object parameter = null)
        {
            ExecuteFunc(Parameter1 ?? parameter, Parameter2);
        }

        public static ReAct Do(Action action)
        {
            return new(action);
        }

        public static ReAct Do(Action action, Func<bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action action, Func<object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action action, Func<object, object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do<T>(Action action, Func<T, bool> func)
        {
            return new(action, o => func((T)o));
        }

        public static ReAct Do<T1, T2>(Action action, Func<T1, T2, bool> func)
        {
            return new(action, (o1, o2) => func((T1)o1, (T2)o2));
        }

        public static ReAct Do(Action<object> action)
        {
            return new(action);
        }

        public static ReAct Do(Action<object> action, Func<bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action<object> action, Func<object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action<object> action, Func<object, object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do<T>(Action<object> action, Func<T, bool> func)
        {
            return new(action, o => func((T)o));
        }

        public static ReAct Do(Action<object, object> action)
        {
            return new(action);
        }

        public static ReAct Do(Action<object, object> action, Func<bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action<object, object> action, Func<object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do(Action<object, object> action, Func<object, object, bool> func)
        {
            return new(action, func);
        }

        public static ReAct Do<T>(Action<T> action)
        {
            return new(o => action((T)o));
        }

        public static ReAct Do<T>(Action<T> action, Func<bool> func)
        {
            return new(o => action((T)o), func);
        }

        public static ReAct Do<T>(Action<T> action, Func<T, bool> func)
        {
            return new(o => action((T)o), o => func((T)o));
        }

        public static ReAct Do<T>(Action<T> action, Func<T, object, bool> func)
        {
            return new(o => action((T)o), (o1, o2) => func((T)o1, o2));
        }

        public static ReAct Do<T1, T2>(Action<T1> action, Func<T1, T2, bool> func)
        {
            return new(o => action((T1)o), (o1, o2) => func((T1)o1, (T2)o2));
        }

        public static ReAct Do<T1, T2>(Action<T1, T2> action)
        {
            return new((o1, o2) => action((T1)o1, (T2)o2));
        }

        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<bool> func)
        {
            return new((o1, o2) => action((T1)o1, (T2)o2), func);
        }

        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<T1, bool> func)
        {
            return new((o1, o2) => action((T1)o1, (T2)o2), o => func((T1)o));
        }

        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<T1, T2, bool> func)
        {
            return new((o1, o2) => action((T1)o1, (T2)o2), (o1, o2) => func((T1)o1, (T2)o2));
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}