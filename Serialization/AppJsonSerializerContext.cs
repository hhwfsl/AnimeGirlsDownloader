using AnimeGirlsDownloader.Models;
using AnimeGirlsDownloader.Requests;
using AnimeGirlsDownloader.Responses;
using System.Text.Json.Serialization;

namespace AnimeGirlsDownloader.Serialization;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(Settings))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(Tag))]
[JsonSerializable(typeof(GetImageRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(TagRequest))]
[JsonSerializable(typeof(UploadImageRequest))]
[JsonSerializable(typeof(GetImageResponse))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(RegisterResponse))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(TagResponse))]
[JsonSerializable(typeof(UserProfileResponse))]
[JsonSerializable(typeof(GitHubReleaseResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
