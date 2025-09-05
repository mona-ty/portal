using System.Windows.Forms;

namespace FF14SubmarinesAct.Ui
{
    partial class SettingsControl
    {
        private TextBox _textLog;
        private TableLayoutPanel _table;
        private Label _lblAct;
        private TextBox _txtAct;
        private Button _btnAct;
        private Label _lblFfxiv;
        private TextBox _txtFfxiv;
        private Button _btnFfxiv;
        private FlowLayoutPanel _buttons;
        private Button _btnDumpStart;
        private Button _btnDumpStop;
        private Label _lblDumpFile;
        private Label _lblLogs;
        private TextBox _txtLogs;
        private Button _btnLogs;
        private System.Windows.Forms.NumericUpDown _numDefaultMinutes;
        private System.Windows.Forms.Label _lblDefaultMinutes;
        private System.Windows.Forms.ListView _list;
        private System.Windows.Forms.ColumnHeader _colName;
        private System.Windows.Forms.ColumnHeader _colDeparted;
        private System.Windows.Forms.ColumnHeader _colEta;
        private System.Windows.Forms.ColumnHeader _colRemain;
        private System.Windows.Forms.GroupBox _grpOverrides;
        private System.Windows.Forms.DataGridView _gridOverrides;
        private System.Windows.Forms.Button _btnAddOverride;
        private System.Windows.Forms.Button _btnRemoveOverride;
        private System.Windows.Forms.Button _btnSaveOverrides;

