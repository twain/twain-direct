namespace TwainDirect.Scanner
{
    partial class ConfirmScan
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
            this.m_labelConfirmScan = new System.Windows.Forms.Label();
            this.m_buttonYes = new System.Windows.Forms.Button();
            this.m_buttonNo = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // m_labelConfirmScan
            // 
            this.m_labelConfirmScan.AutoSize = true;
            this.m_labelConfirmScan.Location = new System.Drawing.Point(12, 16);
            this.m_labelConfirmScan.Name = "m_labelConfirmScan";
            this.m_labelConfirmScan.Size = new System.Drawing.Size(121, 13);
            this.m_labelConfirmScan.TabIndex = 0;
            this.m_labelConfirmScan.Text = "Would you like to scan?";
            // 
            // m_buttonYes
            // 
            this.m_buttonYes.Location = new System.Drawing.Point(42, 51);
            this.m_buttonYes.Name = "m_buttonYes";
            this.m_buttonYes.Size = new System.Drawing.Size(75, 23);
            this.m_buttonYes.TabIndex = 1;
            this.m_buttonYes.Text = "Yes";
            this.m_buttonYes.UseVisualStyleBackColor = true;
            this.m_buttonYes.Click += new System.EventHandler(this.m_buttonYes_Click);
            // 
            // m_buttonNo
            // 
            this.m_buttonNo.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.m_buttonNo.Location = new System.Drawing.Point(124, 51);
            this.m_buttonNo.Name = "m_buttonNo";
            this.m_buttonNo.Size = new System.Drawing.Size(75, 23);
            this.m_buttonNo.TabIndex = 2;
            this.m_buttonNo.Text = "No";
            this.m_buttonNo.UseVisualStyleBackColor = true;
            this.m_buttonNo.Click += new System.EventHandler(this.m_buttonNo_Click);
            // 
            // ConfirmScan
            // 
            this.AcceptButton = this.m_buttonYes;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.m_buttonNo;
            this.ClientSize = new System.Drawing.Size(206, 80);
            this.ControlBox = false;
            this.Controls.Add(this.m_buttonNo);
            this.Controls.Add(this.m_buttonYes);
            this.Controls.Add(this.m_labelConfirmScan);
            this.Name = "ConfirmScan";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Confirmation";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label m_labelConfirmScan;
        private System.Windows.Forms.Button m_buttonYes;
        private System.Windows.Forms.Button m_buttonNo;
    }
}