using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiscordStreamOverlay
{
    public class SettingsForm : Form
    {
        private AppConfig config;
        
        private TextBox txtUrl;
        private TextBox txtHotkey;
        private TextBox txtOffsetX, txtOffsetY, txtMarginRight, txtMarginBottom;

        public event EventHandler SettingsSaved;

        public SettingsForm(AppConfig currentConfig)
        {
            this.config = currentConfig;
            
            this.Text = "Options (Stream Settings)";
            this.Width = 600;
            this.Height = 400;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.WindowState = FormWindowState.Minimized;

            InitializeComponents();
            LoadConfigValues();

            this.FormClosing += SettingsForm_FormClosing;
        }

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit the application and stop watching the stream?",
                "Exit Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                e.Cancel = true;
            }
        }

        private Label lblActivePreset;
        private Label lblSavedMessage;

        private void InitializeComponents()
        {
            Font titleFont = new Font("Segoe UI", 10, FontStyle.Bold);
            Font boldFont = new Font("Segoe UI", 9, FontStyle.Bold);
            Font normFont = new Font("Segoe UI", 9);

            this.BackColor = SystemColors.Window;
            int currentY = 20;

            // URL
            Label lblUrl = new Label { Text = "Stream URL:", Font = boldFont, AutoSize = true, Location = new Point(25, currentY) };
            this.Controls.Add(lblUrl);
            currentY += 22;
            
            txtUrl = new TextBox { Font = normFont, Width = 530, Location = new Point(25, currentY) };
            this.Controls.Add(txtUrl);
            currentY += 35;

            // Hotkey
            Label lblHotkey = new Label { Text = "Toggle Visibility Hotkey (e.g. f8+f7):", Font = boldFont, AutoSize = true, Location = new Point(25, currentY) };
            this.Controls.Add(lblHotkey);
            currentY += 22;
            
            txtHotkey = new TextBox { Font = normFont, Width = 530, Location = new Point(25, currentY) };
            this.Controls.Add(txtHotkey);
            currentY += 35;

            // Margins GroupBox
            GroupBox gbMargins = new GroupBox { Text = " Overlay Adjustments ", Font = titleFont, Width = 530, Height = 100, Location = new Point(25, currentY), BackColor = SystemColors.Window };
            this.Controls.Add(gbMargins);
            currentY += 115;

            Label lblOx = new Label { Text = "Offset X:", Font = normFont, AutoSize = true, Location = new Point(20, 32) };
            txtOffsetX = new TextBox { Font = normFont, Width = 80, Location = new Point(80, 29) };
            
            Label lblOy = new Label { Text = "Offset Y:", Font = normFont, AutoSize = true, Location = new Point(270, 32) };
            txtOffsetY = new TextBox { Font = normFont, Width = 80, Location = new Point(330, 29) };

            Label lblMr = new Label { Text = "Margin R:", Font = normFont, AutoSize = true, Location = new Point(20, 65) };
            txtMarginRight = new TextBox { Font = normFont, Width = 80, Location = new Point(80, 62) };
            
            Label lblMb = new Label { Text = "Margin B:", Font = normFont, AutoSize = true, Location = new Point(270, 65) };
            txtMarginBottom = new TextBox { Font = normFont, Width = 80, Location = new Point(330, 62) };

            gbMargins.Controls.AddRange(new Control[] { lblOx, txtOffsetX, lblOy, txtOffsetY, lblMr, txtMarginRight, lblMb, txtMarginBottom });

            EventHandler onMarginChanged = (s, e) => { if (lblActivePreset != null) lblActivePreset.Text = "Active Preset: Custom"; };
            txtOffsetX.TextChanged += onMarginChanged;
            txtOffsetY.TextChanged += onMarginChanged;
            txtMarginRight.TextChanged += onMarginChanged;
            txtMarginBottom.TextChanged += onMarginChanged;

            // Presets GroupBox
            GroupBox gbPresets = new GroupBox { Text = " Presets ", Font = titleFont, Width = 530, Height = 85, Location = new Point(25, currentY), BackColor = SystemColors.Window };
            this.Controls.Add(gbPresets);
            currentY += 105;

            lblActivePreset = new Label { Text = "Active Preset: Custom", Font = boldFont, ForeColor = Color.FromArgb(0, 120, 215), AutoSize = true, Location = new Point(350, 0) };
            gbPresets.Controls.Add(lblActivePreset);

            for (int i = 1; i <= 3; i++)
            {
                string pid = i.ToString();
                int xOffset = 50 + (i - 1) * 160;
                
                Label l = new Label { Text = $"Preset {i}", Font = boldFont, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Width = 110, Location = new Point(xOffset, 20) };
                Button btnLoad = new Button { Text = "Load", Font = normFont, Width = 52, Height = 28, Location = new Point(xOffset, 42) };
                Button btnSave = new Button { Text = "Save", Font = normFont, Width = 52, Height = 28, Location = new Point(xOffset + 58, 42) };

                btnLoad.Click += (s, e) => LoadPreset(pid);
                btnSave.Click += (s, e) => SavePreset(pid);

                gbPresets.Controls.AddRange(new Control[] { l, btnLoad, btnSave });
            }

            // Buttons
            Button btnSaveMain = new Button { Text = "Save and Apply", Font = titleFont, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Width = 150, Height = 35, Location = new Point(215, currentY) };
            btnSaveMain.FlatAppearance.BorderSize = 0;
            btnSaveMain.Cursor = Cursors.Hand;
            btnSaveMain.Click += BtnSaveMain_Click;
            this.Controls.Add(btnSaveMain);

            lblSavedMessage = new Label { Text = "", Font = boldFont, ForeColor = Color.SeaGreen, AutoSize = true, Location = new Point(380, currentY + 8) };
            this.Controls.Add(lblSavedMessage);

            this.Height = currentY + 90;
        }

        private void LoadConfigValues()
        {
            txtUrl.Text = config.STREAM_URL;
            txtHotkey.Text = config.HOTKEY_TOGGLE_STREAM;
            txtOffsetX.Text = config.OFFSET_X.ToString();
            txtOffsetY.Text = config.OFFSET_Y.ToString();
            txtMarginRight.Text = config.MARGIN_RIGHT.ToString();
            txtMarginBottom.Text = config.MARGIN_BOTTOM.ToString();
            if (lblActivePreset != null) lblActivePreset.Text = "Active Preset: Custom";
        }

        private void LoadPreset(string pid)
        {
            if (config.PRESETS.TryGetValue(pid, out Preset p))
            {
                txtOffsetX.Text = p.OFFSET_X.ToString();
                txtOffsetY.Text = p.OFFSET_Y.ToString();
                txtMarginRight.Text = p.MARGIN_RIGHT.ToString();
                txtMarginBottom.Text = p.MARGIN_BOTTOM.ToString();
                lblActivePreset.Text = $"Active Preset: {pid}";
            }
        }

        private void SavePreset(string pid)
        {
            if (int.TryParse(txtOffsetX.Text, out int ox) && int.TryParse(txtOffsetY.Text, out int oy) &&
                int.TryParse(txtMarginRight.Text, out int mr) && int.TryParse(txtMarginBottom.Text, out int mb))
            {
                config.PRESETS[pid] = new Preset { OFFSET_X = ox, OFFSET_Y = oy, MARGIN_RIGHT = mr, MARGIN_BOTTOM = mb };
                ConfigManager.Save(config);
                MessageBox.Show($"Saved settings to Preset {pid}", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Margins must be integers!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnSaveMain_Click(object sender, EventArgs e)
        {
            if (int.TryParse(txtOffsetX.Text, out int ox) && int.TryParse(txtOffsetY.Text, out int oy) &&
                int.TryParse(txtMarginRight.Text, out int mr) && int.TryParse(txtMarginBottom.Text, out int mb))
            {
                config.STREAM_URL = txtUrl.Text.Trim();
                config.HOTKEY_TOGGLE_STREAM = txtHotkey.Text.Trim();
                config.OFFSET_X = ox;
                config.OFFSET_Y = oy;
                config.MARGIN_RIGHT = mr;
                config.MARGIN_BOTTOM = mb;

                ConfigManager.Save(config);
                SettingsSaved?.Invoke(this, EventArgs.Empty);
                
                if (lblSavedMessage != null)
                {
                    lblSavedMessage.Text = "Settings applied!";
                    await System.Threading.Tasks.Task.Delay(3000);
                    lblSavedMessage.Text = "";
                }
            }
            else
            {
                MessageBox.Show("Margins must be integers!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