        private void InitializeComponent()
        {
            this._table = new System.Windows.Forms.TableLayoutPanel();
            this._lblAct = new System.Windows.Forms.Label();
            this._txtAct = new System.Windows.Forms.TextBox();
            this._btnAct = new System.Windows.Forms.Button();
            this._lblFfxiv = new System.Windows.Forms.Label();
            this._txtFfxiv = new System.Windows.Forms.TextBox();
            this._btnFfxiv = new System.Windows.Forms.Button();
            this._textLog = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // _table
            // 
            this._table.ColumnCount = 3;
            this._table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            this._table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this._table.RowCount = 6;
            this._table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this._table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            this._table.Dock = DockStyle.Fill;
            // 
            // _lblAct
            // 
            this._lblAct.Text = "ACTフォルダ:";
            this._lblAct.AutoSize = true;
            this._table.Controls.Add(this._lblAct, 0, 0);
            // 
            // _txtAct
            // 
            this._txtAct.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this._table.Controls.Add(this._txtAct, 1, 0);
            // 
            // _btnAct
            // 
            this._btnAct.Text = "参照";
            this._btnAct.AutoSize = true;
            this._btnAct.Click += (s, e) => OnBrowseAct();
            this._table.Controls.Add(this._btnAct, 2, 0);
            // 
            // _lblFfxiv
            // 
            this._lblFfxiv.Text = "FFXIV_ACT_Plugin.dll:";
            this._lblFfxiv.AutoSize = true;
            this._table.Controls.Add(this._lblFfxiv, 0, 1);
            // 
            // _txtFfxiv
            // 
            this._txtFfxiv.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this._table.Controls.Add(this._txtFfxiv, 1, 1);
            // 
            // _btnFfxiv
            // 
            this._btnFfxiv.Text = "参照";
            this._btnFfxiv.AutoSize = true;
            this._btnFfxiv.Click += (s, e) => OnBrowseFfxiv();
            this._table.Controls.Add(this._btnFfxiv, 2, 1);
            // 
            // Logs folder
            // 
            this._lblLogs = new Label();
            this._lblLogs.Text = "FFXIVLogs フォルダ:";
            this._lblLogs.AutoSize = true;
            this._txtLogs = new TextBox();
            this._txtLogs.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this._btnLogs = new Button();
            this._btnLogs.Text = "参照";
            this._btnLogs.AutoSize = true;
            this._btnLogs.Click += (s, e) => OnBrowseLogs();
            this._table.Controls.Add(this._lblLogs, 0, 2);
            this._table.Controls.Add(this._txtLogs, 1, 2);
            this._table.Controls.Add(this._btnLogs, 2, 2);
            // 
            // default minutes
            // 
            this._lblDefaultMinutes = new Label();
            this._lblDefaultMinutes.Text = "既定の航海時間(分):";
            this._lblDefaultMinutes.AutoSize = true;
            this._numDefaultMinutes = new NumericUpDown();
            this._numDefaultMinutes.Minimum = 1;
            this._numDefaultMinutes.Maximum = 10000;
            this._numDefaultMinutes.ValueChanged += (s, e) => OnDefaultMinutesChanged();
            this._table.Controls.Add(this._lblDefaultMinutes, 0, 3);
            this._table.Controls.Add(this._numDefaultMinutes, 1, 3);
            // 
            // Overrides group
            this._grpOverrides = new GroupBox();
            this._grpOverrides.Text = "所要時間の上書き（艦名/ルート）";
            this._grpOverrides.Dock = DockStyle.Fill;
            var overridesPanel = new FlowLayoutPanel();
            overridesPanel.Dock = DockStyle.Fill;
            overridesPanel.FlowDirection = FlowDirection.TopDown;
            overridesPanel.WrapContents = false;
            this._gridOverrides = new DataGridView();
            this._gridOverrides.AllowUserToAddRows = false;
            this._gridOverrides.AllowUserToDeleteRows = false;
            this._gridOverrides.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._gridOverrides.Height = 140;
            this._gridOverrides.Columns.Add("Key", "キー（艦名/ルート）");
            this._gridOverrides.Columns.Add("Minutes", "分");
            var btnBar = new FlowLayoutPanel();
            btnBar.FlowDirection = FlowDirection.LeftToRight;
            btnBar.AutoSize = true;
            this._btnAddOverride = new Button(); this._btnAddOverride.Text = "追加"; this._btnAddOverride.AutoSize = true; this._btnAddOverride.Click += (s,e)=>OnAddOverride();
            this._btnRemoveOverride = new Button(); this._btnRemoveOverride.Text = "削除"; this._btnRemoveOverride.AutoSize = true; this._btnRemoveOverride.Click += (s,e)=>OnRemoveOverride();
            this._btnSaveOverrides = new Button(); this._btnSaveOverrides.Text = "保存"; this._btnSaveOverrides.AutoSize = true; this._btnSaveOverrides.Click += (s,e)=>OnSaveOverrides();
            btnBar.Controls.Add(this._btnAddOverride);
            btnBar.Controls.Add(this._btnRemoveOverride);
            btnBar.Controls.Add(this._btnSaveOverrides);
            overridesPanel.Controls.Add(this._gridOverrides);
            overridesPanel.Controls.Add(btnBar);
            this._grpOverrides.Controls.Add(overridesPanel);
            this._table.Controls.Add(this._grpOverrides, 0, 4);
            this._table.SetColumnSpan(this._grpOverrides, 3);

            // _buttons (dump controls)
            // 
            this._buttons = new FlowLayoutPanel();
            this._buttons.AutoSize = true;
            this._buttons.FlowDirection = FlowDirection.LeftToRight;
            this._btnDumpStart = new Button();
            this._btnDumpStart.Text = "ネットワークダンプ開始";
            this._btnDumpStart.AutoSize = true;
            this._btnDumpStart.Click += (s, e) => OnDumpStart();
            this._btnDumpStop = new Button();
            this._btnDumpStop.Text = "停止";
            this._btnDumpStop.AutoSize = true;
            this._btnDumpStop.Click += (s, e) => OnDumpStop();
            this._lblDumpFile = new Label();
            this._lblDumpFile.AutoSize = true;
            this._lblDumpFile.Text = "";
            this._buttons.Controls.Add(this._btnDumpStart);
            this._buttons.Controls.Add(this._btnDumpStop);
            this._buttons.Controls.Add(this._lblDumpFile);
            this._table.Controls.Add(this._buttons, 0, 5);
            this._table.SetColumnSpan(this._buttons, 3);
            // 
            // _textLog
            // 
            this._textLog.Multiline = true;
            this._textLog.ScrollBars = ScrollBars.Vertical;
            this._textLog.Dock = DockStyle.Fill;
            // list view for current ETAs
            this._list = new System.Windows.Forms.ListView();
            this._list.View = View.Details;
            this._list.FullRowSelect = true;
            this._colName = new ColumnHeader(); this._colName.Text = "艦名"; this._colName.Width = 140;
            this._colDeparted = new ColumnHeader(); this._colDeparted.Text = "出港"; this._colDeparted.Width = 160;
            this._colEta = new ColumnHeader(); this._colEta.Text = "ETA"; this._colEta.Width = 160;
            this._colRemain = new ColumnHeader(); this._colRemain.Text = "残り(分)"; this._colRemain.Width = 80;
            this._list.Columns.AddRange(new ColumnHeader[] { _colName, _colDeparted, _colEta, _colRemain });
            this._list.Dock = DockStyle.Fill;
            this._table.Controls.Add(this._list, 0, 6);
            this._table.SetColumnSpan(this._list, 3);
            // 
            // SettingsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this._table);
            this.Name = "SettingsControl";
            this.Size = new System.Drawing.Size(600, 400);
            this.ResumeLayout(false);
        }
    }
}
