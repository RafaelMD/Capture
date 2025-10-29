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
    public string? LastError { get; private set; }
    public string? LastRequestBody { get; private set; }

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
            // Serialize metadata with proper JSON options
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            
            var jsonBody = JsonSerializer.Serialize(metadata, jsonOptions);
            LastRequestBody = jsonBody;
            
            // Debug output
            System.Diagnostics.Debug.WriteLine($"Jaboo Sync Request Body:\n{jsonBody}");
            
            // Prepare the request
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("x-api-key", apiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            // Send the request
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                LastError = $"HTTP {(int)response.StatusCode}: {errorContent}";
                System.Diagnostics.Debug.WriteLine($"Jaboo Sync Failed: {LastError}");
            }
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            System.Diagnostics.Debug.WriteLine($"Jaboo Sync Exception: {ex}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
