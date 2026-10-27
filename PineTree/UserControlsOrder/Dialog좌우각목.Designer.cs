
namespace PineTree.UserControlsOrder
{
    partial class Dialog좌우각목
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Dialog좌우각목));
            this.c1SplitContainer1 = new C1.Win.C1SplitContainer.C1SplitContainer();
            this.c1SplitterPanel1 = new C1.Win.C1SplitContainer.C1SplitterPanel();
            this.btnCancel = new C1.Win.C1Input.C1Button();
            this.btnOK = new C1.Win.C1Input.C1Button();
            this.c1InputPanel1 = new C1.Win.C1InputPanel.C1InputPanel();
            this.inputGroupHeader1 = new C1.Win.C1InputPanel.InputGroupHeader();
            this.tbExtraLine = new C1.Win.C1InputPanel.InputNumericBox();
            this.chkDesignLine = new C1.Win.C1InputPanel.InputCheckBox();
            this.tbDesignLine = new C1.Win.C1InputPanel.InputNumericBox();
            this.inputLabel1 = new C1.Win.C1InputPanel.InputLabel();
            this.inputLabel2 = new C1.Win.C1InputPanel.InputLabel();
            this.inputLabel3 = new C1.Win.C1InputPanel.InputLabel();
            ((System.ComponentModel.ISupportInitialize)(this.c1SplitContainer1)).BeginInit();
            this.c1SplitContainer1.SuspendLayout();
            this.c1SplitterPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnCancel)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnOK)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1InputPanel1)).BeginInit();
            this.SuspendLayout();
            // 
            // c1SplitContainer1
            // 
            this.c1SplitContainer1.AutoSizeElement = C1.Framework.AutoSizeElement.Both;
            this.c1SplitContainer1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(139)))), ((int)(((byte)(139)))), ((int)(((byte)(139)))));
            this.c1SplitContainer1.CollapsingAreaColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.c1SplitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.c1SplitContainer1.FixedLineColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(82)))), ((int)(((byte)(82)))));
            this.c1SplitContainer1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))));
            this.c1SplitContainer1.HeaderForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(226)))), ((int)(((byte)(226)))), ((int)(((byte)(226)))));
            this.c1SplitContainer1.Location = new System.Drawing.Point(0, 0);
            this.c1SplitContainer1.Name = "c1SplitContainer1";
            this.c1SplitContainer1.Panels.Add(this.c1SplitterPanel1);
            this.c1SplitContainer1.Size = new System.Drawing.Size(387, 286);
            this.c1SplitContainer1.SplitterColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(82)))), ((int)(((byte)(82)))));
            this.c1SplitContainer1.TabIndex = 0;
            this.c1SplitContainer1.ToolTipGradient = C1.Win.C1SplitContainer.ToolTipGradient.Vista;
            this.c1SplitContainer1.UseParentVisualStyle = false;
            // 
            // c1SplitterPanel1
            // 
            this.c1SplitterPanel1.Controls.Add(this.btnCancel);
            this.c1SplitterPanel1.Controls.Add(this.btnOK);
            this.c1SplitterPanel1.Controls.Add(this.c1InputPanel1);
            this.c1SplitterPanel1.Height = 286;
            this.c1SplitterPanel1.Location = new System.Drawing.Point(0, 0);
            this.c1SplitterPanel1.Name = "c1SplitterPanel1";
            this.c1SplitterPanel1.Size = new System.Drawing.Size(387, 286);
            this.c1SplitterPanel1.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(282, 237);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(201, 237);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "확인";
            this.btnOK.UseVisualStyleBackColor = true;
            // 
            // c1InputPanel1
            // 
            this.c1InputPanel1.AutoSizeElement = C1.Framework.AutoSizeElement.Both;
            this.c1InputPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.c1InputPanel1.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.c1InputPanel1.Items.Add(this.inputGroupHeader1);
            this.c1InputPanel1.Items.Add(this.inputLabel3);
            this.c1InputPanel1.Items.Add(this.tbExtraLine);
            this.c1InputPanel1.Items.Add(this.inputLabel1);
            this.c1InputPanel1.Items.Add(this.chkDesignLine);
            this.c1InputPanel1.Items.Add(this.tbDesignLine);
            this.c1InputPanel1.Items.Add(this.inputLabel2);
            this.c1InputPanel1.Location = new System.Drawing.Point(0, 0);
            this.c1InputPanel1.Name = "c1InputPanel1";
            this.c1InputPanel1.Size = new System.Drawing.Size(387, 202);
            this.c1InputPanel1.TabIndex = 0;
            // 
            // inputGroupHeader1
            // 
            this.inputGroupHeader1.Name = "inputGroupHeader1";
            this.inputGroupHeader1.Text = "좌우각목";
            // 
            // tbExtraLine
            // 
            this.tbExtraLine.Break = C1.Win.C1InputPanel.BreakType.None;
            this.tbExtraLine.Maximum = new decimal(new int[] {
            9999,
            0,
            0,
            0});
            this.tbExtraLine.Name = "tbExtraLine";
            this.tbExtraLine.Padding = new System.Windows.Forms.Padding(10, 40, 0, 0);
            // 
            // chkDesignLine
            // 
            this.chkDesignLine.Break = C1.Win.C1InputPanel.BreakType.None;
            this.chkDesignLine.HorizontalAlign = C1.Win.C1InputPanel.InputContentAlignment.Far;
            this.chkDesignLine.Name = "chkDesignLine";
            this.chkDesignLine.Padding = new System.Windows.Forms.Padding(25, 20, 2, 2);
            this.chkDesignLine.Text = "디자인선";
            // 
            // tbDesignLine
            // 
            this.tbDesignLine.Break = C1.Win.C1InputPanel.BreakType.None;
            this.tbDesignLine.Maximum = new decimal(new int[] {
            9999,
            0,
            0,
            0});
            this.tbDesignLine.Name = "tbDesignLine";
            this.tbDesignLine.Padding = new System.Windows.Forms.Padding(10, 20, 0, 0);
            this.tbDesignLine.Width = 98;
            // 
            // inputLabel1
            // 
            this.inputLabel1.Break = C1.Win.C1InputPanel.BreakType.Row;
            this.inputLabel1.Name = "inputLabel1";
            this.inputLabel1.Padding = new System.Windows.Forms.Padding(0, 40, 0, 0);
            this.inputLabel1.Text = "mm";
            // 
            // inputLabel2
            // 
            this.inputLabel2.Name = "inputLabel2";
            this.inputLabel2.Padding = new System.Windows.Forms.Padding(0, 20, 0, 0);
            this.inputLabel2.Text = "mm";
            // 
            // inputLabel3
            // 
            this.inputLabel3.HorizontalAlign = C1.Win.C1InputPanel.InputContentAlignment.Far;
            this.inputLabel3.Name = "inputLabel3";
            this.inputLabel3.Padding = new System.Windows.Forms.Padding(0, 40, 0, 0);
            this.inputLabel3.Text = "여분선";
            this.inputLabel3.Width = 90;
            // 
            // Dialog좌우각목
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(387, 286);
            this.Controls.Add(this.c1SplitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Dialog좌우각목";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "후가공 정보";
            ((System.ComponentModel.ISupportInitialize)(this.c1SplitContainer1)).EndInit();
            this.c1SplitContainer1.ResumeLayout(false);
            this.c1SplitterPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.btnCancel)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnOK)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1InputPanel1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private C1.Win.C1SplitContainer.C1SplitContainer c1SplitContainer1;
        private C1.Win.C1SplitContainer.C1SplitterPanel c1SplitterPanel1;
        private C1.Win.C1InputPanel.C1InputPanel c1InputPanel1;
        private C1.Win.C1Input.C1Button btnCancel;
        private C1.Win.C1Input.C1Button btnOK;
        private C1.Win.C1InputPanel.InputGroupHeader inputGroupHeader1;
        private C1.Win.C1InputPanel.InputNumericBox tbExtraLine;
        private C1.Win.C1InputPanel.InputCheckBox chkDesignLine;
        private C1.Win.C1InputPanel.InputNumericBox tbDesignLine;
        private C1.Win.C1InputPanel.InputLabel inputLabel1;
        private C1.Win.C1InputPanel.InputLabel inputLabel2;
        private C1.Win.C1InputPanel.InputLabel inputLabel3;
    }
}