namespace PineTree.UserControls
{
    partial class UcMain
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
            this.picBackground = new System.Windows.Forms.PictureBox();
            this.c1DockingTab1 = new C1.Win.C1Command.C1DockingTab();
            this.c1DockingTabPage1 = new C1.Win.C1Command.C1DockingTabPage();
            this.c1DockingTabPage2 = new C1.Win.C1Command.C1DockingTabPage();
            this.c1DockingTabPage3 = new C1.Win.C1Command.C1DockingTabPage();
            this.c1DockingTabPage4 = new C1.Win.C1Command.C1DockingTabPage();
            this.c1DockingTabPage5 = new C1.Win.C1Command.C1DockingTabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.FGrid1 = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.panel2 = new System.Windows.Forms.Panel();
            this.FGrid2 = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.panel3 = new System.Windows.Forms.Panel();
            this.FGrid3 = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.panel4 = new System.Windows.Forms.Panel();
            this.FGrid4 = new C1.Win.C1FlexGrid.C1FlexGrid();
            this.panel5 = new System.Windows.Forms.Panel();
            this.FGrid5 = new C1.Win.C1FlexGrid.C1FlexGrid();
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1DockingTab1)).BeginInit();
            this.c1DockingTab1.SuspendLayout();
            this.c1DockingTabPage1.SuspendLayout();
            this.c1DockingTabPage2.SuspendLayout();
            this.c1DockingTabPage3.SuspendLayout();
            this.c1DockingTabPage4.SuspendLayout();
            this.c1DockingTabPage5.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid1)).BeginInit();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid2)).BeginInit();
            this.panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid3)).BeginInit();
            this.panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid4)).BeginInit();
            this.panel5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.FGrid5)).BeginInit();
            this.SuspendLayout();
            // 
            // picBackground
            // 
            this.picBackground.Image = global::PineTree.Properties.Resources.BackMain_01;
            this.picBackground.Location = new System.Drawing.Point(0, 0);
            this.picBackground.Name = "picBackground";
            this.picBackground.Size = new System.Drawing.Size(1912, 1004);
            this.picBackground.TabIndex = 0;
            this.picBackground.TabStop = false;
            // 
            // c1DockingTab1
            // 
            this.c1DockingTab1.Controls.Add(this.c1DockingTabPage1);
            this.c1DockingTab1.Controls.Add(this.c1DockingTabPage2);
            this.c1DockingTab1.Controls.Add(this.c1DockingTabPage3);
            this.c1DockingTab1.Controls.Add(this.c1DockingTabPage4);
            this.c1DockingTab1.Controls.Add(this.c1DockingTabPage5);
            this.c1DockingTab1.Location = new System.Drawing.Point(342, 404);
            this.c1DockingTab1.Name = "c1DockingTab1";
            this.c1DockingTab1.Size = new System.Drawing.Size(1549, 573);
            this.c1DockingTab1.TabIndex = 1;
            this.c1DockingTab1.TabsSpacing = 5;
            this.c1DockingTab1.TabStyle = C1.Win.C1Command.TabStyleEnum.Office2010;
            this.c1DockingTab1.VisualStyle = C1.Win.C1Command.VisualStyle.Office2010Black;
            this.c1DockingTab1.VisualStyleBase = C1.Win.C1Command.VisualStyle.Office2010Black;
            // 
            // c1DockingTabPage1
            // 
            this.c1DockingTabPage1.Controls.Add(this.panel1);
            this.c1DockingTabPage1.Location = new System.Drawing.Point(1, 25);
            this.c1DockingTabPage1.Name = "c1DockingTabPage1";
            this.c1DockingTabPage1.Size = new System.Drawing.Size(1547, 547);
            this.c1DockingTabPage1.TabIndex = 0;
            this.c1DockingTabPage1.Text = "오늘 주문";
            // 
            // c1DockingTabPage2
            // 
            this.c1DockingTabPage2.Controls.Add(this.panel2);
            this.c1DockingTabPage2.Location = new System.Drawing.Point(1, 25);
            this.c1DockingTabPage2.Name = "c1DockingTabPage2";
            this.c1DockingTabPage2.Size = new System.Drawing.Size(1547, 547);
            this.c1DockingTabPage2.TabIndex = 1;
            this.c1DockingTabPage2.Text = "출력 중";
            // 
            // c1DockingTabPage3
            // 
            this.c1DockingTabPage3.Controls.Add(this.panel3);
            this.c1DockingTabPage3.Location = new System.Drawing.Point(1, 25);
            this.c1DockingTabPage3.Name = "c1DockingTabPage3";
            this.c1DockingTabPage3.Size = new System.Drawing.Size(1547, 547);
            this.c1DockingTabPage3.TabIndex = 2;
            this.c1DockingTabPage3.Text = "후가공";
            // 
            // c1DockingTabPage4
            // 
            this.c1DockingTabPage4.Controls.Add(this.panel4);
            this.c1DockingTabPage4.Location = new System.Drawing.Point(1, 25);
            this.c1DockingTabPage4.Name = "c1DockingTabPage4";
            this.c1DockingTabPage4.Size = new System.Drawing.Size(1547, 547);
            this.c1DockingTabPage4.TabIndex = 3;
            this.c1DockingTabPage4.Text = "포장대기";
            // 
            // c1DockingTabPage5
            // 
            this.c1DockingTabPage5.Controls.Add(this.panel5);
            this.c1DockingTabPage5.Location = new System.Drawing.Point(1, 25);
            this.c1DockingTabPage5.Name = "c1DockingTabPage5";
            this.c1DockingTabPage5.Size = new System.Drawing.Size(1547, 547);
            this.c1DockingTabPage5.TabIndex = 4;
            this.c1DockingTabPage5.Text = "포장완료";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.LightGray;
            this.panel1.Controls.Add(this.FGrid1);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Padding = new System.Windows.Forms.Padding(5);
            this.panel1.Size = new System.Drawing.Size(1547, 547);
            this.panel1.TabIndex = 0;
            // 
            // FGrid1
            // 
            this.FGrid1.AllowEditing = false;
            this.FGrid1.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid1.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid1.Location = new System.Drawing.Point(5, 5);
            this.FGrid1.Name = "FGrid1";
            this.FGrid1.Rows.DefaultSize = 20;
            this.FGrid1.Size = new System.Drawing.Size(1537, 537);
            this.FGrid1.TabIndex = 6;
            // 
            // panel2
            // 
            this.panel2.BackColor = System.Drawing.Color.LightGray;
            this.panel2.Controls.Add(this.FGrid2);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Padding = new System.Windows.Forms.Padding(5);
            this.panel2.Size = new System.Drawing.Size(1547, 547);
            this.panel2.TabIndex = 1;
            // 
            // FGrid2
            // 
            this.FGrid2.AllowEditing = false;
            this.FGrid2.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid2.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid2.Location = new System.Drawing.Point(5, 5);
            this.FGrid2.Name = "FGrid2";
            this.FGrid2.Rows.DefaultSize = 20;
            this.FGrid2.Size = new System.Drawing.Size(1537, 537);
            this.FGrid2.TabIndex = 6;
            // 
            // panel3
            // 
            this.panel3.BackColor = System.Drawing.Color.LightGray;
            this.panel3.Controls.Add(this.FGrid3);
            this.panel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel3.Location = new System.Drawing.Point(0, 0);
            this.panel3.Name = "panel3";
            this.panel3.Padding = new System.Windows.Forms.Padding(5);
            this.panel3.Size = new System.Drawing.Size(1547, 547);
            this.panel3.TabIndex = 1;
            // 
            // FGrid3
            // 
            this.FGrid3.AllowEditing = false;
            this.FGrid3.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid3.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid3.Location = new System.Drawing.Point(5, 5);
            this.FGrid3.Name = "FGrid3";
            this.FGrid3.Rows.DefaultSize = 20;
            this.FGrid3.Size = new System.Drawing.Size(1537, 537);
            this.FGrid3.TabIndex = 6;
            // 
            // panel4
            // 
            this.panel4.BackColor = System.Drawing.Color.LightGray;
            this.panel4.Controls.Add(this.FGrid4);
            this.panel4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel4.Location = new System.Drawing.Point(0, 0);
            this.panel4.Name = "panel4";
            this.panel4.Padding = new System.Windows.Forms.Padding(5);
            this.panel4.Size = new System.Drawing.Size(1547, 547);
            this.panel4.TabIndex = 1;
            // 
            // FGrid4
            // 
            this.FGrid4.AllowEditing = false;
            this.FGrid4.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid4.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid4.Location = new System.Drawing.Point(5, 5);
            this.FGrid4.Name = "FGrid4";
            this.FGrid4.Rows.DefaultSize = 20;
            this.FGrid4.Size = new System.Drawing.Size(1537, 537);
            this.FGrid4.TabIndex = 6;
            // 
            // panel5
            // 
            this.panel5.BackColor = System.Drawing.Color.LightGray;
            this.panel5.Controls.Add(this.FGrid5);
            this.panel5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel5.Location = new System.Drawing.Point(0, 0);
            this.panel5.Name = "panel5";
            this.panel5.Padding = new System.Windows.Forms.Padding(5);
            this.panel5.Size = new System.Drawing.Size(1547, 547);
            this.panel5.TabIndex = 1;
            // 
            // FGrid5
            // 
            this.FGrid5.AllowEditing = false;
            this.FGrid5.AllowSorting = C1.Win.C1FlexGrid.AllowSortingEnum.None;
            this.FGrid5.ColumnInfo = "10,0,0,0,0,100,Columns:";
            this.FGrid5.Dock = System.Windows.Forms.DockStyle.Fill;
            this.FGrid5.Location = new System.Drawing.Point(5, 5);
            this.FGrid5.Name = "FGrid5";
            this.FGrid5.Rows.DefaultSize = 20;
            this.FGrid5.Size = new System.Drawing.Size(1537, 537);
            this.FGrid5.TabIndex = 6;
            // 
            // UcMain
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.c1DockingTab1);
            this.Controls.Add(this.picBackground);
            this.Name = "UcMain";
            this.Size = new System.Drawing.Size(1912, 1004);
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1DockingTab1)).EndInit();
            this.c1DockingTab1.ResumeLayout(false);
            this.c1DockingTabPage1.ResumeLayout(false);
            this.c1DockingTabPage2.ResumeLayout(false);
            this.c1DockingTabPage3.ResumeLayout(false);
            this.c1DockingTabPage4.ResumeLayout(false);
            this.c1DockingTabPage5.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid1)).EndInit();
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid2)).EndInit();
            this.panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid3)).EndInit();
            this.panel4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid4)).EndInit();
            this.panel5.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.FGrid5)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox picBackground;
        private C1.Win.C1Command.C1DockingTab c1DockingTab1;
        private C1.Win.C1Command.C1DockingTabPage c1DockingTabPage1;
        private System.Windows.Forms.Panel panel1;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid1;
        private C1.Win.C1Command.C1DockingTabPage c1DockingTabPage2;
        private System.Windows.Forms.Panel panel2;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid2;
        private C1.Win.C1Command.C1DockingTabPage c1DockingTabPage3;
        private System.Windows.Forms.Panel panel3;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid3;
        private C1.Win.C1Command.C1DockingTabPage c1DockingTabPage4;
        private System.Windows.Forms.Panel panel4;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid4;
        private C1.Win.C1Command.C1DockingTabPage c1DockingTabPage5;
        private System.Windows.Forms.Panel panel5;
        private C1.Win.C1FlexGrid.C1FlexGrid FGrid5;
    }
}
