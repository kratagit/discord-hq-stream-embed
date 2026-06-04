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

            InitializeComponents();
            LoadConfigValues();
        }

        private void InitializeComponents()
        {
            Font boldFont = new Font("Arial", 10, FontStyle.Bold);
            Font normFont = new Font("Arial", 10);

            int currentY = 15;

            // URL
            Label lblUrl = new Label { Text = "Stream address (STREAM_URL):", Font = boldFont, AutoSize = true, Location = new Point(20, currentY) };
            this.Controls.Add(lblUrl);
            currentY += 25;
            
            txtUrl = new TextBox { Font = normFont, Width = 540, Location = new Point(20, currentY) };
            this.Controls.Add(txtUrl);
            currentY += 35;

            // Hotkey
            Label lblHotkey = new Label { Text = "Shortcut to hide window (HOTKEY_TOGGLE_STREAM):", Font = boldFont, AutoSize = true, Location = new Point(20, currentY) };
            this.Controls.Add(lblHotkey);
            currentY += 25;
            
            txtHotkey = new TextBox { Font = normFont, Width = 540, Location = new Point(20, currentY) };
            this.Controls.Add(txtHotkey);
            currentY += 35;

            // Margins GroupBox
            GroupBox gbMargins = new GroupBox { Text = " Margins and Position (Window Adjustment) ", Font = boldFont, Width = 540, Height = 100, Location = new Point(20, currentY) };
            this.Controls.Add(gbMargins);
            currentY += 110;

            Label lblOx = new Label { Text = "OFFSET_X:", Font = normFont, AutoSize = true, Location = new Point(20, 30) };
            txtOffsetX = new TextBox { Font = normFont, Width = 100, Location = new Point(120, 27) };
            Label lblOy = new Label { Text = "OFFSET_Y:", Font = normFont, AutoSize = true, Location = new Point(280, 30) };
            txtOffsetY = new TextBox { Font = normFont, Width = 100, Location = new Point(380, 27) };

            Label lblMr = new Label { Text = "MARGIN_RIGHT:", Font = normFont, AutoSize = true, Location = new Point(5, 65) };
            txtMarginRight = new TextBox { Font = normFont, Width = 100, Location = new Point(120, 62) };
            Label lblMb = new Label { Text = "MARGIN_BOTTOM:", Font = normFont, AutoSize = true, Location = new Point(245, 65) };
            txtMarginBottom = new TextBox { Font = normFont, Width = 100, Location = new Point(380, 62) };

            gbMargins.Controls.AddRange(new Control[] { lblOx, txtOffsetX, lblOy, txtOffsetY, lblMr, txtMarginRight, lblMb, txtMarginBottom });

            // Presets GroupBox
            GroupBox gbPresets = new GroupBox { Text = " Saved Margin Presets ", Font = boldFont, Width = 540, Height = 80, Location = new Point(20, currentY) };
            this.Controls.Add(gbPresets);
            currentY += 90;

            for (int i = 1; i <= 3; i++)
            {
                string pid = i.ToString();
                int xOffset = 30 + (i - 1) * 170;
                
                Label l = new Label { Text = $"Preset {i}", Font = boldFont, AutoSize = true, Location = new Point(xOffset + 30, 20) };
                Button btnLoad = new Button { Text = "Load", Font = normFont, Width = 60, Location = new Point(xOffset, 40) };
                Button btnSave = new Button { Text = "Save", Font = normFont, Width = 60, Location = new Point(xOffset + 65, 40) };

                btnLoad.Click += (s, e) => LoadPreset(pid);
                btnSave.Click += (s, e) => SavePreset(pid);

                gbPresets.Controls.AddRange(new Control[] { l, btnLoad, btnSave });
            }

            // Buttons
            Button btnSaveMain = new Button { Text = "Save and Restart", Font = boldFont, BackColor = Color.Green, ForeColor = Color.White, Width = 150, Height = 30, Location = new Point(20, currentY) };
            Button btnCancel = new Button { Text = "Cancel", Font = normFont, Width = 100, Height = 30, Location = new Point(460, currentY) };

            btnSaveMain.Click += BtnSaveMain_Click;
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(btnSaveMain);
            this.Controls.Add(btnCancel);
        }

        private void LoadConfigValues()
        {
            txtUrl.Text = config.STREAM_URL;
            txtHotkey.Text = config.HOTKEY_TOGGLE_STREAM;
            txtOffsetX.Text = config.OFFSET_X.ToString();
            txtOffsetY.Text = config.OFFSET_Y.ToString();
            txtMarginRight.Text = config.MARGIN_RIGHT.ToString();
            txtMarginBottom.Text = config.MARGIN_BOTTOM.ToString();
        }

        private void LoadPreset(string pid)
        {
            if (config.PRESETS.TryGetValue(pid, out Preset p))
            {
                txtOffsetX.Text = p.OFFSET_X.ToString();
                txtOffsetY.Text = p.OFFSET_Y.ToString();
                txtMarginRight.Text = p.MARGIN_RIGHT.ToString();
                txtMarginBottom.Text = p.MARGIN_BOTTOM.ToString();
                MessageBox.Show($"Loaded Preset {pid}", "Loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void BtnSaveMain_Click(object sender, EventArgs e)
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
                this.Close();
            }
            else
            {
                MessageBox.Show("Margins must be integers!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
