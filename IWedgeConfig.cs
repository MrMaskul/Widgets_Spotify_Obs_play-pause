using System;
using System.Collections.Generic;
using System.Drawing;

public interface IWedgeConfig
{
    List<WedgeDefinition> GetWedges();
    Size GetContainerSize();
}

public class WedgeDefinition
{
    public Rectangle Bounds { get; set; }
    public WedgeIcon Icon { get; set; }
    public Action OnClick { get; set; }
    public string Label { get; set; }
}

/// de modificat OBSControl!!! pentru a se conecta la OBS WebSocket si a trimite comenzi
public class DefaultWedgeConfig : IWedgeConfig
{
    private OBSControl obsControl;
    private bool obsConnected = false;

    public DefaultWedgeConfig()
    {
        obsControl = new OBSControl("ws://localhost:portul_de_conectare_obs", "parola_ta_aici");
    }

    public async void InitializeOBS()
    {
        try
        {
            await obsControl.ConnectAsync();
            obsConnected = true;
            System.Windows.Forms.MessageBox.Show("OBS Connected successfully!");
        }
        catch (Exception ex)
        {
            obsConnected = false;
            System.Windows.Forms.MessageBox.Show("OBS connection failed: " + ex.Message + "\n\nMake sure OBS is running and WebSocket server is enabled (Tools → WebSocket Server Settings)");
        }
    }

    public void Disconnect()
    {
        try { obsControl?.Disconnect(); } catch { }
    }
    public List<WedgeDefinition> GetWedges()
    {
        return new List<WedgeDefinition>
        {
            new WedgeDefinition
            {
                Bounds = new Rectangle(0, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 1");
                },
                Label = "OBS Scene 1"
            },
            new WedgeDefinition
            {
                Bounds = new Rectangle(250, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 2");
                },
                Label = "OBS Scene 2"
            },
            new WedgeDefinition
            {
                Bounds = new Rectangle(500, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 3");
                },
                Label = "OBS Scene 2"
            },
            new WedgeDefinition
            {
                Bounds = new Rectangle(750, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 4");
                },
                Label = "OBS Scene 2"
            },
            new WedgeDefinition
            {
                Bounds = new Rectangle(1000, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 5");
                },
                Label = "OBS Scene 2"
            },
            new WedgeDefinition
            {
                Bounds = new Rectangle(1250, 750, 250, 250),
                Icon = WedgeIcon.None,
                OnClick = () => 
                {
                    if (!obsConnected) { System.Windows.Forms.MessageBox.Show("OBS not connected"); return; }
                    _ = obsControl.SetSceneAsync("Scene 6");
                },
                Label = "OBS Scene 2"
            }
        };
    }

    public Size GetContainerSize()
    {
        return new Size(1500, 1000);
    }
}