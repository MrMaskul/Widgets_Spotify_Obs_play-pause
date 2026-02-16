using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

public enum WedgeIcon { None, Back, Forward }

public class Wedge : Control
{
    public Action OnTrigger;
    readonly WedgeIcon icon;

    public Wedge(WedgeIcon icon = WedgeIcon.None)
    {
        this.icon = icon;
        this.BackColor = Color.White;
        this.Cursor = Cursors.Hand;
        this.DoubleBuffered = true;
        this.MouseClick += (s, e) => OnTrigger?.Invoke();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(this.BackColor))
            g.FillRectangle(bg, this.ClientRectangle);

        int w = ClientSize.Width, h = ClientSize.Height;
        int size = Math.Min(w, h) * 2 / 3;
        var rect = new Rectangle((w - size) / 2, (h - size) / 2, size, size);

        using (var borderPen = new Pen(Color.FromArgb(180, 0, 0, 0), Math.Max(2, size / 30)))
            g.DrawRectangle(borderPen, rect);

        if (icon == WedgeIcon.Forward)
            DrawForwardArrow(g, rect, Brushes.Black);
        else if (icon == WedgeIcon.Back)
            DrawBackArrow(g, rect, Brushes.Black);
    }

    void DrawForwardArrow(Graphics g, Rectangle r, Brush brush)
    {
        var pts = new Point[]
        {
            new Point(r.Left, r.Top),
            new Point(r.Right, r.Top + r.Height / 2),
            new Point(r.Left, r.Bottom)
        };
        g.FillPolygon(brush, pts);
        int tailW = Math.Max(2, r.Width / 6);
        var tailRect = new Rectangle(r.Left - tailW / 2, r.Top + r.Height / 4, tailW, r.Height / 2);
        g.FillRectangle(brush, tailRect);
    }

    void DrawBackArrow(Graphics g, Rectangle r, Brush brush)
    {
        var pts = new Point[]
        {
            new Point(r.Right, r.Top),
            new Point(r.Left, r.Top + r.Height / 2),
            new Point(r.Right, r.Bottom)
        };
        g.FillPolygon(brush, pts);
        int tailW = Math.Max(2, r.Width / 6);
        var tailRect = new Rectangle(r.Right - tailW / 2, r.Top + r.Height / 4, tailW, r.Height / 2);
        g.FillRectangle(brush, tailRect);
    }
}

