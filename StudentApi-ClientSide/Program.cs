using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using static System.Net.WebRequestMethods;

class Program
{
    static readonly HttpClient httpClient = new HttpClient();
    private const string BaseUrl = "https://localhost:7088/";
    private const string Email = "QAS@GMAIL";
    private const string Password = "1234";
    private const int WaitSecondsBeforeSecondCall = 15;
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Student API Console Client (JWT) ===");
        Console.WriteLine();

        using var http = CreateHttpClientForLocalDev(BaseUrl);

        var tokenPair = await LoginAndGetTokenAsync(http, Email, Password);

        if (tokenPair == null ||
                string.IsNullOrWhiteSpace(tokenPair.AccessToken) ||
                string.IsNullOrWhiteSpace(tokenPair.RefreshToken))
        {
            Console.WriteLine("Login failed.");
            return;
        }

        var tokenState = new TokenState(tokenPair.AccessToken, tokenPair.RefreshToken);

        Console.WriteLine("Login succeeded.");
        Console.WriteLine("======================================");
        Console.WriteLine("Initial Tokens:");
        Console.WriteLine("======================================");

        Console.WriteLine($"Access Token:\n{tokenState.AccessToken}");
        Console.WriteLine();
        Console.WriteLine($"Refresh Token:\n{tokenState.RefreshToken}");
        Console.WriteLine("======================================");
        Console.WriteLine();

        Console.WriteLine("First call: GET /api/Student/GetAllStudents (expected 200)...");
        await CallGetAllStudentsWithAutoRefreshAsync(http, Email, tokenState);

        // 3) Wait to let access token expire (same run, no restart)
        Console.WriteLine();
        Console.WriteLine($"Waiting {WaitSecondsBeforeSecondCall} seconds to let the access token expire...");
        await Task.Delay(TimeSpan.FromSeconds(WaitSecondsBeforeSecondCall));
        Console.WriteLine("Wait done.");
        Console.WriteLine();

        // 4) Second secured call (expected 401 then refresh then 200)
        Console.WriteLine("Second call: GET /api/Students/All (expected 401 -> refresh -> 200)...");
        await CallGetAllStudentsWithAutoRefreshAsync(http, Email, tokenState);

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    static async Task CallGetAllStudentsWithAutoRefreshAsync(HttpClient http, string email, TokenState tokenState)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/Student/All");

