using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;

public class Program
{
    private static readonly HttpClient client = new HttpClient();
    static TextWriter traceWriter;
    public static async Task AddNewsAsync(string title, string text, string imagePath, CookieContainer cookies)
    {
        try
        {
            string url = "http://news.permaviat.ru/add"; // Замените на ваш URL

            // ВАЖНО:  Используйте FormUrlEncodedContent для корректного кодирования
            var content = new Dictionary<string, string>
            {
                {"title", title},
                {"text", text},
                {"image", imagePath}  // Используйте корректное имя поля для изображения
            };

            var formContent = new FormUrlEncodedContent(content);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = formContent;

            // ВАЖНО: Добавляем куки в заголовок
            foreach (Cookie cookie in cookies.GetCookies(new Uri(url)))
            {
                request.Headers.Add("Cookie", cookie.ToString());
            }

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode(); // Важно проверить статус

                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Ответ сервера: {responseContent}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Ошибка HTTP: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
    static async Task Main(string[] args)
    {
        // Настройка логирования в файл
        string logFilePath = "debug_log.txt";
        traceWriter = new StreamWriter(logFilePath, true);
        Debug.Listeners.Add(new TextWriterTraceListener(traceWriter));


        try
        {
            Console.WriteLine("Получаем главную страницу...");
            var mainPageContent = await GetContentAsync("http://news.permaviat.ru/main");
            Console.WriteLine("Главная страница получена.");
            //Console.WriteLine(mainPageContent);


            Console.WriteLine("Выполняем вход...");
            var loginResponse = await SingInAsync("student", "Asdfg123", "http://news.permaviat.ru/ajax/login.php");

            if (loginResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("Вход выполнен успешно.");
                string setCookieHeader = loginResponse.Headers.GetValues("Set-Cookie").FirstOrDefault();

                if (setCookieHeader != null)
                {
                    string[] cookies = setCookieHeader.Split(';');
                    Cookie cookie = null;
                    foreach (string cookieString in cookies)
                    {
                        if (cookieString.Contains("PHPSESSID=")) // или другое имя вашей куки
                        {
                            string cookieValue = cookieString.Split('=')[1].Trim();
                            cookie = new Cookie("PHPSESSID", cookieValue); // или другое имя вашей куки
                            cookie.Domain = new Uri("http://news.permaviat.ru/").Host;
                            cookie.Path = "/";
                            break;
                        }
                    }

                    if (cookie != null)
                    {
                        var cookieContainer = new CookieContainer();
                        cookieContainer.Add(new Uri("http://news.permaviat.ru/"), cookie);
                        Console.WriteLine("Получаем контент после входа...");
                        var contentAfterLogin = await GetContentAsync("http://news.permaviat.ru/main", cookieContainer);
                        await Program.AddNewsAsync("Новое название", "Новый текст", "путь/к/изображению.jpg", cookieContainer);
                        Console.WriteLine("Контент получен.");
                        ParsingHtml(contentAfterLogin);
                    }
                    else
                    {
                        Console.WriteLine("Куки PHPSESSID не найдены в ответе.");
                    }

                }
                else
                {
                    Console.WriteLine("Заголовок Set-Cookie отсутствует в ответе.");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка входа: {loginResponse.StatusCode} - {loginResponse.ReasonPhrase}");
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Ошибка HTTP: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
        finally
        {
            client.Dispose();
            traceWriter.Close();
        }

        Console.Read();
    }


    public static async Task<string> GetContentAsync(string url, CookieContainer cookies = null)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (cookies != null)
            {
                foreach (Cookie cookie in cookies.GetCookies(new Uri(url)))
                {
                    request.Headers.Add("Cookie", cookie.ToString());
                }
            }

            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при запросе: {ex.Message}");
            throw;
        }
    }

    public static async Task<HttpResponseMessage> SingInAsync(string login, string password, string url)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("login", login),
            new KeyValuePair<string, string>("password", password)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = content;

        var response = await client.SendAsync(request);
        return response;
    }



    public static void ParsingHtml(string htmlCode)
    {
        var html = new HtmlDocument();
        html.LoadHtml(htmlCode);
        var Document = html.DocumentNode;
        IEnumerable<HtmlNode> DivsNews = Document.Descendants("div").Where(n => n.HasClass("news"));
        foreach (HtmlNode DivNews in DivsNews)
        {
            try
            {
                var src = DivNews.ChildNodes[1].GetAttributeValue("src", "none");
                var name = DivNews.ChildNodes[3].InnerText;
                var description = DivNews.ChildNodes[5].InnerText;
                Console.WriteLine(name + "\n" + "Изображение: " + src + "\n" + "Описание: " + description + "\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга новости: {ex.Message}");
            }
        }
    }
}