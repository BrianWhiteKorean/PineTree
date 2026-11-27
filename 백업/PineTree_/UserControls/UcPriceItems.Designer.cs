namespace PineTree.UserControls
{
    partial class UcPriceItems
    {
        /// <summary> 
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _navigationController?.Dispose();

                if (components != null)
                {
                    components.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        #region 구성 요소 디자이너에서 생성한 코드

        /// <summary> 
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UcPriceItems));
            this.c1SplitContainer1 = new C1.Win.C1SplitContainer.C1SplitContainer();
            this.c1SplitterPanel1 = new C1.Win.C1SplitContainer.C1SplitterPanel();
            this.tbPriceItemName = new C1.Win.C1Input.C1TextBox();
            this.cbCategoryName = new C1.Win.C1Input.C1ComboBox();
            this.cbCategory = new C1.Win.C1Input.C1ComboBox();
            this.labelCount = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.FGrid1 = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.panel1 = new System.Windows.Forms.Panel();
            this.label5 = new System.Windows.Forms.Label();
            this.btnDelete = new C1.Win.C1Input.C1Button();
            this.btnEdit = new C1.Win.C1Input.C1Button();
            this.btnAdd = new C1.Win.C1Input.C1Button();
            this.btnSelect = new C1.Win.C1Input.C1Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.picBackground = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.c1SplitContainer1)).BeginInit();
            this.c1SplitContainer1.SuspendLayout();
            this.c1SplitterPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbPriceItemName)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbCategoryName)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbCategory)).BeginInit();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid1)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnDelete)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnEdit)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnAdd)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnSelect)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).BeginInit();
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
            this.c1SplitContainer1.ImeMode = System.Windows.Forms.ImeMode.Hangul;
            this.c1SplitContainer1.Location = new System.Drawing.Point(0, 0);
            this.c1SplitContainer1.Name = "c1SplitContainer1";
            this.c1SplitContainer1.Panels.Add(this.c1SplitterPanel1);
            this.c1SplitContainer1.Size = new System.Drawing.Size(1912, 1004);
            this.c1SplitContainer1.SplitterColor = System.Drawing.Color.FromArgb(((int)(((byte)(82)))), ((int)(((byte)(82)))), ((int)(((byte)(82)))));
            this.c1SplitContainer1.TabIndex = 1;
            this.c1SplitContainer1.ToolTipGradient = C1.Win.C1SplitContainer.ToolTipGradient.Vista;
            this.c1SplitContainer1.UseParentVisualStyle = false;
            // 
            // c1SplitterPanel1
            // 
            this.c1SplitterPanel1.BackColor = System.Drawing.Color.LightGray;
            this.c1SplitterPanel1.Controls.Add(this.tbPriceItemName);
            this.c1SplitterPanel1.Controls.Add(this.cbCategoryName);
            this.c1SplitterPanel1.Controls.Add(this.cbCategory);
            this.c1SplitterPanel1.Controls.Add(this.labelCount);
            this.c1SplitterPanel1.Controls.Add(this.panel2);
            this.c1SplitterPanel1.Controls.Add(this.panel1);
            this.c1SplitterPanel1.Controls.Add(this.btnDelete);
            this.c1SplitterPanel1.Controls.Add(this.btnEdit);
            this.c1SplitterPanel1.Controls.Add(this.btnAdd);
            this.c1SplitterPanel1.Controls.Add(this.btnSelect);
            this.c1SplitterPanel1.Controls.Add(this.label3);
            this.c1SplitterPanel1.Controls.Add(this.label2);
            this.c1SplitterPanel1.Controls.Add(this.label1);
            this.c1SplitterPanel1.Controls.Add(this.picBackground);
            this.c1SplitterPanel1.Height = 1004;
            this.c1SplitterPanel1.Location = new System.Drawing.Point(0, 0);
            this.c1SplitterPanel1.Name = "c1SplitterPanel1";
            this.c1SplitterPanel1.Size = new System.Drawing.Size(1912, 1004);
            this.c1SplitterPanel1.TabIndex = 0;
            // 
            // tbPriceItemName
            // 
            this.tbPriceItemName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.tbPriceItemName.Location = new System.Drawing.Point(649, 180);
            this.tbPriceItemName.Name = "tbPriceItemName";
            this.tbPriceItemName.Size = new System.Drawing.Size(100, 19);
            this.tbPriceItemName.TabIndex = 11;
            this.tbPriceItemName.Tag = null;
            // 
            // cbCategoryName
            // 
            this.cbCategoryName.AllowSpinLoop = false;
            this.cbCategoryName.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cbCategoryName.GapHeight = 0;
            this.cbCategoryName.ImagePadding = new System.Windows.Forms.Padding(0);
            this.cbCategoryName.ItemsDisplayMember = "";
            this.cbCategoryName.ItemsValueMember = "";
            this.cbCategoryName.Location = new System.Drawing.Point(516, 180);
            this.cbCategoryName.Name = "cbCategoryName";
            this.cbCategoryName.Size = new System.Drawing.Size(127, 19);
            this.cbCategoryName.TabIndex = 10;
            this.cbCategoryName.Tag = null;
            // 
            // cbCategory
            // 
            this.cbCategory.AllowSpinLoop = false;
            this.cbCategory.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.cbCategory.GapHeight = 0;
            this.cbCategory.ImagePadding = new System.Windows.Forms.Padding(0);
            this.cbCategory.ItemsDisplayMember = "";
            this.cbCategory.ItemsValueMember = "";
            this.cbCategory.Location = new System.Drawing.Point(382, 180);
            this.cbCategory.Name = "cbCategory";
            this.cbCategory.Size = new System.Drawing.Size(127, 19);
            this.cbCategory.TabIndex = 10;
            this.cbCategory.Tag = null;
            // 
            // labelCount
            // 
            this.labelCount.AutoSize = true;
            this.labelCount.BackColor = System.Drawing.Color.LightGray;
            this.labelCount.Location = new System.Drawing.Point(388, 945);
            this.labelCount.Name = "labelCount";
            this.labelCount.Size = new System.Drawing.Size(39, 12);
            this.labelCount.TabIndex = 7;
            this.labelCount.Text = "총 0건";
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.LightGray;
            this.panel2.Controls.Add(this.FGrid1);
            this.panel2.Location = new System.Drawing.Point(383, 300);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(1480, 635);
            this.panel2.TabIndex = 6;
            // 
            // FGrid1
            // 
            this.FGrid1.AllowEditing = false;
            this.FGrid1.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid1.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid1.Location = new System.Drawing.Point(0, 0);
            this.FGrid1.Name = "FGrid1";
            this.FGrid1.Rows.DefaultSize = 20;
            this.FGrid1.Size = new System.Drawing.Size(1480, 635);
            this.FGrid1.TabIndex = 5;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.LightGray;
            this.panel1.Controls.Add(this.label5);
            this.panel1.Location = new System.Drawing.Point(383, 262);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1480, 32);
            this.panel1.TabIndex = 6;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.BackColor = System.Drawing.Color.LightGray;
            this.label5.Font = new System.Drawing.Font("맑은 고딕", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.label5.Location = new System.Drawing.Point(11, 3);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(88, 25);
            this.label5.TabIndex = 1;
            this.label5.Text = "조회결과";
            // 
            // btnDelete
            // 
            this.btnDelete.Font = new System.Drawing.Font("맑은 고딕", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.btnDelete.Image = ((System.Drawing.Image)(resources.GetObject("btnDelete.Image")));
            this.btnDelete.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnDelete.Location = new System.Drawing.Point(1050, 165);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(90, 34);
            this.btnDelete.TabIndex = 4;
            this.btnDelete.Text = "삭제";
            this.btnDelete.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnDelete.UseVisualStyleBackColor = true;
            // 
            // btnEdit
            // 
            this.btnEdit.Font = new System.Drawing.Font("맑은 고딕", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.btnEdit.Image = ((System.Drawing.Image)(resources.GetObject("btnEdit.Image")));
            this.btnEdit.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnEdit.Location = new System.Drawing.Point(954, 165);
            this.btnEdit.Name = "btnEdit";
            this.btnEdit.Size = new System.Drawing.Size(90, 34);
            this.btnEdit.TabIndex = 4;
            this.btnEdit.Text = "수정";
            this.btnEdit.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnEdit.UseVisualStyleBackColor = true;
            // 
            // btnAdd
            // 
            this.btnAdd.Font = new System.Drawing.Font("맑은 고딕", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.btnAdd.Image = ((System.Drawing.Image)(resources.GetObject("btnAdd.Image")));
            this.btnAdd.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnAdd.Location = new System.Drawing.Point(858, 165);
            this.btnAdd.Name = "btnAdd";
            this.btnAdd.Size = new System.Drawing.Size(90, 34);
            this.btnAdd.TabIndex = 4;
            this.btnAdd.Text = "신규";
            this.btnAdd.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnAdd.UseVisualStyleBackColor = true;
            // 
            // btnSelect
            // 
            this.btnSelect.Font = new System.Drawing.Font("맑은 고딕", 14.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(129)));
            this.btnSelect.Image = ((System.Drawing.Image)(resources.GetObject("btnSelect.Image")));
            this.btnSelect.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.btnSelect.Location = new System.Drawing.Point(762, 165);
            this.btnSelect.Name = "btnSelect";
            this.btnSelect.Size = new System.Drawing.Size(90, 34);
            this.btnSelect.TabIndex = 4;
            this.btnSelect.Text = "조회";
            this.btnSelect.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.btnSelect.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.Color.LightGray;
            this.label3.Location = new System.Drawing.Point(647, 165);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(41, 12);
            this.label3.TabIndex = 1;
            this.label3.Text = "품목명";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.Color.LightGray;
            this.label2.Location = new System.Drawing.Point(514, 165);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(53, 12);
            this.label2.TabIndex = 1;
            this.label2.Text = "품목분류";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.BackColor = System.Drawing.Color.LightGray;
            this.label1.Location = new System.Drawing.Point(380, 165);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(53, 12);
            this.label1.TabIndex = 1;
            this.label1.Text = "카테고리";
            // 
            // picBackground
            // 
            this.picBackground.BackColor = System.Drawing.Color.LightGray;
            this.picBackground.Image = global::PineTree.Properties.Resources.BackPriceItems_Sample;
            this.picBackground.Location = new System.Drawing.Point(0, 0);
            this.picBackground.Name = "picBackground";
            this.picBackground.Size = new System.Drawing.Size(1912, 1004);
            this.picBackground.TabIndex = 0;
            this.picBackground.TabStop = false;
            // 
            // UcPriceItems
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.c1SplitContainer1);
            this.Name = "UcPriceItems";
            this.Size = new System.Drawing.Size(1912, 1004);
            ((System.ComponentModel.ISupportInitialize)(this.c1SplitContainer1)).EndInit();
            this.c1SplitContainer1.ResumeLayout(false);
            this.c1SplitterPanel1.ResumeLayout(false);
            this.c1SplitterPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tbPriceItemName)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbCategoryName)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.cbCategory)).EndInit();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid1)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.btnDelete)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnEdit)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnAdd)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.btnSelect)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox picBackground;
        private C1.Win.C1SplitContainer.C1SplitContainer c1SplitContainer1;
        private C1.Win.C1SplitContainer.C1SplitterPanel c1SplitterPanel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid1;
        private C1.Win.C1Input.C1Button btnDelete;
        private C1.Win.C1Input.C1Button btnEdit;
        private C1.Win.C1Input.C1Button btnAdd;
        private C1.Win.C1Input.C1Button btnSelect;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelCount;
        private C1.Win.C1Input.C1TextBox tbPriceItemName;
        private C1.Win.C1Input.C1ComboBox cbCategoryName;
        private C1.Win.C1Input.C1ComboBox cbCategory;
        private System.Windows.Forms.Label label3;
    }
}

