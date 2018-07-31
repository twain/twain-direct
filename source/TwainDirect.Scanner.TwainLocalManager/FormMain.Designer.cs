namespace TwainDirect.Scanner.TwainLocalManager
{
    partial class FormMain
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
            this.m_buttonDeleteCertificates = new System.Windows.Forms.Button();
            this.m_buttonRefreshCertificates = new System.Windows.Forms.Button();
            this.m_groupboxSelfSignedCertificates = new System.Windows.Forms.GroupBox();
            this.m_richtextboxUrlAcl = new System.Windows.Forms.RichTextBox();
            this.m_labelUrlAcl = new System.Windows.Forms.Label();
            this.m_richtextboxExchange = new System.Windows.Forms.RichTextBox();
            this.m_richtextboxRoot = new System.Windows.Forms.RichTextBox();
            this.m_labelExchange = new System.Windows.Forms.Label();
            this.m_labelRoot = new System.Windows.Forms.Label();
            this.m_groupboxFirewall = new System.Windows.Forms.GroupBox();
            this.m_buttonDeleteFirewall = new System.Windows.Forms.Button();
            this.m_buttonCreateFirewall = new System.Windows.Forms.Button();
            this.m_richtextboxFirewall = new System.Windows.Forms.RichTextBox();
            this.m_labelFirewall = new System.Windows.Forms.Label();
            this.m_groupboxBonjour = new System.Windows.Forms.GroupBox();
            this.m_buttonUninstallBonjour = new System.Windows.Forms.Button();
            this.m_buttonInstallBonjour = new System.Windows.Forms.Button();
            this.m_richtextboxStatus = new System.Windows.Forms.RichTextBox();
            this.m_labelStatus = new System.Windows.Forms.Label();
            this.m_groupboxSelfSignedCertificates.SuspendLayout();
            this.m_groupboxFirewall.SuspendLayout();
            this.m_groupboxBonjour.SuspendLayout();
            this.SuspendLayout();
            // 
            // m_buttonDeleteCertificates
            // 
            this.m_buttonDeleteCertificates.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonDeleteCertificates.Location = new System.Drawing.Point(410, 152);
            this.m_buttonDeleteCertificates.Name = "m_buttonDeleteCertificates";
            this.m_buttonDeleteCertificates.Size = new System.Drawing.Size(141, 23);
            this.m_buttonDeleteCertificates.TabIndex = 6;
            this.m_buttonDeleteCertificates.Text = "Delete Certificates...";
            this.m_buttonDeleteCertificates.UseVisualStyleBackColor = true;
            this.m_buttonDeleteCertificates.Click += new System.EventHandler(this.DeleteCertificates_Click);
            // 
            // m_buttonRefreshCertificates
            // 
            this.m_buttonRefreshCertificates.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonRefreshCertificates.Location = new System.Drawing.Point(557, 152);
            this.m_buttonRefreshCertificates.Name = "m_buttonRefreshCertificates";
            this.m_buttonRefreshCertificates.Size = new System.Drawing.Size(141, 23);
            this.m_buttonRefreshCertificates.TabIndex = 7;
            this.m_buttonRefreshCertificates.Text = "Install Certificates...";
            this.m_buttonRefreshCertificates.UseVisualStyleBackColor = true;
            this.m_buttonRefreshCertificates.Click += new System.EventHandler(this.CreateCertificates_Click);
            // 
            // m_groupboxSelfSignedCertificates
            // 
            this.m_groupboxSelfSignedCertificates.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_richtextboxUrlAcl);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_labelUrlAcl);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_richtextboxExchange);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_buttonDeleteCertificates);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_buttonRefreshCertificates);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_richtextboxRoot);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_labelExchange);
            this.m_groupboxSelfSignedCertificates.Controls.Add(this.m_labelRoot);
            this.m_groupboxSelfSignedCertificates.Location = new System.Drawing.Point(12, 102);
            this.m_groupboxSelfSignedCertificates.Name = "m_groupboxSelfSignedCertificates";
            this.m_groupboxSelfSignedCertificates.Size = new System.Drawing.Size(715, 187);
            this.m_groupboxSelfSignedCertificates.TabIndex = 1;
            this.m_groupboxSelfSignedCertificates.TabStop = false;
            this.m_groupboxSelfSignedCertificates.Text = "Self-Signed Certificates";
            // 
            // m_richtextboxUrlAcl
            // 
            this.m_richtextboxUrlAcl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxUrlAcl.BackColor = System.Drawing.Color.White;
            this.m_richtextboxUrlAcl.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_richtextboxUrlAcl.DetectUrls = false;
            this.m_richtextboxUrlAcl.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_richtextboxUrlAcl.Location = new System.Drawing.Point(75, 87);
            this.m_richtextboxUrlAcl.Name = "m_richtextboxUrlAcl";
            this.m_richtextboxUrlAcl.ReadOnly = true;
            this.m_richtextboxUrlAcl.Size = new System.Drawing.Size(623, 59);
            this.m_richtextboxUrlAcl.TabIndex = 5;
            this.m_richtextboxUrlAcl.TabStop = false;
            this.m_richtextboxUrlAcl.Text = "";
            // 
            // m_labelUrlAcl
            // 
            this.m_labelUrlAcl.AutoSize = true;
            this.m_labelUrlAcl.Location = new System.Drawing.Point(7, 93);
            this.m_labelUrlAcl.Name = "m_labelUrlAcl";
            this.m_labelUrlAcl.Size = new System.Drawing.Size(38, 13);
            this.m_labelUrlAcl.TabIndex = 4;
            this.m_labelUrlAcl.Text = "UrlAcl:";
            // 
            // m_richtextboxExchange
            // 
            this.m_richtextboxExchange.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxExchange.BackColor = System.Drawing.Color.White;
            this.m_richtextboxExchange.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_richtextboxExchange.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_richtextboxExchange.Location = new System.Drawing.Point(75, 54);
            this.m_richtextboxExchange.Multiline = false;
            this.m_richtextboxExchange.Name = "m_richtextboxExchange";
            this.m_richtextboxExchange.ReadOnly = true;
            this.m_richtextboxExchange.Size = new System.Drawing.Size(623, 24);
            this.m_richtextboxExchange.TabIndex = 3;
            this.m_richtextboxExchange.TabStop = false;
            this.m_richtextboxExchange.Text = "";
            // 
            // m_richtextboxRoot
            // 
            this.m_richtextboxRoot.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxRoot.BackColor = System.Drawing.Color.White;
            this.m_richtextboxRoot.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_richtextboxRoot.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_richtextboxRoot.Location = new System.Drawing.Point(75, 21);
            this.m_richtextboxRoot.Multiline = false;
            this.m_richtextboxRoot.Name = "m_richtextboxRoot";
            this.m_richtextboxRoot.ReadOnly = true;
            this.m_richtextboxRoot.Size = new System.Drawing.Size(623, 24);
            this.m_richtextboxRoot.TabIndex = 1;
            this.m_richtextboxRoot.TabStop = false;
            this.m_richtextboxRoot.Text = "";
            // 
            // m_labelExchange
            // 
            this.m_labelExchange.AutoSize = true;
            this.m_labelExchange.Location = new System.Drawing.Point(7, 60);
            this.m_labelExchange.Name = "m_labelExchange";
            this.m_labelExchange.Size = new System.Drawing.Size(58, 13);
            this.m_labelExchange.TabIndex = 2;
            this.m_labelExchange.Text = "Exchange:";
            // 
            // m_labelRoot
            // 
            this.m_labelRoot.AutoSize = true;
            this.m_labelRoot.Location = new System.Drawing.Point(7, 27);
            this.m_labelRoot.Name = "m_labelRoot";
            this.m_labelRoot.Size = new System.Drawing.Size(33, 13);
            this.m_labelRoot.TabIndex = 0;
            this.m_labelRoot.Text = "Root:";
            // 
            // m_groupboxFirewall
            // 
            this.m_groupboxFirewall.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_groupboxFirewall.Controls.Add(this.m_buttonDeleteFirewall);
            this.m_groupboxFirewall.Controls.Add(this.m_buttonCreateFirewall);
            this.m_groupboxFirewall.Controls.Add(this.m_richtextboxFirewall);
            this.m_groupboxFirewall.Controls.Add(this.m_labelFirewall);
            this.m_groupboxFirewall.Location = new System.Drawing.Point(12, 295);
            this.m_groupboxFirewall.Name = "m_groupboxFirewall";
            this.m_groupboxFirewall.Size = new System.Drawing.Size(715, 174);
            this.m_groupboxFirewall.TabIndex = 2;
            this.m_groupboxFirewall.TabStop = false;
            this.m_groupboxFirewall.Text = "Firewall";
            // 
            // m_buttonDeleteFirewall
            // 
            this.m_buttonDeleteFirewall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonDeleteFirewall.Location = new System.Drawing.Point(408, 139);
            this.m_buttonDeleteFirewall.Name = "m_buttonDeleteFirewall";
            this.m_buttonDeleteFirewall.Size = new System.Drawing.Size(142, 23);
            this.m_buttonDeleteFirewall.TabIndex = 2;
            this.m_buttonDeleteFirewall.Text = "Delete Firewall Rule...";
            this.m_buttonDeleteFirewall.UseVisualStyleBackColor = true;
            this.m_buttonDeleteFirewall.Click += new System.EventHandler(this.DeleteFirewall_Click);
            // 
            // m_buttonCreateFirewall
            // 
            this.m_buttonCreateFirewall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonCreateFirewall.Location = new System.Drawing.Point(556, 139);
            this.m_buttonCreateFirewall.Name = "m_buttonCreateFirewall";
            this.m_buttonCreateFirewall.Size = new System.Drawing.Size(142, 23);
            this.m_buttonCreateFirewall.TabIndex = 3;
            this.m_buttonCreateFirewall.Text = "Create Firewall Rule...";
            this.m_buttonCreateFirewall.UseVisualStyleBackColor = true;
            this.m_buttonCreateFirewall.Click += new System.EventHandler(this.CreateFirewall_Click);
            // 
            // m_richtextboxFirewall
            // 
            this.m_richtextboxFirewall.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxFirewall.BackColor = System.Drawing.Color.White;
            this.m_richtextboxFirewall.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_richtextboxFirewall.DetectUrls = false;
            this.m_richtextboxFirewall.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_richtextboxFirewall.Location = new System.Drawing.Point(75, 24);
            this.m_richtextboxFirewall.Name = "m_richtextboxFirewall";
            this.m_richtextboxFirewall.ReadOnly = true;
            this.m_richtextboxFirewall.Size = new System.Drawing.Size(623, 109);
            this.m_richtextboxFirewall.TabIndex = 1;
            this.m_richtextboxFirewall.TabStop = false;
            this.m_richtextboxFirewall.Text = "";
            // 
            // m_labelFirewall
            // 
            this.m_labelFirewall.AutoSize = true;
            this.m_labelFirewall.Location = new System.Drawing.Point(7, 30);
            this.m_labelFirewall.Name = "m_labelFirewall";
            this.m_labelFirewall.Size = new System.Drawing.Size(45, 13);
            this.m_labelFirewall.TabIndex = 0;
            this.m_labelFirewall.Text = "Firewall:";
            // 
            // m_groupboxBonjour
            // 
            this.m_groupboxBonjour.Controls.Add(this.m_buttonUninstallBonjour);
            this.m_groupboxBonjour.Controls.Add(this.m_buttonInstallBonjour);
            this.m_groupboxBonjour.Controls.Add(this.m_richtextboxStatus);
            this.m_groupboxBonjour.Controls.Add(this.m_labelStatus);
            this.m_groupboxBonjour.Location = new System.Drawing.Point(12, 13);
            this.m_groupboxBonjour.Name = "m_groupboxBonjour";
            this.m_groupboxBonjour.Size = new System.Drawing.Size(715, 83);
            this.m_groupboxBonjour.TabIndex = 0;
            this.m_groupboxBonjour.TabStop = false;
            this.m_groupboxBonjour.Text = "Apple\'s Bonjour";
            // 
            // m_buttonUninstallBonjour
            // 
            this.m_buttonUninstallBonjour.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonUninstallBonjour.Location = new System.Drawing.Point(415, 50);
            this.m_buttonUninstallBonjour.Name = "m_buttonUninstallBonjour";
            this.m_buttonUninstallBonjour.Size = new System.Drawing.Size(141, 23);
            this.m_buttonUninstallBonjour.TabIndex = 2;
            this.m_buttonUninstallBonjour.Text = "Uninstall Bonjour...";
            this.m_buttonUninstallBonjour.UseVisualStyleBackColor = true;
            this.m_buttonUninstallBonjour.Click += new System.EventHandler(this.UninstallBonjour_Click);
            // 
            // m_buttonInstallBonjour
            // 
            this.m_buttonInstallBonjour.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonInstallBonjour.Location = new System.Drawing.Point(562, 50);
            this.m_buttonInstallBonjour.Name = "m_buttonInstallBonjour";
            this.m_buttonInstallBonjour.Size = new System.Drawing.Size(141, 23);
            this.m_buttonInstallBonjour.TabIndex = 3;
            this.m_buttonInstallBonjour.Text = "Install Bonjour...";
            this.m_buttonInstallBonjour.UseVisualStyleBackColor = true;
            this.m_buttonInstallBonjour.Click += new System.EventHandler(this.InstallBonjour_Click);
            // 
            // m_richtextboxStatus
            // 
            this.m_richtextboxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxStatus.BackColor = System.Drawing.Color.White;
            this.m_richtextboxStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.m_richtextboxStatus.Font = new System.Drawing.Font("Courier New", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.m_richtextboxStatus.Location = new System.Drawing.Point(80, 20);
            this.m_richtextboxStatus.Multiline = false;
            this.m_richtextboxStatus.Name = "m_richtextboxStatus";
            this.m_richtextboxStatus.ReadOnly = true;
            this.m_richtextboxStatus.Size = new System.Drawing.Size(623, 24);
            this.m_richtextboxStatus.TabIndex = 1;
            this.m_richtextboxStatus.TabStop = false;
            this.m_richtextboxStatus.Text = "";
            // 
            // m_labelStatus
            // 
            this.m_labelStatus.AutoSize = true;
            this.m_labelStatus.Location = new System.Drawing.Point(12, 26);
            this.m_labelStatus.Name = "m_labelStatus";
            this.m_labelStatus.Size = new System.Drawing.Size(40, 13);
            this.m_labelStatus.TabIndex = 0;
            this.m_labelStatus.Text = "Status:";
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(740, 481);
            this.Controls.Add(this.m_groupboxBonjour);
            this.Controls.Add(this.m_groupboxFirewall);
            this.Controls.Add(this.m_groupboxSelfSignedCertificates);
            this.MaximumSize = new System.Drawing.Size(756, 520);
            this.MinimumSize = new System.Drawing.Size(756, 520);
            this.Name = "FormMain";
            this.Text = "TWAIN Local Manager";
            this.m_groupboxSelfSignedCertificates.ResumeLayout(false);
            this.m_groupboxSelfSignedCertificates.PerformLayout();
            this.m_groupboxFirewall.ResumeLayout(false);
            this.m_groupboxFirewall.PerformLayout();
            this.m_groupboxBonjour.ResumeLayout(false);
            this.m_groupboxBonjour.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button m_buttonDeleteCertificates;
        private System.Windows.Forms.Button m_buttonRefreshCertificates;
        private System.Windows.Forms.GroupBox m_groupboxSelfSignedCertificates;
        private System.Windows.Forms.RichTextBox m_richtextboxExchange;
        private System.Windows.Forms.RichTextBox m_richtextboxRoot;
        private System.Windows.Forms.Label m_labelExchange;
        private System.Windows.Forms.Label m_labelRoot;
        private System.Windows.Forms.RichTextBox m_richtextboxUrlAcl;
        private System.Windows.Forms.Label m_labelUrlAcl;
        private System.Windows.Forms.GroupBox m_groupboxFirewall;
        private System.Windows.Forms.Button m_buttonDeleteFirewall;
        private System.Windows.Forms.Button m_buttonCreateFirewall;
        private System.Windows.Forms.RichTextBox m_richtextboxFirewall;
        private System.Windows.Forms.Label m_labelFirewall;
        private System.Windows.Forms.GroupBox m_groupboxBonjour;
        private System.Windows.Forms.Button m_buttonUninstallBonjour;
        private System.Windows.Forms.Button m_buttonInstallBonjour;
        private System.Windows.Forms.RichTextBox m_richtextboxStatus;
        private System.Windows.Forms.Label m_labelStatus;
    }
}

