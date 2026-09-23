namespace AOEKeyboardMacroPro;

partial class ResourceCropTestForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _lastCapturedSource?.Dispose();
            _loadedOfflineBitmap?.Dispose();
            _currentTemplateCropBmp?.Dispose();
            DisposeDigitSlots();
            DisposePopSlots();
            _popOcrService?.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        pnlHeader = new Panel();
        lblHeaderTitle = new Label();
        lblHeaderSubtitle = new Label();
        btnBack = new Button();
        pnlToolbar = new Panel();
        chkAutoRefresh = new CheckBox();
        lblInterval = new Label();
        numInterval = new NumericUpDown();
        lblIntervalUnit = new Label();
        chkTopMost = new CheckBox();
        lblZoom = new Label();
        cboZoom = new ComboBox();
        btnRefreshNow = new Button();
        btnLoadImage = new Button();
        btnUseLive = new Button();
        btnToggleTemplates = new Button();
        btnSave = new Button();
        btnReset = new Button();
        pnlOverview = new Panel();
        lblOverviewTitle = new Label();
        lblMouseCoord = new Label();
        picOverview = new PixelPictureBox();
        tblResources = new TableLayoutPanel();
        pnlGlobalAdjust = new Panel();
        lblGlobalTitle = new Label();
        btnGlobalLeft1 = new Button();
        btnGlobalRight1 = new Button();
        btnGlobalUp1 = new Button();
        btnGlobalDown1 = new Button();
        btnGlobalLeft5 = new Button();
        btnGlobalRight5 = new Button();
        btnGlobalUp5 = new Button();
        btnGlobalDown5 = new Button();
        lblGlobalSize = new Label();
        btnGlobalWMinus = new Button();
        btnGlobalWPlus = new Button();
        btnGlobalHMinus = new Button();
        btnGlobalHPlus = new Button();
        pnlTemplates = new Panel();
        pnlTemplatesHeader = new Panel();
        lblTemplatesTitle = new Label();
        chkTemplateBinarize = new CheckBox();
        btnAutoExtract = new Button();
        btnOpenFolder = new Button();
        pnlTemplatesContent = new Panel();
        pnlTemplateTool = new Panel();
        picTemplatePreview = new PixelPictureBox();
        lblTplCoord = new Label();
        lblTplX = new Label();
        numTplX = new NumericUpDown();
        btnTplLeft = new Button();
        btnTplRight = new Button();
        lblTplY = new Label();
        numTplY = new NumericUpDown();
        btnTplUp = new Button();
        btnTplDown = new Button();
        lblTplW = new Label();
        numTplW = new NumericUpDown();
        btnTplWMinus = new Button();
        btnTplWPlus = new Button();
        lblTplH = new Label();
        numTplH = new NumericUpDown();
        btnTplHMinus = new Button();
        btnTplHPlus = new Button();
        lblAssignTarget = new Label();
        cboTargetDigit = new ComboBox();
        btnAssignCurrent = new Button();
        pnlDigitSlotsContainer = new Panel();
        tblDigitSlots = new TableLayoutPanel();
        pnlStatus = new Panel();
        lblStatus = new Label();
        lblLastUpdate = new Label();
        pnlHeader.SuspendLayout();
        pnlToolbar.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numInterval).BeginInit();
        pnlOverview.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picOverview).BeginInit();
        pnlGlobalAdjust.SuspendLayout();
        pnlTemplates.SuspendLayout();
        pnlTemplatesHeader.SuspendLayout();
        pnlTemplatesContent.SuspendLayout();
        pnlTemplateTool.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)picTemplatePreview).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numTplX).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numTplY).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numTplW).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numTplH).BeginInit();
        pnlDigitSlotsContainer.SuspendLayout();
        pnlStatus.SuspendLayout();
        SuspendLayout();
        // 
        // pnlHeader
        // 
        pnlHeader.BackColor = Color.FromArgb(28, 28, 34);
        pnlHeader.Controls.Add(lblHeaderTitle);
        pnlHeader.Controls.Add(lblHeaderSubtitle);
        pnlHeader.Controls.Add(btnBack);
        pnlHeader.Dock = DockStyle.Top;
        pnlHeader.Location = new Point(0, 0);
        pnlHeader.Name = "pnlHeader";
        pnlHeader.Padding = new Padding(12, 8, 12, 6);
        pnlHeader.Size = new Size(1008, 62);
        pnlHeader.TabIndex = 0;
        // 
        // lblHeaderTitle
        // 
        lblHeaderTitle.AutoSize = true;
        lblHeaderTitle.Font = new Font("Segoe UI", 13.5F, FontStyle.Bold, GraphicsUnit.Point, 0);
        lblHeaderTitle.ForeColor = Color.White;
        lblHeaderTitle.Location = new Point(12, 8);
        lblHeaderTitle.Name = "lblHeaderTitle";
        lblHeaderTitle.Size = new Size(575, 25);
        lblHeaderTitle.TabIndex = 0;
        lblHeaderTitle.Text = "AOE RESOURCE CROP & TEMPLATE MATCHING (0 - 9)";
        // 
        // lblHeaderSubtitle
        // 
        lblHeaderSubtitle.AutoSize = true;
        lblHeaderSubtitle.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
        lblHeaderSubtitle.ForeColor = Color.FromArgb(170, 175, 185);
        lblHeaderSubtitle.Location = new Point(13, 35);
        lblHeaderSubtitle.Name = "lblHeaderSubtitle";
        lblHeaderSubtitle.Size = new Size(640, 15);
        lblHeaderSubtitle.TabIndex = 1;
        lblHeaderSubtitle.Text = "Kiểm tra 4 ô tài nguyên (Wood, Food, Gold, Stone) & Cắt trích xuất bộ template 10 chữ số (0-9) cho thuật toán nhận diện";
        // 
        // btnBack
        // 
        btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBack.BackColor = Color.FromArgb(55, 55, 68);
        btnBack.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 110);
        btnBack.FlatStyle = FlatStyle.Flat;
        btnBack.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        btnBack.ForeColor = Color.White;
        btnBack.Location = new Point(895, 14);
        btnBack.Name = "btnBack";
        btnBack.Size = new Size(100, 34);
        btnBack.TabIndex = 2;
        btnBack.Text = "🔙 Trở về";
        btnBack.UseVisualStyleBackColor = false;
        btnBack.Click += BtnBack_Click;
        // 
        // pnlToolbar
        // 
        pnlToolbar.BackColor = Color.FromArgb(34, 34, 42);
        pnlToolbar.Controls.Add(chkAutoRefresh);
        pnlToolbar.Controls.Add(lblInterval);
        pnlToolbar.Controls.Add(numInterval);
        pnlToolbar.Controls.Add(lblIntervalUnit);
        pnlToolbar.Controls.Add(chkTopMost);
        pnlToolbar.Controls.Add(lblZoom);
        pnlToolbar.Controls.Add(cboZoom);
        pnlToolbar.Controls.Add(btnRefreshNow);
        pnlToolbar.Controls.Add(btnLoadImage);
        pnlToolbar.Controls.Add(btnUseLive);
        pnlToolbar.Controls.Add(btnToggleTemplates);
        pnlToolbar.Controls.Add(btnSave);
        pnlToolbar.Controls.Add(btnReset);
        pnlToolbar.Dock = DockStyle.Top;
        pnlToolbar.Location = new Point(0, 62);
        pnlToolbar.Name = "pnlToolbar";
        pnlToolbar.Padding = new Padding(10, 6, 10, 6);
        pnlToolbar.Size = new Size(1008, 44);
        pnlToolbar.TabIndex = 1;
        // 
        // chkAutoRefresh
        // 
        chkAutoRefresh.AutoSize = true;
        chkAutoRefresh.Checked = true;
        chkAutoRefresh.CheckState = CheckState.Checked;
        chkAutoRefresh.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        chkAutoRefresh.ForeColor = Color.FromArgb(70, 210, 130);
        chkAutoRefresh.Location = new Point(10, 12);
        chkAutoRefresh.Name = "chkAutoRefresh";
        chkAutoRefresh.Size = new Size(116, 19);
        chkAutoRefresh.TabIndex = 0;
        chkAutoRefresh.Text = "Tự làm mới (0.5s)";
        chkAutoRefresh.UseVisualStyleBackColor = true;
        chkAutoRefresh.CheckedChanged += ChkAutoRefresh_CheckedChanged;
        // 
        // lblInterval
        // 
        lblInterval.AutoSize = true;
        lblInterval.ForeColor = Color.LightGray;
        lblInterval.Location = new Point(128, 13);
        lblInterval.Name = "lblInterval";
        lblInterval.Size = new Size(39, 15);
        lblInterval.TabIndex = 1;
        lblInterval.Text = "Chu kỳ:";
        // 
        // numInterval
        // 
        numInterval.BackColor = Color.FromArgb(45, 45, 55);
        numInterval.ForeColor = Color.White;
        numInterval.Increment = new decimal(new int[] { 100, 0, 0, 0 });
        numInterval.Location = new Point(170, 10);
        numInterval.Maximum = new decimal(new int[] { 5000, 0, 0, 0 });
        numInterval.Minimum = new decimal(new int[] { 100, 0, 0, 0 });
        numInterval.Name = "numInterval";
        numInterval.Size = new Size(55, 23);
        numInterval.TabIndex = 2;
        numInterval.Value = new decimal(new int[] { 500, 0, 0, 0 });
        numInterval.ValueChanged += NumInterval_ValueChanged;
        // 
        // lblIntervalUnit
        // 
        lblIntervalUnit.AutoSize = true;
        lblIntervalUnit.ForeColor = Color.LightGray;
        lblIntervalUnit.Location = new Point(228, 13);
        lblIntervalUnit.Name = "lblIntervalUnit";
        lblIntervalUnit.Size = new Size(23, 15);
        lblIntervalUnit.TabIndex = 3;
        lblIntervalUnit.Text = "ms";
        // 
        // chkTopMost
        // 
        chkTopMost.AutoSize = true;
        chkTopMost.Checked = true;
        chkTopMost.CheckState = CheckState.Checked;
        chkTopMost.ForeColor = Color.White;
        chkTopMost.Location = new Point(262, 12);
        chkTopMost.Name = "chkTopMost";
        chkTopMost.Size = new Size(71, 19);
        chkTopMost.TabIndex = 4;
        chkTopMost.Text = "TopMost";
        chkTopMost.UseVisualStyleBackColor = true;
        chkTopMost.CheckedChanged += ChkTopMost_CheckedChanged;
        // 
        // lblZoom
        // 
        lblZoom.AutoSize = true;
        lblZoom.ForeColor = Color.LightGray;
        lblZoom.Location = new Point(340, 13);
        lblZoom.Name = "lblZoom";
        lblZoom.Size = new Size(42, 15);
        lblZoom.TabIndex = 5;
        lblZoom.Text = "Phóng:";
        // 
        // cboZoom
        // 
        cboZoom.BackColor = Color.FromArgb(45, 45, 55);
        cboZoom.DropDownStyle = ComboBoxStyle.DropDownList;
        cboZoom.FlatStyle = FlatStyle.Flat;
        cboZoom.ForeColor = Color.White;
        cboZoom.FormattingEnabled = true;
        cboZoom.Items.AddRange(new object[] { "1x (Gốc)", "2x", "3x", "4x" });
        cboZoom.Location = new Point(384, 9);
        cboZoom.Name = "cboZoom";
        cboZoom.Size = new Size(70, 23);
        cboZoom.TabIndex = 6;
        cboZoom.SelectedIndexChanged += CboZoom_SelectedIndexChanged;
        // 
        // btnRefreshNow
        // 
        btnRefreshNow.BackColor = Color.FromArgb(50, 50, 62);
        btnRefreshNow.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 95);
        btnRefreshNow.FlatStyle = FlatStyle.Flat;
        btnRefreshNow.ForeColor = Color.White;
        btnRefreshNow.Location = new Point(460, 8);
        btnRefreshNow.Name = "btnRefreshNow";
        btnRefreshNow.Size = new Size(68, 26);
        btnRefreshNow.TabIndex = 7;
        btnRefreshNow.Text = "🔄 Chụp";
        btnRefreshNow.UseVisualStyleBackColor = false;
        btnRefreshNow.Click += BtnRefreshNow_Click;
        // 
        // btnLoadImage
        // 
        btnLoadImage.BackColor = Color.FromArgb(50, 50, 62);
        btnLoadImage.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 95);
        btnLoadImage.FlatStyle = FlatStyle.Flat;
        btnLoadImage.ForeColor = Color.White;
        btnLoadImage.Location = new Point(532, 8);
        btnLoadImage.Name = "btnLoadImage";
        btnLoadImage.Size = new Size(75, 26);
        btnLoadImage.TabIndex = 8;
        btnLoadImage.Text = "📁 Nạp ảnh";
        btnLoadImage.UseVisualStyleBackColor = false;
        btnLoadImage.Click += BtnLoadImage_Click;
        // 
        // btnUseLive
        // 
        btnUseLive.BackColor = Color.FromArgb(50, 50, 62);
        btnUseLive.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 95);
        btnUseLive.FlatStyle = FlatStyle.Flat;
        btnUseLive.ForeColor = Color.LightSkyBlue;
        btnUseLive.Location = new Point(611, 8);
        btnUseLive.Name = "btnUseLive";
        btnUseLive.Size = new Size(82, 26);
        btnUseLive.TabIndex = 9;
        btnUseLive.Text = "🖥️ Live Game";
        btnUseLive.UseVisualStyleBackColor = false;
        btnUseLive.Click += BtnUseLive_Click;
        // 
        // btnToggleTemplates
        // 
        btnToggleTemplates.BackColor = Color.FromArgb(60, 50, 80);
        btnToggleTemplates.FlatAppearance.BorderColor = Color.FromArgb(120, 90, 160);
        btnToggleTemplates.FlatStyle = FlatStyle.Flat;
        btnToggleTemplates.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnToggleTemplates.ForeColor = Color.FromArgb(220, 190, 255);
        btnToggleTemplates.Location = new Point(698, 8);
        btnToggleTemplates.Name = "btnToggleTemplates";
        btnToggleTemplates.Size = new Size(130, 26);
        btnToggleTemplates.TabIndex = 10;
        btnToggleTemplates.Text = "✂️ Mẫu 0-9 (Hiện)";
        btnToggleTemplates.UseVisualStyleBackColor = false;
        btnToggleTemplates.Click += BtnToggleTemplates_Click;
        // 
        // btnSave
        // 
        btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnSave.BackColor = Color.FromArgb(40, 90, 50);
        btnSave.FlatAppearance.BorderColor = Color.FromArgb(60, 140, 80);
        btnSave.FlatStyle = FlatStyle.Flat;
        btnSave.ForeColor = Color.White;
        btnSave.Location = new Point(844, 8);
        btnSave.Name = "btnSave";
        btnSave.Size = new Size(76, 26);
        btnSave.TabIndex = 11;
        btnSave.Text = "💾 Lưu tọa độ";
        btnSave.UseVisualStyleBackColor = false;
        btnSave.Click += BtnSave_Click;
        // 
        // btnReset
        // 
        btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnReset.BackColor = Color.FromArgb(60, 45, 45);
        btnReset.FlatAppearance.BorderColor = Color.FromArgb(100, 70, 70);
        btnReset.FlatStyle = FlatStyle.Flat;
        btnReset.ForeColor = Color.White;
        btnReset.Location = new Point(924, 8);
        btnReset.Name = "btnReset";
        btnReset.Size = new Size(72, 26);
        btnReset.TabIndex = 12;
        btnReset.Text = "↺ Reset";
        btnReset.UseVisualStyleBackColor = false;
        btnReset.Click += BtnReset_Click;
        // 
        // pnlOverview
        // 
        pnlOverview.BackColor = Color.FromArgb(24, 24, 30);
        pnlOverview.BorderStyle = BorderStyle.FixedSingle;
        pnlOverview.Controls.Add(lblOverviewTitle);
        pnlOverview.Controls.Add(lblMouseCoord);
        pnlOverview.Controls.Add(picOverview);
        pnlOverview.Dock = DockStyle.Top;
        pnlOverview.Location = new Point(0, 106);
        pnlOverview.Name = "pnlOverview";
        pnlOverview.Padding = new Padding(10);
        pnlOverview.Size = new Size(1008, 130);
        pnlOverview.TabIndex = 2;
        // 
        // lblOverviewTitle
        // 
        lblOverviewTitle.AutoSize = true;
        lblOverviewTitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblOverviewTitle.ForeColor = Color.FromArgb(220, 225, 235);
        lblOverviewTitle.Location = new Point(10, 6);
        lblOverviewTitle.Name = "lblOverviewTitle";
        lblOverviewTitle.Size = new Size(540, 17);
        lblOverviewTitle.TabIndex = 0;
        lblOverviewTitle.Text = "TOÀN CẢNH THANH TÀI NGUYÊN [Click vào số bất kỳ để định vị khung cắt Template 0-9]";
        // 
        // lblMouseCoord
        // 
        lblMouseCoord.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblMouseCoord.AutoSize = true;
        lblMouseCoord.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        lblMouseCoord.ForeColor = Color.Khaki;
        lblMouseCoord.Location = new Point(730, 7);
        lblMouseCoord.Name = "lblMouseCoord";
        lblMouseCoord.Size = new Size(185, 15);
        lblMouseCoord.TabIndex = 1;
        lblMouseCoord.Text = "Tọa độ chuột trên ảnh: X = -, Y = -";
        // 
        // picOverview
        // 
        picOverview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        picOverview.BackColor = Color.Black;
        picOverview.BorderStyle = BorderStyle.FixedSingle;
        picOverview.Cursor = Cursors.Cross;
        picOverview.Location = new Point(10, 28);
        picOverview.Name = "picOverview";
        picOverview.Size = new Size(986, 90);
        picOverview.SizeMode = PictureBoxSizeMode.Zoom;
        picOverview.TabIndex = 2;
        picOverview.TabStop = false;
        picOverview.MouseDown += PicOverview_MouseDown;
        picOverview.MouseMove += PicOverview_MouseMove;
        picOverview.MouseLeave += PicOverview_MouseLeave;
        // 
        // tblResources
        // 
        tblResources.ColumnCount = 4;
        tblResources.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tblResources.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tblResources.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tblResources.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        tblResources.Dock = DockStyle.Fill;
        tblResources.Location = new Point(0, 236);
        tblResources.Name = "tblResources";
        tblResources.Padding = new Padding(6);
        tblResources.RowCount = 1;
        tblResources.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tblResources.Size = new Size(1008, 252);
        tblResources.TabIndex = 3;
        // 
        // pnlGlobalAdjust
        // 
        pnlGlobalAdjust.BackColor = Color.FromArgb(28, 28, 34);
        pnlGlobalAdjust.BorderStyle = BorderStyle.FixedSingle;
        pnlGlobalAdjust.Controls.Add(lblGlobalTitle);
        pnlGlobalAdjust.Controls.Add(btnGlobalLeft1);
        pnlGlobalAdjust.Controls.Add(btnGlobalRight1);
        pnlGlobalAdjust.Controls.Add(btnGlobalUp1);
        pnlGlobalAdjust.Controls.Add(btnGlobalDown1);
        pnlGlobalAdjust.Controls.Add(btnGlobalLeft5);
        pnlGlobalAdjust.Controls.Add(btnGlobalRight5);
        pnlGlobalAdjust.Controls.Add(btnGlobalUp5);
        pnlGlobalAdjust.Controls.Add(btnGlobalDown5);
        pnlGlobalAdjust.Controls.Add(lblGlobalSize);
        pnlGlobalAdjust.Controls.Add(btnGlobalWMinus);
        pnlGlobalAdjust.Controls.Add(btnGlobalWPlus);
        pnlGlobalAdjust.Controls.Add(btnGlobalHMinus);
        pnlGlobalAdjust.Controls.Add(btnGlobalHPlus);
        pnlGlobalAdjust.Dock = DockStyle.Bottom;
        pnlGlobalAdjust.Location = new Point(0, 488);
        pnlGlobalAdjust.Name = "pnlGlobalAdjust";
        pnlGlobalAdjust.Padding = new Padding(8, 4, 8, 4);
        pnlGlobalAdjust.Size = new Size(1008, 40);
        pnlGlobalAdjust.TabIndex = 4;
        // 
        // lblGlobalTitle
        // 
        lblGlobalTitle.AutoSize = true;
        lblGlobalTitle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblGlobalTitle.ForeColor = Color.FromArgb(220, 220, 230);
        lblGlobalTitle.Location = new Point(8, 11);
        lblGlobalTitle.Name = "lblGlobalTitle";
        lblGlobalTitle.Size = new Size(150, 15);
        lblGlobalTitle.TabIndex = 0;
        lblGlobalTitle.Text = "Dịch 4 ô (Ấn giữ lặp lại):";
        // 
        // btnGlobalLeft1
        // 
        btnGlobalLeft1.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalLeft1.FlatStyle = FlatStyle.Flat;
        btnGlobalLeft1.Font = new Font("Segoe UI", 8F);
        btnGlobalLeft1.ForeColor = Color.White;
        btnGlobalLeft1.Location = new Point(164, 7);
        btnGlobalLeft1.Name = "btnGlobalLeft1";
        btnGlobalLeft1.Size = new Size(48, 25);
        btnGlobalLeft1.TabIndex = 1;
        btnGlobalLeft1.Text = "◀ 1px";
        btnGlobalLeft1.UseVisualStyleBackColor = false;
        // 
        // btnGlobalRight1
        // 
        btnGlobalRight1.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalRight1.FlatStyle = FlatStyle.Flat;
        btnGlobalRight1.Font = new Font("Segoe UI", 8F);
        btnGlobalRight1.ForeColor = Color.White;
        btnGlobalRight1.Location = new Point(216, 7);
        btnGlobalRight1.Name = "btnGlobalRight1";
        btnGlobalRight1.Size = new Size(48, 25);
        btnGlobalRight1.TabIndex = 2;
        btnGlobalRight1.Text = "▶ 1px";
        btnGlobalRight1.UseVisualStyleBackColor = false;
        // 
        // btnGlobalUp1
        // 
        btnGlobalUp1.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalUp1.FlatStyle = FlatStyle.Flat;
        btnGlobalUp1.Font = new Font("Segoe UI", 8F);
        btnGlobalUp1.ForeColor = Color.White;
        btnGlobalUp1.Location = new Point(268, 7);
        btnGlobalUp1.Name = "btnGlobalUp1";
        btnGlobalUp1.Size = new Size(48, 25);
        btnGlobalUp1.TabIndex = 3;
        btnGlobalUp1.Text = "▲ 1px";
        btnGlobalUp1.UseVisualStyleBackColor = false;
        // 
        // btnGlobalDown1
        // 
        btnGlobalDown1.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalDown1.FlatStyle = FlatStyle.Flat;
        btnGlobalDown1.Font = new Font("Segoe UI", 8F);
        btnGlobalDown1.ForeColor = Color.White;
        btnGlobalDown1.Location = new Point(320, 7);
        btnGlobalDown1.Name = "btnGlobalDown1";
        btnGlobalDown1.Size = new Size(48, 25);
        btnGlobalDown1.TabIndex = 4;
        btnGlobalDown1.Text = "▼ 1px";
        btnGlobalDown1.UseVisualStyleBackColor = false;
        // 
        // btnGlobalLeft5
        // 
        btnGlobalLeft5.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalLeft5.FlatStyle = FlatStyle.Flat;
        btnGlobalLeft5.Font = new Font("Segoe UI", 8F);
        btnGlobalLeft5.ForeColor = Color.LightSkyBlue;
        btnGlobalLeft5.Location = new Point(378, 7);
        btnGlobalLeft5.Name = "btnGlobalLeft5";
        btnGlobalLeft5.Size = new Size(48, 25);
        btnGlobalLeft5.TabIndex = 5;
        btnGlobalLeft5.Text = "◀ 5px";
        btnGlobalLeft5.UseVisualStyleBackColor = false;
        // 
        // btnGlobalRight5
        // 
        btnGlobalRight5.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalRight5.FlatStyle = FlatStyle.Flat;
        btnGlobalRight5.Font = new Font("Segoe UI", 8F);
        btnGlobalRight5.ForeColor = Color.LightSkyBlue;
        btnGlobalRight5.Location = new Point(430, 7);
        btnGlobalRight5.Name = "btnGlobalRight5";
        btnGlobalRight5.Size = new Size(48, 25);
        btnGlobalRight5.TabIndex = 6;
        btnGlobalRight5.Text = "▶ 5px";
        btnGlobalRight5.UseVisualStyleBackColor = false;
        // 
        // btnGlobalUp5
        // 
        btnGlobalUp5.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalUp5.FlatStyle = FlatStyle.Flat;
        btnGlobalUp5.Font = new Font("Segoe UI", 8F);
        btnGlobalUp5.ForeColor = Color.LightSkyBlue;
        btnGlobalUp5.Location = new Point(482, 7);
        btnGlobalUp5.Name = "btnGlobalUp5";
        btnGlobalUp5.Size = new Size(48, 25);
        btnGlobalUp5.TabIndex = 7;
        btnGlobalUp5.Text = "▲ 5px";
        btnGlobalUp5.UseVisualStyleBackColor = false;
        // 
        // btnGlobalDown5
        // 
        btnGlobalDown5.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalDown5.FlatStyle = FlatStyle.Flat;
        btnGlobalDown5.Font = new Font("Segoe UI", 8F);
        btnGlobalDown5.ForeColor = Color.LightSkyBlue;
        btnGlobalDown5.Location = new Point(534, 7);
        btnGlobalDown5.Name = "btnGlobalDown5";
        btnGlobalDown5.Size = new Size(48, 25);
        btnGlobalDown5.TabIndex = 8;
        btnGlobalDown5.Text = "▼ 5px";
        btnGlobalDown5.UseVisualStyleBackColor = false;
        // 
        // lblGlobalSize
        // 
        lblGlobalSize.AutoSize = true;
        lblGlobalSize.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        lblGlobalSize.ForeColor = Color.FromArgb(220, 220, 230);
        lblGlobalSize.Location = new Point(595, 11);
        lblGlobalSize.Name = "lblGlobalSize";
        lblGlobalSize.Size = new Size(71, 15);
        lblGlobalSize.TabIndex = 9;
        lblGlobalSize.Text = "Kích thước:";
        // 
        // btnGlobalWMinus
        // 
        btnGlobalWMinus.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalWMinus.FlatStyle = FlatStyle.Flat;
        btnGlobalWMinus.Font = new Font("Segoe UI", 8F);
        btnGlobalWMinus.ForeColor = Color.White;
        btnGlobalWMinus.Location = new Point(670, 7);
        btnGlobalWMinus.Name = "btnGlobalWMinus";
        btnGlobalWMinus.Size = new Size(50, 25);
        btnGlobalWMinus.TabIndex = 10;
        btnGlobalWMinus.Text = "W - 2";
        btnGlobalWMinus.UseVisualStyleBackColor = false;
        // 
        // btnGlobalWPlus
        // 
        btnGlobalWPlus.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalWPlus.FlatStyle = FlatStyle.Flat;
        btnGlobalWPlus.Font = new Font("Segoe UI", 8F);
        btnGlobalWPlus.ForeColor = Color.White;
        btnGlobalWPlus.Location = new Point(724, 7);
        btnGlobalWPlus.Name = "btnGlobalWPlus";
        btnGlobalWPlus.Size = new Size(50, 25);
        btnGlobalWPlus.TabIndex = 11;
        btnGlobalWPlus.Text = "W + 2";
        btnGlobalWPlus.UseVisualStyleBackColor = false;
        // 
        // btnGlobalHMinus
        // 
        btnGlobalHMinus.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalHMinus.FlatStyle = FlatStyle.Flat;
        btnGlobalHMinus.Font = new Font("Segoe UI", 8F);
        btnGlobalHMinus.ForeColor = Color.White;
        btnGlobalHMinus.Location = new Point(782, 7);
        btnGlobalHMinus.Name = "btnGlobalHMinus";
        btnGlobalHMinus.Size = new Size(50, 25);
        btnGlobalHMinus.TabIndex = 12;
        btnGlobalHMinus.Text = "H - 2";
        btnGlobalHMinus.UseVisualStyleBackColor = false;
        // 
        // btnGlobalHPlus
        // 
        btnGlobalHPlus.BackColor = Color.FromArgb(45, 45, 55);
        btnGlobalHPlus.FlatStyle = FlatStyle.Flat;
        btnGlobalHPlus.Font = new Font("Segoe UI", 8F);
        btnGlobalHPlus.ForeColor = Color.White;
        btnGlobalHPlus.Location = new Point(836, 7);
        btnGlobalHPlus.Name = "btnGlobalHPlus";
        btnGlobalHPlus.Size = new Size(50, 25);
        btnGlobalHPlus.TabIndex = 13;
        btnGlobalHPlus.Text = "H + 2";
        btnGlobalHPlus.UseVisualStyleBackColor = false;
        // 
        // pnlTemplates
        // 
        pnlTemplates.BackColor = Color.FromArgb(22, 22, 28);
        pnlTemplates.BorderStyle = BorderStyle.FixedSingle;
        pnlTemplates.Controls.Add(pnlTemplatesContent);
        pnlTemplates.Controls.Add(pnlTemplatesHeader);
        pnlTemplates.Dock = DockStyle.Bottom;
        pnlTemplates.Location = new Point(0, 528);
        pnlTemplates.Name = "pnlTemplates";
        pnlTemplates.Size = new Size(1008, 230);
        pnlTemplates.TabIndex = 5;
        // 
        // pnlTemplatesHeader
        // 
        pnlTemplatesHeader.BackColor = Color.FromArgb(32, 28, 42);
        pnlTemplatesHeader.Controls.Add(lblTemplatesTitle);
        pnlTemplatesHeader.Controls.Add(chkTemplateBinarize);
        pnlTemplatesHeader.Controls.Add(btnAutoExtract);
        pnlTemplatesHeader.Controls.Add(btnOpenFolder);
        pnlTemplatesHeader.Dock = DockStyle.Top;
        pnlTemplatesHeader.Location = new Point(0, 0);
        pnlTemplatesHeader.Name = "pnlTemplatesHeader";
        pnlTemplatesHeader.Padding = new Padding(10, 4, 10, 4);
        pnlTemplatesHeader.Size = new Size(1006, 32);
        pnlTemplatesHeader.TabIndex = 0;
        // 
        // lblTemplatesTitle
        // 
        lblTemplatesTitle.AutoSize = true;
        lblTemplatesTitle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        lblTemplatesTitle.ForeColor = Color.FromArgb(215, 185, 255);
        lblTemplatesTitle.Location = new Point(10, 6);
        lblTemplatesTitle.Name = "lblTemplatesTitle";
        lblTemplatesTitle.Size = new Size(330, 17);
        lblTemplatesTitle.TabIndex = 0;
        lblTemplatesTitle.Text = "✂️ BỘ TEMPLATE 10 CHỮ SỐ (0 -> 9) CHO MATCHING";
        // 
        // chkTemplateBinarize
        // 
        chkTemplateBinarize.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        chkTemplateBinarize.AutoSize = true;
        chkTemplateBinarize.Checked = true;
        chkTemplateBinarize.CheckState = CheckState.Checked;
        chkTemplateBinarize.ForeColor = Color.White;
        chkTemplateBinarize.Location = new Point(560, 6);
        chkTemplateBinarize.Name = "chkTemplateBinarize";
        chkTemplateBinarize.Size = new Size(160, 19);
        chkTemplateBinarize.TabIndex = 1;
        chkTemplateBinarize.Text = "Nhị phân hóa (Đen/Trắng)";
        chkTemplateBinarize.UseVisualStyleBackColor = true;
        chkTemplateBinarize.CheckedChanged += ChkTemplateBinarize_CheckedChanged;
        // 
        // btnAutoExtract
        // 
        btnAutoExtract.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnAutoExtract.BackColor = Color.FromArgb(60, 50, 85);
        btnAutoExtract.FlatAppearance.BorderColor = Color.FromArgb(110, 90, 150);
        btnAutoExtract.FlatStyle = FlatStyle.Flat;
        btnAutoExtract.Font = new Font("Segoe UI", 8.5F);
        btnAutoExtract.ForeColor = Color.White;
        btnAutoExtract.Location = new Point(730, 3);
        btnAutoExtract.Name = "btnAutoExtract";
        btnAutoExtract.Size = new Size(130, 24);
        btnAutoExtract.TabIndex = 2;
        btnAutoExtract.Text = "⚡ Tự trích xuất mẫu";
        btnAutoExtract.UseVisualStyleBackColor = false;
        btnAutoExtract.Click += BtnAutoExtract_Click;
        // 
        // btnOpenFolder
        // 
        btnOpenFolder.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnOpenFolder.BackColor = Color.FromArgb(50, 50, 65);
        btnOpenFolder.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 110);
        btnOpenFolder.FlatStyle = FlatStyle.Flat;
        btnOpenFolder.Font = new Font("Segoe UI", 8.5F);
        btnOpenFolder.ForeColor = Color.White;
        btnOpenFolder.Location = new Point(868, 3);
        btnOpenFolder.Name = "btnOpenFolder";
        btnOpenFolder.Size = new Size(128, 24);
        btnOpenFolder.TabIndex = 3;
        btnOpenFolder.Text = "📁 Mở thư mục mẫu";
        btnOpenFolder.UseVisualStyleBackColor = false;
        btnOpenFolder.Click += BtnOpenFolder_Click;
        // 
        // pnlTemplatesContent
        // 
        pnlTemplatesContent.Controls.Add(pnlDigitSlotsContainer);
        pnlTemplatesContent.Controls.Add(pnlTemplateTool);
        pnlTemplatesContent.Dock = DockStyle.Fill;
        pnlTemplatesContent.Location = new Point(0, 32);
        pnlTemplatesContent.Name = "pnlTemplatesContent";
        pnlTemplatesContent.Padding = new Padding(6);
        pnlTemplatesContent.Size = new Size(1006, 196);
        pnlTemplatesContent.TabIndex = 1;
        // 
        // pnlTemplateTool
        // 
        pnlTemplateTool.BackColor = Color.FromArgb(28, 28, 36);
        pnlTemplateTool.BorderStyle = BorderStyle.FixedSingle;
        pnlTemplateTool.Controls.Add(picTemplatePreview);
        pnlTemplateTool.Controls.Add(lblTplCoord);
        pnlTemplateTool.Controls.Add(lblTplX);
        pnlTemplateTool.Controls.Add(numTplX);
        pnlTemplateTool.Controls.Add(btnTplLeft);
        pnlTemplateTool.Controls.Add(btnTplRight);
        pnlTemplateTool.Controls.Add(lblTplY);
        pnlTemplateTool.Controls.Add(numTplY);
        pnlTemplateTool.Controls.Add(btnTplUp);
        pnlTemplateTool.Controls.Add(btnTplDown);
        pnlTemplateTool.Controls.Add(lblTplW);
        pnlTemplateTool.Controls.Add(numTplW);
        pnlTemplateTool.Controls.Add(btnTplWMinus);
        pnlTemplateTool.Controls.Add(btnTplWPlus);
        pnlTemplateTool.Controls.Add(lblTplH);
        pnlTemplateTool.Controls.Add(numTplH);
        pnlTemplateTool.Controls.Add(btnTplHMinus);
        pnlTemplateTool.Controls.Add(btnTplHPlus);
        pnlTemplateTool.Controls.Add(lblAssignTarget);
        pnlTemplateTool.Controls.Add(cboTargetDigit);
        pnlTemplateTool.Controls.Add(btnAssignCurrent);
        pnlTemplateTool.Dock = DockStyle.Left;
        pnlTemplateTool.Location = new Point(6, 6);
        pnlTemplateTool.Name = "pnlTemplateTool";
        pnlTemplateTool.Padding = new Padding(6);
        pnlTemplateTool.Size = new Size(295, 184);
        pnlTemplateTool.TabIndex = 0;
        // 
        // picTemplatePreview
        // 
        picTemplatePreview.BackColor = Color.Black;
        picTemplatePreview.BorderStyle = BorderStyle.FixedSingle;
        picTemplatePreview.Location = new Point(8, 8);
        picTemplatePreview.Name = "picTemplatePreview";
        picTemplatePreview.Size = new Size(72, 72);
        picTemplatePreview.SizeMode = PictureBoxSizeMode.CenterImage;
        picTemplatePreview.TabIndex = 0;
        picTemplatePreview.TabStop = false;
        // 
        // lblTplCoord
        // 
        lblTplCoord.AutoSize = true;
        lblTplCoord.Font = new Font("Segoe UI", 7.5F);
        lblTplCoord.ForeColor = Color.Yellow;
        lblTplCoord.Location = new Point(8, 82);
        lblTplCoord.Name = "lblTplCoord";
        lblTplCoord.Size = new Size(68, 12);
        lblTplCoord.TabIndex = 1;
        lblTplCoord.Text = "Preview (4x)";
        // 
        // lblTplX
        // 
        lblTplX.AutoSize = true;
        lblTplX.ForeColor = Color.White;
        lblTplX.Location = new Point(86, 10);
        lblTplX.Name = "lblTplX";
        lblTplX.Size = new Size(17, 15);
        lblTplX.TabIndex = 2;
        lblTplX.Text = "X:";
        // 
        // numTplX
        // 
        numTplX.BackColor = Color.FromArgb(45, 45, 55);
        numTplX.ForeColor = Color.White;
        numTplX.Location = new Point(106, 7);
        numTplX.Maximum = new decimal(new int[] { 3840, 0, 0, 0 });
        numTplX.Name = "numTplX";
        numTplX.Size = new Size(54, 23);
        numTplX.TabIndex = 3;
        numTplX.Value = new decimal(new int[] { 25, 0, 0, 0 });
        // 
        // btnTplLeft
        // 
        btnTplLeft.BackColor = Color.FromArgb(50, 50, 60);
        btnTplLeft.FlatStyle = FlatStyle.Flat;
        btnTplLeft.Font = new Font("Segoe UI", 7.5F);
        btnTplLeft.ForeColor = Color.White;
        btnTplLeft.Location = new Point(164, 7);
        btnTplLeft.Name = "btnTplLeft";
        btnTplLeft.Size = new Size(24, 23);
        btnTplLeft.TabIndex = 4;
        btnTplLeft.Text = "◀";
        btnTplLeft.UseVisualStyleBackColor = false;
        // 
        // btnTplRight
        // 
        btnTplRight.BackColor = Color.FromArgb(50, 50, 60);
        btnTplRight.FlatStyle = FlatStyle.Flat;
        btnTplRight.Font = new Font("Segoe UI", 7.5F);
        btnTplRight.ForeColor = Color.White;
        btnTplRight.Location = new Point(190, 7);
        btnTplRight.Name = "btnTplRight";
        btnTplRight.Size = new Size(24, 23);
        btnTplRight.TabIndex = 5;
        btnTplRight.Text = "▶";
        btnTplRight.UseVisualStyleBackColor = false;
        // 
        // lblTplY
        // 
        lblTplY.AutoSize = true;
        lblTplY.ForeColor = Color.White;
        lblTplY.Location = new Point(86, 37);
        lblTplY.Name = "lblTplY";
        lblTplY.Size = new Size(17, 15);
        lblTplY.TabIndex = 6;
        lblTplY.Text = "Y:";
        // 
        // numTplY
        // 
        numTplY.BackColor = Color.FromArgb(45, 45, 55);
        numTplY.ForeColor = Color.White;
        numTplY.Location = new Point(106, 34);
        numTplY.Maximum = new decimal(new int[] { 2160, 0, 0, 0 });
        numTplY.Name = "numTplY";
        numTplY.Size = new Size(54, 23);
        numTplY.TabIndex = 7;
        numTplY.Value = new decimal(new int[] { 4, 0, 0, 0 });
        // 
        // btnTplUp
        // 
        btnTplUp.BackColor = Color.FromArgb(50, 50, 60);
        btnTplUp.FlatStyle = FlatStyle.Flat;
        btnTplUp.Font = new Font("Segoe UI", 7.5F);
        btnTplUp.ForeColor = Color.White;
        btnTplUp.Location = new Point(164, 34);
        btnTplUp.Name = "btnTplUp";
        btnTplUp.Size = new Size(24, 23);
        btnTplUp.TabIndex = 8;
        btnTplUp.Text = "▲";
        btnTplUp.UseVisualStyleBackColor = false;
        // 
        // btnTplDown
        // 
        btnTplDown.BackColor = Color.FromArgb(50, 50, 60);
        btnTplDown.FlatStyle = FlatStyle.Flat;
        btnTplDown.Font = new Font("Segoe UI", 7.5F);
        btnTplDown.ForeColor = Color.White;
        btnTplDown.Location = new Point(190, 34);
        btnTplDown.Name = "btnTplDown";
        btnTplDown.Size = new Size(24, 23);
        btnTplDown.TabIndex = 9;
        btnTplDown.Text = "▼";
        btnTplDown.UseVisualStyleBackColor = false;
        // 
        // lblTplW
        // 
        lblTplW.AutoSize = true;
        lblTplW.ForeColor = Color.White;
        lblTplW.Location = new Point(86, 64);
        lblTplW.Name = "lblTplW";
        lblTplW.Size = new Size(20, 15);
        lblTplW.TabIndex = 10;
        lblTplW.Text = "W:";
        // 
        // numTplW
        // 
        numTplW.BackColor = Color.FromArgb(45, 45, 55);
        numTplW.ForeColor = Color.White;
        numTplW.Location = new Point(106, 61);
        numTplW.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
        numTplW.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
        numTplW.Name = "numTplW";
        numTplW.Size = new Size(54, 23);
        numTplW.TabIndex = 11;
        numTplW.Value = new decimal(new int[] { 8, 0, 0, 0 });
        // 
        // btnTplWMinus
        // 
        btnTplWMinus.BackColor = Color.FromArgb(50, 50, 60);
        btnTplWMinus.FlatStyle = FlatStyle.Flat;
        btnTplWMinus.Font = new Font("Segoe UI", 7.5F);
        btnTplWMinus.ForeColor = Color.White;
        btnTplWMinus.Location = new Point(164, 61);
        btnTplWMinus.Name = "btnTplWMinus";
        btnTplWMinus.Size = new Size(24, 23);
        btnTplWMinus.TabIndex = 12;
        btnTplWMinus.Text = "-";
        btnTplWMinus.UseVisualStyleBackColor = false;
        // 
        // btnTplWPlus
        // 
        btnTplWPlus.BackColor = Color.FromArgb(50, 50, 60);
        btnTplWPlus.FlatStyle = FlatStyle.Flat;
        btnTplWPlus.Font = new Font("Segoe UI", 7.5F);
        btnTplWPlus.ForeColor = Color.White;
        btnTplWPlus.Location = new Point(190, 61);
        btnTplWPlus.Name = "btnTplWPlus";
        btnTplWPlus.Size = new Size(24, 23);
        btnTplWPlus.TabIndex = 13;
        btnTplWPlus.Text = "+";
        btnTplWPlus.UseVisualStyleBackColor = false;
        // 
        // lblTplH
        // 
        lblTplH.AutoSize = true;
        lblTplH.ForeColor = Color.White;
        lblTplH.Location = new Point(86, 91);
        lblTplH.Name = "lblTplH";
        lblTplH.Size = new Size(19, 15);
        lblTplH.TabIndex = 14;
        lblTplH.Text = "H:";
        // 
        // numTplH
        // 
        numTplH.BackColor = Color.FromArgb(45, 45, 55);
        numTplH.ForeColor = Color.White;
        numTplH.Location = new Point(106, 88);
        numTplH.Maximum = new decimal(new int[] { 100, 0, 0, 0 });
        numTplH.Minimum = new decimal(new int[] { 2, 0, 0, 0 });
        numTplH.Name = "numTplH";
        numTplH.Size = new Size(54, 23);
        numTplH.TabIndex = 15;
        numTplH.Value = new decimal(new int[] { 13, 0, 0, 0 });
        // 
        // btnTplHMinus
        // 
        btnTplHMinus.BackColor = Color.FromArgb(50, 50, 60);
        btnTplHMinus.FlatStyle = FlatStyle.Flat;
        btnTplHMinus.Font = new Font("Segoe UI", 7.5F);
        btnTplHMinus.ForeColor = Color.White;
        btnTplHMinus.Location = new Point(164, 88);
        btnTplHMinus.Name = "btnTplHMinus";
        btnTplHMinus.Size = new Size(24, 23);
        btnTplHMinus.TabIndex = 16;
        btnTplHMinus.Text = "-";
        btnTplHMinus.UseVisualStyleBackColor = false;
        // 
        // btnTplHPlus
        // 
        btnTplHPlus.BackColor = Color.FromArgb(50, 50, 60);
        btnTplHPlus.FlatStyle = FlatStyle.Flat;
        btnTplHPlus.Font = new Font("Segoe UI", 7.5F);
        btnTplHPlus.ForeColor = Color.White;
        btnTplHPlus.Location = new Point(190, 88);
        btnTplHPlus.Name = "btnTplHPlus";
        btnTplHPlus.Size = new Size(24, 23);
        btnTplHPlus.TabIndex = 17;
        btnTplHPlus.Text = "+";
        btnTplHPlus.UseVisualStyleBackColor = false;
        // 
        // lblAssignTarget
        // 
        lblAssignTarget.AutoSize = true;
        lblAssignTarget.ForeColor = Color.LightGray;
        lblAssignTarget.Location = new Point(8, 122);
        lblAssignTarget.Name = "lblAssignTarget";
        lblAssignTarget.Size = new Size(50, 15);
        lblAssignTarget.TabIndex = 18;
        lblAssignTarget.Text = "Chọn số:";
        // 
        // cboTargetDigit
        // 
        cboTargetDigit.BackColor = Color.FromArgb(45, 45, 55);
        cboTargetDigit.DropDownStyle = ComboBoxStyle.DropDownList;
        cboTargetDigit.FlatStyle = FlatStyle.Flat;
        cboTargetDigit.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        cboTargetDigit.ForeColor = Color.White;
        cboTargetDigit.FormattingEnabled = true;
        cboTargetDigit.Items.AddRange(new object[] { "Số 0", "Số 1", "Số 2", "Số 3", "Số 4", "Số 5", "Số 6", "Số 7", "Số 8", "Số 9" });
        cboTargetDigit.Location = new Point(62, 118);
        cboTargetDigit.Name = "cboTargetDigit";
        cboTargetDigit.Size = new Size(75, 25);
        cboTargetDigit.TabIndex = 19;
        // 
        // btnAssignCurrent
        // 
        btnAssignCurrent.BackColor = Color.FromArgb(60, 110, 70);
        btnAssignCurrent.FlatAppearance.BorderColor = Color.FromArgb(90, 160, 100);
        btnAssignCurrent.FlatStyle = FlatStyle.Flat;
        btnAssignCurrent.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnAssignCurrent.ForeColor = Color.White;
        btnAssignCurrent.Location = new Point(144, 117);
        btnAssignCurrent.Name = "btnAssignCurrent";
        btnAssignCurrent.Size = new Size(135, 27);
        btnAssignCurrent.TabIndex = 20;
        btnAssignCurrent.Text = "💾 Gán vào Số";
        btnAssignCurrent.UseVisualStyleBackColor = false;
        btnAssignCurrent.Click += BtnAssignCurrent_Click;
        // 
        // pnlDigitSlotsContainer
        // 
        pnlDigitSlotsContainer.Controls.Add(tblDigitSlots);
        pnlDigitSlotsContainer.Dock = DockStyle.Fill;
        pnlDigitSlotsContainer.Location = new Point(301, 6);
        pnlDigitSlotsContainer.Name = "pnlDigitSlotsContainer";
        pnlDigitSlotsContainer.Padding = new Padding(4, 0, 0, 0);
        pnlDigitSlotsContainer.Size = new Size(699, 184);
        pnlDigitSlotsContainer.TabIndex = 1;
        // 
        // tblDigitSlots
        // 
        tblDigitSlots.ColumnCount = 10;
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        tblDigitSlots.Dock = DockStyle.Fill;
        tblDigitSlots.Location = new Point(4, 0);
        tblDigitSlots.Name = "tblDigitSlots";
        tblDigitSlots.RowCount = 1;
        tblDigitSlots.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tblDigitSlots.Size = new Size(695, 184);
        tblDigitSlots.TabIndex = 0;
        // 
        // pnlStatus
        // 
        pnlStatus.BackColor = Color.FromArgb(20, 20, 25);
        pnlStatus.Controls.Add(lblStatus);
        pnlStatus.Controls.Add(lblLastUpdate);
        pnlStatus.Dock = DockStyle.Bottom;
        pnlStatus.Location = new Point(0, 758);
        pnlStatus.Name = "pnlStatus";
        pnlStatus.Padding = new Padding(12, 6, 12, 6);
        pnlStatus.Size = new Size(1008, 32);
        pnlStatus.TabIndex = 6;
        // 
        // lblStatus
        // 
        lblStatus.AutoSize = true;
        lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        lblStatus.ForeColor = Color.LightGreen;
        lblStatus.Location = new Point(12, 8);
        lblStatus.Name = "lblStatus";
        lblStatus.Size = new Size(160, 15);
        lblStatus.TabIndex = 0;
        lblStatus.Text = "Trạng thái: Đang khởi tạo...";
        // 
        // lblLastUpdate
        // 
        lblLastUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        lblLastUpdate.AutoSize = true;
        lblLastUpdate.Font = new Font("Segoe UI", 8.5F);
        lblLastUpdate.ForeColor = Color.Gray;
        lblLastUpdate.Location = new Point(748, 8);
        lblLastUpdate.Name = "lblLastUpdate";
        lblLastUpdate.Size = new Size(130, 15);
        lblLastUpdate.TabIndex = 1;
        lblLastUpdate.Text = "Cập nhật lần cuối: --:--:--";
        // 
        // ResourceCropTestForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(20, 20, 24);
        ClientSize = new Size(1008, 790);
        Controls.Add(tblResources);
        Controls.Add(pnlGlobalAdjust);
        Controls.Add(pnlTemplates);
        Controls.Add(pnlOverview);
        Controls.Add(pnlToolbar);
        Controls.Add(pnlHeader);
        Controls.Add(pnlStatus);
        MinimumSize = new Size(980, 750);
        Name = "ResourceCropTestForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "AOE Macro - Resource Crop & Template Matching Tool";
        pnlHeader.ResumeLayout(false);
        pnlHeader.PerformLayout();
        pnlToolbar.ResumeLayout(false);
        pnlToolbar.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numInterval).EndInit();
        pnlOverview.ResumeLayout(false);
        pnlOverview.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picOverview).EndInit();
        pnlGlobalAdjust.ResumeLayout(false);
        pnlGlobalAdjust.PerformLayout();
        pnlTemplates.ResumeLayout(false);
        pnlTemplatesHeader.ResumeLayout(false);
        pnlTemplatesHeader.PerformLayout();
        pnlTemplatesContent.ResumeLayout(false);
        pnlTemplateTool.ResumeLayout(false);
        pnlTemplateTool.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)picTemplatePreview).EndInit();
        ((System.ComponentModel.ISupportInitialize)numTplX).EndInit();
        ((System.ComponentModel.ISupportInitialize)numTplY).EndInit();
        ((System.ComponentModel.ISupportInitialize)numTplW).EndInit();
        ((System.ComponentModel.ISupportInitialize)numTplH).EndInit();
        pnlDigitSlotsContainer.ResumeLayout(false);
        pnlStatus.ResumeLayout(false);
        pnlStatus.PerformLayout();
        ResumeLayout(false);
    }

    private Panel pnlHeader;
    private Label lblHeaderTitle;
    private Label lblHeaderSubtitle;
    private Button btnBack;
    private Panel pnlToolbar;
    private CheckBox chkAutoRefresh;
    private Label lblInterval;
    private NumericUpDown numInterval;
    private Label lblIntervalUnit;
    private CheckBox chkTopMost;
    private Label lblZoom;
    private ComboBox cboZoom;
    private Button btnRefreshNow;
    private Button btnLoadImage;
    private Button btnUseLive;
    private Button btnToggleTemplates;
    private Button btnSave;
    private Button btnReset;
    private Panel pnlOverview;
    private Label lblOverviewTitle;
    private Label lblMouseCoord;
    private PixelPictureBox picOverview;
    private TableLayoutPanel tblResources;
    private Panel pnlGlobalAdjust;
    private Label lblGlobalTitle;
    private Button btnGlobalLeft1;
    private Button btnGlobalRight1;
    private Button btnGlobalUp1;
    private Button btnGlobalDown1;
    private Button btnGlobalLeft5;
    private Button btnGlobalRight5;
    private Button btnGlobalUp5;
    private Button btnGlobalDown5;
    private Label lblGlobalSize;
    private Button btnGlobalWMinus;
    private Button btnGlobalWPlus;
    private Button btnGlobalHMinus;
    private Button btnGlobalHPlus;
    private Panel pnlTemplates;
    private Panel pnlTemplatesHeader;
    private Label lblTemplatesTitle;
    private CheckBox chkTemplateBinarize;
    private Button btnAutoExtract;
    private Button btnOpenFolder;
    private Panel pnlTemplatesContent;
    private Panel pnlTemplateTool;
    private PixelPictureBox picTemplatePreview;
    private Label lblTplCoord;
    private Label lblTplX;
    private NumericUpDown numTplX;
    private Button btnTplLeft;
    private Button btnTplRight;
    private Label lblTplY;
    private NumericUpDown numTplY;
    private Button btnTplUp;
    private Button btnTplDown;
    private Label lblTplW;
    private NumericUpDown numTplW;
    private Button btnTplWMinus;
    private Button btnTplWPlus;
    private Label lblTplH;
    private NumericUpDown numTplH;
    private Button btnTplHMinus;
    private Button btnTplHPlus;
    private Label lblAssignTarget;
    private ComboBox cboTargetDigit;
    private Button btnAssignCurrent;
    private Panel pnlDigitSlotsContainer;
    private TableLayoutPanel tblDigitSlots;
    private Panel pnlStatus;
    private Label lblStatus;
    private Label lblLastUpdate;
}
