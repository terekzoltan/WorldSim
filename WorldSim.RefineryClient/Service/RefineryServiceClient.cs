using System.Net.Http.Json;
using WorldSimRefineryClient.Apply;
using WorldSimRefineryClient.Contracts;
using WorldSimRefineryClient.Serialization;

namespace WorldSimRefineryClient.Service;

public sealed class RefineryServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly PatchResponseParser _parser;

    public RefineryServiceClient(HttpClient httpClient, PatchResponseParser? parser = null)
    {
        _httpClient = httpClient;
        _parser = parser ?? new PatchResponseParser();
    }

    public async Task<PatchResponse> GetPatchAsync(
        PatchRequest request,
        PatchApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resp = await _httpClient.PostAsJsonAsync("/v1/patch", request, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        return _parser.Parse(body, options);
    }
}
