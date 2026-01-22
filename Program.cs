using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using gigachat.Classes;
using gigachat.Models;
using gigachat.Responce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace gigachat
{
    internal class Program
    {
        /// <summary>
        /// Клиент ID
        /// </summary>
        static string ClientId = "019b038f-be32-7728-9ba8-86b03afb5efc";
        /// <summary>
        /// Код авторизации
        /// </summary>
        static string AuthorizationKey = "MDE5YjAzOGYtYmUzMi03NzI4LTliYTgtODZiMDNhZmI1ZWZjOjdlZTk5Y2MzLTFlMGEtNGFjMC1hMjI0LWMxY2ZiZjY1ZmNiMA==";

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Для создания изображений: /img Ваш запрос");
            Console.WriteLine("Для текстового чата: просто введите сообщение");
            Console.ResetColor();

            string Token = await GetToken(ClientId, AuthorizationKey);
            if (Token == null)
            {
                Console.WriteLine("❌ Не удалось получить токен");
                return;
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("✅ Токен получен. Чат готов!");
            Console.ResetColor();

            List<Request.Message> ConversationHistory = new List<Request.Message>();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("Вы: ");
                string UserInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(UserInput))
                    continue;

                UserInput = UserInput.Trim();

                // Команды
                if (UserInput.Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    ConversationHistory.Clear();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("🧹 История чата очищена");
                    continue;
                }

                if (UserInput.Equals("/exit", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("👋 До свидания!");
                    break;
                }

                if (UserInput.StartsWith("/img", StringComparison.OrdinalIgnoreCase))
                {
                    string prompt = UserInput.Substring(4).Trim();
                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        Console.WriteLine("❌ Введите описание после /img");
                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("🖼️  Генерация изображения...");

                    var imgMessages = new List<Request.Message>()
                    {
                        new Request.Message() { role = "user", content = prompt }
                    };

                    string imagePath = await GetPictureAndSave(Token, imgMessages, ClientId);
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ Изображение сохранено: {imagePath}");

                        Console.Write("Установить как обои? (д/н): ");
                        string ans = Console.ReadLine()?.Trim().ToLower();
                        if (ans == "д" || ans == "да" || ans == "y" || ans == "yes")
                        {
                            try
                            {
                                WallpaperSetter.SetWallpaper(imagePath);
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("🖼️  Обои установлены!");
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"❌ Ошибка установки обоев: {ex.Message}");
                            }
                        }
                    }
                    Console.ResetColor();
                    continue;
                }

                // Текстовый чат
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("🤖 GigaChat думает...");

                // Добавляем сообщение пользователя
                ConversationHistory.Add(new Request.Message { role = "user", content = UserInput });

                // Ограничиваем историю (последние 20 сообщений)
                if (ConversationHistory.Count > 20)
                {
                    ConversationHistory.RemoveRange(0, ConversationHistory.Count - 20);
                }

                var response = await GetAnswer(Token, ConversationHistory);

                if (response != null && response.choices?.Count > 0)
                {
                    string aiResponse = response.choices[0].message.content;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"GigaChat: {aiResponse}");

                    // Добавляем ответ в историю
                    ConversationHistory.Add(new Request.Message { role = "assistant", content = aiResponse });
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("❌ Ошибка получения ответа от GigaChat");
                }
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Метод получения токена пользователя
        /// </summary>
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
                    else
                    {
                        Console.WriteLine($"Ошибка получения токена: {Response.StatusCode}");
                    }
                }
            }
            return ReturnToken;
        }

        /// <summary>
        /// Метод получения текстового ответа
        /// </summary>
        public static async Task<ResponseMessage> GetAnswer(string token, List<Request.Message> messages)
        {
            ResponseMessage responseMessage = null;
            string Url = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
            using (HttpClientHandler Handler = new HttpClientHandler())
            {
                Handler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => true;
                using (HttpClient client = new HttpClient(Handler))
                {
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    client.DefaultRequestHeaders.Add("X-Client-ID", ClientId);

                    Models.Request DataRequest = new Models.Request()
                    {
                        model = "GigaChat",
                        stream = false,
                        repetition_penalty = 1,
                        messages = messages
                    };

                    string JsonContent = JsonConvert.SerializeObject(DataRequest);
                    using (var content = new StringContent(JsonContent, Encoding.UTF8, "application/json"))
                    {
                        HttpResponseMessage Response = await client.PostAsync(Url, content);

                        if (Response.IsSuccessStatusCode)
                        {
                            string ResponseContent = await Response.Content.ReadAsStringAsync();
                            responseMessage = JsonConvert.DeserializeObject<ResponseMessage>(ResponseContent);
                        }
                        else
                        {
                            string errorBody = await Response.Content.ReadAsStringAsync();
                            Console.WriteLine($"❌ API ошибка ({Response.StatusCode}): {errorBody}");
                        }
                    }
                }
            }
            return responseMessage;
        }

        /// <summary>
        /// Метод для генерации изображения
        /// </summary>
        public static async Task<string> GetPictureAndSave(string token, List<Models.Request.Message> messages, string clientId = null)
        {
            if (string.IsNullOrEmpty(token)) throw new ArgumentNullException(nameof(token));
            string baseUrl = "https://gigachat.devices.sberbank.ru/api/v1";

            using (var handler = new HttpClientHandler())
            {
                handler.ServerCertificateCustomValidationCallback = (msg, cert, chain, ssl) => true;

                using (var http = new HttpClient(handler))
                {
                    http.DefaultRequestHeaders.Clear();
                    http.DefaultRequestHeaders.Add("Accept", "application/json");
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                    if (!string.IsNullOrEmpty(clientId))
                    {
                        http.DefaultRequestHeaders.Add("X-Client-ID", clientId);
                    }

                    var payload = new
                    {
                        model = "GigaChat",
                        messages = messages,
                        function_call = "auto"
                    };

                    var jsonPayload = JsonConvert.SerializeObject(payload);
                    using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
                    {
                        var postUrl = $"{baseUrl}/chat/completions";
                        var resp = await http.PostAsync(postUrl, content);

                        if (!resp.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"❌ Ошибка при создании изображения: {resp.StatusCode}");
                            var errBody = await resp.Content.ReadAsStringAsync();
                            Console.WriteLine(errBody);
                            return null;
                        }

                        var respJson = await resp.Content.ReadAsStringAsync();
                        string htmlContent = null;
                        try
                        {
                            var j = JObject.Parse(respJson);
                            htmlContent = j["choices"]?[0]?["message"]?["content"]?.ToString();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("❌ Не удалось распарсить ответ: " + ex.Message);
                            return null;
                        }

                        if (string.IsNullOrEmpty(htmlContent))
                        {
                            Console.WriteLine("❌ В ответе нет содержимого с тегом <img>");
                            return null;
                        }

                        var m = Regex.Match(htmlContent, "<img\\s+[^>]*src\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                        if (!m.Success)
                        {
                            Console.WriteLine("❌ Тег <img> не найден");
                            Console.WriteLine("Ответ: " + htmlContent.Substring(0, Math.Min(200, htmlContent.Length)));
                            return null;
                        }

                        string fileId = m.Groups[1].Value;
                        var fileUrl = $"{baseUrl}/files/{fileId}/content";

                        using (var request = new HttpRequestMessage(HttpMethod.Get, fileUrl))
                        {
                            request.Headers.Add("Accept", "image/jpeg");
                            if (!string.IsNullOrEmpty(clientId))
                            {
                                request.Headers.Add("X-Client-ID", clientId);
                            }

                            var fileResp = await http.SendAsync(request);
                            if (!fileResp.IsSuccessStatusCode)
                            {
                                Console.WriteLine($"❌ Ошибка скачивания: {fileResp.StatusCode}");
                                return null;
                            }

                            var bytes = await fileResp.Content.ReadAsByteArrayAsync();
                            string outPath = Path.Combine(Environment.CurrentDirectory, $"gigachat_{fileId}.jpg");

                            using (FileStream fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                            {
                                await fs.WriteAsync(bytes, 0, bytes.Length);
                            }
                            return outPath;
                        }
                    }
                }
            }
        }
    }
}
