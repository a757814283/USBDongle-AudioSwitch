using System.Drawing;
using System.Windows.Forms;

namespace USBDongle_AudioSwitch
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private TableLayoutPanel _root;
        private GroupBox _audioGroup;
        private Label _lblHeadset;
        private ComboBox _cboHeadset;
        private Label _lblSpeaker;
        private ComboBox _cboSpeaker;
        private Button _btnRefreshDevices;

        private GroupBox _hidGroup;
        private Label _lblHidMatch;
        private TextBox _txtHidMatch;
        private Button _btnListHid;
        private Label _lblSigOn;
        private TextBox _txtSigOn;
        private Label _lblSigOnCheck;
        private Label _lblSigOff;
        private TextBox _txtSigOff;
        private Label _lblSigOffCheck;
        private Label _lblSigLength;
        private NumericUpDown _numSigLength;
        private Label _lblCooldown;
        private NumericUpDown _numCooldown;
        private Label _lblReopen;
        private NumericUpDown _numReopen;
        private CheckBox _chkVerifyChecksum;
        private CheckBox _chkLogUnknown;

        private Panel _actionPanel;
        private CheckBox _chkAutoStart;
        private Button _btnSave;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnClearLog;
        private Button _btnOpenFolder;

        private GroupBox _logGroup;
        private TextBox _txtLog;

        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _lblStatus;
        private ToolStripStatusLabel _lblDevicePath;
        private ToolStripStatusLabel _lblEventCount;
        private ToolStripStatusLabel _lblLanguage;
        private ToolStripComboBox _cboLanguage;

        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _menuShow;
        private ToolStripMenuItem _menuStart;
        private ToolStripMenuItem _menuStop;
        private ToolStripMenuItem _menuExit;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Builds the layout only. Every piece of visible text is left to
        /// <see cref="ApplyLocalization"/> so the window can be re-labelled in
        /// place when the language changes - a designer that hard-codes strings
        /// here would need the whole form rebuilt to switch.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            Font uiFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            Font monoFont = new Font("Consolas", 8.25F, FontStyle.Regular, GraphicsUnit.Point);

            // ---------- audio targets ----------
            this._lblHeadset = new Label();
            this._lblHeadset.AutoSize = true;
            this._lblHeadset.Location = new Point(14, 24);

            this._cboHeadset = new ComboBox();
            this._cboHeadset.DropDownStyle = ComboBoxStyle.DropDownList;
            this._cboHeadset.Location = new Point(190, 21);
            this._cboHeadset.Size = new Size(455, 23);
            this._cboHeadset.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            this._lblSpeaker = new Label();
            this._lblSpeaker.AutoSize = true;
            this._lblSpeaker.Location = new Point(14, 54);

            this._cboSpeaker = new ComboBox();
            this._cboSpeaker.DropDownStyle = ComboBoxStyle.DropDownList;
            this._cboSpeaker.Location = new Point(190, 51);
            this._cboSpeaker.Size = new Size(455, 23);
            this._cboSpeaker.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            this._btnRefreshDevices = new Button();
            this._btnRefreshDevices.Location = new Point(657, 20);
            this._btnRefreshDevices.Size = new Size(132, 26);
            this._btnRefreshDevices.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._audioGroup = new GroupBox();
            this._audioGroup.Dock = DockStyle.Fill;
            this._audioGroup.Margin = new Padding(8, 6, 8, 2);
            // Size seeded before the children are added, because WinForms
            // captures each anchored child's margins against the parent's size
            // *at the moment it is added*. A new GroupBox is 200x100, so the
            // combo box below (at x=140, width 520) would record a right margin
            // of -466 and later stretch to 1124px. The Dock below overrides
            // this size; it only exists to give the anchors a sane baseline.
            //
            // The label at x=140 is sized for the *wider* of the two languages:
            // Chinese is the compact one, and English must still clear the
            // combo box without overlapping it.
            this._audioGroup.Size = new Size(804, 92);
            this._audioGroup.Controls.Add(this._lblHeadset);
            this._audioGroup.Controls.Add(this._cboHeadset);
            this._audioGroup.Controls.Add(this._btnRefreshDevices);
            this._audioGroup.Controls.Add(this._lblSpeaker);
            this._audioGroup.Controls.Add(this._cboSpeaker);

            // ---------- HID detection ----------
            this._lblHidMatch = new Label();
            this._lblHidMatch.AutoSize = true;
            this._lblHidMatch.Location = new Point(14, 26);

            this._txtHidMatch = new TextBox();
            this._txtHidMatch.Location = new Point(190, 23);
            this._txtHidMatch.Size = new Size(396, 23);
            this._txtHidMatch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            this._btnListHid = new Button();
            this._btnListHid.Location = new Point(596, 22);
            this._btnListHid.Size = new Size(178, 26);
            this._btnListHid.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._lblSigOn = new Label();
            this._lblSigOn.AutoSize = true;
            this._lblSigOn.Location = new Point(14, 54);

            this._txtSigOn = new TextBox();
            this._txtSigOn.Location = new Point(190, 51);
            this._txtSigOn.Size = new Size(396, 23);
            this._txtSigOn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this._txtSigOn.Font = monoFont;

            this._lblSigOnCheck = new Label();
            this._lblSigOnCheck.Location = new Point(596, 55);
            this._lblSigOnCheck.Size = new Size(178, 18);
            this._lblSigOnCheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this._lblSigOnCheck.ForeColor = Color.Gray;

            this._lblSigOff = new Label();
            this._lblSigOff.AutoSize = true;
            this._lblSigOff.Location = new Point(14, 82);

            this._txtSigOff = new TextBox();
            this._txtSigOff.Location = new Point(190, 79);
            this._txtSigOff.Size = new Size(396, 23);
            this._txtSigOff.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this._txtSigOff.Font = monoFont;

            this._lblSigOffCheck = new Label();
            this._lblSigOffCheck.Location = new Point(596, 83);
            this._lblSigOffCheck.Size = new Size(178, 18);
            this._lblSigOffCheck.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this._lblSigOffCheck.ForeColor = Color.Gray;

            // This row is spread wider than the labels strictly need, because
            // the English captions are up to 40% longer than the Chinese ones
            // and the three numeric fields have to stay clear of them.
            this._lblSigLength = new Label();
            this._lblSigLength.AutoSize = true;
            this._lblSigLength.Location = new Point(14, 116);

            this._numSigLength = new NumericUpDown();
            this._numSigLength.Location = new Point(130, 113);
            this._numSigLength.Size = new Size(58, 23);
            this._numSigLength.Minimum = 1;
            this._numSigLength.Maximum = 64;
            this._numSigLength.Value = 8;

            this._lblCooldown = new Label();
            this._lblCooldown.AutoSize = true;
            this._lblCooldown.Location = new Point(210, 116);

            this._numCooldown = new NumericUpDown();
            this._numCooldown.Location = new Point(330, 113);
            this._numCooldown.Size = new Size(74, 23);
            this._numCooldown.Minimum = 0;
            this._numCooldown.Maximum = 60000;
            this._numCooldown.Increment = 100;
            this._numCooldown.Value = 1500;

            this._lblReopen = new Label();
            this._lblReopen.AutoSize = true;
            this._lblReopen.Location = new Point(430, 116);

            this._numReopen = new NumericUpDown();
            this._numReopen.Location = new Point(560, 113);
            this._numReopen.Size = new Size(74, 23);
            this._numReopen.Minimum = 100;
            this._numReopen.Maximum = 60000;
            this._numReopen.Increment = 100;
            this._numReopen.Value = 2000;

            this._chkVerifyChecksum = new CheckBox();
            this._chkVerifyChecksum.AutoSize = true;
            this._chkVerifyChecksum.Location = new Point(14, 144);

            this._chkLogUnknown = new CheckBox();
            this._chkLogUnknown.AutoSize = true;
            this._chkLogUnknown.Location = new Point(230, 144);

            this._hidGroup = new GroupBox();
            this._hidGroup.Dock = DockStyle.Fill;
            this._hidGroup.Margin = new Padding(8, 2, 8, 2);
            // Seeded for the same reason as _audioGroup above.
            this._hidGroup.Size = new Size(804, 192);
            this._hidGroup.Controls.Add(this._lblHidMatch);
            this._hidGroup.Controls.Add(this._txtHidMatch);
            this._hidGroup.Controls.Add(this._btnListHid);
            this._hidGroup.Controls.Add(this._lblSigOn);
            this._hidGroup.Controls.Add(this._txtSigOn);
            this._hidGroup.Controls.Add(this._lblSigOnCheck);
            this._hidGroup.Controls.Add(this._lblSigOff);
            this._hidGroup.Controls.Add(this._txtSigOff);
            this._hidGroup.Controls.Add(this._lblSigOffCheck);
            this._hidGroup.Controls.Add(this._lblSigLength);
            this._hidGroup.Controls.Add(this._numSigLength);
            this._hidGroup.Controls.Add(this._lblCooldown);
            this._hidGroup.Controls.Add(this._numCooldown);
            this._hidGroup.Controls.Add(this._lblReopen);
            this._hidGroup.Controls.Add(this._numReopen);
            this._hidGroup.Controls.Add(this._chkVerifyChecksum);
            this._hidGroup.Controls.Add(this._chkLogUnknown);

            // ---------- actions ----------
            this._chkAutoStart = new CheckBox();
            this._chkAutoStart.AutoSize = true;
            this._chkAutoStart.Location = new Point(10, 12);

            // Widths are set by the English captions, which are the longer of
            // the two: "Start monitoring" measures 103px, well past the 96 these
            // buttons used to be.
            //
            // The margin matters as much as the measurement. A Button does not
            // clip its caption, it *wraps* it - so a button 7px too narrow for
            // its text silently renders as "Start" with "monitoring" wrapped on
            // to a second line that the 27px height cuts off. Each width here is
            // the measured text plus roughly 20px of border and padding.
            //
            // They stay right-anchored, laid out right-to-left with an 8px
            // gutter, ending at the same 802 edge as before.
            this._btnSave = new Button();
            this._btnSave.Location = new Point(234, 6);
            this._btnSave.Size = new Size(95, 27);
            this._btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._btnStart = new Button();
            this._btnStart.Location = new Point(337, 6);
            this._btnStart.Size = new Size(123, 27);
            this._btnStart.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._btnStop = new Button();
            this._btnStop.Location = new Point(468, 6);
            this._btnStop.Size = new Size(123, 27);
            this._btnStop.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            this._btnStop.Enabled = false;

            this._btnClearLog = new Button();
            this._btnClearLog.Location = new Point(599, 6);
            this._btnClearLog.Size = new Size(90, 27);
            this._btnClearLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._btnOpenFolder = new Button();
            this._btnOpenFolder.Location = new Point(697, 6);
            this._btnOpenFolder.Size = new Size(105, 27);
            this._btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            this._actionPanel = new Panel();
            this._actionPanel.Dock = DockStyle.Fill;
            this._actionPanel.Margin = new Padding(8, 2, 8, 2);
            // Seeded for the same reason as _audioGroup above. Without it the
            // right-anchored buttons record margins against the Panel's default
            // 200x100 and are laid out past the right edge of the window.
            this._actionPanel.Size = new Size(804, 36);
            this._actionPanel.Controls.Add(this._chkAutoStart);
            this._actionPanel.Controls.Add(this._btnSave);
            this._actionPanel.Controls.Add(this._btnStart);
            this._actionPanel.Controls.Add(this._btnStop);
            this._actionPanel.Controls.Add(this._btnClearLog);
            this._actionPanel.Controls.Add(this._btnOpenFolder);

            // ---------- log ----------
            this._txtLog = new TextBox();
            this._txtLog.Dock = DockStyle.Fill;
            this._txtLog.Multiline = true;
            this._txtLog.ReadOnly = true;
            this._txtLog.WordWrap = false;
            this._txtLog.ScrollBars = ScrollBars.Both;
            this._txtLog.Font = monoFont;
            this._txtLog.BackColor = Color.White;
            this._txtLog.Margin = new Padding(6, 2, 6, 6);

            this._logGroup = new GroupBox();
            this._logGroup.Dock = DockStyle.Fill;
            this._logGroup.Margin = new Padding(8, 2, 8, 2);
            this._logGroup.Controls.Add(this._txtLog);

            // ---------- status bar ----------
            this._lblStatus = new ToolStripStatusLabel();
            this._lblStatus.AutoSize = true;

            this._lblDevicePath = new ToolStripStatusLabel();
            this._lblDevicePath.Spring = true;
            this._lblDevicePath.TextAlign = ContentAlignment.MiddleLeft;

            this._lblEventCount = new ToolStripStatusLabel();
            this._lblEventCount.AutoSize = true;

            // The language picker lives here rather than in the action panel:
            // that row is already full of right-anchored buttons, and the
            // status bar's right-hand end is where a setting like this belongs.
            this._lblLanguage = new ToolStripStatusLabel();
            this._lblLanguage.AutoSize = true;

            this._cboLanguage = new ToolStripComboBox();
            this._cboLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            this._cboLanguage.AutoSize = false;
            // Wide enough for "Auto (follow system)", the longest entry.
            this._cboLanguage.Size = new Size(175, 23);

            this._statusStrip = new StatusStrip();
            this._statusStrip.Dock = DockStyle.Fill;
            this._statusStrip.SizingGrip = false;
            this._statusStrip.Margin = new Padding(0);
            this._statusStrip.Items.Add(this._lblStatus);
            this._statusStrip.Items.Add(this._lblDevicePath);
            this._statusStrip.Items.Add(this._lblEventCount);
            this._statusStrip.Items.Add(this._lblLanguage);
            this._statusStrip.Items.Add(this._cboLanguage);

            // ---------- tray ----------
            this._menuShow = new ToolStripMenuItem();

            this._menuStart = new ToolStripMenuItem();

            this._menuStop = new ToolStripMenuItem();

            this._menuExit = new ToolStripMenuItem();

            this._trayMenu = new ContextMenuStrip(this.components);
            this._trayMenu.Items.Add(this._menuShow);
            this._trayMenu.Items.Add(new ToolStripSeparator());
            this._trayMenu.Items.Add(this._menuStart);
            this._trayMenu.Items.Add(this._menuStop);
            this._trayMenu.Items.Add(new ToolStripSeparator());
            this._trayMenu.Items.Add(this._menuExit);

            this._trayIcon = new NotifyIcon(this.components);
            // Line art on transparency in whichever ink colour the taskbar is
            // not: the notification area is drawn over the taskbar and so needs
            // real alpha, which leaves the drawing itself to carry the contrast.
            // AppIcons picks white or black from the current taskbar theme, and
            // sizes the frame against the display's true DPI - which an unaware
            // process is not told. MainForm.WndProc swaps it when the theme moves.
            this._trayIcon.Icon = AppIcons.Tray;
            this._trayIcon.ContextMenuStrip = this._trayMenu;
            this._trayIcon.Visible = true;

            // ---------- root layout ----------
            // A TableLayoutPanel with explicit rows keeps the stacking order
            // independent of control z-order, which pure Dock=Top/Fill mixing
            // is sensitive to.
            this._root = new TableLayoutPanel();
            this._root.Dock = DockStyle.Fill;
            this._root.ColumnCount = 1;
            this._root.RowCount = 5;
            this._root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this._root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));  // audio
            this._root.RowStyles.Add(new RowStyle(SizeType.Absolute, 196F));  // hid
            this._root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));   // actions
            this._root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));   // log
            // Taller than the 24px it used to be, so the language combo in the
            // status bar is not clipped by the row.
            this._root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));   // status
            // Seeded to the form's client size before any group is added.
            // A TableLayoutPanel sizes the control in each cell the moment it
            // is added, and a brand new one is only 200x100 - so adding a group
            // to it would first shrink that group to ~184 wide, and the group's
            // anchored children would capture their right-hand margins against
            // *that*. The groups ended up 100px out of position because of it.
            this._root.Size = new Size(820, 506);
            this._root.Controls.Add(this._audioGroup, 0, 0);
            this._root.Controls.Add(this._hidGroup, 0, 1);
            this._root.Controls.Add(this._actionPanel, 0, 2);
            this._root.Controls.Add(this._logGroup, 0, 3);
            this._root.Controls.Add(this._statusStrip, 0, 4);

            // ---------- form ----------
            // Deliberately not AutoScaleMode.Font. That mode multiplies every
            // coordinate by the ratio between this form's font and the recorded
            // design measurement, and the two disagree here - (7,17) is the
            // measurement for the *default* 8.25pt font, while this form sets
            // YaHei UI 9pt. It rescaled ClientSize (820x504 rendered as
            // 957x714) without rescaling the children's anchor margins, which
            // is one of the two reasons the action buttons ended up off-window.
            //
            // None makes the designer coordinates exact. Every control is
            // anchored, so resizing still behaves, and .NET Framework 4.8 apps
            // are DPI-unaware by default - Windows bitmap-scales the whole
            // window on a high-DPI display rather than rendering it tiny.
            // AppIcons reads the true DPI separately for the tray icon, which
            // is the one thing bitmap scaling visibly ruins.
            this.AutoScaleMode = AutoScaleMode.None;
            // Sized so the log row (all remaining space) lands at ~145px: the
            // log is meant to be a status ticker read through, not the point of
            // the window. Growing the window still grows it.
            this.ClientSize = new Size(820, 506);
            this.MinimumSize = new Size(800, 490);
            this.Font = uiFont;
            this.Icon = AppIcons.Application;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Controls.Add(this._root);
        }
    }
}
