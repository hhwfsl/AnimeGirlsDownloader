using System;
using System.Net;

namespace AnimeGirlsDownloader.Services;

public sealed class ApiClientException : Exception
{
    public ApiClientException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
