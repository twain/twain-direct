namespace TwainDirect.Scanner
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.m_buttonStart = new System.Windows.Forms.Button();
            this.m_buttonStop = new System.Windows.Forms.Button();
            this.m_richtextboxTask = new System.Windows.Forms.RichTextBox();
            this.m_notifyicon = new System.Windows.Forms.NotifyIcon(this.components);
            this.panel1 = new System.Windows.Forms.Panel();
            this.m_buttonSetup = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // m_buttonStart
            // 
            this.m_buttonStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStart.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonStart.Location = new System.Drawing.Point(457, 286);
            this.m_buttonStart.Name = "m_buttonStart";
            this.m_buttonStart.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStart.TabIndex = 58;
            this.m_buttonStart.Text = "Start";
            this.m_buttonStart.UseVisualStyleBackColor = true;
            this.m_buttonStart.Click += new System.EventHandler(this.m_buttonStart_Click);
            // 
            // m_buttonStop
            // 
            this.m_buttonStop.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonStop.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonStop.Location = new System.Drawing.Point(538, 286);
            this.m_buttonStop.Name = "m_buttonStop";
            this.m_buttonStop.Size = new System.Drawing.Size(75, 23);
            this.m_buttonStop.TabIndex = 59;
            this.m_buttonStop.Text = "Stop";
            this.m_buttonStop.UseVisualStyleBackColor = true;
            this.m_buttonStop.Click += new System.EventHandler(this.m_buttonStop_Click);
            // 
            // m_richtextboxTask
            // 
            this.m_richtextboxTask.BackColor = System.Drawing.SystemColors.Window;
            this.m_richtextboxTask.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.m_richtextboxTask.Dock = System.Windows.Forms.DockStyle.Fill;
            this.m_richtextboxTask.Location = new System.Drawing.Point(0, 0);
            this.m_richtextboxTask.Name = "m_richtextboxTask";
            this.m_richtextboxTask.ReadOnly = true;
            this.m_richtextboxTask.Size = new System.Drawing.Size(598, 266);
            this.m_richtextboxTask.TabIndex = 61;
            this.m_richtextboxTask.Text = "";
            // 
            // m_notifyicon
            // 
            this.m_notifyicon.BalloonTipText = "TWAIN Direct on TWAIN Bridge";
            this.m_notifyicon.BalloonTipTitle = "TWAIN Direct";
            this.m_notifyicon.Icon = ((System.Drawing.Icon)(resources.GetObject("m_notifyicon.Icon")));
            this.m_notifyicon.Text = "TWAIN Direct";
            this.m_notifyicon.Visible = true;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.m_richtextboxTask);
            this.panel1.Location = new System.Drawing.Point(13, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(600, 268);
            this.panel1.TabIndex = 63;
            // 
            // m_buttonSetup
            // 
            this.m_buttonSetup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonSetup.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonSetup.Location = new System.Drawing.Point(14, 286);
            this.m_buttonSetup.Name = "m_buttonSetup";
            this.m_buttonSetup.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSetup.TabIndex = 66;
            this.m_buttonSetup.Text = "Setup...";
            this.m_buttonSetup.UseVisualStyleBackColor = true;
            this.m_buttonSetup.Click += new System.EventHandler(this.m_buttonSetup_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(625, 318);
            this.Controls.Add(this.m_buttonSetup);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.m_buttonStop);
            this.Controls.Add(this.m_buttonStart);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(383, 274);
            this.Name = "FormMain";
            this.Text = "TWAIN Direct: Scanner";
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button m_buttonStart;
        private System.Windows.Forms.Button m_buttonStop;
        private System.Windows.Forms.RichTextBox m_richtextboxTask;
        private System.Windows.Forms.NotifyIcon m_notifyicon;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button m_buttonSetup;
    }
}

