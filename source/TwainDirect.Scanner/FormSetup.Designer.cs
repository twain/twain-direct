namespace TwainDirect.Scanner
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
            this.m_checkboxRunOnLogin = new System.Windows.Forms.CheckBox();
            this.m_buttonManageTwainLocal = new System.Windows.Forms.Button();
            this.m_buttonCloudRegister = new System.Windows.Forms.Button();
            this.m_buttonRegister = new System.Windows.Forms.Button();
            this.m_labelStep1 = new System.Windows.Forms.Label();
            this.m_labelStep2 = new System.Windows.Forms.Label();
            this.m_labelStep3 = new System.Windows.Forms.Label();
            this.m_checkboxAdvertise = new System.Windows.Forms.CheckBox();
            this.m_checkboxConfirmation = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // m_checkboxRunOnLogin
            // 
            this.m_checkboxRunOnLogin.AutoSize = true;
            this.m_checkboxRunOnLogin.Location = new System.Drawing.Point(12, 195);
            this.m_checkboxRunOnLogin.Name = "m_checkboxRunOnLogin";
            this.m_checkboxRunOnLogin.Size = new System.Drawing.Size(187, 17);
            this.m_checkboxRunOnLogin.TabIndex = 70;
            this.m_checkboxRunOnLogin.Text = "Run this program when you log on";
            this.m_checkboxRunOnLogin.UseVisualStyleBackColor = true;
            this.m_checkboxRunOnLogin.CheckedChanged += new System.EventHandler(this.m_checkboxRunOnLogin_CheckedChanged);
            // 
            // m_buttonManageTwainLocal
            // 
            this.m_buttonManageTwainLocal.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonManageTwainLocal.Location = new System.Drawing.Point(12, 80);
            this.m_buttonManageTwainLocal.Name = "m_buttonManageTwainLocal";
            this.m_buttonManageTwainLocal.Size = new System.Drawing.Size(142, 23);
            this.m_buttonManageTwainLocal.TabIndex = 69;
            this.m_buttonManageTwainLocal.Text = "Manage Local...";
            this.m_buttonManageTwainLocal.UseVisualStyleBackColor = true;
            this.m_buttonManageTwainLocal.Click += new System.EventHandler(this.m_buttonManageTwainLocal_Click);
            // 
            // m_buttonCloudRegister
            // 
            this.m_buttonCloudRegister.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonCloudRegister.Location = new System.Drawing.Point(12, 139);
            this.m_buttonCloudRegister.Name = "m_buttonCloudRegister";
            this.m_buttonCloudRegister.Size = new System.Drawing.Size(142, 23);
            this.m_buttonCloudRegister.TabIndex = 68;
            this.m_buttonCloudRegister.Text = "Register Cloud...";
            this.m_buttonCloudRegister.UseVisualStyleBackColor = true;
            this.m_buttonCloudRegister.Click += new System.EventHandler(this.m_buttonCloudRegister_Click);
            // 
            // m_buttonRegister
            // 
            this.m_buttonRegister.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonRegister.Location = new System.Drawing.Point(12, 25);
            this.m_buttonRegister.Name = "m_buttonRegister";
            this.m_buttonRegister.Size = new System.Drawing.Size(142, 23);
            this.m_buttonRegister.TabIndex = 67;
            this.m_buttonRegister.Text = "Select Scanner...";
            this.m_buttonRegister.UseVisualStyleBackColor = true;
            this.m_buttonRegister.Click += new System.EventHandler(this.m_buttonRegister_Click);
            // 
            // m_labelStep1
            // 
            this.m_labelStep1.AutoSize = true;
            this.m_labelStep1.Location = new System.Drawing.Point(9, 9);
            this.m_labelStep1.Name = "m_labelStep1";
            this.m_labelStep1.Size = new System.Drawing.Size(242, 13);
            this.m_labelStep1.TabIndex = 71;
            this.m_labelStep1.Text = "Step 1: Select the TWAIN driver for your scanner.";
            // 
            // m_labelStep2
            // 
            this.m_labelStep2.AutoSize = true;
            this.m_labelStep2.Location = new System.Drawing.Point(9, 64);
            this.m_labelStep2.Name = "m_labelStep2";
            this.m_labelStep2.Size = new System.Drawing.Size(314, 13);
            this.m_labelStep2.TabIndex = 72;
            this.m_labelStep2.Text = "Step 2: Configure your system to support TWAIN Local (optional).";
            // 
            // m_labelStep3
            // 
            this.m_labelStep3.AutoSize = true;
            this.m_labelStep3.Location = new System.Drawing.Point(9, 123);
            this.m_labelStep3.Name = "m_labelStep3";
            this.m_labelStep3.Size = new System.Drawing.Size(223, 13);
            this.m_labelStep3.TabIndex = 73;
            this.m_labelStep3.Text = "Step 3: Register with TWAIN Cloud (optional).";
            // 
            // m_checkboxAdvertise
            // 
            this.m_checkboxAdvertise.AutoSize = true;
            this.m_checkboxAdvertise.Location = new System.Drawing.Point(12, 218);
            this.m_checkboxAdvertise.Name = "m_checkboxAdvertise";
            this.m_checkboxAdvertise.Size = new System.Drawing.Size(360, 17);
            this.m_checkboxAdvertise.TabIndex = 74;
            this.m_checkboxAdvertise.Text = "Advertise on TWAIN Local and TWAIN Cloud when this program starts";
            this.m_checkboxAdvertise.UseVisualStyleBackColor = true;
            // 
            // m_checkboxConfirmation
            // 
            this.m_checkboxConfirmation.AutoSize = true;
            this.m_checkboxConfirmation.Location = new System.Drawing.Point(12, 241);
            this.m_checkboxConfirmation.Name = "m_checkboxConfirmation";
            this.m_checkboxConfirmation.Size = new System.Drawing.Size(237, 17);
            this.m_checkboxConfirmation.TabIndex = 75;
            this.m_checkboxConfirmation.Text = "Prompt for confirmation when scanning starts";
            this.m_checkboxConfirmation.UseVisualStyleBackColor = true;
            // 
            // FormSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(557, 278);
            this.Controls.Add(this.m_checkboxConfirmation);
            this.Controls.Add(this.m_checkboxAdvertise);
            this.Controls.Add(this.m_labelStep3);
            this.Controls.Add(this.m_labelStep2);
            this.Controls.Add(this.m_labelStep1);
            this.Controls.Add(this.m_checkboxRunOnLogin);
            this.Controls.Add(this.m_buttonManageTwainLocal);
            this.Controls.Add(this.m_buttonCloudRegister);
            this.Controls.Add(this.m_buttonRegister);
            this.Name = "FormSetup";
            this.Text = "FormSetup";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox m_checkboxRunOnLogin;
        private System.Windows.Forms.Button m_buttonManageTwainLocal;
        private System.Windows.Forms.Button m_buttonCloudRegister;
        private System.Windows.Forms.Button m_buttonRegister;
        private System.Windows.Forms.Label m_labelStep1;
        private System.Windows.Forms.Label m_labelStep2;
        private System.Windows.Forms.Label m_labelStep3;
        private System.Windows.Forms.CheckBox m_checkboxAdvertise;
        private System.Windows.Forms.CheckBox m_checkboxConfirmation;
    }
}