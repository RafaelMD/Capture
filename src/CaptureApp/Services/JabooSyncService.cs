using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CaptureApp.Models;

namespace CaptureApp.Services;

public class JabooSyncService : IDisposable
{
    private readonly HttpClient _httpClient;

    public JabooSyncService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<bool> SyncAsync(string apiUrl, string apiKey, RecordingMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new ArgumentException("API URL is required", nameof(apiUrl));
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API Key is required", nameof(apiKey));
        }

        try
        {
            // Prepare the request
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("x-api-key", apiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }),
                Encoding.UTF8,
                "application/json"
            );

            // Send the request
            var response = await _httpClient.SendAsync(request);
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
