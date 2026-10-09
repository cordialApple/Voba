using System.Net;

namespace Voba.Client;

public sealed class VobaApiException(HttpStatusCode statusCode, string code, string message)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
