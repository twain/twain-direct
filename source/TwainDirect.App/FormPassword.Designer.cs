namespace TwainDirect.App
{
    partial class FormPassword
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
            this.m_richtextboxExplanation = new System.Windows.Forms.RichTextBox();
            this.m_checkboxShowPassword = new System.Windows.Forms.CheckBox();
            this.m_textboxPassword = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // m_richtextboxExplanation
            // 
            this.m_richtextboxExplanation.BackColor = System.Drawing.SystemColors.Window;
            this.m_richtextboxExplanation.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.m_richtextboxExplanation.Location = new System.Drawing.Point(12, 13);
            this.m_richtextboxExplanation.Name = "m_richtextboxExplanation";
            this.m_richtextboxExplanation.ReadOnly = true;
            this.m_richtextboxExplanation.Size = new System.Drawing.Size(412, 39);
            this.m_richtextboxExplanation.TabIndex = 0;
            this.m_richtextboxExplanation.Text = "The task appears to include encryption.  If you wish your images to be displayed " +
    "during scanning, please provide the password in the box below:";
            // 
            // m_checkboxShowPassword
            // 
            this.m_checkboxShowPassword.AutoSize = true;
            this.m_checkboxShowPassword.Location = new System.Drawing.Point(12, 78);
            this.m_checkboxShowPassword.Name = "m_checkboxShowPassword";
            this.m_checkboxShowPassword.Size = new System.Drawing.Size(146, 17);
            this.m_checkboxShowPassword.TabIndex = 1;
            this.m_checkboxShowPassword.Text = "Display the password text";
            this.m_checkboxShowPassword.UseVisualStyleBackColor = true;
            this.m_checkboxShowPassword.CheckedChanged += new System.EventHandler(this.m_checkboxShowPassword_CheckedChanged);
            // 
            // m_textboxPassword
            // 
            this.m_textboxPassword.Location = new System.Drawing.Point(12, 46);
            this.m_textboxPassword.Name = "m_textboxPassword";
            this.m_textboxPassword.Size = new System.Drawing.Size(412, 20);
            this.m_textboxPassword.TabIndex = 2;
            this.m_textboxPassword.KeyDown += m_textboxPassword_KeyDown;
            // 
            // FormPassword
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(437, 107);
            this.Controls.Add(this.m_textboxPassword);
            this.Controls.Add(this.m_checkboxShowPassword);
            this.Controls.Add(this.m_richtextboxExplanation);
            this.MaximumSize = new System.Drawing.Size(453, 146);
            this.MinimumSize = new System.Drawing.Size(453, 146);
            this.Name = "FormPassword";
            this.Text = "Enter Password";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.RichTextBox m_richtextboxExplanation;
        private System.Windows.Forms.CheckBox m_checkboxShowPassword;
        private System.Windows.Forms.TextBox m_textboxPassword;
    }
}