static class MediaKey
{
    const byte VK_MEDIA_NEXT_TRACK = 0xB0;
    const byte VK_MEDIA_PREV_TRACK = 0xB1;
    const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
    const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static void SendNext()
    {
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(VK_MEDIA_NEXT_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static void SendPrevious()
    {
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(VK_MEDIA_PREV_TRACK, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static void Pause()
    {
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
        keybd_event(VK_MEDIA_PLAY_PAUSE, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}

public class VolumeSlider : Control
{
    private int volume = 50;
    private readonly SpotifyAuth spotifyAuth;

    // domaneajuta la debounce
    System.Threading.CancellationTokenSource debounceCts;
    readonly System.Threading.SemaphoreSlim sendLock = new System.Threading.SemaphoreSlim(1, 1);
    bool isDragging = false;

    public VolumeSlider(SpotifyAuth spotifyAuth)
    {
        this.spotifyAuth = spotifyAuth ?? throw new ArgumentNullException(nameof(spotifyAuth));
        this.BackColor = Color.White;
        this.Cursor = Cursors.Default;
        this.DoubleBuffered = true;

        this.MouseDown += (s, e) => { isDragging = true; UpdateVisualVolumeFromMouse(e.X); };
        this.MouseUp += (s, e) => { isDragging = false; UpdateVolumeFromMouse(e.X, immediate: true); };
        this.MouseClick += (s, e) => UpdateVolumeFromMouse(e.X, immediate: true);
        this.MouseMove += (s, e) => { if (e.Button == MouseButtons.Left) { UpdateVisualVolumeFromMouse(e.X); DebounceSendVolume(); } };
    }

    // vizual updateare doar
    private void UpdateVisualVolumeFromMouse(int mouseX)
    {
        int sliderWidth = Math.Max(1, this.ClientSize.Width - 20);
        volume = Math.Max(0, Math.Min(100, (mouseX - 10) * 100 / sliderWidth));
        this.Invalidate();
    }

    // request trimis cat de rapid posibil
    private void UpdateVolumeFromMouse(int mouseX, bool immediate = false)
    {
        UpdateVisualVolumeFromMouse(mouseX);
        if (immediate) CancelAndSendNow();
        else DebounceSendVolume();
    }

    private void DebounceSendVolume()
    {
        debounceCts?.Cancel();
        debounceCts = new System.Threading.CancellationTokenSource();
        var token = debounceCts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(300, token); // sa nu genereze prea multe requesturi
                if (!token.IsCancellationRequested)
                    await SendVolumeAsync(volume);
            }
            catch (TaskCanceledException) { }
            catch { /* ignore */ }
        }, token);
    }

    private void CancelAndSendNow()
    {
        debounceCts?.Cancel();
        _ = SendVolumeAsync(volume);
    }

    // spotify volume api call
    private async Task SendVolumeAsync(int vol)
    {
        await sendLock.WaitAsync();
        try
        {
            var accessToken = await spotifyAuth.GetAccessTokenAsync();
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var resp = await client.PutAsync($"https://api.spotify.com/v1/me/player/volume?volume_percent={vol}", null);
            if (resp.IsSuccessStatusCode) return;

            if ((int)resp.StatusCode == 429)
            {
                // handle sa nu moara codul
                int retrySeconds = 2;
                if (resp.Headers.TryGetValues("Retry-After", out var vals))
                {
                    if (int.TryParse(System.Linq.Enumerable.FirstOrDefault(vals), out var parsed))
                        retrySeconds = Math.Max(1, parsed);
                }

                await Task.Delay(retrySeconds * 1000);
                var token2 = await spotifyAuth.GetAccessTokenAsync();
                using var client2 = new HttpClient();
                client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);
                var resp2 = await client2.PutAsync($"https://api.spotify.com/v1/me/player/volume?volume_percent={vol}", null);
                if (!resp2.IsSuccessStatusCode)
                {
                    var body = await resp2.Content.ReadAsStringAsync();
                    MessageBox.Show($"Spotify volume request failed after retry: {resp2.StatusCode} - {body}", "WedgeApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                var body = await resp.Content.ReadAsStringAsync();
                MessageBox.Show($"Spotify volume request failed: {resp.StatusCode} - {body}", "WedgeApp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Volume error: " + ex.Message, "WedgeApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            sendLock.Release();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(Color.White))
            g.FillRectangle(bg, this.ClientRectangle);

        int sliderWidth = Math.Max(1, this.ClientSize.Width - 20);
        var trackRect = new Rectangle(10, this.ClientSize.Height / 2 - 3, sliderWidth, 6);
        using (var trackBrush = new SolidBrush(Color.LightGray))
            g.FillRectangle(trackBrush, trackRect);

        int filledWidth = (volume * sliderWidth) / 100;
        var filledRect = new Rectangle(10, this.ClientSize.Height / 2 - 3, filledWidth, 6);
        using (var filledBrush = new SolidBrush(Color.FromArgb(60, 120, 215)))
            g.FillRectangle(filledBrush, filledRect);

        int thumbX = 10 + filledWidth - 5;
        var thumbRect = new Rectangle(thumbX, this.ClientSize.Height / 2 - 8, 10, 16);
        using (var thumbBrush = new SolidBrush(Color.FromArgb(30, 90, 180)))
            g.FillRectangle(thumbBrush, thumbRect);

        using (var font = new Font("Arial", 10))
        using (var textBrush = new SolidBrush(Color.Black))
            g.DrawString($"{volume}%", font, textBrush, this.ClientSize.Width - 40, this.ClientSize.Height / 2 - 7);
    }
}

public class WedgeContainer : Form
{
    public WedgeContainer(IWedgeConfig config)
    {
        this.Text = "WedgeApp";
        this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        this.ShowInTaskbar = true;
        this.BackColor = Color.LightGray;
        this.TopMost = true;
        this.Cursor = Cursors.SizeAll;

        this.ClientSize = config.GetContainerSize();
        this.StartPosition = FormStartPosition.Manual;
        this.Location = new Point(100, 100);

        var wedgeDefs = config.GetWedges();
    }
}

public class WedgeContext : ApplicationContext
{
    WedgeContainer containerForm;
    NotifyIcon trayIcon;

    public WedgeContext(IWedgeConfig config)
    {
        containerForm = new WedgeContainer(config);
        containerForm.Show();

        trayIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            Text = "WedgeApp (right-click to exit)",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ExitThread();
        menu.Items.Add(exitItem);

        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (s, e) =>
        {
            if (!containerForm.Visible) containerForm.Show();
            containerForm.BringToFront();
        };

        containerForm.FormClosed += (s, e) => ExitThread();
    }

    protected override void ExitThreadCore()
    {
        if (containerForm != null && !containerForm.IsDisposed) containerForm.Close();
        trayIcon.Visible = false;
        trayIcon.Dispose();
        base.ExitThreadCore();
    }
}

public static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        DefaultWedgeConfig config = null;
        

        try
        {

            config = new DefaultWedgeConfig();
            config.InitializeOBS();

            Application.Run(new WedgeContext(config));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Startup error: " + ex.Message, "WedgeApp", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Cleanup with timeouts
            try { config?.Disconnect(); } catch { }
            
            // Force exit if hanging (5 second timeout)
            var exitTask = Task.Run(() => System.Threading.Thread.Sleep(5000));
            if (!exitTask.Wait(5000))
            {
                Environment.Exit(0); // hard exit if cleanup hangs
            }
        }
    }
}