namespace PineTree.UserControls
{
    partial class UcProcessRule
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.splitMain = new System.Windows.Forms.SplitContainer();
            this.priceItemGrid = new PineTree.UserControls.PagedGrid();
            this.btnSelect = new System.Windows.Forms.Button();
            this.lblItemsTitle = new System.Windows.Forms.Label();
            this.lblDetailTitle = new System.Windows.Forms.Label();
            this.lblSelectedItem = new System.Windows.Forms.Label();
            this.lblSelectedCategory = new System.Windows.Forms.Label();
            this.lblSelectedItemCode = new System.Windows.Forms.Label();
            this.lblRulesTitle = new System.Windows.Forms.Label();
            this.ruleEditorPanel = new System.Windows.Forms.Panel();
            this.lblRuleHint = new System.Windows.Forms.Label();
            this.btnAddRule = new System.Windows.Forms.Button();
            this.btnDeleteRule = new System.Windows.Forms.Button();
            this.ruleList = new System.Windows.Forms.FlowLayoutPanel();
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).BeginInit();
            this.splitMain.Panel1.SuspendLayout();
            this.splitMain.Panel2.SuspendLayout();
            this.splitMain.SuspendLayout();
            this.ruleEditorPanel.SuspendLayout();
            this.SuspendLayout();
            //
            // splitMain
            //
            this.splitMain.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.splitMain.BackColor = System.Drawing.Color.FromArgb(45, 58, 95);
            this.splitMain.Location = new System.Drawing.Point(20, 20);
            this.splitMain.Name = "splitMain";
            this.splitMain.Size = new System.Drawing.Size(1456, 616);
            this.splitMain.SplitterDistance = 650;
            this.splitMain.Panel1MinSize = 550;
            this.splitMain.Panel2MinSize = 450;
            this.splitMain.SplitterWidth = 6;
            this.splitMain.TabIndex = 0;
            this.splitMain.Panel1.BackColor = System.Drawing.Color.FromArgb(23, 28, 54);
            this.splitMain.Panel1.Controls.Add(this.lblItemsTitle);
            this.splitMain.Panel1.Controls.Add(this.btnSelect);
            this.splitMain.Panel1.Controls.Add(this.priceItemGrid);
            this.splitMain.Panel2.BackColor = System.Drawing.Color.FromArgb(23, 28, 54);
            this.splitMain.Panel2.Controls.Add(this.lblDetailTitle);
            this.splitMain.Panel2.Controls.Add(this.lblSelectedItem);
            this.splitMain.Panel2.Controls.Add(this.lblSelectedCategory);
            this.splitMain.Panel2.Controls.Add(this.lblSelectedItemCode);
            this.splitMain.Panel2.Controls.Add(this.lblRulesTitle);
            this.splitMain.Panel2.Controls.Add(this.ruleEditorPanel);
            //
            // priceItemGrid
            //
            this.priceItemGrid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.priceItemGrid.BackColor = System.Drawing.Color.FromArgb(23, 28, 54);
            this.priceItemGrid.Location = new System.Drawing.Point(12, 64);
            this.priceItemGrid.Name = "priceItemGrid";
            this.priceItemGrid.Size = new System.Drawing.Size(626, 540);
            this.priceItemGrid.TabIndex = 1;
            //
            // btnSelect
            //
            this.btnSelect.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right));
            this.btnSelect.BackColor = System.Drawing.Color.FromArgb(48, 108, 180);
            this.btnSelect.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSelect.FlatAppearance.BorderSize = 0;
            this.btnSelect.ForeColor = System.Drawing.Color.White;
            this.btnSelect.Location = new System.Drawing.Point(548, 14);
            this.btnSelect.Name = "btnSelect";
            this.btnSelect.Size = new System.Drawing.Size(90, 34);
            this.btnSelect.Text = "조회";
            this.btnSelect.TabIndex = 0;
            this.btnSelect.UseVisualStyleBackColor = false;
            //
            // ruleEditorPanel
            //
            this.ruleEditorPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.ruleEditorPanel.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.ruleEditorPanel.Controls.Add(this.lblRuleHint);
            this.ruleEditorPanel.Controls.Add(this.btnAddRule);
            this.ruleEditorPanel.Controls.Add(this.btnDeleteRule);
            this.ruleEditorPanel.Controls.Add(this.ruleList);
            this.ruleEditorPanel.Location = new System.Drawing.Point(16, 198);
            this.ruleEditorPanel.Name = "ruleEditorPanel";
            this.ruleEditorPanel.Size = new System.Drawing.Size(768, 402);
            this.ruleEditorPanel.TabIndex = 0;
            //
            // lblRuleHint
            //
            this.lblRuleHint.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblRuleHint.Location = new System.Drawing.Point(0, 0);
            this.lblRuleHint.ForeColor = System.Drawing.Color.FromArgb(110, 128, 159);
            this.lblRuleHint.Name = "lblRuleHint";
            this.lblRuleHint.Padding = new System.Windows.Forms.Padding(16);
            this.lblRuleHint.Size = new System.Drawing.Size(472, 64);
            this.lblRuleHint.Text = "등록된 후가공 규칙이 없습니다";
            //
            // btnAddRule
            //
            this.btnAddRule.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right));
            this.btnAddRule.BackgroundImage = global::PineTree.Properties.Resources.규칙추가;
            this.btnAddRule.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btnAddRule.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAddRule.FlatAppearance.BorderSize = 0;
            this.btnAddRule.Location = new System.Drawing.Point(486, 8);
            this.btnAddRule.Size = new System.Drawing.Size(130, 48);
            this.btnAddRule.Name = "btnAddRule";
            this.btnAddRule.AccessibleName = "규칙추가";
            this.btnAddRule.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnAddRule.TabIndex = 0;
            //
            // btnDeleteRule
            //
            this.btnDeleteRule.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right));
            this.btnDeleteRule.BackgroundImage = global::PineTree.Properties.Resources.규칙삭제;
            this.btnDeleteRule.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.btnDeleteRule.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDeleteRule.FlatAppearance.BorderSize = 0;
            this.btnDeleteRule.Location = new System.Drawing.Point(624, 8);
            this.btnDeleteRule.Size = new System.Drawing.Size(130, 48);
            this.btnDeleteRule.Name = "btnDeleteRule";
            this.btnDeleteRule.AccessibleName = "규칙삭제";
            this.btnDeleteRule.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnDeleteRule.Enabled = false;
            this.btnDeleteRule.TabIndex = 1;
            //
            // ruleList
            //
            this.ruleList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                | System.Windows.Forms.AnchorStyles.Left) | System.Windows.Forms.AnchorStyles.Right)));
            this.ruleList.AutoScroll = true;
            this.ruleList.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.ruleList.WrapContents = false;
            this.ruleList.Location = new System.Drawing.Point(8, 64);
            this.ruleList.Name = "ruleList";
            this.ruleList.Size = new System.Drawing.Size(750, 328);
            this.ruleList.TabIndex = 1;
            //
            // lblItemsTitle
            //
            this.lblItemsTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblItemsTitle.AutoEllipsis = true;
            this.lblItemsTitle.Font = new System.Drawing.Font("맑은 고딕", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblItemsTitle.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblItemsTitle.Location = new System.Drawing.Point(12, 16);
            this.lblItemsTitle.Name = "lblItemsTitle";
            this.lblItemsTitle.Size = new System.Drawing.Size(260, 32);
            this.lblItemsTitle.Text = "전체 품목";
            //
            // lblDetailTitle
            //
            this.lblDetailTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblDetailTitle.AutoEllipsis = true;
            this.lblDetailTitle.Font = new System.Drawing.Font("맑은 고딕", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblDetailTitle.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblDetailTitle.Location = new System.Drawing.Point(16, 16);
            this.lblDetailTitle.Name = "lblDetailTitle";
            this.lblDetailTitle.Size = new System.Drawing.Size(768, 32);
            this.lblDetailTitle.Text = "후가공 기본 규칙";
            //
            // lblSelectedItem
            //
            this.lblSelectedItem.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblSelectedItem.AutoEllipsis = true;
            this.lblSelectedItem.Font = new System.Drawing.Font("맑은 고딕", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblSelectedItem.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblSelectedItem.Location = new System.Drawing.Point(16, 58);
            this.lblSelectedItem.Name = "lblSelectedItem";
            this.lblSelectedItem.Size = new System.Drawing.Size(768, 28);
            this.lblSelectedItem.Text = "선택된 품목이 없습니다.";
            //
            // lblSelectedCategory
            //
            this.lblSelectedCategory.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblSelectedCategory.AutoEllipsis = true;
            this.lblSelectedCategory.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 129);
            this.lblSelectedCategory.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblSelectedCategory.Location = new System.Drawing.Point(16, 96);
            this.lblSelectedCategory.Name = "lblSelectedCategory";
            this.lblSelectedCategory.Size = new System.Drawing.Size(768, 24);
            this.lblSelectedCategory.Text = "좌측 목록에서 품목을 선택하세요.";
            //
            // lblSelectedItemCode
            //
            this.lblSelectedItemCode.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblSelectedItemCode.AutoEllipsis = true;
            this.lblSelectedItemCode.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 129);
            this.lblSelectedItemCode.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblSelectedItemCode.Location = new System.Drawing.Point(16, 126);
            this.lblSelectedItemCode.Name = "lblSelectedItemCode";
            this.lblSelectedItemCode.Size = new System.Drawing.Size(768, 24);
            this.lblSelectedItemCode.Text = "";
            //
            // lblRulesTitle
            //
            this.lblRulesTitle.Anchor = ((System.Windows.Forms.AnchorStyles)(System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right));
            this.lblRulesTitle.AutoEllipsis = true;
            this.lblRulesTitle.Font = new System.Drawing.Font("맑은 고딕", 11F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblRulesTitle.ForeColor = System.Drawing.Color.FromArgb(238, 247, 255);
            this.lblRulesTitle.Location = new System.Drawing.Point(16, 162);
            this.lblRulesTitle.Name = "lblRulesTitle";
            this.lblRulesTitle.Size = new System.Drawing.Size(768, 26);
            this.lblRulesTitle.Text = "규칙 세부";
            //
            // UcProcessRule
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.FromArgb(23, 28, 54);
            this.BackgroundImage = global::PineTree.Properties.Resources.ConfigCommon;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.Controls.Add(this.splitMain);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.Name = "UcProcessRule";
            this.Size = new System.Drawing.Size(1496, 656);
            this.splitMain.Panel1.ResumeLayout(false);
            this.splitMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitMain)).EndInit();
            this.splitMain.ResumeLayout(false);
            this.ruleEditorPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.SplitContainer splitMain;
        private PineTree.UserControls.PagedGrid priceItemGrid;
        private System.Windows.Forms.Button btnSelect;
        private System.Windows.Forms.Label lblItemsTitle;
        private System.Windows.Forms.Label lblDetailTitle;
        private System.Windows.Forms.Label lblSelectedItem;
        private System.Windows.Forms.Label lblSelectedCategory;
        private System.Windows.Forms.Label lblSelectedItemCode;
        private System.Windows.Forms.Label lblRulesTitle;
        private System.Windows.Forms.Panel ruleEditorPanel;
        private System.Windows.Forms.Label lblRuleHint;
        private System.Windows.Forms.Button btnAddRule;
        private System.Windows.Forms.Button btnDeleteRule;
        private System.Windows.Forms.FlowLayoutPanel ruleList;
    }
}
