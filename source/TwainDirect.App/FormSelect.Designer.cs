namespace TwainDirect.App
{
    partial class FormSelect
    {
        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.Windows.Forms.ListViewGroup listViewGroup1 = new System.Windows.Forms.ListViewGroup("Local Scanners", System.Windows.Forms.HorizontalAlignment.Left);
            System.Windows.Forms.ListViewGroup listViewGroup2 = new System.Windows.Forms.ListViewGroup("Cloud Scanners", System.Windows.Forms.HorizontalAlignment.Left);
            this.m_buttonOpen = new System.Windows.Forms.Button();
            this.m_labelSelect = new System.Windows.Forms.Label();
            this.m_listviewSelect = new System.Windows.Forms.ListView();
            this.m_buttonManageTwainLocal = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // m_buttonOpen
            // 
            this.m_buttonOpen.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonOpen.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.m_buttonOpen.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonOpen.Location = new System.Drawing.Point(552, 363);
            this.m_buttonOpen.Name = "m_buttonOpen";
            this.m_buttonOpen.Size = new System.Drawing.Size(75, 23);
            this.m_buttonOpen.TabIndex = 1;
            this.m_buttonOpen.Text = "open";
            this.m_buttonOpen.UseVisualStyleBackColor = true;
            this.m_buttonOpen.Click += new System.EventHandler(this.m_buttonOpen_Click);
            // 
            // m_labelSelect
            // 
            this.m_labelSelect.AutoSize = true;
            this.m_labelSelect.Location = new System.Drawing.Point(12, 12);
            this.m_labelSelect.Name = "m_labelSelect";
            this.m_labelSelect.Size = new System.Drawing.Size(79, 13);
            this.m_labelSelect.TabIndex = 3;
            this.m_labelSelect.Text = "select scanner:";
            // 
            // m_listviewSelect
            // 
            this.m_listviewSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.m_listviewSelect.FullRowSelect = true;
            this.m_listviewSelect.GridLines = true;
            listViewGroup1.Header = "Local Scanners";
            listViewGroup1.Name = "localScannersGroup";
            listViewGroup2.Header = "Cloud Scanners";
            listViewGroup2.Name = "cloudScannersGroup";
            this.m_listviewSelect.Groups.AddRange(new System.Windows.Forms.ListViewGroup[] {
            listViewGroup1,
            listViewGroup2});
            this.m_listviewSelect.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.m_listviewSelect.HideSelection = false;
            this.m_listviewSelect.Location = new System.Drawing.Point(15, 39);
            this.m_listviewSelect.MultiSelect = false;
            this.m_listviewSelect.Name = "m_listviewSelect";
            this.m_listviewSelect.Size = new System.Drawing.Size(612, 318);
            this.m_listviewSelect.TabIndex = 4;
            this.m_listviewSelect.UseCompatibleStateImageBehavior = false;
            this.m_listviewSelect.View = System.Windows.Forms.View.Details;
            // 
            // m_buttonManageTwainLocal
            // 
            this.m_buttonManageTwainLocal.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.m_buttonManageTwainLocal.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.m_buttonManageTwainLocal.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.m_buttonManageTwainLocal.Location = new System.Drawing.Point(15, 363);
            this.m_buttonManageTwainLocal.Name = "m_buttonManageTwainLocal";
            this.m_buttonManageTwainLocal.Size = new System.Drawing.Size(142, 23);
            this.m_buttonManageTwainLocal.TabIndex = 5;
            this.m_buttonManageTwainLocal.Text = "Manage Local...";
            this.m_buttonManageTwainLocal.UseVisualStyleBackColor = true;
            this.m_buttonManageTwainLocal.Click += new System.EventHandler(this.m_buttonManageTwainLocal_Click);
            // 
            // FormSelect
            // 
            this.AcceptButton = this.m_buttonOpen;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(639, 394);
            this.Controls.Add(this.m_buttonManageTwainLocal);
            this.Controls.Add(this.m_listviewSelect);
            this.Controls.Add(this.m_labelSelect);
            this.Controls.Add(this.m_buttonOpen);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormSelect";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "open scanner";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button m_buttonOpen;
        private System.Windows.Forms.Label m_labelSelect;
        private System.Windows.Forms.ListView m_listviewSelect;
        private System.Windows.Forms.Button m_buttonManageTwainLocal;
    }
}

