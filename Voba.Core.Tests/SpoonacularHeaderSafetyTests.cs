using System.Reflection;
using RestSharp;
using spoonacular.Client;
using Xunit;
using SpoonacularHttpMethod = spoonacular.Client.HttpMethod;

namespace Voba.Core.Tests;

public sealed class SpoonacularHeaderSafetyTests
{
    [Fact]
    public void ApiClientKeepsOrdinaryHeader()
    {
        var request = CreateRequest("ordinary");

        Assert.Contains(request.Parameters,
            parameter => parameter.Name == "X-Probe" && Equals(parameter.Value, "ordinary"));
    }

    [Fact]
    public void ApiClientRejectsCrlfHeaderValue()
    {
        var exception = Assert.Throws<TargetInvocationException>(() =>
            CreateRequest("ordinary\r\nX-Injected: yes"));

        Assert.IsType<ArgumentException>(exception.InnerException);
    }

    private static RestRequest CreateRequest(string headerValue)
    {
        var options = new RequestOptions();
        options.HeaderParameters.Add("X-Probe", new List<string> { headerValue });
        var apiClient = new ApiClient("http://127.0.0.1");
        var method = typeof(ApiClient).GetMethod("NewRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (RestRequest)method.Invoke(apiClient,
            [SpoonacularHttpMethod.Get, "/probe", options, new Configuration()])!;
    }
}
