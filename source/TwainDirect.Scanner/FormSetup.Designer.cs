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
            this.m_labelCurrentDriver = new System.Windows.Forms.Label();
            this.m_textboxCurrentDriver = new System.Windows.Forms.TextBox();
            this.m_textboxCurrentNote = new System.Windows.Forms.TextBox();
            this.m_labelCurrentNote = new System.Windows.Forms.Label();
            this.m_buttonManageCloud = new System.Windows.Forms.Button();
            this.m_RegisteredDeviceLabel = new System.Windows.Forms.Label();
            this.m_CloudDevicesComboBox = new System.Windows.Forms.ComboBox();
            this.m_comboboxCloudApiRoot = new System.Windows.Forms.ComboBox();
            this.m_checkboxStartNpm = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // m_checkboxRunOnLogin
            // 
            this.m_checkboxRunOnLogin.AutoSize = true;
            this.m_checkboxRunOnLogin.Location = new System.Drawing.Point(12, 313);
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
            this.m_buttonManageTwainLocal.Location = new System.Drawing.Point(49, 145);
            this.m_buttonManageTwainLocal.Name = "m_buttonManageTwainLocal";
            this.m_buttonManageTwainLocal.Size = new System.Drawing.Size(142, 23);
            this.m_buttonManageTwainLocal.TabIndex = 69;
            this.m_buttonManageTwainLocal.Text = "manage local...";
            this.m_buttonManageTwainLocal.UseVisualStyleBackColor = true;
            this.m_buttonManageTwainLocal.Click += new System.EventHandler(this.m_buttonManageTwainLocal_Click);
            // 
            // m_buttonCloudRegister
            // 
            this.m_buttonCloudRegister.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonCloudRegister.Location = new System.Drawing.Point(249, 207);
            this.m_buttonCloudRegister.Name = "m_buttonCloudRegister";
            this.m_buttonCloudRegister.Size = new System.Drawing.Size(142, 23);
            this.m_buttonCloudRegister.TabIndex = 68;
            this.m_buttonCloudRegister.Text = "register cloud...";
            this.m_buttonCloudRegister.UseVisualStyleBackColor = true;
            this.m_buttonCloudRegister.Click += new System.EventHandler(this.m_buttonCloudRegister_Click);
            // 
            // m_buttonRegister
            // 
            this.m_buttonRegister.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonRegister.Location = new System.Drawing.Point(49, 81);
            this.m_buttonRegister.Name = "m_buttonRegister";
            this.m_buttonRegister.Size = new System.Drawing.Size(142, 23);
            this.m_buttonRegister.TabIndex = 67;
            this.m_buttonRegister.Text = "select scanner...";
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
            this.m_labelStep2.Location = new System.Drawing.Point(9, 129);
            this.m_labelStep2.Name = "m_labelStep2";
            this.m_labelStep2.Size = new System.Drawing.Size(314, 13);
            this.m_labelStep2.TabIndex = 72;
            this.m_labelStep2.Text = "Step 2: Configure your system to support TWAIN Local (optional).";
            // 
            // m_labelStep3
            // 
            this.m_labelStep3.AutoSize = true;
            this.m_labelStep3.Location = new System.Drawing.Point(9, 191);
            this.m_labelStep3.Name = "m_labelStep3";
            this.m_labelStep3.Size = new System.Drawing.Size(223, 13);
            this.m_labelStep3.TabIndex = 73;
            this.m_labelStep3.Text = "Step 3: Register with TWAIN Cloud (optional).";
            // 
            // m_checkboxAdvertise
            // 
            this.m_checkboxAdvertise.AutoSize = true;
            this.m_checkboxAdvertise.Location = new System.Drawing.Point(12, 336);
            this.m_checkboxAdvertise.Name = "m_checkboxAdvertise";
            this.m_checkboxAdvertise.Size = new System.Drawing.Size(360, 17);
            this.m_checkboxAdvertise.TabIndex = 74;
            this.m_checkboxAdvertise.Text = "Advertise on TWAIN Local and TWAIN Cloud when this program starts";
            this.m_checkboxAdvertise.UseVisualStyleBackColor = true;
            this.m_checkboxAdvertise.CheckedChanged += new System.EventHandler(this.m_checkboxAdvertise_CheckedChanged);
            // 
            // m_checkboxConfirmation
            // 
            this.m_checkboxConfirmation.AutoSize = true;
            this.m_checkboxConfirmation.Location = new System.Drawing.Point(12, 359);
            this.m_checkboxConfirmation.Name = "m_checkboxConfirmation";
            this.m_checkboxConfirmation.Size = new System.Drawing.Size(237, 17);
            this.m_checkboxConfirmation.TabIndex = 75;
            this.m_checkboxConfirmation.Text = "Prompt for confirmation when scanning starts";
            this.m_checkboxConfirmation.UseVisualStyleBackColor = true;
            this.m_checkboxConfirmation.CheckedChanged += new System.EventHandler(this.m_checkboxConfirmation_CheckedChanged);
            // 
            // m_labelCurrentDriver
            // 
            this.m_labelCurrentDriver.AutoSize = true;
            this.m_labelCurrentDriver.Location = new System.Drawing.Point(13, 33);
            this.m_labelCurrentDriver.Name = "m_labelCurrentDriver";
            this.m_labelCurrentDriver.Size = new System.Drawing.Size(72, 13);
            this.m_labelCurrentDriver.TabIndex = 76;
            this.m_labelCurrentDriver.Text = "current driver:";
            // 
            // m_textboxCurrentDriver
            // 
            this.m_textboxCurrentDriver.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textboxCurrentDriver.BackColor = System.Drawing.SystemColors.Window;
            this.m_textboxCurrentDriver.Location = new System.Drawing.Point(91, 29);
            this.m_textboxCurrentDriver.Name = "m_textboxCurrentDriver";
            this.m_textboxCurrentDriver.ReadOnly = true;
            this.m_textboxCurrentDriver.Size = new System.Drawing.Size(447, 20);
            this.m_textboxCurrentDriver.TabIndex = 77;
            // 
            // m_textboxCurrentNote
            // 
            this.m_textboxCurrentNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textboxCurrentNote.BackColor = System.Drawing.SystemColors.Window;
            this.m_textboxCurrentNote.Location = new System.Drawing.Point(91, 55);
            this.m_textboxCurrentNote.Name = "m_textboxCurrentNote";
            this.m_textboxCurrentNote.ReadOnly = true;
            this.m_textboxCurrentNote.Size = new System.Drawing.Size(447, 20);
            this.m_textboxCurrentNote.TabIndex = 79;
            // 
            // m_labelCurrentNote
            // 
            this.m_labelCurrentNote.AutoSize = true;
            this.m_labelCurrentNote.Location = new System.Drawing.Point(13, 59);
            this.m_labelCurrentNote.Name = "m_labelCurrentNote";
            this.m_labelCurrentNote.Size = new System.Drawing.Size(67, 13);
            this.m_labelCurrentNote.TabIndex = 78;
            this.m_labelCurrentNote.Text = "current note:";
            // 
            // m_buttonManageCloud
            // 
            this.m_buttonManageCloud.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonManageCloud.Location = new System.Drawing.Point(397, 207);
            this.m_buttonManageCloud.Name = "m_buttonManageCloud";
            this.m_buttonManageCloud.Size = new System.Drawing.Size(142, 23);
            this.m_buttonManageCloud.TabIndex = 80;
            this.m_buttonManageCloud.Text = "manage cloud...";
            this.m_buttonManageCloud.UseVisualStyleBackColor = true;
            this.m_buttonManageCloud.Click += new System.EventHandler(this.m_buttonManageCloud_Click);
            // 
            // m_RegisteredDeviceLabel
            // 
            this.m_RegisteredDeviceLabel.AutoSize = true;
            this.m_RegisteredDeviceLabel.Location = new System.Drawing.Point(46, 239);
            this.m_RegisteredDeviceLabel.Name = "m_RegisteredDeviceLabel";
            this.m_RegisteredDeviceLabel.Size = new System.Drawing.Size(111, 13);
            this.m_RegisteredDeviceLabel.TabIndex = 81;
            this.m_RegisteredDeviceLabel.Text = "Current Cloud Device:";
            // 
            // m_CloudDevicesComboBox
            // 
            this.m_CloudDevicesComboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_CloudDevicesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.m_CloudDevicesComboBox.FormattingEnabled = true;
            this.m_CloudDevicesComboBox.Location = new System.Drawing.Point(163, 236);
            this.m_CloudDevicesComboBox.Name = "m_CloudDevicesComboBox";
            this.m_CloudDevicesComboBox.Size = new System.Drawing.Size(375, 21);
            this.m_CloudDevicesComboBox.TabIndex = 82;
            this.m_CloudDevicesComboBox.DropDown += new System.EventHandler(this.m_CloudDevicesComboBox_DropDown);
            this.m_CloudDevicesComboBox.SelectedIndexChanged += new System.EventHandler(this.m_CloudDevicesComboBox_SelectedIndexChanged);
            // 
            // m_comboboxCloudApiRoot
            // 
            this.m_comboboxCloudApiRoot.FormattingEnabled = true;
            this.m_comboboxCloudApiRoot.Location = new System.Drawing.Point(49, 208);
            this.m_comboboxCloudApiRoot.Name = "m_comboboxCloudApiRoot";
            this.m_comboboxCloudApiRoot.Size = new System.Drawing.Size(194, 21);
            this.m_comboboxCloudApiRoot.TabIndex = 83;
            this.m_comboboxCloudApiRoot.SelectedIndexChanged += new System.EventHandler(this.m_comboboxCloudApiRoot_SelectedIndexChanged);
            // 
            // m_checkboxStartNpm
            // 
            this.m_checkboxStartNpm.AutoSize = true;
            this.m_checkboxStartNpm.Location = new System.Drawing.Point(49, 266);
            this.m_checkboxStartNpm.Name = "m_checkboxStartNpm";
            this.m_checkboxStartNpm.Size = new System.Drawing.Size(189, 17);
            this.m_checkboxStartNpm.TabIndex = 84;
            this.m_checkboxStartNpm.Text = "Run \'npm i\' for twain-cloud-express";
            this.m_checkboxStartNpm.UseVisualStyleBackColor = true;
            // 
            // FormSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(550, 388);
            this.Controls.Add(this.m_checkboxStartNpm);
            this.Controls.Add(this.m_comboboxCloudApiRoot);
            this.Controls.Add(this.m_CloudDevicesComboBox);
            this.Controls.Add(this.m_RegisteredDeviceLabel);
            this.Controls.Add(this.m_buttonManageCloud);
            this.Controls.Add(this.m_textboxCurrentNote);
            this.Controls.Add(this.m_labelCurrentNote);
            this.Controls.Add(this.m_textboxCurrentDriver);
            this.Controls.Add(this.m_labelCurrentDriver);
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
        private System.Windows.Forms.Label m_labelCurrentDriver;
        private System.Windows.Forms.TextBox m_textboxCurrentDriver;
        private System.Windows.Forms.TextBox m_textboxCurrentNote;
        private System.Windows.Forms.Label m_labelCurrentNote;
        private System.Windows.Forms.Button m_buttonManageCloud;
        private System.Windows.Forms.Label m_RegisteredDeviceLabel;
        private System.Windows.Forms.ComboBox m_CloudDevicesComboBox;
        private System.Windows.Forms.ComboBox m_comboboxCloudApiRoot;
        private System.Windows.Forms.CheckBox m_checkboxStartNpm;
    }
}