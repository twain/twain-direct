namespace TwainDirect.App
{
    partial class FormSetup
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSetup));
            this.m_labelSelectDestinationFolder = new System.Windows.Forms.Label();
            this.m_textboxFolder = new System.Windows.Forms.TextBox();
            this.m_buttonSelectDestinationFolder = new System.Windows.Forms.Button();
            this.m_checkboxThumbnails = new System.Windows.Forms.CheckBox();
            this.m_checkboxMetadataWithImage = new System.Windows.Forms.CheckBox();
            this.m_textboxUseUiSettings = new System.Windows.Forms.TextBox();
            this.m_buttonUseUiSettings = new System.Windows.Forms.Button();
            this.m_labelUseUiSettings = new System.Windows.Forms.Label();
            this.m_buttonGetEncryptionReport = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // m_labelSelectDestinationFolder
            // 
            this.m_labelSelectDestinationFolder.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_labelSelectDestinationFolder.AutoSize = true;
            this.m_labelSelectDestinationFolder.Location = new System.Drawing.Point(14, 12);
            this.m_labelSelectDestinationFolder.Name = "m_labelSelectDestinationFolder";
            this.m_labelSelectDestinationFolder.Size = new System.Drawing.Size(152, 13);
            this.m_labelSelectDestinationFolder.TabIndex = 4;
            this.m_labelSelectDestinationFolder.Text = "select image destination folder:";
            // 
            // m_textboxFolder
            // 
            this.m_textboxFolder.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_textboxFolder.Location = new System.Drawing.Point(17, 30);
            this.m_textboxFolder.Name = "m_textboxFolder";
            this.m_textboxFolder.Size = new System.Drawing.Size(456, 20);
            this.m_textboxFolder.TabIndex = 5;
            this.m_textboxFolder.TextChanged += new System.EventHandler(this.m_textboxFolder_TextChanged);
            // 
            // m_buttonSelectDestinationFolder
            // 
            this.m_buttonSelectDestinationFolder.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_buttonSelectDestinationFolder.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_buttonSelectDestinationFolder.BackgroundImage")));
            this.m_buttonSelectDestinationFolder.Location = new System.Drawing.Point(492, 28);
            this.m_buttonSelectDestinationFolder.Name = "m_buttonSelectDestinationFolder";
            this.m_buttonSelectDestinationFolder.Size = new System.Drawing.Size(26, 23);
            this.m_buttonSelectDestinationFolder.TabIndex = 6;
            this.m_buttonSelectDestinationFolder.UseVisualStyleBackColor = true;
            this.m_buttonSelectDestinationFolder.Click += new System.EventHandler(this.m_buttonBrowse_Click);
            // 
            // m_checkboxThumbnails
            // 
            this.m_checkboxThumbnails.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_checkboxThumbnails.AutoSize = true;
            this.m_checkboxThumbnails.Location = new System.Drawing.Point(17, 114);
            this.m_checkboxThumbnails.Name = "m_checkboxThumbnails";
            this.m_checkboxThumbnails.Size = new System.Drawing.Size(100, 17);
            this.m_checkboxThumbnails.TabIndex = 17;
            this.m_checkboxThumbnails.Text = "Get Thumbnails";
            this.m_checkboxThumbnails.UseVisualStyleBackColor = true;
            // 
            // m_checkboxMetadataWithImage
            // 
            this.m_checkboxMetadataWithImage.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_checkboxMetadataWithImage.AutoSize = true;
            this.m_checkboxMetadataWithImage.Location = new System.Drawing.Point(17, 134);
            this.m_checkboxMetadataWithImage.Name = "m_checkboxMetadataWithImage";
            this.m_checkboxMetadataWithImage.Size = new System.Drawing.Size(269, 17);
            this.m_checkboxMetadataWithImage.TabIndex = 19;
            this.m_checkboxMetadataWithImage.Text = "Include JSON metadata when transferring an image";
            this.m_checkboxMetadataWithImage.UseVisualStyleBackColor = true;
            // 
            // m_textboxUseUiSettings
            // 
            this.m_textboxUseUiSettings.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_textboxUseUiSettings.Location = new System.Drawing.Point(17, 78);
            this.m_textboxUseUiSettings.Name = "m_textboxUseUiSettings";
            this.m_textboxUseUiSettings.Size = new System.Drawing.Size(456, 20);
            this.m_textboxUseUiSettings.TabIndex = 7;
            // 
            // m_buttonUseUiSettings
            // 
            this.m_buttonUseUiSettings.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_buttonUseUiSettings.BackgroundImage = ((System.Drawing.Image)(resources.GetObject("m_buttonUseUiSettings.BackgroundImage")));
            this.m_buttonUseUiSettings.Location = new System.Drawing.Point(492, 78);
            this.m_buttonUseUiSettings.Name = "m_buttonUseUiSettings";
            this.m_buttonUseUiSettings.Size = new System.Drawing.Size(26, 23);
            this.m_buttonUseUiSettings.TabIndex = 8;
            this.m_buttonUseUiSettings.UseVisualStyleBackColor = true;
            this.m_buttonUseUiSettings.Click += new System.EventHandler(this.m_buttonUseUiSettings_Click);
            // 
            // m_labelUseUiSettings
            // 
            this.m_labelUseUiSettings.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.m_labelUseUiSettings.AutoSize = true;
            this.m_labelUseUiSettings.Location = new System.Drawing.Point(14, 62);
            this.m_labelUseUiSettings.Name = "m_labelUseUiSettings";
            this.m_labelUseUiSettings.Size = new System.Drawing.Size(271, 13);
            this.m_labelUseUiSettings.TabIndex = 1;
            this.m_labelUseUiSettings.Text = "Select task for next scan (if blank use scanner defaults):";
            // 
            // m_buttonGetEncryptionReport
            // 
            this.m_buttonGetEncryptionReport.Location = new System.Drawing.Point(17, 169);
            this.m_buttonGetEncryptionReport.Name = "m_buttonGetEncryptionReport";
            this.m_buttonGetEncryptionReport.Size = new System.Drawing.Size(186, 23);
            this.m_buttonGetEncryptionReport.TabIndex = 20;
            this.m_buttonGetEncryptionReport.Text = "Get Encryption Report";
            this.m_buttonGetEncryptionReport.UseVisualStyleBackColor = true;
            this.m_buttonGetEncryptionReport.Click += new System.EventHandler(this.m_buttonGetEncryptionReport_Click);
            // 
            // FormSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(536, 211);
            this.Controls.Add(this.m_buttonGetEncryptionReport);
            this.Controls.Add(this.m_buttonSelectDestinationFolder);
            this.Controls.Add(this.m_buttonUseUiSettings);
            this.Controls.Add(this.m_labelUseUiSettings);
            this.Controls.Add(this.m_checkboxMetadataWithImage);
            this.Controls.Add(this.m_textboxUseUiSettings);
            this.Controls.Add(this.m_checkboxThumbnails);
            this.Controls.Add(this.m_textboxFolder);
            this.Controls.Add(this.m_labelSelectDestinationFolder);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(552, 250);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(552, 250);
            this.Name = "FormSetup";
            this.Text = "setup scan session";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label m_labelSelectDestinationFolder;
        private System.Windows.Forms.TextBox m_textboxFolder;
        private System.Windows.Forms.Button m_buttonSelectDestinationFolder;
        private System.Windows.Forms.CheckBox m_checkboxThumbnails;
        private System.Windows.Forms.CheckBox m_checkboxMetadataWithImage;
        private System.Windows.Forms.TextBox m_textboxUseUiSettings;
        private System.Windows.Forms.Button m_buttonUseUiSettings;
        private System.Windows.Forms.Label m_labelUseUiSettings;
        private System.Windows.Forms.Button m_buttonGetEncryptionReport;
    }
}