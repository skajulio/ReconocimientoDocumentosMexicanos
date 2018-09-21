using Acr.UserDialogs;
using Newtonsoft.Json;
using Plugin.Media;
using Plugin.Media.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using CustomV5.Models;
using static CustomV5.Models.ImageTextModel;
using Region = CustomV5.Models.ImageTextModel.Region;
using static CustomV5.Models.PredictionResponseModel;

namespace CustomV5
{
    public partial class MainPage : ContentPage
    {
        private MediaFile _foto; //Almacena stream de la imagen a analizar
        private string tagFoto; //Almacena el tipo de documento analizado (IFE, Acta de naciemiento, etc...)
        public MainPage()
        {
            InitializeComponent();
        }

        //Función que se manda llamar desde el xaml. Tomar imagen desde la galería del dispositivo
        private async void ElegirClick(object sender, EventArgs e)
        {
            //Reinicio de campos en el xaml
            Resultado.Text = "";
            Precision.Progress = 0;
            text.Text = "";


            using (UserDialogs.Instance.Loading("Cargando imagen..."))
            {
                //Inicializa el plugin media
                await CrossMedia.Current.Initialize();

                //Accede a la galería del dispositivo
                var foto = await CrossMedia.Current.PickPhotoAsync(new PickMediaOptions()
                {
                    CompressionQuality = 92
                });

                //En caso de regresar al xaml sin elegir imagen
                if (foto == null)
                {
                    return;
                }

                //Asigna la variable a la variable tipo mediafile
                _foto = foto;

                //Muestra la imagen en el xaml
                ImgSource.Source = FileImageSource.FromFile(foto.Path);

                //Manda llamar la función de análisis de imágen (custom vision)
                await ClasificadorClick();
            }
        }

        //Funcion que se manda llamar desde xaml. Accede a la camara
        private async void TomarClick(object sender, EventArgs e)
        {
            Resultado.Text = "";
            Precision.Progress = 0;
            text.Text = "";

            using (UserDialogs.Instance.Loading("Cargando imagen..."))
            {
                await CrossMedia.Current.Initialize();

                //Acceso a la cámara y almacenamiento en el dispositivo
                var foto = await CrossMedia.Current.TakePhotoAsync(new StoreCameraMediaOptions()
                {
                    CompressionQuality = 92,
                    SaveToAlbum = true
                    //Directory="clasificator",
                    //Name="source.jpg"
                });

                if (foto == null)
                {
                    return;
                }

                _foto = foto;
                ImgSource.Source = FileImageSource.FromFile(foto.Path);

            }

            //Llama al método de análisis de imagen (Custom Vision)
            await ClasificadorClick();
        }

        //Método para el análisis de la imágen. Identificación del tipo de documento (Custom Vision)
        private async Task ClasificadorClick()
        {
            //Endpont y prediccion key para acceder a la API de azure
            const string endpoint = "https://southcentralus.api.cognitive.microsoft.com/customvision/v2.0/Prediction/1cd65429-17d7-4a80-a31e-a57023de206f/image?iterationId=09d73512-1314-48a6-9a94-1a75ccd984bf";
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Prediction-Key", "d20c03142343439d8598d1cf03558421");

            //Obtiene el stream de la imagen
            var contentStream = new StreamContent(_foto.GetStream());

            using (UserDialogs.Instance.Loading("Identificando documento..."))
            {
                //Envío de información a la Api para su análisis
                var response = await httpClient.PostAsync(endpoint, contentStream);

                //Valida si se obtiene respuesta del servidor
                if (!response.IsSuccessStatusCode)
                {
                    UserDialogs.Instance.Toast("Un error a ocurrido");
                    return;
                }

                //Obtiene un json
                var json = await response.Content.ReadAsStringAsync();

                //Deserealización de json
                var prediction = JsonConvert.DeserializeObject<PredictionResponse>(json);

                //Se obtiene el tipo de documento del json
                var tag = prediction.predictions.First();//Obtiene el tag con mayor porcentage de probabilidad
                tagFoto = tag.tagName;

                //Se imprimen valores en el xaml
                Resultado.Text = $"{tag.tagName} - {tag.probability:p0}";
                Precision.Progress = tag.probability;
            }
            //Se manda llamar el análisis de texto
            await AnalizarTexto();
        }

