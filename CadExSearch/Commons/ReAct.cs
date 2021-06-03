using System;
using System.Windows.Input;
using System.Windows.Markup;

namespace CEOCC.Helper.Commons
{
    public class ReAct : MarkupExtension, ICommand
    {
        protected readonly Action<object, object> execute;
        protected readonly Func<object, object, bool> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public object Parameter1 { set; private get; }
        public object Parameter2 { set; private get; }
        public ReAct NextReAct { set; private get; }
        public ReAct(ReAct act, object param1, object param2)
        {
            execute = act.execute;
            canExecute = act.canExecute;
            Parameter1 = param1;
            Parameter2 = param2;
        }
        public ReAct(ReAct act, object param1) : this(act, param1, null)
        { }
        public ReAct(ReAct act) : this(act, null, null)
        { }

        public static ReAct Do(Action action) => new(action);
        public static ReAct Do(Action action, Func<bool> func) => new(action, func);
        public static ReAct Do(Action action, Func<object, bool> func) => new(action, func);
        public static ReAct Do(Action action, Func<object, object, bool> func) => new(action, func);
        public static ReAct Do<T>(Action action, Func<T, bool> func) => new(action, o => func((T)o));
        public static ReAct Do<T1, T2>(Action action, Func<T1, T2, bool> func) => new(action, (o1, o2) => func((T1)o1, (T2)o2));

        public static ReAct Do(Action<object> action) => new(action);
        public static ReAct Do(Action<object> action, Func<bool> func) => new(action, func);
        public static ReAct Do(Action<object> action, Func<object, bool> func) => new(action, func);
        public static ReAct Do(Action<object> action, Func<object, object, bool> func) => new(action, func);
        public static ReAct Do<T>(Action<object> action, Func<T, bool> func) => new(action, o => func((T)o));
        
        public static ReAct Do(Action<object, object> action) => new(action);
        public static ReAct Do(Action<object, object> action, Func<bool> func) => new(action, func);
        public static ReAct Do(Action<object, object> action, Func<object, bool> func) => new(action, func);
        public static ReAct Do(Action<object, object> action, Func<object, object, bool> func) => new(action, func);
        
        public static ReAct Do<T>(Action<T> action) => new(o => action((T)o));
        public static ReAct Do<T>(Action<T> action, Func<bool> func) => new(o => action((T)o), func);
        public static ReAct Do<T>(Action<T> action, Func<T, bool> func) => new(o => action((T)o), o => func((T)o));
        public static ReAct Do<T>(Action<T> action, Func<T, object, bool> func) => new(o => action((T)o), (o1, o2) => func((T)o1, o2));
        public static ReAct Do<T1, T2>(Action<T1> action, Func<T1, T2, bool> func) => new(o => action((T1)o), (o1, o2) => func((T1)o1, (T2)o2));

        public static ReAct Do<T1, T2>(Action<T1, T2> action) => new((o1, o2) => action((T1)o1, (T2)o2));
        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<bool> func) => new((o1, o2) => action((T1)o1, (T2)o2), func);
        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<T1, bool> func) => new((o1, o2) => action((T1)o1, (T2)o2), o => func((T1)o));
        public static ReAct Do<T1, T2>(Action<T1, T2> action, Func<T1, T2, bool> func) => new((o1, o2) => action((T1)o1, (T2)o2), (o1, o2) => func((T1)o1, (T2)o2));


        protected ReAct(Action<object, object> action, Func<object, object, bool> can)
        {
            execute = action;
            canExecute = can;
        }
        protected ReAct(Action<object, object> action, Func<object, bool> can) : this(action, (o1, o2) => can(o1))
        { }
        protected ReAct(Action<object, object> action, Func<bool> can) : this(action, (o1, o2) => can())
        { }
        protected ReAct(Action<object, object> action) : this(action, (o1, o2) => true)
        { }

        protected ReAct(Action<object> action, Func<object, object, bool> can) : this((o1, o2) => action(o1), can)
        { }
        protected ReAct(Action<object> action, Func<object, bool> can) : this((o1, o2) => action(o1), (o1, o2) => can(o1))
        { }
        protected ReAct(Action<object> action, Func<bool> can) : this((o1, o2) => action(o1), (o1, o2) => can())
        { }
        protected ReAct(Action<object> action) : this((o1, o2) => action(o1), (o1, o2) => true)
        { }

        protected ReAct(Action action, Func<object, object, bool> can) : this((o1, o2) => action(), can)
        { }
        protected ReAct(Action action, Func<object, bool> can) : this((o1, o2) => action(), (o1, o2) => can(o1))
        { }
        protected ReAct(Action action, Func<bool> can) : this((o1, o2) => action(), (o1, o2) => can())
        { }
        protected ReAct(Action action) : this((o1, o2) => action(), (o1, o2) => true)
        { }


        public bool CanExecute(object parameter = null)
        {
            return (canExecute == null || canExecute(Parameter1 ?? parameter, Parameter2)) 
                && (NextReAct == null || NextReAct.canExecute(Parameter1 ?? parameter, Parameter2));
        }

        public void Execute(object parameter = null)
        {
            execute(Parameter1 ?? parameter, Parameter2);
            
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