        var response = await SendWithAutoRefreshAsync(http, request, email, tokenState);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("401 Unauthorized. Access token expired and refresh failed (need re-login).");
            return;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            Console.WriteLine("403 Forbidden. You are authenticated, but not allowed to do this action.");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Request failed: {response.StatusCode}");
            return;
        }

        var students = await response.Content.ReadFromJsonAsync<List<Student>>();
        if (students == null)
        {
            Console.WriteLine("No data returned.");
            return;
        }

        Console.WriteLine($"{students.Count} students returned:");
        foreach (var s in students)
        {
            Console.WriteLine($"- {s.Name} (Age: {s.Age}, Grade: {s.Grade})");
        }

        Console.WriteLine();
        Console.WriteLine("======================================");
        Console.WriteLine("Current Token State After Request:");
        Console.WriteLine("======================================");

        Console.WriteLine($"Access Token:\n{tokenState.AccessToken}");
        Console.WriteLine();
        Console.WriteLine($"Refresh Token:\n{tokenState.RefreshToken}");

        Console.WriteLine("======================================");
        Console.WriteLine();
    }

    static async Task<HttpResponseMessage> SendWithAutoRefreshAsync(HttpClient http, HttpRequestMessage Request, string email,
                                                                    TokenState tokenState)
    {
        Request.Headers.Authorization = new AuthenticationHeaderValue("Beare", tokenState.AccessToken);

        var respons = await http.SendAsync(Request);

        if (respons.StatusCode != HttpStatusCode.Unauthorized)
            return respons;

        Console.WriteLine("Access token rejected (401). Refreshing tokens...");
        respons.Dispose();

        var newTokens = await RefreshTokensAsync(http, email, tokenState.RefreshToken);
        if (newTokens == null ||
            string.IsNullOrWhiteSpace(newTokens.AccessToken) ||
            string.IsNullOrWhiteSpace(newTokens.RefreshToken))
        {
            // Refresh failed => force re-login scenario
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        tokenState.RefreshToken = newTokens.RefreshToken;
        tokenState.AccessToken = newTokens.AccessToken;

        Console.WriteLine("Refresh succeeded. Retrying the original request...");
        Console.WriteLine("======================================");
        Console.WriteLine("NEW TOKENS RECEIVED AFTER REFRESH:");
        Console.WriteLine("======================================");

        Console.WriteLine($"New Access Token:\n{tokenState.AccessToken}");
        Console.WriteLine();
        Console.WriteLine($"New Refresh Token:\n{tokenState.RefreshToken}");

        Console.WriteLine("======================================");
        Console.WriteLine();

        using var retryRequest = CloneRequest(Request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer",newTokens.AccessToken);

        return await http.SendAsync(retryRequest);
    }

    static HttpRequestMessage CloneRequest(HttpRequestMessage Request)
    {
        var Clone = new HttpRequestMessage(Request.Method, Request.RequestUri);

        foreach (var item in Request.Headers)
            Clone.Headers.TryAddWithoutValidation(item.Key, item.Value);

        if (Request.Content != null)
            Clone.Content = Request.Content;

        return Clone;
    }

    static async Task<TokenResponse?> RefreshTokensAsync(HttpClient http, string email, string RefreshToken)
    {
        var request = new RefreshRequest { Email = email, RefreshToken = RefreshToken };

        var response = await http.PostAsJsonAsync("/api/Auth/refresh", request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Refresh failed: Unauthorized (refresh token invalid/expired/revoked).");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Refresh failed: {response.StatusCode}");
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenResponse>();
    }

    static HttpClient CreateHttpClientForLocalDev(string BaseUrl)
    {
        var Handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, certificate, chain, sslErrors) =>
            sslErrors == SslPolicyErrors.None || sslErrors == SslPolicyErrors.RemoteCertificateChainErrors
        };

        return new HttpClient(Handler) { BaseAddress = new Uri(BaseUrl) };
    }

    static async Task<TokenResponse?> LoginAndGetTokenAsync(HttpClient http, string email, string password)
    {
        var request = new LoginRequest
        {
            Email = email,
            Password = password
        };

        var response = await http.PostAsJsonAsync("/api/Auth/login", request);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("Invalid credentials.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Login failed: {response.StatusCode}");
            return null;
        }

        var tokenrespones = await response.Content.ReadFromJsonAsync<TokenResponse>();

        return tokenrespones;
    }

    static async Task GetAllStudents(HttpClient http, string token)
    {
        try
        {
            Console.WriteLine("\nFetching All Students.");
            Console.WriteLine("---------------------------");

            using var request = new HttpRequestMessage(HttpMethod.Get, "api/Student/All");

            if (!string.IsNullOrEmpty(token))
            {
               request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var respone = await http.SendAsync(request);

            if (respone.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("401 Unauthorized");
                return;
            }

            var students = await respone.Content.ReadFromJsonAsync<List<Student>>();

            if (students != null)
            {
                foreach (var item in students)
                {
                    Console.WriteLine($"Id: {item.Id}, Name: {item.Name}, Age: {item.Age}, Grade: {item.Grade}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task GetPassedStudents(HttpClient http, string token)
    {
        try 
        {
            Console.WriteLine("\nFetching Passed Students.");
            Console.WriteLine("---------------------------");

            var students = await httpClient.GetFromJsonAsync<List<Student>>("PASSED");

            if (students != null)
            {
                foreach (var item in students)
                {
                    Console.WriteLine($"Id: {item.Id}, Name: {item.Name}, Age: {item.Age}, Grade: {item.Grade}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task GetAverageGrade(HttpClient http, string token)
    {
        try
        {
            Console.WriteLine("\nFetching Average Grades Students.");
            Console.WriteLine("---------------------------");
            var Avearge = await httpClient.GetFromJsonAsync<float>("AverageGrade");
            Console.WriteLine($"\nThe Avege Of Grade: {Avearge}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task GetStudentByID(HttpClient http, string token, int ID)
    {
        try
        {
            Console.WriteLine("\nFetching Student WitH ID "+ID);
            Console.WriteLine("---------------------------");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/Student/{ID}");

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var Response = await http.SendAsync(request);

            
            if (Response.IsSuccessStatusCode)
            {
                var student = await Response.Content.ReadFromJsonAsync<Student>();

                if (student!=null)
                {
                    Console.WriteLine($"ID: {student.Id}, Name: {student.Name}, Age: {student.Age}, Grade: {student.Grade}");
                }
            }
            else if(Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("401 Unauthorized");
                return;
            }
            else if (Response.StatusCode == HttpStatusCode.Forbidden)
            {
                Console.WriteLine("403 Forbidden");
                return;
            }
            else if(Response.StatusCode==System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"Bad Request: Not accepted ID {ID}");
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Not Found: Student with ID {ID} not found.");
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task AddNewStudent(HttpClient http, string token, Student student)
    {
        try
        {
            Console.WriteLine("\nFetching Bew Student");
            Console.WriteLine("---------------------------");

            var Response = await httpClient.PostAsJsonAsync("", student);

            if (Response.IsSuccessStatusCode)
            {
                var Newstudent = await Response.Content.ReadFromJsonAsync<Student>();

                if (Newstudent != null)
                {
                    Console.WriteLine($"Added Student: ID: {Newstudent.Id}, Name: {Newstudent.Name}, Age: {Newstudent.Age}, Grade: {Newstudent.Grade}");
                }
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"Bad Request: Not accepted ID");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task DeleteByID(HttpClient http, string token, int ID)
    {
        try
        {
            Console.WriteLine("\nDelete Student WitH ID" + ID);
            Console.WriteLine("---------------------------");

            var Response = await httpClient.DeleteAsync($"{ID}");

            if (Response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Sucesseful Delete.");
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"Bad Request: Not accepted ID {ID}");
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Not Found: Student with ID {ID} not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    static async Task UbdateStudent(HttpClient http, string token, int ID, Student student)
    {
        try
        {
            Console.WriteLine("\nUbdate Student WitH ID" + ID);
            Console.WriteLine("---------------------------");

            var Response = await httpClient.PutAsJsonAsync($"{ID}", student);

            if (Response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Sucesseful Ubdate.");
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"Bad Request: Not accepted ID {ID}");
            }
            else if (Response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Not Found: Student with ID {ID} not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public class Student
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public int Grade { get; set; }
    }

    class LoginRequest
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    class TokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }

    class TokenState
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }

        public TokenState(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }
    }

    class RefreshRequest
    {
        public string Email { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }
}