namespace AOEKeyboardMacroPro;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        pnlOuterBorder = new Panel();
        lblTitle = new Label();
        btnThemeToggle = new Button();
        pnlStatusRow = new Panel();
        btnToggleMacro = new Button();
        lblStatusTitle = new Label();
        lblStatusValue = new Label();
        lblFarmTimer1 = new Label();
        lblFarmTimerSeparator = new Label();
        lblFarmTimer2 = new Label();
        lblFarmIntervalConfig = new Label();
        numFarmInterval = new NumericUpDown();
        lblSecondsUnit = new Label();
        lblGridTitle = new Label();
        pnlKeyboardGrid = new TableLayoutPanel();
        lblLogTitle = new Label();
        rtbLog = new RichTextBox();
        btnClearLog = new Button();
        chkAutoScroll = new CheckBox();
        pnlOuterBorder.SuspendLayout();
        pnlStatusRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numFarmInterval).BeginInit();
        SuspendLayout();
        // 
        // pnlOuterBorder
        // 
        pnlOuterBorder.BackColor = Color.White;
        pnlOuterBorder.BorderStyle = BorderStyle.FixedSingle;
        pnlOuterBorder.Controls.Add(btnThemeToggle);
        pnlOuterBorder.Controls.Add(rtbLog);
        pnlOuterBorder.Controls.Add(lblLogTitle);
        pnlOuterBorder.Controls.Add(btnClearLog);
        pnlOuterBorder.Controls.Add(chkAutoScroll);
        pnlOuterBorder.Controls.Add(pnlKeyboardGrid);
        pnlOuterBorder.Controls.Add(lblGridTitle);
        pnlOuterBorder.Controls.Add(lblFarmTimer1);
        pnlOuterBorder.Controls.Add(lblFarmTimerSeparator);
        pnlOuterBorder.Controls.Add(lblFarmTimer2);
        pnlOuterBorder.Controls.Add(lblFarmIntervalConfig);
        pnlOuterBorder.Controls.Add(numFarmInterval);
        pnlOuterBorder.Controls.Add(lblSecondsUnit);
        pnlOuterBorder.Controls.Add(pnlStatusRow);
        pnlOuterBorder.Controls.Add(lblTitle);
        pnlOuterBorder.Dock = DockStyle.Fill;
        pnlOuterBorder.Location = new Point(12, 12);
        pnlOuterBorder.Name = "pnlOuterBorder";
        pnlOuterBorder.Padding = new Padding(20);
        pnlOuterBorder.Size = new Size(826, 616);
        pnlOuterBorder.TabIndex = 0;
        // 
        // lblTitle
        // 
        lblTitle.Dock = DockStyle.Top;
        lblTitle.Font = new Font("Segoe UI", 26F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblTitle.ForeColor = Color.Black;
        lblTitle.Location = new Point(20, 20);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(784, 55);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "AOE MACRO SYSTEM";
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        // 
        // btnThemeToggle
        // 
        btnThemeToggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnThemeToggle.BackColor = Color.White;
        btnThemeToggle.FlatAppearance.BorderColor = Color.Black;
        btnThemeToggle.FlatStyle = FlatStyle.Flat;
        btnThemeToggle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        btnThemeToggle.ForeColor = Color.Black;
        btnThemeToggle.Location = new Point(700, 24);
        btnThemeToggle.Name = "btnThemeToggle";
        btnThemeToggle.Size = new Size(94, 30);
        btnThemeToggle.TabIndex = 9;
        btnThemeToggle.Text = "🌙 Giao diện tối";
        btnThemeToggle.UseVisualStyleBackColor = false;
        btnThemeToggle.Click += BtnThemeToggle_Click;
        // 
        // pnlStatusRow
        // 
        pnlStatusRow.Controls.Add(btnToggleMacro);
        pnlStatusRow.Controls.Add(lblStatusTitle);
        pnlStatusRow.Controls.Add(lblStatusValue);
        pnlStatusRow.Dock = DockStyle.Top;
        pnlStatusRow.Location = new Point(20, 75);
        pnlStatusRow.Name = "pnlStatusRow";
        pnlStatusRow.Size = new Size(784, 45);
        pnlStatusRow.TabIndex = 1;
        // 
        // btnToggleMacro
        // 
        btnToggleMacro.BackColor = Color.White;
        btnToggleMacro.FlatAppearance.BorderColor = Color.Black;
        btnToggleMacro.FlatStyle = FlatStyle.Flat;
        btnToggleMacro.Font = new Font("Segoe UI", 11F, FontStyle.Regular, GraphicsUnit.Point, 0);
        btnToggleMacro.ForeColor = Color.Black;
        btnToggleMacro.Location = new Point(10, 5);
        btnToggleMacro.Name = "btnToggleMacro";
        btnToggleMacro.Size = new Size(240, 34);
        btnToggleMacro.TabIndex = 0;
        btnToggleMacro.Text = "[      Kích hoạt (F1)      ]";
        btnToggleMacro.UseVisualStyleBackColor = false;
        btnToggleMacro.Click += BtnToggleMacro_Click;
        // 
        // lblStatusTitle
        // 
        lblStatusTitle.AutoSize = true;
        lblStatusTitle.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblStatusTitle.ForeColor = Color.Black;
        lblStatusTitle.Location = new Point(340, 10);
        lblStatusTitle.Name = "lblStatusTitle";
        lblStatusTitle.Size = new Size(106, 21);
        lblStatusTitle.TabIndex = 1;
        lblStatusTitle.Text = "TRẠNG THÁI:";
        // 
        // lblStatusValue
        // 
        lblStatusValue.AutoSize = true;
        lblStatusValue.Font = new Font("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lblStatusValue.ForeColor = Color.Red;
        lblStatusValue.Location = new Point(450, 10);
        lblStatusValue.Name = "lblStatusValue";
        lblStatusValue.Size = new Size(200, 21);
        lblStatusValue.TabIndex = 2;
        lblStatusValue.Text = "Tạm dừng (Ngoài game)";
        // 
        // lblFarmTimer1
        // 
        lblFarmTimer1.AutoSize = true;
        lblFarmTimer1.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblFarmTimer1.ForeColor = Color.Black;
        lblFarmTimer1.Location = new Point(30, 131);
        lblFarmTimer1.Name = "lblFarmTimer1";
        lblFarmTimer1.Size = new Size(160, 19);
        lblFarmTimer1.TabIndex = 2;
        lblFarmTimer1.Text = "🌾 Ruộng 1 (Ctrl+F): Chưa bật";
        // 
        // lblFarmTimerSeparator
        // 
        lblFarmTimerSeparator.AutoSize = true;
        lblFarmTimerSeparator.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblFarmTimerSeparator.ForeColor = Color.Gray;
        lblFarmTimerSeparator.Location = new Point(275, 131);
        lblFarmTimerSeparator.Name = "lblFarmTimerSeparator";
        lblFarmTimerSeparator.Size = new Size(13, 19);
        lblFarmTimerSeparator.TabIndex = 3;
        lblFarmTimerSeparator.Text = "|";
        // 
        // lblFarmTimer2
        // 
        lblFarmTimer2.AutoSize = true;
        lblFarmTimer2.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblFarmTimer2.ForeColor = Color.Black;
        lblFarmTimer2.Location = new Point(290, 131);
        lblFarmTimer2.Name = "lblFarmTimer2";
        lblFarmTimer2.Size = new Size(160, 19);
        lblFarmTimer2.TabIndex = 4;
        lblFarmTimer2.Text = "🌾 Ruộng 2 (Ctrl+G): Chưa bật";
        // 
        // lblFarmIntervalConfig
        // 
        lblFarmIntervalConfig.AutoSize = true;
        lblFarmIntervalConfig.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblFarmIntervalConfig.ForeColor = Color.Black;
        lblFarmIntervalConfig.Location = new Point(540, 131);
        lblFarmIntervalConfig.Name = "lblFarmIntervalConfig";
        lblFarmIntervalConfig.Size = new Size(115, 19);
        lblFarmIntervalConfig.TabIndex = 10;
        lblFarmIntervalConfig.Text = "Thời gian ruộng:";
        // 
        // numFarmInterval
        // 
        numFarmInterval.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        numFarmInterval.Location = new Point(660, 128);
        numFarmInterval.Maximum = new decimal(new int[] { 600, 0, 0, 0 });
        numFarmInterval.Minimum = new decimal(new int[] { 10, 0, 0, 0 });
        numFarmInterval.Name = "numFarmInterval";
        numFarmInterval.Size = new Size(65, 24);
        numFarmInterval.TabIndex = 11;
        numFarmInterval.Value = new decimal(new int[] { 200, 0, 0, 0 });
        numFarmInterval.ValueChanged += NumFarmInterval_ValueChanged;
        // 
        // lblSecondsUnit
        // 
        lblSecondsUnit.AutoSize = true;
        lblSecondsUnit.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblSecondsUnit.ForeColor = Color.Black;
        lblSecondsUnit.Location = new Point(730, 131);
        lblSecondsUnit.Name = "lblSecondsUnit";
        lblSecondsUnit.Size = new Size(33, 19);
        lblSecondsUnit.TabIndex = 12;
        lblSecondsUnit.Text = "giây";
        // 
        // lblGridTitle
        // 
        lblGridTitle.AutoSize = true;
        lblGridTitle.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblGridTitle.ForeColor = Color.Black;
        lblGridTitle.Location = new Point(30, 170);
        lblGridTitle.Name = "lblGridTitle";
        lblGridTitle.Size = new Size(100, 21);
        lblGridTitle.TabIndex = 3;
        lblGridTitle.Text = "Bảng macro:";
        // 
        // pnlKeyboardGrid
        // 
        pnlKeyboardGrid.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
        pnlKeyboardGrid.ColumnCount = 8;
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12.5F));
        pnlKeyboardGrid.Location = new Point(30, 198);
        pnlKeyboardGrid.Name = "pnlKeyboardGrid";
        pnlKeyboardGrid.RowCount = 3;
        pnlKeyboardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        pnlKeyboardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        pnlKeyboardGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        pnlKeyboardGrid.Size = new Size(764, 210);
        pnlKeyboardGrid.TabIndex = 4;
        // 
        // lblLogTitle
        // 
        lblLogTitle.AutoSize = true;
        lblLogTitle.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblLogTitle.ForeColor = Color.Black;
        lblLogTitle.Location = new Point(30, 422);
        lblLogTitle.Name = "lblLogTitle";
        lblLogTitle.Size = new Size(146, 21);
        lblLogTitle.TabIndex = 5;
        lblLogTitle.Text = "Nhật ký hoạt động:";
        // 
        // chkAutoScroll
        // 
        chkAutoScroll.AutoSize = true;
        chkAutoScroll.Checked = true;
        chkAutoScroll.CheckState = CheckState.Checked;
        chkAutoScroll.Font = new Font("Segoe UI", 9F);
        chkAutoScroll.Location = new Point(600, 425);
        chkAutoScroll.Name = "chkAutoScroll";
        chkAutoScroll.Size = new Size(101, 19);
        chkAutoScroll.TabIndex = 6;
        chkAutoScroll.Text = "Tự động cuộn";
        chkAutoScroll.UseVisualStyleBackColor = true;
        // 
        // btnClearLog
        // 
        btnClearLog.FlatStyle = FlatStyle.Flat;
        btnClearLog.Font = new Font("Segoe UI", 8.5F);
        btnClearLog.Location = new Point(709, 421);
        btnClearLog.Name = "btnClearLog";
        btnClearLog.Size = new Size(85, 25);
        btnClearLog.TabIndex = 7;
        btnClearLog.Text = "Xóa log";
        btnClearLog.UseVisualStyleBackColor = true;
        btnClearLog.Click += BtnClearLog_Click;
        // 
        // rtbLog
        // 
        rtbLog.BackColor = Color.White;
        rtbLog.BorderStyle = BorderStyle.FixedSingle;
        rtbLog.Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
        rtbLog.ForeColor = Color.Black;
        rtbLog.Location = new Point(30, 452);
        rtbLog.Name = "rtbLog";
        rtbLog.ReadOnly = true;
        rtbLog.Size = new Size(764, 138);
        rtbLog.TabIndex = 8;
        rtbLog.Text = "";
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        ClientSize = new Size(850, 640);
        Controls.Add(pnlOuterBorder);
        KeyPreview = true;
        MinimumSize = new Size(860, 650);
        Name = "MainForm";
        Padding = new Padding(12);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AOE MACRO SYSTEM";
        KeyDown += MainForm_KeyDown;
        pnlOuterBorder.ResumeLayout(false);
        pnlOuterBorder.PerformLayout();
        pnlStatusRow.ResumeLayout(false);
        pnlStatusRow.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numFarmInterval).EndInit();
        ResumeLayout(false);
    }

    #endregion

    private Panel pnlOuterBorder;
    private Label lblTitle;
    private Button btnThemeToggle;
    private Panel pnlStatusRow;
    private Button btnToggleMacro;
    private Label lblStatusTitle;
    private Label lblStatusValue;
    private Label lblFarmTimer1;
    private Label lblFarmTimerSeparator;
    private Label lblFarmTimer2;
    private Label lblFarmIntervalConfig;
    private NumericUpDown numFarmInterval;
    private Label lblSecondsUnit;
    private Label lblGridTitle;
    private TableLayoutPanel pnlKeyboardGrid;
    private Label lblLogTitle;
    private RichTextBox rtbLog;
    private Button btnClearLog;
    private CheckBox chkAutoScroll;
}
