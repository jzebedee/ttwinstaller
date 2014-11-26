namespace TaleOfTwoWastelands.UI
{
    partial class frm_Main
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frm_Main));
            this.tbl_Main = new System.Windows.Forms.TableLayoutPanel();
            this.prgOverall = new TaleOfTwoWastelands.UI.TextProgressBar();
            this.pic_Banner = new System.Windows.Forms.PictureBox();
            this.txt_Progress = new System.Windows.Forms.RichTextBox();
            this.btn_Install = new System.Windows.Forms.Button();
            this.txt_Instructions = new System.Windows.Forms.RichTextBox();
            this.btn_FO3Browse = new System.Windows.Forms.Button();
            this.btn_FNVBrowse = new System.Windows.Forms.Button();
            this.btn_TTWBrowse = new System.Windows.Forms.Button();
            this.txt_FO3Location = new System.Windows.Forms.RichTextBox();
            this.txt_FNVLocation = new System.Windows.Forms.RichTextBox();
            this.txt_TTWLocation = new System.Windows.Forms.RichTextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.prgCurrent = new TaleOfTwoWastelands.UI.TextProgressBar();
            this.chkYou = new System.Windows.Forms.CheckBox();
            this.dlg_FindGame = new System.Windows.Forms.OpenFileDialog();
            this.dlg_SaveTTW = new System.Windows.Forms.SaveFileDialog();
            this.tbl_Main.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pic_Banner)).BeginInit();
            this.SuspendLayout();
            // 
            // tbl_Main
            // 
            this.tbl_Main.ColumnCount = 3;
            this.tbl_Main.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 37.26885F));
            this.tbl_Main.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 62.73115F));
            this.tbl_Main.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 106F));
            this.tbl_Main.Controls.Add(this.prgOverall, 1, 9);
            this.tbl_Main.Controls.Add(this.pic_Banner, 0, 0);
            this.tbl_Main.Controls.Add(this.txt_Progress, 1, 7);
            this.tbl_Main.Controls.Add(this.btn_Install, 2, 9);
            this.tbl_Main.Controls.Add(this.txt_Instructions, 1, 0);
            this.tbl_Main.Controls.Add(this.btn_FO3Browse, 2, 1);
            this.tbl_Main.Controls.Add(this.btn_FNVBrowse, 2, 3);
            this.tbl_Main.Controls.Add(this.btn_TTWBrowse, 2, 5);
            this.tbl_Main.Controls.Add(this.txt_FO3Location, 1, 1);
            this.tbl_Main.Controls.Add(this.txt_FNVLocation, 1, 3);
            this.tbl_Main.Controls.Add(this.txt_TTWLocation, 1, 5);
            this.tbl_Main.Controls.Add(this.label1, 1, 2);
            this.tbl_Main.Controls.Add(this.label2, 1, 4);
            this.tbl_Main.Controls.Add(this.label3, 1, 6);
            this.tbl_Main.Controls.Add(this.prgCurrent, 1, 8);
            this.tbl_Main.Controls.Add(this.chkYou, 2, 8);
            this.tbl_Main.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbl_Main.Location = new System.Drawing.Point(0, 0);
            this.tbl_Main.Name = "tbl_Main";
            this.tbl_Main.RowCount = 9;
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tbl_Main.Size = new System.Drawing.Size(784, 561);
            this.tbl_Main.TabIndex = 0;
            // 
            // prgOverall
            // 
            this.prgOverall.CustomText = null;
            this.prgOverall.DisplayStyle = TaleOfTwoWastelands.UI.ProgressBarDisplayText.CustomText;
            this.prgOverall.Dock = System.Windows.Forms.DockStyle.Fill;
            this.prgOverall.Location = new System.Drawing.Point(255, 534);
            this.prgOverall.Name = "prgOverall";
            this.prgOverall.Size = new System.Drawing.Size(419, 24);
            this.prgOverall.TabIndex = 14;
            // 
            // pic_Banner
            // 
            this.pic_Banner.BackColor = System.Drawing.SystemColors.InfoText;
            this.pic_Banner.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pic_Banner.Image = ((System.Drawing.Image)(resources.GetObject("pic_Banner.Image")));
            this.pic_Banner.Location = new System.Drawing.Point(0, 0);
            this.pic_Banner.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.pic_Banner.Name = "pic_Banner";
            this.tbl_Main.SetRowSpan(this.pic_Banner, 10);
            this.pic_Banner.Size = new System.Drawing.Size(249, 561);
            this.pic_Banner.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pic_Banner.TabIndex = 0;
            this.pic_Banner.TabStop = false;
            // 
            // txt_Progress
            // 
            this.txt_Progress.BackColor = System.Drawing.SystemColors.Window;
            this.tbl_Main.SetColumnSpan(this.txt_Progress, 2);
            this.txt_Progress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_Progress.Location = new System.Drawing.Point(255, 213);
            this.txt_Progress.Name = "txt_Progress";
            this.txt_Progress.ReadOnly = true;
            this.txt_Progress.Size = new System.Drawing.Size(526, 285);
            this.txt_Progress.TabIndex = 1;
            this.txt_Progress.TabStop = false;
            this.txt_Progress.Text = "";
            // 
            // btn_Install
            // 
            this.btn_Install.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btn_Install.Location = new System.Drawing.Point(693, 534);
            this.btn_Install.Name = "btn_Install";
            this.btn_Install.Size = new System.Drawing.Size(75, 23);
            this.btn_Install.TabIndex = 2;
            this.btn_Install.Text = "Install";
            this.btn_Install.UseVisualStyleBackColor = true;
            this.btn_Install.Click += new System.EventHandler(this.btn_Install_Click);
            // 
            // txt_Instructions
            // 
            this.txt_Instructions.BackColor = System.Drawing.SystemColors.Control;
            this.txt_Instructions.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tbl_Main.SetColumnSpan(this.txt_Instructions, 2);
            this.txt_Instructions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_Instructions.Location = new System.Drawing.Point(255, 3);
            this.txt_Instructions.Name = "txt_Instructions";
            this.txt_Instructions.ReadOnly = true;
            this.txt_Instructions.Size = new System.Drawing.Size(526, 34);
            this.txt_Instructions.TabIndex = 3;
            this.txt_Instructions.TabStop = false;
            this.txt_Instructions.Text = "Please indicate where Fallout 3 and Fallout New Vegas are located, and where you " +
    "would like Tale of Two Wastelands installed, using the input boxes below.";
            // 
            // btn_FO3Browse
            // 
            this.btn_FO3Browse.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btn_FO3Browse.Location = new System.Drawing.Point(693, 43);
            this.btn_FO3Browse.Name = "btn_FO3Browse";
            this.btn_FO3Browse.Size = new System.Drawing.Size(74, 23);
            this.btn_FO3Browse.TabIndex = 4;
            this.btn_FO3Browse.Text = "Browse...";
            this.btn_FO3Browse.UseVisualStyleBackColor = true;
            this.btn_FO3Browse.Click += new System.EventHandler(this.btn_FO3Browse_Click);
            // 
            // btn_FNVBrowse
            // 
            this.btn_FNVBrowse.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btn_FNVBrowse.Location = new System.Drawing.Point(693, 103);
            this.btn_FNVBrowse.Name = "btn_FNVBrowse";
            this.btn_FNVBrowse.Size = new System.Drawing.Size(74, 23);
            this.btn_FNVBrowse.TabIndex = 5;
            this.btn_FNVBrowse.Text = "Browse...";
            this.btn_FNVBrowse.UseVisualStyleBackColor = true;
            this.btn_FNVBrowse.Click += new System.EventHandler(this.btn_FNVBrowse_Click);
            // 
            // btn_TTWBrowse
            // 
            this.btn_TTWBrowse.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btn_TTWBrowse.Location = new System.Drawing.Point(693, 163);
            this.btn_TTWBrowse.Name = "btn_TTWBrowse";
            this.btn_TTWBrowse.Size = new System.Drawing.Size(74, 23);
            this.btn_TTWBrowse.TabIndex = 6;
            this.btn_TTWBrowse.Text = "Browse...";
            this.btn_TTWBrowse.UseVisualStyleBackColor = true;
            this.btn_TTWBrowse.Click += new System.EventHandler(this.btn_TTWBrowse_Click);
            // 
            // txt_FO3Location
            // 
            this.txt_FO3Location.BackColor = System.Drawing.SystemColors.Window;
            this.txt_FO3Location.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_FO3Location.EnableAutoDragDrop = true;
            this.txt_FO3Location.Location = new System.Drawing.Point(255, 43);
            this.txt_FO3Location.Multiline = false;
            this.txt_FO3Location.Name = "txt_FO3Location";
            this.txt_FO3Location.ReadOnly = true;
            this.txt_FO3Location.Size = new System.Drawing.Size(419, 24);
            this.txt_FO3Location.TabIndex = 7;
            this.txt_FO3Location.Text = "";
            // 
            // txt_FNVLocation
            // 
            this.txt_FNVLocation.BackColor = System.Drawing.SystemColors.Window;
            this.txt_FNVLocation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_FNVLocation.EnableAutoDragDrop = true;
            this.txt_FNVLocation.Location = new System.Drawing.Point(255, 103);
            this.txt_FNVLocation.Multiline = false;
            this.txt_FNVLocation.Name = "txt_FNVLocation";
            this.txt_FNVLocation.ReadOnly = true;
            this.txt_FNVLocation.Size = new System.Drawing.Size(419, 24);
            this.txt_FNVLocation.TabIndex = 8;
            this.txt_FNVLocation.Text = "";
            // 
            // txt_TTWLocation
            // 
            this.txt_TTWLocation.BackColor = System.Drawing.SystemColors.Window;
            this.txt_TTWLocation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txt_TTWLocation.EnableAutoDragDrop = true;
            this.txt_TTWLocation.Location = new System.Drawing.Point(255, 163);
            this.txt_TTWLocation.Multiline = false;
            this.txt_TTWLocation.Name = "txt_TTWLocation";
            this.txt_TTWLocation.ReadOnly = true;
            this.txt_TTWLocation.Size = new System.Drawing.Size(419, 24);
            this.txt_TTWLocation.TabIndex = 9;
            this.txt_TTWLocation.Text = "";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(255, 70);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(91, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Fallout 3 Location";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(255, 130);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(140, 13);
            this.label2.TabIndex = 11;
            this.label2.Text = "Fallout New Vegas Location";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(255, 190);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(125, 13);
            this.label3.TabIndex = 12;
            this.label3.Text = "Tale Of Two Wastelands";
            // 
            // prgCurrent
            // 
            this.prgCurrent.CustomText = null;
            this.prgCurrent.DisplayStyle = TaleOfTwoWastelands.UI.ProgressBarDisplayText.CustomText;
            this.prgCurrent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.prgCurrent.Location = new System.Drawing.Point(255, 504);
            this.prgCurrent.Name = "prgCurrent";
            this.prgCurrent.Size = new System.Drawing.Size(419, 24);
            this.prgCurrent.TabIndex = 13;
            // 
            // chkYou
            // 
            this.chkYou.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.chkYou.AutoSize = true;
            this.chkYou.Checked = true;
            this.chkYou.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkYou.Location = new System.Drawing.Point(706, 507);
            this.chkYou.Name = "chkYou";
            this.chkYou.Size = new System.Drawing.Size(49, 17);
            this.chkYou.TabIndex = 15;
            this.chkYou.Text = "Sexy";
            this.chkYou.UseVisualStyleBackColor = true;
            this.chkYou.CheckedChanged += new System.EventHandler(this.chkYou_CheckedChanged);
            // 
            // dlg_FindGame
            // 
            this.dlg_FindGame.Filter = "Fallout 3|Fallout3.exe|Fallout New Vegas|FalloutNV.exe|Tale Of Two Wastelands|Tal" +
    "eOfTwoWastelands.esm";
            // 
            // dlg_SaveTTW
            // 
            this.dlg_SaveTTW.FileName = "TaleOfTwoWastelands.fomod";
            this.dlg_SaveTTW.Title = "Tale of Two Wastelands";
            // 
            // frm_Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Controls.Add(this.tbl_Main);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "frm_Main";
            this.Text = "Tale Of Two Wastelands Installer";
            this.Load += new System.EventHandler(this.frm_Main_Load);
            this.tbl_Main.ResumeLayout(false);
            this.tbl_Main.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pic_Banner)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tbl_Main;
        private System.Windows.Forms.PictureBox pic_Banner;
        private System.Windows.Forms.RichTextBox txt_Progress;
        private System.Windows.Forms.Button btn_Install;
        private System.Windows.Forms.RichTextBox txt_Instructions;
        private System.Windows.Forms.Button btn_FO3Browse;
        private System.Windows.Forms.Button btn_FNVBrowse;
        private System.Windows.Forms.Button btn_TTWBrowse;
        private System.Windows.Forms.RichTextBox txt_FO3Location;
        private System.Windows.Forms.RichTextBox txt_FNVLocation;
        private System.Windows.Forms.RichTextBox txt_TTWLocation;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.OpenFileDialog dlg_FindGame;
        private System.Windows.Forms.SaveFileDialog dlg_SaveTTW;
        private TextProgressBar prgCurrent;
        private TextProgressBar prgOverall;
        private System.Windows.Forms.CheckBox chkYou;

    }
}

