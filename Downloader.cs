using AnimeGirlsDownloader.Enums;
using AnimeGirlsDownloader.Interfaces;
using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AnimeGirlsDownloader
{
    public class Downloader
    {
        private TimeSpan _timeout = TimeSpan.FromSeconds(40);

        private Action<byte[]?, string, List<Tag>?>? _bytesToBitmapImageHandler = null;
        public Downloader(Action<byte[]?, string, List<Tag>?> bytesToBitmapImageHandler)
        {
            _bytesToBitmapImageHandler = bytesToBitmapImageHandler;
        }
        /// <summary>
        /// Just set the delegate to method that converts byte array to BitmapImage.
        /// </summary>
        /// <param name="bytesToBitmapImageHandler">delegate</param>
        public void Initialize(Action<byte[]?, string, List<Tag>?> bytesToBitmapImageHandler)
        {
            _bytesToBitmapImageHandler = bytesToBitmapImageHandler;
        }
        public async Task GetImageById(long imageId)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
            string url = $"{AppConsts.AnimeGirlApiEndpoint}image/id";
            GetImageRequest request = new GetImageRequest
            {
                Id = imageId,
            };
            try
            {
                var payload = JsonSerializer.Serialize<GetImageRequest>(request, GetImageRequestContext.Default.GetImageRequest);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                AppLogger.LogInfo($"{AppResourceLoader.GetString("Info_Downloader_GetRandomImage_1")}{url}");
                var response = await client.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var getImageResponse = JsonSerializer.Deserialize<GetImageResponse>(json, GetImageResponseContext.Default.GetImageResponse);
                string id = getImageResponse!.Id;
                byte[] bytes = getImageResponse.Data;
                List<Tag>? imageTags = getImageResponse.Tags;
                client.Dispose();
                ConvertToBitmapImage(bytes, id, imageTags);
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
        private void ConvertToBitmapImage(byte[]? bytes, string id, List<Tag>? tags)
        {
            _bytesToBitmapImageHandler?.Invoke(bytes, id, tags);
        }

        public async Task GetRandomImage(List<Tag>? tags, ImageType type, ImageIsAllowAiType isAllowAiGenerated)
        {
            using HttpClient client = new HttpClient();
            client.Timeout = _timeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConsts.AppUserAgent);
            string url = $"{AppConsts.AnimeGirlApiEndpoint}image/random";
            GetImageRequest request = new GetImageRequest
            {
                Tags = tags,
                Type = type,
                IsAllowAiGenerated = isAllowAiGenerated
            };
            try
            {
                var payload = JsonSerializer.Serialize<GetImageRequest>(request, GetImageRequestContext.Default.GetImageRequest);
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                AppLogger.LogInfo($"{AppResourceLoader.GetString("Info_Downloader_GetRandomImage_1")}{url}");
                var response = await client.PostAsync(url,content);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var getImageResponse = JsonSerializer.Deserialize<GetImageResponse>(json, GetImageResponseContext.Default.GetImageResponse);
                string id = getImageResponse!.Id;
                byte[] bytes = getImageResponse.Data;
                List<Tag>? imageTags = getImageResponse.Tags;
                client.Dispose();
                ConvertToBitmapImage(bytes, id, imageTags);
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
    }
}
