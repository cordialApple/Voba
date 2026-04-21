namespace Voba.Services
{
    public abstract class BaseApiService
    {
        protected readonly HttpClient _httpClient;

        protected BaseApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        protected abstract HttpRequestMessage BuildRequest(object input);
        protected abstract T ParseResponse<T>(string responseBody);

        protected async Task<ServiceResult<T>> ExecuteAsync<T>(object input)
        {
            try
            {
                var request  = BuildRequest(input);
                var response = await _httpClient.SendAsync(request);
                var body     = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return ServiceResult<T>.Fail(ErrorCodes.ExternalApiFailure, body);

                return ServiceResult<T>.Ok(ParseResponse<T>(body));
            }
            catch (HttpRequestException ex)
            {
                return ServiceResult<T>.Fail(ErrorCodes.ExternalApiFailure, ex.Message);
            }
        }
    }
}
