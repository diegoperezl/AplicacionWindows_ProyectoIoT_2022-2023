using System;
using System.Collections.Generic;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Web.Http;
using Windows.UI.Core;
using Windows.UI.Xaml.Shapes;
using Windows.UI;
using Newtonsoft.Json;
using System.IO;

namespace ProyectoIOT_DiegoPerez
{
    public sealed partial class MainPage : Page
    {
        private HttpClient httpClient;

        //Diccionarios para almacenar los valores de los IDs de los dispositivos
        Dictionary<string, Dictionary<string, string>> leds;
        Dictionary<string, Rectangle> ledsText;
        Dictionary<string, Dictionary<string, string>> pantallas;
        Dictionary<string, TextBlock> pantallasText;
        Dictionary<string, Dictionary<string, string>> sensores;
        Dictionary<string, string> vagones;
        Dictionary<string, TextBlock> vagonesText;

        public MainPage()
        {
            this.InitializeComponent();
            httpClient = new HttpClient();
            leds = new Dictionary<string, Dictionary<string, string>>();
            ledsText = new Dictionary<string, Rectangle>();
            pantallas = new Dictionary<string, Dictionary<string, string>>();
            pantallasText = new Dictionary<string, TextBlock>();
            sensores = new Dictionary<string, Dictionary<string, string>>();
            vagones = new Dictionary<string, string>();
            vagonesText = new Dictionary<string, TextBlock>();

            this.SizeChanged += MainPage_SizeChanged;

            iniciarLeds();
            iniciarPantallas();
            iniciarSensores();
            iniciarVagones();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            string[] credentials = (string[])e.Parameter;
            iniciarConexion(credentials);
        }

