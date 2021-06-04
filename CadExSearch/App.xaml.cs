using System.Windows;
using Akavache;

namespace CadExSearch
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            Registrations.Start("CadExSearch");
        }
    }
}