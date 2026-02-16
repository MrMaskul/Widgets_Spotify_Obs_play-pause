using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// De modificat OBSControl pentru a se conecta la OBS WebSocket si a trimite comenzi
public class OBSControl
{
    private ClientWebSocket webSocket;
    private readonly string obsAddress;
    private readonly string obsPassword;
    private int requestId = 0;

    public OBSControl(string obsAddress = "ws://localhost:portul_de_la_obs", string obsPassword = "pus_parola_aici_obs")
    {
        this.obsAddress = obsAddress;
        this.obsPassword = obsPassword;
    }

/// se conecteaza la OBS WebSocket

    public async Task ConnectAsync()
    {
        try
        {
            webSocket = new ClientWebSocket();
            await webSocket.ConnectAsync(new Uri(obsAddress), CancellationToken.None);
            

            var buffer = new byte[4096];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var hello = Encoding.UTF8.GetString(buffer, 0, result.Count);
            

            var identify = new
            {
                op = 1,
                d = new
                {
                    rpcVersion = 1,
                    authentication = string.IsNullOrEmpty(obsPassword) ? null : GenerateAuth(hello, obsPassword),
                    eventSubscriptions = 33 
                }
            };
            
            var json = JsonSerializer.Serialize(identify);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            

            result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to connect to OBS at {obsAddress}: {ex.Message}");
        }
    }

    public async Task SetSceneAsync(string sceneName)
    {
        if (webSocket?.State != WebSocketState.Open)
            throw new Exception("OBS WebSocket not connected");

        var req = new
        {
            op = 6,
            d = new
            {
                requestType = "SetCurrentProgramScene",
                requestId = (++requestId).ToString(),
                requestData = new { sceneName }
            }
        };

        var json = JsonSerializer.Serialize(req);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private string GenerateAuth(string hello, string password)
    {
        // autentificare OBS WebSocket
        using var doc = JsonDocument.Parse(hello);
        if (!doc.RootElement.TryGetProperty("d", out var d)) return null;
        if (!d.TryGetProperty("authentication", out var auth)) return null;
        if (!auth.TryGetProperty("challenge", out var challenge) || !auth.TryGetProperty("salt", out var salt))
            return null;

        var challengeStr = challenge.GetString();
        var saltStr = salt.GetString();

        // SHA256(password + salt) -> base64 ,functionare SHA256 pe UTF8
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var passAndSalt = password + saltStr;
            var hash1 = sha.ComputeHash(Encoding.UTF8.GetBytes(passAndSalt));
            var hash1Base64 = Convert.ToBase64String(hash1);

            // SHA256(base64hash + challenge) -> base64
            var hash2 = sha.ComputeHash(Encoding.UTF8.GetBytes(hash1Base64 + challengeStr));
            return Convert.ToBase64String(hash2);
        }
    }

/// Deconecteaza de la OBS WebSocket
    public void Disconnect()
{
    try
    {
        if (webSocket != null)
        {
            if (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    var closeTask = webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
                    if (!closeTask.Wait(1000)) // 1 second timeout
                    {
                        // timeout - just dispose
                    }
                }
                catch { }
            }

            webSocket?.Dispose();
            webSocket = null;
        }
    }
    catch { }
}
}