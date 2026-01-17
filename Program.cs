using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using gigachat.Models;
using gigachat.Responce;
using Newtonsoft.Json;

namespace gigachat
{
    internal class Program
    {
        /// <summary>
        /// Клиент ID
        /// </summary>
        static string ClientId = "019bca1f-0f50-72b2-b33b-7fb5c3b89be6";
        /// <summary>
        /// Код авторизации
        /// </summary>
        static string AuthorizationKey = "MDE5YmNhMWYtMGY1MC03MmIyLWIzM2ItN2ZiNWMzYjg5YmU2OmFkNWNiODI0LTE1M2MtNDBmZC04MTI2LTc0ZWVjNDUyZmVhOQ==";
        static async Task Main(string[] args)
        {
            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("Не удалось получить токен");
            }
            while (true)
            {
                Console.Write("Сообщение: ");
                string Message = Console.ReadLine();
                ResponseMessage Answer = await GetAnswer(Token, Message);
                Console.WriteLine("Ответ: " + Answer.choices[0].message.content);
            }
        }
        /// <summary>
        /// Метод получения токена пользователя
        /// </summary>
        /// <param name="RqUID"></param>
        /// <param name="Bearer"></param>
        /// <returns>Токен для выполнения запросов</returns>
        public static async Task<string> GetToken(string rqUID, string bearer)
        {
            string ReturnToken = null;
            string Url = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyError) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("RqUID", rqUID);
                    Request.Headers.Add("Authorization", $"Bearer {bearer}");
                    var Data = new List<KeyValuePair<string, string>>
                    {
                       new KeyValuePair<string, string>("scope", "GIGACHAT_API_PERS")
                    };
                    Request.Content = new FormUrlEncodedContent(Data);
                    HttpResponseMessage Response = await client.SendAsync(Request);
                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        ResponseToken Token = JsonConvert.DeserializeObject<ResponseToken>(ResponseContent);
                        ReturnToken = Token.access_token;
                    }

                }
            }
            return ReturnToken;
        }
        ///<summary>
        ///Метод получения ответа
        ///</summary>
        ///<param name="token">Токен пользователя</param>
        ///<param name="message">Сообщение</param>
        ///<returns></returns>
        public static async Task<ResponseMessage> GetAnswer(string token, string message)
        {
            ResponseMessage responseMessage = null;
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, sslPolicyErrors) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    HttpRequestMessage Request = new HttpRequestMessage(HttpMethod.Post, Url);
                    Request.Headers.Add("Accept", "application/json");
                    Request.Headers.Add("Authorization", $"Bearer {token}");
                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat",
                        stream = false,
                        repetition_penalty = 1,
                        messages = new List<Models.Request.Message>()
                        {
                            new Models.Request.Message()
                            {

                                role="user",
                                content=message
                            }
                        }
                    };
                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    Request.Content = new StringContent(JsonContent, Encoding.UTF8, "application/json");
                    HttpResponseMessage Response = await client.SendAsync(Request);
                    if (Response.IsSuccessStatusCode)
                    {
                        string ResponseContent = await Response.Content.ReadAsStringAsync();
                        responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                    }
                }
            }
            return responseMessage;

        }
    }
}