        //Hilo que actualiza la información del panel cada 2 segundos
        static async void updateUIAsync(object parameter)
        {
            MainPage page = (MainPage)parameter;

            while (true)
            {
                System.Threading.Thread.Sleep(2000);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    _ = page.obtenerValoresVagonAsync("vagon1");
                    _ = page.obtenerValoresVagonAsync("vagon2");
                    _ = page.obtenerValoresVagonAsync("vagon3");
                });
                
            }
            
        }

        //Se establece la conexión con Thingsboard a través de ls credenciales introducidas
        private async void iniciarConexion(string[] credentials)
        {
            HttpResponseMessage httpResponse = await httpClient.PostAsync(
                               new Uri("https://srv-iot.diatel.upm.es/api/auth/login"),
                               new HttpStringContent(
                                   "{\"username\":\""+credentials[0]+"\",\"password\":\""+credentials[1]+"\"}",
                                   Windows.Storage.Streams.UnicodeEncoding.Utf8,
                                   "application/json"));
            string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
            dynamic jsonResponseBody = JsonConvert.DeserializeObject(httpResponseBody);
            string token = jsonResponseBody.token;
            httpClient.DefaultRequestHeaders.Add(
                 "X-Authorization",
                 string.Format("Bearer {0}", token));
            try
            {
                httpResponse.EnsureSuccessStatusCode();
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(Login), "Credenciales no válidas");
            }
        }

        //Función correspondiente al boton "iniciar", inicia el hilo de actualización
        private void Iniciar(object sender, RoutedEventArgs e)
        {

            System.Threading.Thread tarea = new System.Threading.Thread(new System.Threading.ParameterizedThreadStart(updateUIAsync));
            tarea.Start(this);
        }

        //Actualiza los valores de los dispositivos de un vagón en concreto, a través de llamadas a la API
        private async Task obtenerValoresVagonAsync(string vagon)
        {
            HttpResponseMessage httpResponse;

            //Actualiza valores de los LEDs
            foreach (KeyValuePair<string, string> entry in leds[vagon])
            {

                httpResponse = await httpClient.GetAsync(new Uri("https://srv-iot.diatel.upm.es/api/v1/"+entry.Value+"/attributes?sharedKeys=color"));
                httpResponse.EnsureSuccessStatusCode();
                string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                dynamic ledAttribute = JsonConvert.DeserializeObject(httpResponseBody);
                string color = ledAttribute.shared.color;
                Rectangle rect = ledsText[entry.Key];

                if (color.Equals("GREEN"))
                {
                    SolidColorBrush mySolidColorBrush = new SolidColorBrush();
                    mySolidColorBrush.Color = Color.FromArgb(255, 0, 255, 0);
                    rect.Fill = mySolidColorBrush;
                }else if (color.Equals("RED"))
                {
                    SolidColorBrush mySolidColorBrush = new SolidColorBrush();
                    mySolidColorBrush.Color = Color.FromArgb(255, 255, 0, 0);
                    rect.Fill = mySolidColorBrush;
                }
            }

            //Actualiza valores de las pantallas
            foreach (KeyValuePair<string, string> entry in pantallas[vagon])
            {

                httpResponse = await httpClient.GetAsync(new Uri("https://srv-iot.diatel.upm.es/api/v1/" + entry.Value + "/attributes?sharedKeys=numPersonas"));
                httpResponse.EnsureSuccessStatusCode();
                string httpResponseBody = await httpResponse.Content.ReadAsStringAsync();
                dynamic pantallaAttribute = JsonConvert.DeserializeObject(httpResponseBody);
                TextBlock block = pantallasText[entry.Key];
                block.Text = pantallaAttribute.shared.numPersonas;
            }

            //Actualiza valores del número de personas y máximo número de personas del vagón
            httpResponse = await httpClient.GetAsync(new Uri("https://srv-iot.diatel.upm.es/api/plugins/telemetry/ASSET/"+vagones[vagon]+ "/values/attributes?keys=maxPersonas,personasVagon"));
            httpResponse.EnsureSuccessStatusCode();
            string httpResponseBodyVagon = await httpResponse.Content.ReadAsStringAsync();
            dynamic jsonVagon = JsonConvert.DeserializeObject(httpResponseBodyVagon);
            string maxPersonas = jsonVagon[0].value;
            string personasVagon = jsonVagon[1].value;
            vagonesText[vagon + "maxPersonas"].Text = maxPersonas;
            vagonesText[vagon + "personasVagon"].Text = personasVagon;


        }

        //Función para los botones que simulan el sensor de la puerta, introduciendo o sacando personas
        private async void detecionClickAsync(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            string bName = b.Name;
            string accion = bName;
            int valor;

            if (accion.Contains("salir")){
                valor = -1;
                accion = accion.Replace("salir", "sensor");
            }
            else
            {
                valor = 1;
                accion = accion.Replace("entrar", "sensor");
            }

            if (accion.Contains("Vagon1")){
                await deteccionPersona(valor, sensores["vagon1"][accion]);
                await obtenerValoresVagonAsync("vagon1");
                if (bName.Equals("entrarPuertaTraseraVagon1"))
                {
                    await deteccionPersona(-1, sensores["vagon2"]["sensorPuertaFrontalVagon2"]);
                    await obtenerValoresVagonAsync("vagon2");
                }else if (bName.Equals("salirPuertaTraseraVagon1"))
                {
                    await deteccionPersona(1, sensores["vagon2"]["sensorPuertaFrontalVagon2"]);
                    await obtenerValoresVagonAsync("vagon2");
                }
            }
            else if (accion.Contains("Vagon2")){
                await deteccionPersona(valor, sensores["vagon2"][accion]);
                await obtenerValoresVagonAsync("vagon2");
                if (bName.Equals("entrarPuertaTraseraVagon2"))
                {
                    await deteccionPersona(-1, sensores["vagon3"]["sensorPuertaFrontalVagon3"]);
                    await obtenerValoresVagonAsync("vagon3");
                }
                else if (bName.Equals("salirPuertaTraseraVagon2"))
                {
                    await deteccionPersona(1, sensores["vagon3"]["sensorPuertaFrontalVagon3"]);
                    await obtenerValoresVagonAsync("vagon3");
                }
            }
            else{
                await deteccionPersona(valor, sensores["vagon3"][accion]);
                await obtenerValoresVagonAsync("vagon3");
            }

        }


        //Función complementaria a "deteccionCLickAsync", haciendo la llamada a la API con el JSON que enviaría el sensor de la puerta
        private async Task deteccionPersona(int entraSale, String deviceKey)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                Uri uri = new Uri("https://srv-iot.diatel.upm.es/api/v1/"+deviceKey+"/telemetry");

                HttpStringContent content = new HttpStringContent(
                    "{ \"contadorPersonas\": " + entraSale + " }",
                    Windows.Storage.Streams.UnicodeEncoding.Utf8,
                    "application/json");

                HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(
                    uri,
                    content);

                httpResponseMessage.EnsureSuccessStatusCode();
                var httpResponseBody = await httpResponseMessage.Content.ReadAsStringAsync();
                Debug.WriteLine(httpResponseBody);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        //Inicialización de los valores IDs de los LEDS
        private void iniciarLeds()
        {
            leds.Add("vagon1", new Dictionary<string, string>());
            leds["vagon1"].Add("ledPuertaFrontalVagon1", "DaEWvc7uTEEKM5hWffgd");
            leds["vagon1"].Add("ledPuertaLateral1Vagon1", "ZeTg2ACUbLTZcolYvaOi");
            leds["vagon1"].Add("ledPuertaLateral2Vagon1", "iJ35V3XAt0f9ieQz6BzU");
            leds["vagon1"].Add("ledPuertaTraseraVagon1", "elQ0WbdIDDvtlWItzv2S");

            leds.Add("vagon2", new Dictionary<string, string>());
            leds["vagon2"].Add("ledPuertaFrontalVagon2", "FznjKn82zwr16gxCL0KL");
            leds["vagon2"].Add("ledPuertaLateral1Vagon2", "69wp1umIvRr2o4ULl616");
            leds["vagon2"].Add("ledPuertaLateral2Vagon2", "UlNk1M2lPXns41nokOar");
            leds["vagon2"].Add("ledPuertaTraseraVagon2", "26eXlNdbqN5IHkgf1bRv");

            leds.Add("vagon3", new Dictionary<string, string>());
            leds["vagon3"].Add("ledPuertaFrontalVagon3", "axOjNBHOzI4UjaZCBW9f");
            leds["vagon3"].Add("ledPuertaLateral1Vagon3", "1TlxvKYjl5HCFHEX911H");
            leds["vagon3"].Add("ledPuertaLateral2Vagon3", "DLTUjBsGWEymgQnQwxPg");
            leds["vagon3"].Add("ledPuertaTraseraVagon3", "vxEXJnf7HJqBc2qin567");

            ledsText.Add("ledPuertaFrontalVagon1", ledPuertaFrontalVagon1);
            ledsText.Add("ledPuertaLateral1Vagon1", ledPuertaLateral1Vagon1);
            ledsText.Add("ledPuertaLateral2Vagon1", ledPuertaLateral2Vagon1);
            ledsText.Add("ledPuertaTraseraVagon1", ledPuertaTraseraVagon1);
            ledsText.Add("ledPuertaFrontalVagon2", ledPuertaFrontalVagon2);
            ledsText.Add("ledPuertaLateral1Vagon2", ledPuertaLateral1Vagon2);
            ledsText.Add("ledPuertaLateral2Vagon2", ledPuertaLateral2Vagon2);
            ledsText.Add("ledPuertaTraseraVagon2", ledPuertaTraseraVagon2);
            ledsText.Add("ledPuertaFrontalVagon3", ledPuertaFrontalVagon3);
            ledsText.Add("ledPuertaLateral1Vagon3", ledPuertaLateral1Vagon3);
            ledsText.Add("ledPuertaLateral2Vagon3", ledPuertaLateral2Vagon3);
            ledsText.Add("ledPuertaTraseraVagon3", ledPuertaTraseraVagon3);

        }

        //Inicialización de los valores IDs de las pantallas
        private void iniciarPantallas()
        {
            pantallas.Add("vagon1", new Dictionary<string, string>());
            pantallas["vagon1"].Add("pantallaPuertaFrontalVagon1", "7XSW90W6evKWKvb4TAKI");
            pantallas["vagon1"].Add("pantallaPuertaLateral1Vagon1", "JpnxkZfFBTa89PHK3JhA");
            pantallas["vagon1"].Add("pantallaPuertaLateral2Vagon1", "si4ii90uFgjNO4ks7GLd");
            pantallas["vagon1"].Add("pantallaPuertaTraseraVagon1", "QKwujjIhDhs8vUObPQtC");

            pantallas.Add("vagon2", new Dictionary<string, string>());
            pantallas["vagon2"].Add("pantallaPuertaFrontalVagon2", "K6NmpMg9Tyam0GPA7leZ");
            pantallas["vagon2"].Add("pantallaPuertaLateral1Vagon2", "A7lTTvacSvbVkENJzQ2e");
            pantallas["vagon2"].Add("pantallaPuertaLateral2Vagon2", "p6bYUDu9RVzXzkbVk2GJ");
            pantallas["vagon2"].Add("pantallaPuertaTraseraVagon2", "R653cyW6UvfOrVMJGvpj");

            pantallas.Add("vagon3", new Dictionary<string, string>());
            pantallas["vagon3"].Add("pantallaPuertaFrontalVagon3", "NWScpKeaBk0vcCWyr5OU");
            pantallas["vagon3"].Add("pantallaPuertaLateral1Vagon3", "NL1VmKawkes8G16mbyX0");
            pantallas["vagon3"].Add("pantallaPuertaLateral2Vagon3", "Pcu5CKNq2ZO2ZBScBFw8");
            pantallas["vagon3"].Add("pantallaPuertaTraseraVagon3", "A8BiB3YyCHg3hLGw9KyN");

            pantallasText.Add("pantallaPuertaFrontalVagon1", pantallaPuertaFrontalVagon1);
            pantallasText.Add("pantallaPuertaLateral1Vagon1", pantallaPuertaLateral1Vagon1);
            pantallasText.Add("pantallaPuertaLateral2Vagon1", pantallaPuertaLateral2Vagon1);
            pantallasText.Add("pantallaPuertaTraseraVagon1", pantallaPuertaTraseraVagon1);
            pantallasText.Add("pantallaPuertaFrontalVagon2", pantallaPuertaFrontalVagon2);
            pantallasText.Add("pantallaPuertaLateral1Vagon2", pantallaPuertaLateral1Vagon2);
            pantallasText.Add("pantallaPuertaLateral2Vagon2", pantallaPuertaLateral2Vagon2);
            pantallasText.Add("pantallaPuertaTraseraVagon2", pantallaPuertaTraseraVagon2);
            pantallasText.Add("pantallaPuertaFrontalVagon3", pantallaPuertaFrontalVagon3);
            pantallasText.Add("pantallaPuertaLateral1Vagon3", pantallaPuertaLateral1Vagon3);
            pantallasText.Add("pantallaPuertaLateral2Vagon3", pantallaPuertaLateral2Vagon3);
            pantallasText.Add("pantallaPuertaTraseraVagon3", pantallaPuertaTraseraVagon3);

        }

        //Inicialización de los valores IDs de los sensores
        private void iniciarSensores()
        {
            sensores.Add("vagon1", new Dictionary<string, string>());
            sensores["vagon1"].Add("sensorPuertaFrontalVagon1", "7aY2y3cC4NfZeqxMdrGt");
            sensores["vagon1"].Add("sensorPuertaLateral1Vagon1", "dJylko6mZUjCV0BmqpaA");
            sensores["vagon1"].Add("sensorPuertaLateral2Vagon1", "VNvgFb3okQzsQC5RgbZf");
            sensores["vagon1"].Add("sensorPuertaTraseraVagon1", "2l8N5x4o7C0zMWROKHQ9");

            sensores.Add("vagon2", new Dictionary<string, string>());
            sensores["vagon2"].Add("sensorPuertaFrontalVagon2", "w7J39N3ZJ1O6pr5fKpJZ");
            sensores["vagon2"].Add("sensorPuertaLateral1Vagon2", "xwRFUJjRJUXtJd0wgGm9");
            sensores["vagon2"].Add("sensorPuertaLateral2Vagon2", "hIYR9haVQUUWlyl2Mwi3");
            sensores["vagon2"].Add("sensorPuertaTraseraVagon2", "bPhEJPYxS2AGpAVjhfU0");

            sensores.Add("vagon3", new Dictionary<string, string>());
            sensores["vagon3"].Add("sensorPuertaFrontalVagon3", "ewDLrQUXcPXxyjxepBnD");
            sensores["vagon3"].Add("sensorPuertaLateral1Vagon3", "RHZQj9bEV0sJJWH9PC6v");
            sensores["vagon3"].Add("sensorPuertaLateral2Vagon3", "zO4o1Kal7DFJv4JJBweH");
            sensores["vagon3"].Add("sensorPuertaTraseraVagon3", "hlrE3dBdtxZ38ZhjzE5A");

        }

        //Inicialización de los valores IDs de los vagones
        private void iniciarVagones()
        {
            vagones.Add("vagon1", "c9a50c70-6984-11ed-8d2b-073de4e14907");
            vagones.Add("vagon2", "10a5c4a0-6c0c-11ed-8d2b-073de4e14907");
            vagones.Add("vagon3", "a7b640e0-6c0c-11ed-8d2b-073de4e14907");

            vagonesText.Add("vagon1personasVagon", numPersonasVagon1);
            vagonesText.Add("vagon2personasVagon", numPersonasVagon2);
            vagonesText.Add("vagon3personasVagon", numPersonasVagon3);

            vagonesText.Add("vagon1maxPersonas", maxPersonasVagon1);
            vagonesText.Add("vagon2maxPersonas", maxPersonasVagon2);
            vagonesText.Add("vagon3maxPersonas", maxPersonasVagon3);

        }

        //Cambia el tamaño del texto a proporción del tamaño de la ventana
        private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var bounds = Window.Current.Bounds;
            double height = bounds.Height;
            double width = bounds.Width;

            if (height < 400 || width < 800)
            {

                Grid g = this.FindName("AppGrid") as Grid;
                foreach (UIElement element in g.Children)
                {
                    if (element is Border)
                    {
                        Border b = (Border)element;
                        ((TextBlock)b.Child).FontSize = 15;
                    }
                }

            }
            else if (height < 600 || width < 1200)
            {
                Grid g = this.FindName("AppGrid") as Grid;
                foreach (UIElement element in g.Children)
                {
                    if (element is Border)
                    {
                        Border b = (Border)element;
                        ((TextBlock)b.Child).FontSize = 20;
                    }
                }
            }
            else
            {
                Grid g = this.FindName("AppGrid") as Grid;
                foreach (UIElement element in g.Children)
                {
                    if (element is Border)
                    {
                        Border b = (Border)element;
                        ((TextBlock)b.Child).FontSize = 30;
                    }
                }
            }


        }

    }
}
