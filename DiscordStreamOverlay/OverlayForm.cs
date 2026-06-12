using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DiscordStreamOverlay
{
    public class OverlayForm : Form
    {
        private WebView2 webView;
        private AppConfig config;
        private bool isStandalone;
        public bool IsFullscreenMode { get; private set; }

        public event EventHandler FullscreenStateChanged;
        public event EventHandler<AppConfig> SettingsSaved;
        public event EventHandler<int> PresetSaved;
        public event EventHandler<int> PresetLoaded;
        public event EventHandler ExitRequested;

        public OverlayForm(AppConfig config, bool standalone = false, string windowTitle = "Stream", Icon appIcon = null)
        {
            this.config = config;
            this.isStandalone = standalone;

            if (isStandalone)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.ShowInTaskbar = true;
                this.Text = windowTitle;
                if (appIcon != null) this.Icon = appIcon;
                this.TopMost = false;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.Size = new Size(1280, 720); // Domyślny rozmiar
            }
            else
            {
                this.FormBorderStyle = FormBorderStyle.None;
                this.ShowInTaskbar = false;
                this.TopMost = false;
                this.StartPosition = FormStartPosition.Manual;
            }

            this.BackColor = Color.Black;

            webView = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };
            this.Controls.Add(webView);

            InitializeAsync();
        }

        async void InitializeAsync()
        {
            string userDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Discord_Stream_Overlay", "WebView2Data");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);
            
            // Disable default Edge hotkeys (like F7 for Caret Browsing, F5, etc.)
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            
            webView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
            {
                bool isFs = webView.CoreWebView2.ContainsFullScreenElement;
                if (isFs != IsFullscreenMode)
                {
                    IsFullscreenMode = isFs;
                    FullscreenStateChanged?.Invoke(this, EventArgs.Empty);
                }
            };

            string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body, html {{ margin: 0; padding: 0; width: 100%; height: 100%; overflow: hidden; background-color: #000; }}
        .container {{ position: relative; width: 100%; height: 100%; }}
        iframe {{ width: 100%; height: 100%; border: none; }}
        
        /* SHIELD: blocks clicking in the center of the image, leaving 52px at the bottom for play/pause/fullscreen buttons */
        .click-shield {{
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: calc(100% - 52px);
            z-index: 999;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <iframe src=""{config.STREAM_URL}"" allow=""autoplay; fullscreen; camera; microphone"" allowfullscreen=""true"" webkitallowfullscreen=""true"" mozallowfullscreen=""true""></iframe>
        
        <!-- Click absorbing layer -->
        <div class=""click-shield"" oncontextmenu=""return false;"" ondblclick=""toggleNativeFs()""></div>
    </div>

    <script>
        function toggleNativeFs() {{
            if (!document.fullscreenElement) {{
                document.documentElement.requestFullscreen().catch(err => console.log(err));
            }} else {{
                document.exitFullscreen();
            }}
        }}
    </script>
" + GetMenuHtml() + @"
</body>
</html>";

            webView.CoreWebView2InitializationCompleted += (s, e) =>
            {
                webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"LOAD_CONFIG\",\"config\":" + System.Text.Json.JsonSerializer.Serialize(config) + "}");
            };

            webView.NavigateToString(htmlContent);
        }

        private string GetMenuHtml()
        {
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("DiscordStreamOverlay.menu.html"))
                {
                    if (stream != null)
                    {
                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(json) || json == "null")
                {
                    json = e.TryGetWebMessageAsString();
                }
                
                using (var doc = System.Text.Json.JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("type", out var typeProp))
                    {
                        string type = typeProp.GetString();
                        
                        if (type == "SAVE_AND_RESTART")
                        {
                            var cfgJson = root.GetProperty("config").GetRawText();
                            var newConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(cfgJson);
                            if (newConfig != null) SettingsSaved?.Invoke(this, newConfig);
                        }
                        else if (type == "SAVE_PRESET")
                        {
                            int presetId = root.GetProperty("presetId").GetInt32();
                            var cfgJson = root.GetProperty("config").GetRawText();
                            var newConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(cfgJson);
                            if (newConfig != null)
                            {
                                config = newConfig; // Save to current config reference
                                PresetSaved?.Invoke(this, presetId);
                            }
                        }
                        else if (type == "LOAD_PRESET")
                        {
                            int presetId = root.GetProperty("presetId").GetInt32();
                            PresetLoaded?.Invoke(this, presetId);
                        }
                        else if (type == "EXIT_APP")
                        {
                            ExitRequested?.Invoke(this, EventArgs.Empty);
                        }
                        else if (type == "REQUEST_CONFIG")
                        {
                            webView.CoreWebView2.PostWebMessageAsJson("{\"type\":\"LOAD_CONFIG\",\"config\":" + System.Text.Json.JsonSerializer.Serialize(config) + "}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing web message: " + ex.Message);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (!isStandalone)
                {
                    // Exclude from taskbar / Alt+Tab when attached to Discord
                    cp.ExStyle |= WindowManager.WS_EX_TOOLWINDOW;
                    cp.ExStyle &= ~WindowManager.WS_EX_APPWINDOW;
                }
                return cp;
            }
        }
    }
}
