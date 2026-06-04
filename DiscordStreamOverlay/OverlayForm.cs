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
        private string streamUrl;
        private bool isStandalone;
        public bool IsFullscreenMode { get; private set; }

        public event EventHandler FullscreenStateChanged;

        public OverlayForm(string url, bool standalone = false, string windowTitle = "Stream", Icon appIcon = null)
        {
            this.streamUrl = url;
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
            await webView.EnsureCoreWebView2Async(null);
            
            // Disable default Edge hotkeys (like F7 for Caret Browsing, F5, etc.)
            webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

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
        <iframe src=""{streamUrl}"" allow=""autoplay; fullscreen; camera; microphone"" allowfullscreen=""true"" webkitallowfullscreen=""true"" mozallowfullscreen=""true""></iframe>
        
        <!-- Click absorbing layer -->
        <div class=""click-shield"" oncontextmenu=""return false;""></div>
    </div>

    <script>
        let lastFs = false;
        function checkFs() {{
            let isFs = !!document.fullscreenElement;
            if (isFs !== lastFs) {{
                lastFs = isFs;
                window.chrome.webview.postMessage(isFs ? 'FS_ON' : 'FS_OFF');
            }}
        }}
        document.addEventListener('fullscreenchange', checkFs);
        setInterval(checkFs, 200);
    </script>
</body>
</html>";

            webView.NavigateToString(htmlContent);
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string msg = e.TryGetWebMessageAsString();
            if (msg == "FS_ON" && !IsFullscreenMode)
            {
                IsFullscreenMode = true;
                FullscreenStateChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (msg == "FS_OFF" && IsFullscreenMode)
            {
                IsFullscreenMode = false;
                FullscreenStateChanged?.Invoke(this, EventArgs.Empty);
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
