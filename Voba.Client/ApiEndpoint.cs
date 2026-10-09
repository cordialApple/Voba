namespace Voba.Client;

public static class ApiEndpoint
{
    public static Uri Parse(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https") ||
            (endpoint.Scheme == "http" && !endpoint.IsLoopback) ||
            !string.IsNullOrEmpty(endpoint.UserInfo) ||
            endpoint.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(endpoint.Query) ||
            !string.IsNullOrEmpty(endpoint.Fragment))
            throw new ArgumentException("VOBA_API_BASE_URL must be local HTTP or HTTPS without credentials or a path.",
                nameof(value));

        return endpoint;
    }
}
