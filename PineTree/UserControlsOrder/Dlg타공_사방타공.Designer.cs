
namespace PineTree.UserControlsOrder
{
    partial class Dlg타공_사방타공
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Dlg타공_사방타공));
            this.c1SplitContainer1 = new C1.Win.C1SplitContainer.C1SplitContainer();
            this.c1SplitterPanel1 = new C1.Win.C1SplitContainer.C1SplitterPanel();
            this.btnCancel = new C1.Win.C1Input.C1Button();
            this.btnOK = new C1.Win.C1Input.C1Button();
            this.c1InputPanel1 = new C1.Win.C1InputPanel.C1InputPanel();
            this.inputGroupHeader1 = new C1.Win.C1InputPanel.InputGroupHeader();
            this.tbTop = new C1.Win.C1InputPanel.InputNumericBox();
            this.tbBottom = new C1.Win.C1InputPanel.InputNumericBox();
            this.tbLeft = new C1.Win.C1InputPanel.InputNumericBox();
            this.tbRight = new C1.Win.C1InputPanel.InputNumericBox();
            this.inputCheckBox1 = new C1.Win.C1InputPanel.InputCheckBox();
            this.inputCheckBox2 = new C1.Win.C1InputPanel.InputCheckBox();
            this.inputCheckBox3 = new C1.Win.C1InputPanel.InputCheckBox();
            this.inputCheckBox4 = new C1.Win.C1InputPanel.InputCheckBox();
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
            this.c1SplitContainer1.Size = new System.Drawing.Size(240, 265);
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
            this.c1SplitterPanel1.Height = 265;
            this.c1SplitterPanel1.Location = new System.Drawing.Point(0, 0);
            this.c1SplitterPanel1.Name = "c1SplitterPanel1";
            this.c1SplitterPanel1.Size = new System.Drawing.Size(240, 265);
            this.c1SplitterPanel1.TabIndex = 0;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(135, 216);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 1;
            this.btnCancel.Text = "취소";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.Location = new System.Drawing.Point(54, 216);
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
            this.c1InputPanel1.Items.Add(this.inputCheckBox1);
            this.c1InputPanel1.Items.Add(this.tbTop);
            this.c1InputPanel1.Items.Add(this.inputCheckBox2);
            this.c1InputPanel1.Items.Add(this.tbBottom);
            this.c1InputPanel1.Items.Add(this.inputCheckBox3);
            this.c1InputPanel1.Items.Add(this.tbLeft);
            this.c1InputPanel1.Items.Add(this.inputCheckBox4);
            this.c1InputPanel1.Items.Add(this.tbRight);
            this.c1InputPanel1.Location = new System.Drawing.Point(0, 0);
            this.c1InputPanel1.Name = "c1InputPanel1";
            this.c1InputPanel1.Size = new System.Drawing.Size(240, 205);
            this.c1InputPanel1.TabIndex = 0;
            // 
            // inputGroupHeader1
            // 
            this.inputGroupHeader1.Name = "inputGroupHeader1";
            this.inputGroupHeader1.Text = "사방타공";
            // 
            // tbTop
            // 
            this.tbTop.Name = "tbTop";
            this.tbTop.Padding = new System.Windows.Forms.Padding(0, 20, 0, 0);
            this.tbTop.Width = 70;
            // 
            // tbBottom
            // 
            this.tbBottom.Name = "tbBottom";
            this.tbBottom.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.tbBottom.Width = 70;
            // 
            // tbLeft
            // 
            this.tbLeft.Name = "tbLeft";
            this.tbLeft.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.tbLeft.Width = 70;
            // 
            // tbRight
            // 
            this.tbRight.Name = "tbRight";
            this.tbRight.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.tbRight.Width = 70;
            // 
            // inputCheckBox1
            // 
            this.inputCheckBox1.Break = C1.Win.C1InputPanel.BreakType.None;
            this.inputCheckBox1.Name = "inputCheckBox1";
            this.inputCheckBox1.Padding = new System.Windows.Forms.Padding(20, 20, 2, 2);
            this.inputCheckBox1.Text = "상";
            // 
            // inputCheckBox2
            // 
            this.inputCheckBox2.Break = C1.Win.C1InputPanel.BreakType.None;
            this.inputCheckBox2.Name = "inputCheckBox2";
            this.inputCheckBox2.Padding = new System.Windows.Forms.Padding(20, 5, 2, 2);
            this.inputCheckBox2.Text = "하";
            // 
            // inputCheckBox3
            // 
            this.inputCheckBox3.Break = C1.Win.C1InputPanel.BreakType.None;
            this.inputCheckBox3.Name = "inputCheckBox3";
            this.inputCheckBox3.Padding = new System.Windows.Forms.Padding(20, 5, 2, 2);
            this.inputCheckBox3.Text = "좌";
            // 
            // inputCheckBox4
            // 
            this.inputCheckBox4.Break = C1.Win.C1InputPanel.BreakType.None;
            this.inputCheckBox4.Name = "inputCheckBox4";
            this.inputCheckBox4.Padding = new System.Windows.Forms.Padding(20, 5, 2, 2);
            this.inputCheckBox4.Text = "우";
            // 
            // Dlg타공_사방타공
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(240, 265);
            this.Controls.Add(this.c1SplitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Dlg타공_사방타공";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "사방타공";
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
        private C1.Win.C1InputPanel.InputNumericBox tbTop;
        private C1.Win.C1InputPanel.InputNumericBox tbBottom;
        private C1.Win.C1InputPanel.InputNumericBox tbLeft;
        private C1.Win.C1InputPanel.InputNumericBox tbRight;
        private C1.Win.C1InputPanel.InputCheckBox inputCheckBox1;
        private C1.Win.C1InputPanel.InputCheckBox inputCheckBox2;
        private C1.Win.C1InputPanel.InputCheckBox inputCheckBox3;
        private C1.Win.C1InputPanel.InputCheckBox inputCheckBox4;
    }
}