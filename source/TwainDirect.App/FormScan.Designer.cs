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
            this.m_pictureboxImage1 = new System.Windows.Forms.PictureBox();
            this.m_buttonScan = new System.Windows.Forms.Button();
            this.m_buttonSetup = new System.Windows.Forms.Button();
            this.m_pictureboxImage2 = new System.Windows.Forms.PictureBox();
            this.m_buttonClose = new System.Windows.Forms.Button();
            this.m_buttonOpen = new System.Windows.Forms.Button();
            this.m_buttonStop = new System.Windows.Forms.Button();
            this.m_listviewCertification = new System.Windows.Forms.ListView();
            this.m_buttonSelect = new System.Windows.Forms.Button();
            this.m_textbox1 = new System.Windows.Forms.TextBox();
            this.m_textbox2 = new System.Windows.Forms.TextBox();
            this.m_textboxSummary = new System.Windows.Forms.TextBox();
            this.cloudButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage2)).BeginInit();
            this.SuspendLayout();
            // 
            // m_pictureboxImage1
            // 
            this.m_pictureboxImage1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.m_pictureboxImage1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_pictureboxImage1.Location = new System.Drawing.Point(13, 13);
            this.m_pictureboxImage1.Name = "m_pictureboxImage1";
            this.m_pictureboxImage1.Size = new System.Drawing.Size(335, 424);
            this.m_pictureboxImage1.TabIndex = 0;
            this.m_pictureboxImage1.TabStop = false;
            this.m_pictureboxImage1.DoubleClick += new System.EventHandler(this.m_pictureboxImage_DoubleClick);
            // 
            // m_buttonScan
            // 
            this.m_buttonScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonScan.Location = new System.Drawing.Point(533, 507);
            this.m_buttonScan.Name = "m_buttonScan";
            this.m_buttonScan.Size = new System.Drawing.Size(75, 23);
            this.m_buttonScan.TabIndex = 1;
            this.m_buttonScan.Text = "scan";
            this.m_buttonScan.UseVisualStyleBackColor = true;
            this.m_buttonScan.Click += new System.EventHandler(this.m_buttonScan_Click);
            // 
            // m_buttonSetup
            // 
            this.m_buttonSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonSetup.Location = new System.Drawing.Point(452, 507);
            this.m_buttonSetup.Name = "m_buttonSetup";
            this.m_buttonSetup.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSetup.TabIndex = 3;
            this.m_buttonSetup.Text = "setup...";
            this.m_buttonSetup.UseVisualStyleBackColor = true;
            this.m_buttonSetup.Click += new System.EventHandler(this.m_buttonSetup_Click);
            // 
            // m_pictureboxImage2
            // 
            this.m_pictureboxImage2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_pictureboxImage2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_pictureboxImage2.Location = new System.Drawing.Point(354, 13);
            this.m_pictureboxImage2.Name = "m_pictureboxImage2";
            this.m_pictureboxImage2.Size = new System.Drawing.Size(335, 424);
            this.m_pictureboxImage2.TabIndex = 4;
            this.m_pictureboxImage2.TabStop = false;
            this.m_pictureboxImage2.DoubleClick += new System.EventHandler(this.m_pictureboxImage_DoubleClick);
            // 
            // m_buttonClose
            // 
            this.m_buttonClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonClose.Location = new System.Drawing.Point(95, 507);
            this.m_buttonClose.Name = "m_buttonClose";
            this.m_buttonClose.Size = new System.Drawing.Size(75, 23);
            this.m_buttonClose.TabIndex = 5;
            this.m_buttonClose.Text = "close";
            this.m_buttonClose.UseVisualStyleBackColor = true;
            this.m_buttonClose.Click += new System.EventHandler(this.m_buttonClose_Click);
            // 
            // m_buttonOpen
            // 
            this.m_buttonOpen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonOpen.Location = new System.Drawing.Point(13, 507);
            this.m_buttonOpen.Name = "m_buttonOpen";
            this.m_buttonOpen.Size = new System.Drawing.Size(75, 23);
            this.m_buttonOpen.TabIndex = 6;
            this.m_buttonOpen.Text = "open";
            this.m_buttonOpen.UseVisualStyleBackColor = true;
            this.m_buttonOpen.Click += new System.EventHandler(this.m_buttonOpen_Click);
            // 
            // m_buttonStop
            // 
            this.m_buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStop.Location = new System.Drawing.Point(614, 507);
            this.m_buttonStop.Name = "m_buttonStop";
            this.m_buttonStop.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStop.TabIndex = 7;
            this.m_buttonStop.Text = "stop";
            this.m_buttonStop.UseVisualStyleBackColor = true;
            this.m_buttonStop.Click += new System.EventHandler(this.m_buttonStop_Click);
            // 
            // m_listviewCertification
            // 
            this.m_listviewCertification.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_listviewCertification.Location = new System.Drawing.Point(13, 13);
            this.m_listviewCertification.Name = "m_listviewCertification";
            this.m_listviewCertification.Size = new System.Drawing.Size(676, 424);
            this.m_listviewCertification.TabIndex = 8;
            this.m_listviewCertification.UseCompatibleStateImageBehavior = false;
            // 
            // m_buttonSelect
            // 
            this.m_buttonSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonSelect.Location = new System.Drawing.Point(176, 507);
            this.m_buttonSelect.Name = "m_buttonSelect";
            this.m_buttonSelect.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSelect.TabIndex = 9;
            this.m_buttonSelect.Text = "select...";
            this.m_buttonSelect.UseVisualStyleBackColor = true;
            this.m_buttonSelect.Click += new System.EventHandler(this.m_buttonSelect_Click);
            // 
            // m_textbox1
            // 
            this.m_textbox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textbox1.Location = new System.Drawing.Point(13, 444);
            this.m_textbox1.Name = "m_textbox1";
            this.m_textbox1.ReadOnly = true;
            this.m_textbox1.Size = new System.Drawing.Size(335, 20);
            this.m_textbox1.TabIndex = 13;
            this.m_textbox1.DoubleClick += new System.EventHandler(this.m_textbox_DoubleClick);
            // 
            // m_textbox2
            // 
            this.m_textbox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textbox2.Location = new System.Drawing.Point(354, 444);
            this.m_textbox2.Name = "m_textbox2";
            this.m_textbox2.ReadOnly = true;
            this.m_textbox2.Size = new System.Drawing.Size(335, 20);
            this.m_textbox2.TabIndex = 14;
            this.m_textbox2.DoubleClick += new System.EventHandler(this.m_textbox_DoubleClick);
            // 
            // m_textboxSummary
            // 
            this.m_textboxSummary.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textboxSummary.Location = new System.Drawing.Point(13, 471);
            this.m_textboxSummary.Name = "m_textboxSummary";
            this.m_textboxSummary.ReadOnly = true;
            this.m_textboxSummary.Size = new System.Drawing.Size(676, 20);
            this.m_textboxSummary.TabIndex = 15;
            // 
            // cloudButton
            // 
            this.cloudButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.cloudButton.Location = new System.Drawing.Point(314, 507);
            this.cloudButton.Name = "cloudButton";
            this.cloudButton.Size = new System.Drawing.Size(75, 23);
            this.cloudButton.TabIndex = 16;
            this.cloudButton.Text = "cloud...";
            this.cloudButton.UseVisualStyleBackColor = true;
            this.cloudButton.Click += new System.EventHandler(this.cloudButton_Click);
            // 
            // FormScan
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(702, 545);
            this.Controls.Add(this.cloudButton);
            this.Controls.Add(this.m_textboxSummary);
            this.Controls.Add(this.m_textbox2);
            this.Controls.Add(this.m_textbox1);
            this.Controls.Add(this.m_buttonSelect);
            this.Controls.Add(this.m_buttonStop);
            this.Controls.Add(this.m_buttonOpen);
            this.Controls.Add(this.m_buttonClose);
            this.Controls.Add(this.m_pictureboxImage2);
            this.Controls.Add(this.m_buttonSetup);
            this.Controls.Add(this.m_buttonScan);
            this.Controls.Add(this.m_pictureboxImage1);
            this.Controls.Add(this.m_listviewCertification);
            this.Name = "FormScan";
            this.Text = "twain direct: application";
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.m_pictureboxImage2)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox m_pictureboxImage1;
        private System.Windows.Forms.Button m_buttonScan;
        private System.Windows.Forms.Button m_buttonSetup;
        private System.Windows.Forms.PictureBox m_pictureboxImage2;
        private System.Windows.Forms.Button m_buttonClose;
        private System.Windows.Forms.Button m_buttonOpen;
        private System.Windows.Forms.Button m_buttonStop;
        private System.Windows.Forms.ListView m_listviewCertification;
        private System.Windows.Forms.Button m_buttonSelect;
        private System.Windows.Forms.TextBox m_textbox1;
        private System.Windows.Forms.TextBox m_textbox2;
        private System.Windows.Forms.TextBox m_textboxSummary;
        private System.Windows.Forms.Button cloudButton;
    }
}