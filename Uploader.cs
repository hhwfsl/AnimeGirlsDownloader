using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader
{
    public class Uploader
    {
        private TimeSpan _timeout = TimeSpan.FromSeconds(60);

        private Action<string>? _resultHandler = null;
        public Uploader()
        {
            
        }
        public void Initialize(Action<string> resultHandler)
        {
            _resultHandler = resultHandler;
        }
        public async Task UploadImage(UploadImageRequest request)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            try
            {
                string token = await File.ReadAllTextAsync(AppConsts.AuthFilePath);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
                string url = $"{AppConsts.AnimeGirlApiEndpoint}image/upload";
                var payload = JsonSerializer.Serialize<UploadImageRequest>(request,UploadImageRequestContext.Default.UploadImageRequest);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MessageResponse>(json, MessageResponseContext.Default.MessageResponse);
                _resultHandler?.Invoke(result!.Message);
                client.Dispose();
            }
            catch (Exception e)
            {
                AppLogger.LogError(e.Message);
            }
            finally
            {
                client.Dispose();
            }
        }
        public async Task<List<Tag>?> GetTagsByPartialTag(TagRequest request)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            try
            {
                string token = await File.ReadAllTextAsync(AppConsts.AuthFilePath);
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
                string url = $"{AppConsts.AnimeGirlApiEndpoint}image/tag/fulltags";
                var payload = JsonSerializer.Serialize<TagRequest>(request, TagRequestContext.Default.TagRequest);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(url,content);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<TagResponse>(json, TagResponseContext.Default.TagResponse);
                var tags = result?.Tags;
                client.Dispose();
                return tags;
            }
            catch (Exception e)
            {
                AppLogger.LogError(e.Message);
                return null;
            }
            finally
            {
                client.Dispose();
            }
            
        }
    }
}
