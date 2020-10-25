namespace TGConfigEditor
{
  partial class MainForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.components = new System.ComponentModel.Container();
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      this.MainPrgBar = new System.Windows.Forms.ProgressBar();
      this.DataGridSection = new System.Windows.Forms.DataGridView();
      this.SaveConfigBtn = new System.Windows.Forms.Button();
      this.Label1 = new System.Windows.Forms.Label();
      this.LoadConfigBtn = new System.Windows.Forms.Button();
      this.OpenConfigFileDlg = new System.Windows.Forms.OpenFileDialog();
      this.CommentsTxtBx = new System.Windows.Forms.TextBox();
      this.DetailsLbl = new System.Windows.Forms.Label();
      this.SaveConfigFileDlg = new System.Windows.Forms.SaveFileDialog();
      this.TreeViewSection = new System.Windows.Forms.TreeView();
      this.TableItemsGrid = new System.Windows.Forms.DataGridView();
      this.groupBox1 = new System.Windows.Forms.GroupBox();
      this.AllItemsCmbBx = new System.Windows.Forms.ComboBox();
      this.AddBtn = new System.Windows.Forms.Button();
      this.RemoveBtn = new System.Windows.Forms.Button();
      this.splitContainer1 = new System.Windows.Forms.SplitContainer();
      this.splitContainer2 = new System.Windows.Forms.SplitContainer();
      this.TranslateBtn = new System.Windows.Forms.Button();
      this.SetValueBtn = new System.Windows.Forms.Button();
      this.CustomValueTxtBx = new System.Windows.Forms.TextBox();
      this.configSectionBindingSource = new System.Windows.Forms.BindingSource(this.components);
      this.PercentageBtn = new System.Windows.Forms.Button();
      ((System.ComponentModel.ISupportInitialize)(this.DataGridSection)).BeginInit();
      ((System.ComponentModel.ISupportInitialize)(this.TableItemsGrid)).BeginInit();
      this.groupBox1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
      this.splitContainer2.Panel1.SuspendLayout();
      this.splitContainer2.Panel2.SuspendLayout();
      this.splitContainer2.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.configSectionBindingSource)).BeginInit();
      this.SuspendLayout();
      // 
      // MainPrgBar
      // 
      this.MainPrgBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.MainPrgBar.Location = new System.Drawing.Point(180, 641);
      this.MainPrgBar.Margin = new System.Windows.Forms.Padding(0);
      this.MainPrgBar.Name = "MainPrgBar";
      this.MainPrgBar.Size = new System.Drawing.Size(633, 10);
      this.MainPrgBar.TabIndex = 11;
      // 
      // DataGridSection
      // 
      this.DataGridSection.AllowUserToAddRows = false;
      this.DataGridSection.AllowUserToDeleteRows = false;
      this.DataGridSection.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
      this.DataGridSection.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this.DataGridSection.Dock = System.Windows.Forms.DockStyle.Fill;
      this.DataGridSection.Location = new System.Drawing.Point(0, 0);
      this.DataGridSection.Margin = new System.Windows.Forms.Padding(2);
      this.DataGridSection.Name = "DataGridSection";
      this.DataGridSection.RowTemplate.Height = 24;
      this.DataGridSection.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
      this.DataGridSection.Size = new System.Drawing.Size(619, 567);
      this.DataGridSection.TabIndex = 10;
      this.DataGridSection.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridSection_CellEndEdit);
      // 
      // SaveConfigBtn
      // 
      this.SaveConfigBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
      this.SaveConfigBtn.Enabled = false;
      this.SaveConfigBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
      this.SaveConfigBtn.Location = new System.Drawing.Point(14, 641);
      this.SaveConfigBtn.Margin = new System.Windows.Forms.Padding(2);
      this.SaveConfigBtn.Name = "SaveConfigBtn";
      this.SaveConfigBtn.Size = new System.Drawing.Size(158, 31);
      this.SaveConfigBtn.TabIndex = 9;
      this.SaveConfigBtn.Text = "Save config file";
      this.SaveConfigBtn.UseVisualStyleBackColor = true;
      this.SaveConfigBtn.Click += new System.EventHandler(this.SaveConfigBtn_Click);
      // 
      // Label1
      // 
      this.Label1.AutoSize = true;
      this.Label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
      this.Label1.Location = new System.Drawing.Point(64, 50);
      this.Label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
      this.Label1.Name = "Label1";
      this.Label1.Size = new System.Drawing.Size(56, 13);
      this.Label1.TabIndex = 8;
      this.Label1.Text = "Sections";
      // 
      // LoadConfigBtn
      // 
      this.LoadConfigBtn.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
      this.LoadConfigBtn.Location = new System.Drawing.Point(14, 8);
      this.LoadConfigBtn.Margin = new System.Windows.Forms.Padding(2);
      this.LoadConfigBtn.Name = "LoadConfigBtn";
      this.LoadConfigBtn.Size = new System.Drawing.Size(158, 31);
      this.LoadConfigBtn.TabIndex = 6;
      this.LoadConfigBtn.Text = "Load config file";
      this.LoadConfigBtn.UseVisualStyleBackColor = true;
      this.LoadConfigBtn.Click += new System.EventHandler(this.LoadConfigBtn_Click);
      // 
      // OpenConfigFileDlg
      // 
      this.OpenConfigFileDlg.FileName = "TGConfig.gen";
      this.OpenConfigFileDlg.Filter = "Transport Giant config|*.gen";
      this.OpenConfigFileDlg.InitialDirectory = "C:\\GRY\\Transport Giant Gold\\config";
      // 
      // CommentsTxtBx
      // 
      this.CommentsTxtBx.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.CommentsTxtBx.Location = new System.Drawing.Point(176, 8);
      this.CommentsTxtBx.Margin = new System.Windows.Forms.Padding(2);
      this.CommentsTxtBx.Multiline = true;
      this.CommentsTxtBx.Name = "CommentsTxtBx";
      this.CommentsTxtBx.ScrollBars = System.Windows.Forms.ScrollBars.Both;
      this.CommentsTxtBx.Size = new System.Drawing.Size(864, 54);
      this.CommentsTxtBx.TabIndex = 13;
      this.CommentsTxtBx.WordWrap = false;
      // 
      // DetailsLbl
      // 
      this.DetailsLbl.BackColor = System.Drawing.SystemColors.ActiveCaption;
      this.DetailsLbl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
      this.DetailsLbl.Dock = System.Windows.Forms.DockStyle.Top;
      this.DetailsLbl.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(238)));
      this.DetailsLbl.Location = new System.Drawing.Point(0, 0);
      this.DetailsLbl.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
      this.DetailsLbl.Name = "DetailsLbl";
      this.DetailsLbl.Size = new System.Drawing.Size(235, 85);
      this.DetailsLbl.TabIndex = 14;
      this.DetailsLbl.Text = "---";
      // 
      // SaveConfigFileDlg
      // 
      this.SaveConfigFileDlg.FileName = "TGConfig NEW.gen";
      this.SaveConfigFileDlg.Filter = "Transport Giant config|*.gen";
      // 
      // TreeViewSection
      // 
      this.TreeViewSection.Dock = System.Windows.Forms.DockStyle.Fill;
      this.TreeViewSection.Location = new System.Drawing.Point(0, 0);
      this.TreeViewSection.Margin = new System.Windows.Forms.Padding(2);
      this.TreeViewSection.Name = "TreeViewSection";
      this.TreeViewSection.Size = new System.Drawing.Size(160, 567);
      this.TreeViewSection.TabIndex = 16;
      this.TreeViewSection.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.TreeViewSection_AfterSelect);
      // 
      // TableItemsGrid
      // 
      this.TableItemsGrid.AllowUserToAddRows = false;
      this.TableItemsGrid.AllowUserToDeleteRows = false;
      this.TableItemsGrid.AllowUserToResizeRows = false;
      this.TableItemsGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.TableItemsGrid.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCells;
      this.TableItemsGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
      this.TableItemsGrid.Location = new System.Drawing.Point(4, 87);
      this.TableItemsGrid.Margin = new System.Windows.Forms.Padding(2);
      this.TableItemsGrid.MultiSelect = false;
      this.TableItemsGrid.Name = "TableItemsGrid";
      this.TableItemsGrid.RowHeadersVisible = false;
      this.TableItemsGrid.RowTemplate.Height = 24;
      this.TableItemsGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
      this.TableItemsGrid.Size = new System.Drawing.Size(215, 374);
      this.TableItemsGrid.TabIndex = 17;
      this.TableItemsGrid.CellEndEdit += new System.Windows.Forms.DataGridViewCellEventHandler(this.TableItemsGrid_CellEndEdit);
      // 
      // groupBox1
      // 
      this.groupBox1.AutoSize = true;
      this.groupBox1.Controls.Add(this.AllItemsCmbBx);
      this.groupBox1.Controls.Add(this.AddBtn);
      this.groupBox1.Controls.Add(this.RemoveBtn);
      this.groupBox1.Dock = System.Windows.Forms.DockStyle.Bottom;
      this.groupBox1.Location = new System.Drawing.Point(0, 483);
      this.groupBox1.Margin = new System.Windows.Forms.Padding(2);
      this.groupBox1.Name = "groupBox1";
      this.groupBox1.Padding = new System.Windows.Forms.Padding(2);
      this.groupBox1.Size = new System.Drawing.Size(235, 84);
      this.groupBox1.TabIndex = 18;
      this.groupBox1.TabStop = false;
      // 
      // AllItemsCmbBx
      // 
      this.AllItemsCmbBx.FormattingEnabled = true;
      this.AllItemsCmbBx.Location = new System.Drawing.Point(4, 18);
      this.AllItemsCmbBx.Margin = new System.Windows.Forms.Padding(2);
      this.AllItemsCmbBx.Name = "AllItemsCmbBx";
      this.AllItemsCmbBx.Size = new System.Drawing.Size(203, 21);
      this.AllItemsCmbBx.Sorted = true;
      this.AllItemsCmbBx.TabIndex = 0;
      // 
      // AddBtn
      // 
      this.AddBtn.AutoSize = true;
      this.AddBtn.Location = new System.Drawing.Point(82, 44);
      this.AddBtn.Margin = new System.Windows.Forms.Padding(2);
      this.AddBtn.Name = "AddBtn";
      this.AddBtn.Size = new System.Drawing.Size(58, 23);
      this.AddBtn.TabIndex = 3;
      this.AddBtn.Text = "ADD";
      this.AddBtn.UseVisualStyleBackColor = true;
      this.AddBtn.Click += new System.EventHandler(this.AddBtn_Click);
      // 
      // RemoveBtn
      // 
      this.RemoveBtn.AutoSize = true;
      this.RemoveBtn.Location = new System.Drawing.Point(144, 43);
      this.RemoveBtn.Margin = new System.Windows.Forms.Padding(2);
      this.RemoveBtn.Name = "RemoveBtn";
      this.RemoveBtn.Size = new System.Drawing.Size(63, 23);
      this.RemoveBtn.TabIndex = 2;
      this.RemoveBtn.Text = "REMOVE";
      this.RemoveBtn.UseVisualStyleBackColor = true;
      this.RemoveBtn.Click += new System.EventHandler(this.RemoveBtn_Click);
      // 
      // splitContainer1
      // 
      this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
      this.splitContainer1.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
      this.splitContainer1.Location = new System.Drawing.Point(14, 67);
      this.splitContainer1.Name = "splitContainer1";
      // 
      // splitContainer1.Panel1
      // 
      this.splitContainer1.Panel1.Controls.Add(this.TreeViewSection);
      // 
      // splitContainer1.Panel2
      // 
      this.splitContainer1.Panel2.Controls.Add(this.splitContainer2);
      this.splitContainer1.Size = new System.Drawing.Size(1026, 567);
      this.splitContainer1.SplitterDistance = 160;
      this.splitContainer1.SplitterWidth = 6;
      this.splitContainer1.TabIndex = 18;
      // 
      // splitContainer2
      // 
      this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
      this.splitContainer2.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
      this.splitContainer2.Location = new System.Drawing.Point(0, 0);
      this.splitContainer2.Name = "splitContainer2";
      // 
      // splitContainer2.Panel1
      // 
      this.splitContainer2.Panel1.Controls.Add(this.DataGridSection);
      // 
      // splitContainer2.Panel2
      // 
      this.splitContainer2.Panel2.Controls.Add(this.DetailsLbl);
      this.splitContainer2.Panel2.Controls.Add(this.TableItemsGrid);
      this.splitContainer2.Panel2.Controls.Add(this.groupBox1);
      this.splitContainer2.Size = new System.Drawing.Size(860, 567);
      this.splitContainer2.SplitterDistance = 619;
      this.splitContainer2.SplitterWidth = 6;
      this.splitContainer2.TabIndex = 11;
      // 
      // TranslateBtn
      // 
      this.TranslateBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.TranslateBtn.Location = new System.Drawing.Point(819, 641);
      this.TranslateBtn.Name = "TranslateBtn";
      this.TranslateBtn.Size = new System.Drawing.Size(221, 31);
      this.TranslateBtn.TabIndex = 19;
      this.TranslateBtn.Text = "Translate names to english";
      this.TranslateBtn.UseVisualStyleBackColor = true;
      this.TranslateBtn.Click += new System.EventHandler(this.TranslateBtn_Click);
      // 
      // SetValueBtn
      // 
      this.SetValueBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.SetValueBtn.Location = new System.Drawing.Point(685, 654);
      this.SetValueBtn.Name = "SetValueBtn";
      this.SetValueBtn.Size = new System.Drawing.Size(128, 23);
      this.SetValueBtn.TabIndex = 20;
      this.SetValueBtn.Text = "Fill selected cells";
      this.SetValueBtn.UseVisualStyleBackColor = true;
      this.SetValueBtn.Click += new System.EventHandler(this.SetValueBtn_Click);
      // 
      // CustomValueTxtBx
      // 
      this.CustomValueTxtBx.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.CustomValueTxtBx.Location = new System.Drawing.Point(436, 656);
      this.CustomValueTxtBx.Name = "CustomValueTxtBx";
      this.CustomValueTxtBx.Size = new System.Drawing.Size(148, 20);
      this.CustomValueTxtBx.TabIndex = 21;
      // 
      // configSectionBindingSource
      // 
      this.configSectionBindingSource.DataSource = typeof(TGConfigEditor.ConfigSection);
      // 
      // PercentageBtn
      // 
      this.PercentageBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
      this.PercentageBtn.Location = new System.Drawing.Point(590, 654);
      this.PercentageBtn.Name = "PercentageBtn";
      this.PercentageBtn.Size = new System.Drawing.Size(89, 23);
      this.PercentageBtn.TabIndex = 22;
      this.PercentageBtn.Text = "Percentage";
      this.PercentageBtn.UseVisualStyleBackColor = true;
      this.PercentageBtn.Click += new System.EventHandler(this.PercentageBtn_Click);
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(1049, 680);
      this.Controls.Add(this.PercentageBtn);
      this.Controls.Add(this.CustomValueTxtBx);
      this.Controls.Add(this.SetValueBtn);
      this.Controls.Add(this.TranslateBtn);
      this.Controls.Add(this.splitContainer1);
      this.Controls.Add(this.CommentsTxtBx);
      this.Controls.Add(this.MainPrgBar);
      this.Controls.Add(this.SaveConfigBtn);
      this.Controls.Add(this.Label1);
      this.Controls.Add(this.LoadConfigBtn);
      this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
      this.Margin = new System.Windows.Forms.Padding(2);
      this.Name = "MainForm";
      this.Text = "Transport Giant - Config editor v0.5";
      this.Load += new System.EventHandler(this.MainForm_Load);
      ((System.ComponentModel.ISupportInitialize)(this.DataGridSection)).EndInit();
      ((System.ComponentModel.ISupportInitialize)(this.TableItemsGrid)).EndInit();
      this.groupBox1.ResumeLayout(false);
      this.groupBox1.PerformLayout();
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
      this.splitContainer1.ResumeLayout(false);
      this.splitContainer2.Panel1.ResumeLayout(false);
      this.splitContainer2.Panel2.ResumeLayout(false);
      this.splitContainer2.Panel2.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
      this.splitContainer2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.configSectionBindingSource)).EndInit();
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    internal System.Windows.Forms.ProgressBar MainPrgBar;
    internal System.Windows.Forms.DataGridView DataGridSection;
    internal System.Windows.Forms.Button SaveConfigBtn;
    internal System.Windows.Forms.Label Label1;
    internal System.Windows.Forms.Button LoadConfigBtn;
    internal System.Windows.Forms.OpenFileDialog OpenConfigFileDlg;
    private System.Windows.Forms.TextBox CommentsTxtBx;
    private System.Windows.Forms.Label DetailsLbl;
    private System.Windows.Forms.BindingSource configSectionBindingSource;
    private System.Windows.Forms.SaveFileDialog SaveConfigFileDlg;
    private System.Windows.Forms.TreeView TreeViewSection;
    private System.Windows.Forms.Button AddBtn;
    private System.Windows.Forms.Button RemoveBtn;
    private System.Windows.Forms.DataGridView TableItemsGrid;
    private System.Windows.Forms.GroupBox groupBox1;
    private System.Windows.Forms.ComboBox AllItemsCmbBx;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.SplitContainer splitContainer2;
    private System.Windows.Forms.Button TranslateBtn;
    private System.Windows.Forms.Button SetValueBtn;
    private System.Windows.Forms.TextBox CustomValueTxtBx;
    private System.Windows.Forms.Button PercentageBtn;
  }
}

