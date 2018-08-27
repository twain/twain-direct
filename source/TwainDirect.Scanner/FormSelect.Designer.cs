namespace TwainDirect.Scanner
{
    partial class FormSelect
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
            this.m_labelSelectScannerDriver = new System.Windows.Forms.Label();
            this.m_listboxSelectScannerDriver = new System.Windows.Forms.ListBox();
            this.m_labelNote = new System.Windows.Forms.Label();
            this.m_textboxNote = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // m_labelSelectScannerDriver
            // 
            this.m_labelSelectScannerDriver.AutoSize = true;
            this.m_labelSelectScannerDriver.Location = new System.Drawing.Point(13, 13);
            this.m_labelSelectScannerDriver.Name = "m_labelSelectScannerDriver";
            this.m_labelSelectScannerDriver.Size = new System.Drawing.Size(108, 13);
            this.m_labelSelectScannerDriver.TabIndex = 0;
            this.m_labelSelectScannerDriver.Text = "select scanner driver:";
            // 
            // m_listboxSelectScannerDriver
            // 
            this.m_listboxSelectScannerDriver.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_listboxSelectScannerDriver.FormattingEnabled = true;
            this.m_listboxSelectScannerDriver.Location = new System.Drawing.Point(16, 30);
            this.m_listboxSelectScannerDriver.Name = "m_listboxSelectScannerDriver";
            this.m_listboxSelectScannerDriver.Size = new System.Drawing.Size(404, 121);
            this.m_listboxSelectScannerDriver.TabIndex = 1;
            // 
            // m_labelNote
            // 
            this.m_labelNote.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_labelNote.AutoSize = true;
            this.m_labelNote.Location = new System.Drawing.Point(13, 165);
            this.m_labelNote.Name = "m_labelNote";
            this.m_labelNote.Size = new System.Drawing.Size(196, 13);
            this.m_labelNote.TabIndex = 2;
            this.m_labelNote.Text = "a note or friendly name for your scanner:";
            // 
            // m_textboxNote
            // 
            this.m_textboxNote.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_textboxNote.Location = new System.Drawing.Point(16, 182);
            this.m_textboxNote.Name = "m_textboxNote";
            this.m_textboxNote.Size = new System.Drawing.Size(404, 20);
            this.m_textboxNote.TabIndex = 3;
            // 
            // FormSelect
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(432, 215);
            this.Controls.Add(this.m_textboxNote);
            this.Controls.Add(this.m_labelNote);
            this.Controls.Add(this.m_listboxSelectScannerDriver);
            this.Controls.Add(this.m_labelSelectScannerDriver);
            this.Name = "FormSelect";
            this.Text = "FormSelect";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label m_labelSelectScannerDriver;
        private System.Windows.Forms.ListBox m_listboxSelectScannerDriver;
        private System.Windows.Forms.Label m_labelNote;
        private System.Windows.Forms.TextBox m_textboxNote;
    }
}