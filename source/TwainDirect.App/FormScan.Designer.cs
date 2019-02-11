namespace TwainDirect.App
{
    partial class FormScan
    {
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        /*
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        */

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.cloudMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.loginWithGoogleToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loginWithFacebookToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.m_comboboxCloudApiRoot = new System.Windows.Forms.ComboBox();
            this.m_buttonCloud = new System.Windows.Forms.Button();
            this.m_textboxSummary = new System.Windows.Forms.TextBox();
            this.m_buttonSelect = new System.Windows.Forms.Button();
            this.m_buttonStop = new System.Windows.Forms.Button();
            this.m_buttonOpen = new System.Windows.Forms.Button();
            this.m_buttonClose = new System.Windows.Forms.Button();
            this.m_buttonSetup = new System.Windows.Forms.Button();
            this.m_buttonScan = new System.Windows.Forms.Button();
            this.m_splitcontainerImages = new System.Windows.Forms.SplitContainer();
            this.m_pictureboxImage1 = new System.Windows.Forms.PictureBox();
            this.m_textbox1 = new System.Windows.Forms.TextBox();
            this.m_pictureboxImage2 = new System.Windows.Forms.PictureBox();
            this.m_textbox2 = new System.Windows.Forms.TextBox();
            this.cloudMenuStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_splitcontainerImages)).BeginInit();
            this.m_splitcontainerImages.Panel1.SuspendLayout();
            this.m_splitcontainerImages.Panel2.SuspendLayout();
            this.m_splitcontainerImages.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage2)).BeginInit();
            this.SuspendLayout();
            // 
            // cloudMenuStrip
            // 
            this.cloudMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.loginWithGoogleToolStripMenuItem,
            this.loginWithFacebookToolStripMenuItem});
            this.cloudMenuStrip.Name = "cloudMenuStrip";
            this.cloudMenuStrip.Size = new System.Drawing.Size(194, 48);
            // 
            // loginWithGoogleToolStripMenuItem
            // 
            this.loginWithGoogleToolStripMenuItem.Name = "loginWithGoogleToolStripMenuItem";
            this.loginWithGoogleToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.loginWithGoogleToolStripMenuItem.Tag = "google";
            this.loginWithGoogleToolStripMenuItem.Text = "Login with Google...";
            this.loginWithGoogleToolStripMenuItem.Click += new System.EventHandler(this.loginToolStripMenuItem_Click);
            // 
            // loginWithFacebookToolStripMenuItem
            // 
            this.loginWithFacebookToolStripMenuItem.Name = "loginWithFacebookToolStripMenuItem";
            this.loginWithFacebookToolStripMenuItem.Size = new System.Drawing.Size(193, 22);
            this.loginWithFacebookToolStripMenuItem.Tag = "facebook";
            this.loginWithFacebookToolStripMenuItem.Text = "Login with Facebook...";
            this.loginWithFacebookToolStripMenuItem.Click += new System.EventHandler(this.loginToolStripMenuItem_Click);
            // 
            // m_comboboxCloudApiRoot
            // 
            this.m_comboboxCloudApiRoot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_comboboxCloudApiRoot.FormattingEnabled = true;
            this.m_comboboxCloudApiRoot.Location = new System.Drawing.Point(13, 418);
            this.m_comboboxCloudApiRoot.Name = "m_comboboxCloudApiRoot";
            this.m_comboboxCloudApiRoot.Size = new System.Drawing.Size(237, 21);
            this.m_comboboxCloudApiRoot.TabIndex = 28;
            this.m_comboboxCloudApiRoot.SelectedIndexChanged += new System.EventHandler(this.m_comboboxCloudApiRoot_SelectedIndexChanged);
            // 
            // m_buttonCloud
            // 
            this.m_buttonCloud.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonCloud.Location = new System.Drawing.Point(256, 418);
            this.m_buttonCloud.Name = "m_buttonCloud";
            this.m_buttonCloud.Size = new System.Drawing.Size(75, 23);
            this.m_buttonCloud.TabIndex = 26;
            this.m_buttonCloud.Text = "cloud...";
            this.m_buttonCloud.UseVisualStyleBackColor = true;
            this.m_buttonCloud.Click += new System.EventHandler(this.m_buttonCloud_Click);
            // 
            // m_textboxSummary
            // 
            this.m_textboxSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textboxSummary.BackColor = System.Drawing.SystemColors.Window;
            this.m_textboxSummary.Location = new System.Drawing.Point(13, 391);
            this.m_textboxSummary.Name = "m_textboxSummary";
            this.m_textboxSummary.ReadOnly = true;
            this.m_textboxSummary.Size = new System.Drawing.Size(579, 20);
            this.m_textboxSummary.TabIndex = 25;
            // 
            // m_buttonSelect
            // 
            this.m_buttonSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonSelect.Location = new System.Drawing.Point(175, 450);
            this.m_buttonSelect.Name = "m_buttonSelect";
            this.m_buttonSelect.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSelect.TabIndex = 24;
            this.m_buttonSelect.Text = "select...";
            this.m_buttonSelect.UseVisualStyleBackColor = true;
            this.m_buttonSelect.Click += new System.EventHandler(this.m_buttonSelect_Click);
            // 
            // m_buttonStop
            // 
            this.m_buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStop.Location = new System.Drawing.Point(517, 450);
            this.m_buttonStop.Name = "m_buttonStop";
            this.m_buttonStop.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStop.TabIndex = 23;
            this.m_buttonStop.Text = "stop";
            this.m_buttonStop.UseVisualStyleBackColor = true;
            this.m_buttonStop.Click += new System.EventHandler(this.m_buttonStop_Click);
            // 
            // m_buttonOpen
            // 
            this.m_buttonOpen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonOpen.Location = new System.Drawing.Point(13, 450);
            this.m_buttonOpen.Name = "m_buttonOpen";
            this.m_buttonOpen.Size = new System.Drawing.Size(75, 23);
            this.m_buttonOpen.TabIndex = 22;
            this.m_buttonOpen.Text = "open";
            this.m_buttonOpen.UseVisualStyleBackColor = true;
            this.m_buttonOpen.Click += new System.EventHandler(this.m_buttonOpen_Click);
            // 
            // m_buttonClose
            // 
            this.m_buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonClose.Location = new System.Drawing.Point(94, 450);
            this.m_buttonClose.Name = "m_buttonClose";
            this.m_buttonClose.Size = new System.Drawing.Size(75, 23);
            this.m_buttonClose.TabIndex = 21;
            this.m_buttonClose.Text = "close";
            this.m_buttonClose.UseVisualStyleBackColor = true;
            this.m_buttonClose.Click += new System.EventHandler(this.m_buttonClose_Click);
            // 
            // m_buttonSetup
            // 
            this.m_buttonSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonSetup.Location = new System.Drawing.Point(355, 450);
            this.m_buttonSetup.Name = "m_buttonSetup";
            this.m_buttonSetup.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSetup.TabIndex = 20;
            this.m_buttonSetup.Text = "setup...";
            this.m_buttonSetup.UseVisualStyleBackColor = true;
            this.m_buttonSetup.Click += new System.EventHandler(this.m_buttonSetup_Click);
            // 
            // m_buttonScan
            // 
            this.m_buttonScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonScan.Location = new System.Drawing.Point(436, 450);
            this.m_buttonScan.Name = "m_buttonScan";
            this.m_buttonScan.Size = new System.Drawing.Size(75, 23);
            this.m_buttonScan.TabIndex = 19;
            this.m_buttonScan.Text = "scan";
            this.m_buttonScan.UseVisualStyleBackColor = true;
            this.m_buttonScan.Click += new System.EventHandler(this.m_buttonScan_Click);
            // 
            // m_splitcontainerImages
            // 
            this.m_splitcontainerImages.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_splitcontainerImages.Location = new System.Drawing.Point(13, 15);
            this.m_splitcontainerImages.Name = "m_splitcontainerImages";
            // 
            // m_splitcontainerImages.Panel1
            // 
            this.m_splitcontainerImages.Panel1.Controls.Add(this.m_pictureboxImage1);
            this.m_splitcontainerImages.Panel1.Controls.Add(this.m_textbox1);
            // 
            // m_splitcontainerImages.Panel2
            // 
            this.m_splitcontainerImages.Panel2.Controls.Add(this.m_pictureboxImage2);
            this.m_splitcontainerImages.Panel2.Controls.Add(this.m_textbox2);
            this.m_splitcontainerImages.Size = new System.Drawing.Size(579, 370);
            this.m_splitcontainerImages.SplitterDistance = 289;
            this.m_splitcontainerImages.TabIndex = 27;
            // 
            // m_pictureboxImage1
            // 
            this.m_pictureboxImage1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_pictureboxImage1.Dock = System.Windows.Forms.DockStyle.Top;
            this.m_pictureboxImage1.Location = new System.Drawing.Point(0, 0);
            this.m_pictureboxImage1.Name = "m_pictureboxImage1";
            this.m_pictureboxImage1.Size = new System.Drawing.Size(289, 341);
            this.m_pictureboxImage1.TabIndex = 0;
            this.m_pictureboxImage1.TabStop = false;
            // 
            // m_textbox1
            // 
            this.m_textbox1.BackColor = System.Drawing.SystemColors.Window;
            this.m_textbox1.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_textbox1.Location = new System.Drawing.Point(0, 350);
            this.m_textbox1.Name = "m_textbox1";
            this.m_textbox1.ReadOnly = true;
            this.m_textbox1.Size = new System.Drawing.Size(289, 20);
            this.m_textbox1.TabIndex = 13;
            // 
            // m_pictureboxImage2
            // 
            this.m_pictureboxImage2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_pictureboxImage2.Dock = System.Windows.Forms.DockStyle.Top;
            this.m_pictureboxImage2.Location = new System.Drawing.Point(0, 0);
            this.m_pictureboxImage2.Name = "m_pictureboxImage2";
            this.m_pictureboxImage2.Size = new System.Drawing.Size(286, 341);
            this.m_pictureboxImage2.TabIndex = 4;
            this.m_pictureboxImage2.TabStop = false;
            // 
            // m_textbox2
            // 
            this.m_textbox2.BackColor = System.Drawing.SystemColors.Window;
            this.m_textbox2.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.m_textbox2.Location = new System.Drawing.Point(0, 350);
            this.m_textbox2.Name = "m_textbox2";
            this.m_textbox2.ReadOnly = true;
            this.m_textbox2.Size = new System.Drawing.Size(286, 20);
            this.m_textbox2.TabIndex = 14;
            // 
            // FormScan
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(605, 482);
            this.Controls.Add(this.m_comboboxCloudApiRoot);
            this.Controls.Add(this.m_buttonCloud);
            this.Controls.Add(this.m_textboxSummary);
            this.Controls.Add(this.m_buttonSelect);
            this.Controls.Add(this.m_buttonStop);
            this.Controls.Add(this.m_buttonOpen);
            this.Controls.Add(this.m_buttonClose);
            this.Controls.Add(this.m_buttonSetup);
            this.Controls.Add(this.m_buttonScan);
            this.Controls.Add(this.m_splitcontainerImages);
            this.Name = "FormScan";
            this.Text = "twain direct: application";
            this.cloudMenuStrip.ResumeLayout(false);
            this.m_splitcontainerImages.Panel1.ResumeLayout(false);
            this.m_splitcontainerImages.Panel1.PerformLayout();
            this.m_splitcontainerImages.Panel2.ResumeLayout(false);
            this.m_splitcontainerImages.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.m_splitcontainerImages)).EndInit();
            this.m_splitcontainerImages.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.ContextMenuStrip cloudMenuStrip;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem loginWithGoogleToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem loginWithFacebookToolStripMenuItem;
        private System.Windows.Forms.ComboBox m_comboboxCloudApiRoot;
        private System.Windows.Forms.Button m_buttonCloud;
        private System.Windows.Forms.TextBox m_textboxSummary;
        private System.Windows.Forms.Button m_buttonSelect;
        private System.Windows.Forms.Button m_buttonStop;
        private System.Windows.Forms.Button m_buttonOpen;
        private System.Windows.Forms.Button m_buttonClose;
        private System.Windows.Forms.Button m_buttonSetup;
        private System.Windows.Forms.Button m_buttonScan;
        private System.Windows.Forms.SplitContainer m_splitcontainerImages;
        private System.Windows.Forms.PictureBox m_pictureboxImage1;
        private System.Windows.Forms.TextBox m_textbox1;
        private System.Windows.Forms.PictureBox m_pictureboxImage2;
        private System.Windows.Forms.TextBox m_textbox2;
    }
}