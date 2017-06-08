namespace TwainDirectOnSane
{
    partial class MainForm
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
            this.m_buttonScan = new System.Windows.Forms.Button();
            this.m_listviewTasks = new System.Windows.Forms.ListView();
            this.Task = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.m_buttonEdit = new System.Windows.Forms.Button();
            this.m_buttonSelect = new System.Windows.Forms.Button();
            this.m_buttonCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // m_buttonScan
            // 
            this.m_buttonScan.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonScan.Location = new System.Drawing.Point(208, 323);
            this.m_buttonScan.Name = "m_buttonScan";
            this.m_buttonScan.Size = new System.Drawing.Size(75, 23);
            this.m_buttonScan.TabIndex = 0;
            this.m_buttonScan.Text = "Run";
            this.m_buttonScan.UseVisualStyleBackColor = true;
            this.m_buttonScan.Click += new System.EventHandler(this.m_buttonRun_Click);
            // 
            // m_listviewTasks
            // 
            this.m_listviewTasks.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_listviewTasks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Task});
            this.m_listviewTasks.Location = new System.Drawing.Point(12, 12);
            this.m_listviewTasks.MultiSelect = false;
            this.m_listviewTasks.Name = "m_listviewTasks";
            this.m_listviewTasks.Size = new System.Drawing.Size(271, 305);
            this.m_listviewTasks.TabIndex = 1;
            this.m_listviewTasks.UseCompatibleStateImageBehavior = false;
            // 
            // Task
            // 
            this.Task.Text = "Task";
            this.Task.Width = 1000;
            // 
            // m_buttonEdit
            // 
            this.m_buttonEdit.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonEdit.Location = new System.Drawing.Point(127, 323);
            this.m_buttonEdit.Name = "m_buttonEdit";
            this.m_buttonEdit.Size = new System.Drawing.Size(75, 23);
            this.m_buttonEdit.TabIndex = 2;
            this.m_buttonEdit.Text = "Edit...";
            this.m_buttonEdit.UseVisualStyleBackColor = true;
            this.m_buttonEdit.Click += new System.EventHandler(this.m_buttonEdit_Click);
            // 
            // m_buttonSelect
            // 
            this.m_buttonSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.m_buttonSelect.Location = new System.Drawing.Point(13, 323);
            this.m_buttonSelect.Name = "m_buttonSelect";
            this.m_buttonSelect.Size = new System.Drawing.Size(75, 23);
            this.m_buttonSelect.TabIndex = 3;
            this.m_buttonSelect.Text = "Device...";
            this.m_buttonSelect.UseVisualStyleBackColor = true;
            this.m_buttonSelect.Click += new System.EventHandler(this.m_buttonSelect_Click);
            // 
            // m_buttonCancel
            // 
            this.m_buttonCancel.Location = new System.Drawing.Point(208, 323);
            this.m_buttonCancel.Name = "m_buttonCancel";
            this.m_buttonCancel.Size = new System.Drawing.Size(75, 23);
            this.m_buttonCancel.TabIndex = 4;
            this.m_buttonCancel.Text = "Cancel";
            this.m_buttonCancel.UseVisualStyleBackColor = true;
            this.m_buttonCancel.Click += new System.EventHandler(this.m_buttonCancel_Click);
            // 
            // MainForm
            // 
            this.AcceptButton = this.m_buttonScan;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(295, 358);
            this.Controls.Add(this.m_buttonSelect);
            this.Controls.Add(this.m_buttonEdit);
            this.Controls.Add(this.m_listviewTasks);
            this.Controls.Add(this.m_buttonScan);
            this.Controls.Add(this.m_buttonCancel);
            this.Name = "MainForm";
            this.Text = "TWAIN Direct on SANE";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button m_buttonScan;
        private System.Windows.Forms.ListView m_listviewTasks;
        private System.Windows.Forms.ColumnHeader Task;
        private System.Windows.Forms.Button m_buttonEdit;
        private System.Windows.Forms.Button m_buttonSelect;
        private System.Windows.Forms.Button m_buttonCancel;
    }
}

