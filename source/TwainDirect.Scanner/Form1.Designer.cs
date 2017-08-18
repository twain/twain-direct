namespace TwainDirect.Scanner
{
    partial class Form1
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
            if (disposing)
            {
                if (m_scanner != null)
                {
                    m_scanner.Dispose();
                    m_scanner = null;
                }
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.m_buttonStart = new System.Windows.Forms.Button();
            this.m_buttonStop = new System.Windows.Forms.Button();
            this.m_richtextboxTask = new System.Windows.Forms.RichTextBox();
            this.m_buttonRegister = new System.Windows.Forms.Button();
            this.m_notifyicon = new System.Windows.Forms.NotifyIcon(this.components);
            this.SuspendLayout();
            // 
            // m_buttonStart
            // 
            this.m_buttonStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStart.Location = new System.Drawing.Point(199, 8);
            this.m_buttonStart.Name = "m_buttonStart";
            this.m_buttonStart.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStart.TabIndex = 58;
            this.m_buttonStart.Text = "Start";
            this.m_buttonStart.UseVisualStyleBackColor = true;
            this.m_buttonStart.Click += new System.EventHandler(this.m_buttonStart_Click);
            // 
            // m_buttonStop
            // 
            this.m_buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStop.Location = new System.Drawing.Point(280, 8);
            this.m_buttonStop.Name = "m_buttonStop";
            this.m_buttonStop.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStop.TabIndex = 59;
            this.m_buttonStop.Text = "Stop";
            this.m_buttonStop.UseVisualStyleBackColor = true;
            this.m_buttonStop.Click += new System.EventHandler(this.m_buttonStop_Click);
            // 
            // m_richtextboxTask
            // 
            this.m_richtextboxTask.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_richtextboxTask.Location = new System.Drawing.Point(11, 37);
            this.m_richtextboxTask.Name = "m_richtextboxTask";
            this.m_richtextboxTask.ReadOnly = true;
            this.m_richtextboxTask.Size = new System.Drawing.Size(344, 186);
            this.m_richtextboxTask.TabIndex = 61;
            this.m_richtextboxTask.Text = "";
            // 
            // m_buttonRegister
            // 
            this.m_buttonRegister.Location = new System.Drawing.Point(11, 8);
            this.m_buttonRegister.Name = "m_buttonRegister";
            this.m_buttonRegister.Size = new System.Drawing.Size(75, 23);
            this.m_buttonRegister.TabIndex = 62;
            this.m_buttonRegister.Text = "Register...";
            this.m_buttonRegister.UseVisualStyleBackColor = true;
            this.m_buttonRegister.Click += new System.EventHandler(this.m_buttonRegister_Click);
            // 
            // m_notifyicon
            // 
            this.m_notifyicon.BalloonTipText = "TWAIN Direct on TWAIN Bridge";
            this.m_notifyicon.BalloonTipTitle = "TWAIN Direct";
            this.m_notifyicon.Icon = ((System.Drawing.Icon)(resources.GetObject("m_notifyicon.Icon")));
            this.m_notifyicon.Text = "TWAIN Direct";
            this.m_notifyicon.Visible = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(367, 235);
            this.Controls.Add(this.m_buttonRegister);
            this.Controls.Add(this.m_richtextboxTask);
            this.Controls.Add(this.m_buttonStop);
            this.Controls.Add(this.m_buttonStart);
            this.Name = "Form1";
            this.Text = "TWAIN Direct: Scanner";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button m_buttonStart;
        private System.Windows.Forms.Button m_buttonStop;
        private System.Windows.Forms.RichTextBox m_richtextboxTask;
        private System.Windows.Forms.Button m_buttonRegister;
        private System.Windows.Forms.NotifyIcon m_notifyicon;
    }
}

