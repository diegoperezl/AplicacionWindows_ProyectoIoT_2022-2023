using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// La plantilla de elemento Página en blanco está documentada en https://go.microsoft.com/fwlink/?LinkId=234238

namespace ProyectoIOT_DiegoPerez
{
    /// <summary>
    /// Una página vacía que se puede usar de forma independiente o a la que se puede navegar dentro de un objeto Frame.
    /// </summary>
    public sealed partial class Login : Page
    {
        public Login()
        {
            this.InitializeComponent();
        }

        //Manda las credenciales a la segunda pantalla para que haga la conexión
        private void sendCredentials_Click(object sender, RoutedEventArgs e)
        {
            string[] credentials = { user.Text, pass.Password };
            Frame.Navigate(typeof(MainPage), credentials);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string log = (string)e.Parameter;
            loginLabel.Text = log;

        }
    }
}