        //private async void AnalizarTexto(object sender, EventArgs e)
        private async Task AnalizarTexto()
        {
            var httpClient2 = new HttpClient();
            const string subscriptionKey = "11353e12efd34147a54b3914bb575f44";
            httpClient2.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            const string endpoint2 = "https://southcentralus.api.cognitive.microsoft.com/vision/v2.0/ocr?language=unk&detectOrientation=false";

            HttpResponseMessage response2;

            //Transforma la imagen segun el path
            byte[] byteData = GetImageAsByteArray(_foto.Path);

            using (UserDialogs.Instance.Loading("Obteniendo información..."))
            {

                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    response2 = await httpClient2.PostAsync(endpoint2, content);
                }

                if (!response2.IsSuccessStatusCode)
                {
                    UserDialogs.Instance.Toast("A ocurrido un error en ocr");
                    return;
                }
                
                //Inicializa los campos del json que se obtendrán de la api
                text.Text = "";
                List<Region> regions = new List<Region>();
                List<Line> lines = new List<Line>();
                List<Word> words = new List<Word>();

                var json2 = await response2.Content.ReadAsStringAsync();

                //Variables de informacion obtenidas del json
                var str = "";
                var nombre = "";
                var claveINE = "";
                //var domicilio = "";
                //var fechaDeNacimiento = "";
                //var claveDeElector = "";
                //var curp = "";

                var textObject = JsonConvert.DeserializeObject<TextObject>(json2);

                //Obtener información del json por medio de foreach
                regions = textObject.regions.ToList();

                foreach (var r in regions)
                {
                    lines.AddRange(r.lines.ToList());
                }

                foreach (var l in lines)
                {
                    words.AddRange(l.words.ToList());
                }

                foreach (var w in words)
                {
                    text.Text = $"{text.Text} {w.text}";

                    str = $"{text.Text} {w.text}";
                }

                //Obtener información según el tipo de documento
                switch (tagFoto)
                {
                    case "INE Frontal":
                        //Obtener datos desde un dni 2.0
                        nombre = getBetween(str, "NOMBRE", "DOMICILIO");
                        //domicilio = getBetween(str, "DOMICILIO", "FECH");
                        //fechaDeNacimiento = getBetween(str, "NACIMIENTO", "SEXO");
                        //claveDeElector = getBetween(str, "ELECTOR", "CURP");
                        //curp = getBetween(str, "CURP", "ESTADO");

                        //Alert para datos de DNI 2.0
                        await DisplayAlert($"{tagFoto}: Datos obtenidos", $"NOMBRE:\n{nombre}", "Ok");
                        break;
                    case "INE Reverso":
                        claveINE = "Por identificar";
                        await DisplayAlert($"{tagFoto}: Datos obtenidos", $"CLAVE:\n{claveINE}", "Ok");
                        break;
                    case "IFE Frontal":
                        //Obtener datos desde un IFE Frontal
                        nombre = getBetween(str, "NOMBRE", "DOMICILIO");
                        //Alert para datos de IFE Frontal
                        await DisplayAlert($"{tagFoto}: Datos obtenidos", $"NOMBRE:\n{nombre}", "Ok");
                        break;
                    case "IFE Reverso":
                        //Obtener datos desde un IFE Reverso

                        //Alert para datos de IFE Reverso
                        await DisplayAlert($"{tagFoto}: Datos obtenidos", $"CLAVE:\n{claveINE}", "Ok");
                        break;
                    default:
                        await DisplayAlert("Error", $"Documento no válido. Se encontró {tagFoto}", "Ok");
                        break;
                }
            }
        }

        //Método para obtención de información de string obtenido del json Parámetros:(string a analizar, string para delimitar inicio, string para delimitar fin)
        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;

            //Valida si el string a analizar contiene los parámetros de inicio y fin
            if (strSource.Contains(strStart) && (strSource.Contains(strEnd) || strEnd == ""))
            {
                //Ubica la posición de inicio en el string a analizar
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;

                //Validación. Si el string final es "", se tomará como punto final la longitud del string a analizar
                if (strEnd != "")
                {
                    End = strSource.IndexOf(strEnd, Start);
                    return strSource.Substring(Start, End - Start); //Retorna cadena de texto desde la posición inicial hasta el final del string principal
                }
                else
                {
                    //Otiene la posiscion para delimir hasta donde se va a obtener del string principal
                    End = Start + 10;
                    return strSource.Substring(Start, End - Start); //Retorna cadena delimitada del string principal
                }
            }
            else
            {
                return "";
            }
        }

        //Obtiene los bytes de la imagen
        static byte[] GetImageAsByteArray(string imageFilePath)
        {
            using (FileStream fileStream =
                new FileStream(imageFilePath, FileMode.Open, FileAccess.Read))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                return binaryReader.ReadBytes((int)fileStream.Length);
            }
        }
    }
}
