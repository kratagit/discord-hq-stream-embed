using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WhipCast
{
    public class OverlayForm : Form
    {
        private WebView2 webView;
        private AppConfig currentConfig;
        public bool IsFullscreenMode { get; private set; }
        private bool isExiting = false;
        public bool IsRestarting = false;

        public event EventHandler FullscreenStateChanged;
        public event EventHandler RequestRestart;
        public event EventHandler RequestExit;

        public OverlayForm(AppConfig config, Icon appIcon = null)
        {
            this.currentConfig = config;

            if (!config.ATTACH_TO_WINDOW)
            {
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.ShowInTaskbar = true;
                this.Text = "WhipCast - " + config.STREAM_URL;
                if (appIcon != null) this.Icon = appIcon;
                this.TopMost = false;
                this.StartPosition = FormStartPosition.CenterScreen;
                this.ClientSize = new Size(1280, 720);
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
            string userDataFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "whip-cast", "WebView2Data");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);
            
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                document.addEventListener('DOMContentLoaded', () => {
                    if (window !== window.parent) {
                        const notifyParent = (status) => {
                            window.parent.postMessage({ type: 'STATUS', status: status }, '*');
                        };

                        // Instantly tell parent we are connecting
                        notifyParent('connecting');
                        document.body.style.backgroundColor = 'transparent';

                        const checkForError = () => {
                            if (document.body) {
                                const text = document.body.innerText.toLowerCase();
                                if (text.includes('stream not found') || text.includes('peer connection closed') || text.includes('retrying in some seconds')) {
                                    document.body.style.color = 'transparent';
                                    notifyParent('offline');
                                }
                            }
                        };

                        const observer = new MutationObserver(() => {
                            checkForError();
                        });
                        observer.observe(document.body, { childList: true, subtree: true, characterData: true });
                        checkForError();

                        const checkVideo = () => {
                            const videos = document.getElementsByTagName('video');
                            if (videos.length > 0) {
                                const v = videos[0];
                                v.addEventListener('playing', () => notifyParent('live'));
                                if (v.currentTime > 0 && !v.paused && !v.ended && v.readyState > 2) {
                                    notifyParent('live');
                                }
                            } else {
                                setTimeout(checkVideo, 100);
                            }
                        };
                        checkVideo();
                    }
                });
            ");

            string streamUrl = currentConfig.STREAM_URL;

                        string htmlContent;
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("WhipCast.menu.html"))
            using (var reader = new System.IO.StreamReader(stream))
            {
                htmlContent = reader.ReadToEnd();
            }
            htmlContent = htmlContent.Replace("__STREAM_URL__", streamUrl);
            webView.NavigateToString(htmlContent);
        }

        private void SendConfigToWeb()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = null };
            string json = JsonSerializer.Serialize(new { type = "LOAD_CONFIG", config = currentConfig }, options);
            webView.CoreWebView2.PostWebMessageAsJson(json);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string msgStr = e.TryGetWebMessageAsString();
                if (msgStr != null)
                {
                    if (msgStr == "FS_ON" && !IsFullscreenMode)
                    {
                        IsFullscreenMode = true;
                        FullscreenStateChanged?.Invoke(this, EventArgs.Empty);
                    }
                    else if (msgStr == "FS_OFF" && IsFullscreenMode)
                    {
                        IsFullscreenMode = false;
                        FullscreenStateChanged?.Invoke(this, EventArgs.Empty);
                    }
                    return;
                }
            }
            catch { }

            try
            {
                var msgJson = e.WebMessageAsJson;
                using (JsonDocument doc = JsonDocument.Parse(msgJson))
                {
                    if (doc.RootElement.TryGetProperty("type", out JsonElement typeEl))
                    {
                        string type = typeEl.GetString();
                        switch (type)
                        {
                            case "REQUEST_CONFIG":
                                SendConfigToWeb();
                                break;
                            case "SAVE_AND_RESTART":
                                if (doc.RootElement.TryGetProperty("config", out JsonElement cfgEl))
                                {
                                    ParseConfigElement(cfgEl);
                                    ConfigManager.Save(currentConfig);
                                    RequestRestart?.Invoke(this, EventArgs.Empty);
                                }
                                break;
                            case "SAVE_PRESET":
                                if (doc.RootElement.TryGetProperty("presetId", out JsonElement pId) &&
                                    doc.RootElement.TryGetProperty("config", out JsonElement pCfg))
                                {
                                    string key = pId.GetRawText();
                                    if (currentConfig.PRESETS.ContainsKey(key))
                                    {
                                        var pr = currentConfig.PRESETS[key];
                                        pr.OFFSET_X = pCfg.GetProperty("OFFSET_X").GetInt32();
                                        pr.OFFSET_Y = pCfg.GetProperty("OFFSET_Y").GetInt32();
                                        pr.MARGIN_RIGHT = pCfg.GetProperty("MARGIN_RIGHT").GetInt32();
                                        pr.MARGIN_BOTTOM = pCfg.GetProperty("MARGIN_BOTTOM").GetInt32();
                                        ConfigManager.Save(currentConfig);
                                    }
                                }
                                break;
                            case "LOAD_PRESET":
                                if (doc.RootElement.TryGetProperty("presetId", out JsonElement pIdLoad))
                                {
                                    string key = pIdLoad.GetRawText();
                                    if (currentConfig.PRESETS.ContainsKey(key))
                                    {
                                        var pr = currentConfig.PRESETS[key];
                                        currentConfig.OFFSET_X = pr.OFFSET_X;
                                        currentConfig.OFFSET_Y = pr.OFFSET_Y;
                                        currentConfig.MARGIN_RIGHT = pr.MARGIN_RIGHT;
                                        currentConfig.MARGIN_BOTTOM = pr.MARGIN_BOTTOM;
                                        SendConfigToWeb();
                                        ConfigManager.Save(currentConfig);
                                        RequestRestart?.Invoke(this, EventArgs.Empty);
                                    }
                                }
                                break;
                            case "EXIT_APP":
                                if (!isExiting)
                                {
                                    isExiting = true;
                                    RequestExit?.Invoke(this, EventArgs.Empty);
                                }
                                break;
                        }
                    }
                }
            }
            catch { }
        }

        private void ParseConfigElement(JsonElement el)
        {
            if (el.TryGetProperty("STREAM_URL", out JsonElement streamUrl))
                currentConfig.STREAM_URL = streamUrl.GetString();
            if (el.TryGetProperty("ATTACH_TO_WINDOW", out JsonElement attach))
                currentConfig.ATTACH_TO_WINDOW = attach.GetBoolean();
            if (el.TryGetProperty("HOTKEY_TOGGLE_STREAM", out JsonElement h1))
                currentConfig.HOTKEY_TOGGLE_STREAM = h1.GetString();
            if (el.TryGetProperty("HOTKEY_TOGGLE_MODE", out JsonElement h2))
                currentConfig.HOTKEY_TOGGLE_MODE = h2.GetString();
            if (el.TryGetProperty("OFFSET_X", out JsonElement ox))
                currentConfig.OFFSET_X = ox.GetInt32();
            if (el.TryGetProperty("OFFSET_Y", out JsonElement oy))
                currentConfig.OFFSET_Y = oy.GetInt32();
            if (el.TryGetProperty("MARGIN_RIGHT", out JsonElement mr))
                currentConfig.MARGIN_RIGHT = mr.GetInt32();
            if (el.TryGetProperty("MARGIN_BOTTOM", out JsonElement mb))
                currentConfig.MARGIN_BOTTOM = mb.GetInt32();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!isExiting && !IsRestarting && e.CloseReason == CloseReason.UserClosing)
            {
                isExiting = true;
                RequestExit?.Invoke(this, EventArgs.Empty);
            }
            base.OnFormClosing(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                if (currentConfig != null && currentConfig.ATTACH_TO_WINDOW)
                {
                    cp.ExStyle |= WindowManager.WS_EX_TOOLWINDOW;
                    cp.ExStyle &= ~WindowManager.WS_EX_APPWINDOW;
                }
                return cp;
            }
        }
    }
}
