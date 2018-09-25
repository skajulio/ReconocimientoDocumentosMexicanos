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
        private MediaFile _foto;
        private string tagFoto;
        public MainPage()
        {
            InitializeComponent();
        }

        private async void ElegirClick(object sender, EventArgs e)
        {
            Resultado.Text = "";
            Precision.Progress = 0;
            text.Text = "";
            using (UserDialogs.Instance.Loading("Cargando imagen..."))
            {
                await CrossMedia.Current.Initialize();

                var foto = await CrossMedia.Current.PickPhotoAsync(new PickMediaOptions()
                {
                    CompressionQuality = 92
                });

                if (foto == null)
                {
                    return;
                }

                _foto = foto;
                ImgSource.Source = FileImageSource.FromFile(foto.Path);
                await ClasificadorClick();
            }
        }

        private async void TomarClick(object sender, EventArgs e)
        {
            Resultado.Text = "";
            Precision.Progress = 0;
            text.Text = "";
            using (UserDialogs.Instance.Loading("Cargando imagen..."))
            {
                await CrossMedia.Current.Initialize();

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
            await ClasificadorClick();
        }

        //private async void ClasificadorClick(object sender, EventArgs e)

        private async Task ClasificadorClick()
        {
            const string endpoint = "https://southcentralus.api.cognitive.microsoft.com/customvision/v2.0/Prediction/1cd65429-17d7-4a80-a31e-a57023de206f/image?iterationId=bbd62279-004f-40e3-a982-96489a19ee8e";
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Prediction-Key", "d20c03142343439d8598d1cf03558421");

            var contentStream = new StreamContent(_foto.GetStream());

            using (UserDialogs.Instance.Loading("Identificando documento..."))
            {
                var response = await httpClient.PostAsync(endpoint, contentStream);

                if (!response.IsSuccessStatusCode)
                {
                    UserDialogs.Instance.Toast("Un error a ocurrido");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();

                var prediction = JsonConvert.DeserializeObject<PredictionResponse>(json);

                var tag = prediction.predictions.First();
                tagFoto = tag.tagName;

                Resultado.Text = $"{tag.tagName} - {tag.probability:p0}";
                Precision.Progress = tag.probability;
            }
            await AnalizarTexto();
        }

        //private async void AnalizarTexto(object sender, EventArgs e)
        private async Task AnalizarTexto()
        {
            var httpClient2 = new HttpClient();
            const string subscriptionKey = "11353e12efd34147a54b3914bb575f44";
            httpClient2.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            const string endpoint2 = "https://southcentralus.api.cognitive.microsoft.com/vision/v2.0/ocr?language=es&detectOrientation=true";

            HttpResponseMessage response2;

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

                text.Text = "";
                List<Region> regions = new List<Region>();
                List<Line> lines = new List<Line>();
                List<Word> words = new List<Word>();
                var json2 = await response2.Content.ReadAsStringAsync();
                var str = "";
                var nombre = "";
                var domicilio = "";
                var fechaDeNacimiento = "";
                var claveDeElector = "";
                var curp = "";

                var textObject = JsonConvert.DeserializeObject<TextObject>(json2);

                regions = textObject.regions.ToList();

                foreach (var r in regions)
                {
                    lines.AddRange(r.lines.ToList());
                }

                foreach (var l in lines)
                {
                    words.AddRange(l.words.ToList());
                }

                //foreach (var w in words)
                //{
                //    if (!w.text.Contains("FECHA") && !w.text.Contains("ESPAÑA") && !w.text.Contains("NACIMIENTO") && !w.text.Contains("ESP ") && !w.text.Contains("DESP") && !w.text.Contains("VALIDO") && !w.text.Contains("HASTA") && !w.text.Contains("SSU") && !w.text.Contains(" M ") && !w.text.Contains(" H "))
                //    {

                //        text.Text = $"{text.Text} {w.text}";
                //        str = $"{text.Text} {w.text}";
                //    }
                //}

                foreach (var w in words)
                {
                    text.Text = $"{text.Text} {w.text}";

                    str = $"{text.Text} {w.text}";

                    if (char.IsDigit(w.text[0]) && w.text.Length == 18 && char.IsLetter(w.text[w.text.Length - 1]))
                    {
                        curp = w.text;
                    }
                }

                List<string> palabras = new List<string>();
                string[] split = str.Split(new Char[] { ' ', ',', '.', ':', '\t' });
                foreach (string s in split)
                {

                    if (s.Trim() != "")
                        palabras.Add(s);
                }
                

                switch (tagFoto)
                {
                    case "INE Frontal":
                        //Obtener datos desde un dni 2.0
                        nombre = getBetween(str, "NOMBRE", "DOMICILIO");
                        domicilio = getBetween(str, "DOMICILIO", "FECH");
                        fechaDeNacimiento = getBetween(str, "NACIMIENTO", "SEXO");
                        claveDeElector = getBetween(str, "ELECTOR", "CURP");
                        //curp = getBetween(str, "CURP", "ESTADO");
                        
                        //Alert para datos de DNI 2.0
                        await DisplayAlert($"{tagFoto}: Datos obtenidos", $"{nombre}\n{curp}\n{domicilio}\n{fechaDeNacimiento}\n{claveDeElector}", "Ok");
                        break;
                    default:
                        await DisplayAlert("Error", $"Documento no válido. {tagFoto} detectado", "Ok");
                        break;
                }
            }
        }

        private async Task AnalizarHandText()
        {
            var httpClient2 = new HttpClient();
            const string subscriptionKey = "11353e12efd34147a54b3914bb575f44";
            httpClient2.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            const string endpoint2 = "https://southcentralus.api.cognitive.microsoft.com/vision/v2.0/recognizeText?mode=Handwritten";

            string operationLocation;

            HttpResponseMessage response2;

            byte[] byteData = GetImageAsByteArray(_foto.Path);

            using (UserDialogs.Instance.Loading("Obteniendo información..."))
            {

                using (ByteArrayContent content = new ByteArrayContent(byteData))
                {
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/octet-stream");

                    response2 = await httpClient2.PostAsync(endpoint2, content);

                    //add hand
                    operationLocation =
                        response2.Headers.GetValues("Operation-Location").FirstOrDefault();
                }

                if (!response2.IsSuccessStatusCode)
                {
                    UserDialogs.Instance.Toast("A ocurrido un error en ocr");
                    return;
                }

                text.Text = "";
                List<Region> regions = new List<Region>();
                List<Line> lines = new List<Line>();
                List<Word> words = new List<Word>();
                var json2 = await response2.Content.ReadAsStringAsync();
                var str = "";
                var nombre = "";
                var primerApellido = "";
                var segundoApellido = "";
                var numDNI = "";
                var apellidos = "";

                //--Hand
                string contentString;
                int i = 0;
                do
                {
                    System.Threading.Thread.Sleep(1000);
                    response2 = await httpClient2.GetAsync(operationLocation);
                    contentString = await response2.Content.ReadAsStringAsync();
                    ++i;
                }
                while (i < 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1);

                if (i == 10 && contentString.IndexOf("\"status\":\"Succeeded\"") == -1)
                {
                    Console.WriteLine("\nTimeout error.\n");
                    return;
                }

                var textObject = JsonConvert.DeserializeObject<TextObject>(json2);

                regions = textObject.regions.ToList();

                foreach (var r in regions)
                {
                    lines.AddRange(r.lines.ToList());
                }

                foreach (var l in lines)
                {
                    words.AddRange(l.words.ToList());
                    text.Text = $"{text.Text} {l.ToString()}";
                }

                foreach (var w in words)
                {
                    if (!w.text.Contains("FECHA") && !w.text.Contains("ESPAÑA") && !w.text.Contains("NACIMIENTO") && !w.text.Contains("ESP ") && !w.text.Contains("DESP") && !w.text.Contains("VALIDO") && !w.text.Contains("HASTA") && !w.text.Contains("SSU") && !w.text.Contains(" M ") && !w.text.Contains(" H "))
                    {

                        text.Text = $"{text.Text} {w.text}";
                        str = $"{text.Text} {w.text}";
                    }
                }

                //foreach (var w in words)
                //{
                //    text.Text = $"{text.Text} {w.text}";

                //    str = $"{text.Text} {w.text}";

                //    if (char.IsDigit(w.text[0]) && w.text.Length == 9 && char.IsLetter(w.text[w.text.Length - 1]))
                //    {
                //        numDNI = w.text;
                //    }
                //}

                // Display the JSON response.
                //text.Text = $"\nResponse:\n\n{JToken.Parse(contentString).ToString()}\n";

                //switch (tagFoto)
                //{
                //    case "DNI 2.0":
                //        //Obtener datos desde un dni 2.0
                //        primerApellido = getBetween(str, "APELLIDO", "SEGUNDO");
                //        segundoApellido = getBetween(str, "SEGUNDO APELLIDO", "NOMBRE");
                //        nombre = getBetween(str, "NOMBRE", "NACIONALIDAD");
                //        //numDNI = getBetween(str, "NÚM. ", "");
                //        //Alert para datos de DNI 2.0
                //        await DisplayAlert("DNI 2.0: Datos obtenidos", $"{nombre}{primerApellido}{segundoApellido} {numDNI}", "Ok");
                //        break;
                //    case "DNI 3.0":
                //        //Obtener datos desde un dni 3.0
                //        apellidos = getBetween(str, "APELLIDOS", "NOMBRE");
                //        nombre = getBetween(str, "NOMBRE", "SEXO");
                //        numDNI = getBetween(str, "DNI ", "");
                //        //if (numDNI == "")
                //        //{
                //        //    numDNI = getBetween(str, "DM ", "");
                //        //}
                //        //Alert para datos de DNI 3.0
                //        await DisplayAlert("DNI 3.0: Datos obtenidos", $"{nombre}{apellidos} {numDNI}", "Ok");
                //        break;
                //    default:
                //        await DisplayAlert("Error", "Documento no válido", "Ok");
                //        break;
                //}
            }
        }

        public static string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && (strSource.Contains(strEnd) || strEnd == ""))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                if (strEnd != "")
                {
                    End = strSource.IndexOf(strEnd, Start);
                    return strSource.Substring(Start, End - Start);
                }
                else
                {
                    End = Start + 10;
                    return strSource.Substring(Start, End - Start);
                }
            }
            else
            {
                return "";
            }
        }

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
