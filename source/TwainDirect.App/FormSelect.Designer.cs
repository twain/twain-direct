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
            this.m_buttonOpen = new System.Windows.Forms.Button();
            this.m_labelSelect = new System.Windows.Forms.Label();
            this.m_listviewSelect = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // m_buttonOpen
            // 
            this.m_buttonOpen.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.m_buttonOpen.Location = new System.Drawing.Point(552, 227);
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
            this.m_listviewSelect.FullRowSelect = true;
            this.m_listviewSelect.GridLines = true;
            this.m_listviewSelect.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.m_listviewSelect.HideSelection = false;
            this.m_listviewSelect.Location = new System.Drawing.Point(15, 39);
            this.m_listviewSelect.MultiSelect = false;
            this.m_listviewSelect.Name = "m_listviewSelect";
            this.m_listviewSelect.Size = new System.Drawing.Size(612, 182);
            this.m_listviewSelect.TabIndex = 4;
            this.m_listviewSelect.UseCompatibleStateImageBehavior = false;
            this.m_listviewSelect.View = System.Windows.Forms.View.Details;
            // 
            // FormSelect
            // 
            this.AcceptButton = this.m_buttonOpen;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(639, 255);
            this.Controls.Add(this.m_listviewSelect);
            this.Controls.Add(this.m_labelSelect);
            this.Controls.Add(this.m_buttonOpen);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
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
    }
}

