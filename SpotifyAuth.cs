using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class SpotifyAuth
{
    private readonly string clientId = "client_id";
    private readonly string clientSecret = "client_secret";
    private readonly string refreshToken = "AQCRd560qxCWJbQTTrCzNyB4Oh-7an1C_QuuxLwLuvHUbE6FdVIYtkZszSdCjz3XmMBeJPx3RZ5668od_CGKXhK3PvrDvh_TW2Fl5e8pAyXFYLkWnvEvPN0fJ4rwQNZ80S0";

    private string accessToken;
    private DateTimeOffset expiresAt = DateTimeOffset.MinValue;
    private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Returns a valid access token. If cached token is still valid (with small safety margin) it is returned.
    /// Otherwise a refresh request is made. Thread-safe.
    /// </summary>
    public async Task<string> GetAccessTokenAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            // keep a small margin (30s) to avoid using near-expired token
            if (!string.IsNullOrEmpty(accessToken) && DateTimeOffset.UtcNow < expiresAt.AddSeconds(-30))
                return accessToken;

            using var client = new HttpClient();
            var authHeader = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken)
            });

            var resp = await client.PostAsync("https://accounts.spotify.com/api/token", form);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Token request failed: {resp.StatusCode} - {body}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                accessToken = at.GetString();
                // parse expires_in if available (seconds)
                if (doc.RootElement.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number)
                {
                    var secs = ei.GetInt32();
                    expiresAt = DateTimeOffset.UtcNow.AddSeconds(secs);
                }
                else
                {
                    // default to 1 hour if expires_in missing
                    expiresAt = DateTimeOffset.UtcNow.AddHours(1);
                }
                return accessToken;
            }

            throw new Exception("access_token missing in token response: " + body);
        }
        catch (JsonException je)
        {
            throw new Exception("Invalid JSON from token endpoint: " + je.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Convenience: prefetch token on launch. Safe to call multiple times.
    /// </summary>
    public Task InitializeAsync() => GetAccessTokenAsync();

}
