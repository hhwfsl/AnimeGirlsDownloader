using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader
{
    public class AccountAuth
    {
        private TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private Action<string>? ErrorHandler;

        public void Initialize(Action<string> errorHandler)
        {
            ErrorHandler = errorHandler;
        }
        public async Task<bool> Login(string username, string password)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
            string url = $"{AppConsts.AnimeGirlsApiEndpoint}auth/login";
            var payload = new LoginRequest { UserName = username, Password = password };
            var content = new StringContent(JsonSerializer.Serialize(payload,LoginRequestContext.Default.LoginRequest), Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(result, LoginResponseContext.Default.LoginResponse);
                File.WriteAllText(AppConsts.AuthFilePath, loginResponse?.Token);
                client.Dispose();
                return true;
            }
            catch (Exception e)
            {
                AppLogger.LogErrorWithInfoBar(e.Message);
                ErrorHandler?.Invoke(e.Message);
                return false;
            }
            finally
            {
                client.Dispose();
            }
        }
        public async Task<bool> LoginWithToken()
        {
            using HttpClient client = new HttpClient();
            string token = await File.ReadAllTextAsync(AppConsts.AuthFilePath);
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
            string url = $"{AppConsts.AnimeGirlsApiEndpoint}auth/login-with-token";
            try
            {
                var response = await client.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                var loginResponse = JsonSerializer.Deserialize<LoginResponse>(result, LoginResponseContext.Default.LoginResponse);
                File.WriteAllText(AppConsts.AuthFilePath, loginResponse?.Token);
                client.Dispose();
                return true;
            }
            catch (Exception e)
            {
                AppLogger.LogErrorWithInfoBar(e.Message);
                ErrorHandler?.Invoke(e.Message);
                return false;
            }
            finally
            {
                client.Dispose();
            }
        }
        public async Task<bool> Register(string username, string password)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
            string url = $"{AppConsts.AnimeGirlsApiEndpoint}auth/register";
            var payload = new RegisterRequest { UserName = username, Password = password };
            var content = new StringContent(JsonSerializer.Serialize(payload, RegisterRequestContext.Default.RegisterRequest), Encoding.UTF8, "application/json");
            try
            {
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadAsStringAsync();
                
                var loginResponse = JsonSerializer.Deserialize<RegisterResponse>(result, RegisterResponseContext.Default.RegisterResponse);
                File.WriteAllText(AppConsts.AuthFilePath, loginResponse?.Token);
                client.Dispose();
                return true;
            }
            catch (Exception e)
            {
                AppLogger.LogErrorWithInfoBar(e.Message);
                ErrorHandler?.Invoke(e.Message);
                return false;
            }
            finally
            {
                client.Dispose();
            }
        }
    }
}
