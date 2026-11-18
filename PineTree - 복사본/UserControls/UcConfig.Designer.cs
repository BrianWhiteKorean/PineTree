namespace PineTree.UserControls
{
    partial class UcConfig
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
            this.picBackground = new System.Windows.Forms.PictureBox();
            this.lblCommonCode = new System.Windows.Forms.Label();
            this.lblProductFinishingRules = new System.Windows.Forms.Label();
            this.lblOther = new System.Windows.Forms.Label();
            this.ucCommonCode = new PineTree.UserControls.UcCommonCode();
            this.ucProcessRule = new PineTree.UserControls.UcProcessRule();
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).BeginInit();
            this.SuspendLayout();
            //
            // picBackground
            //
            this.picBackground.BackColor = System.Drawing.Color.LightGray;
            this.picBackground.Controls.Add(this.lblCommonCode);
            this.picBackground.Controls.Add(this.lblProductFinishingRules);
            this.picBackground.Controls.Add(this.lblOther);
            this.picBackground.Controls.Add(this.ucCommonCode);
            this.picBackground.Controls.Add(this.ucProcessRule);
            this.picBackground.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picBackground.Image = global::PineTree.Properties.Resources.BackConfig;
            this.picBackground.Location = new System.Drawing.Point(0, 0);
            this.picBackground.Name = "picBackground";
            this.picBackground.Size = new System.Drawing.Size(1912, 1004);
            this.picBackground.TabIndex = 0;
            this.picBackground.TabStop = false;
            //
            //
            // lblCommonCode
            //
            this.lblCommonCode.AutoSize = true;
            this.lblCommonCode.BackColor = System.Drawing.Color.Transparent;
            this.lblCommonCode.Font = new System.Drawing.Font("맑은 고딕", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblCommonCode.ForeColor = System.Drawing.Color.White;
            this.lblCommonCode.Location = new System.Drawing.Point(50, 143);
            this.lblCommonCode.Name = "lblCommonCode";
            this.lblCommonCode.TabIndex = 0;
            this.lblCommonCode.Text = "공통코드";
            //
            // lblProductFinishingRules
            //
            this.lblProductFinishingRules.AutoSize = true;
            this.lblProductFinishingRules.BackColor = System.Drawing.Color.Transparent;
            this.lblProductFinishingRules.Font = new System.Drawing.Font("맑은 고딕", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblProductFinishingRules.ForeColor = System.Drawing.Color.White;
            this.lblProductFinishingRules.Location = new System.Drawing.Point(374, 143);
            this.lblProductFinishingRules.Name = "lblProductFinishingRules";
            this.lblProductFinishingRules.TabIndex = 1;
            this.lblProductFinishingRules.Text = "상품별 후가공 규칙";
            //
            // lblOther
            //
            this.lblOther.AutoSize = true;
            this.lblOther.BackColor = System.Drawing.Color.Transparent;
            this.lblOther.Font = new System.Drawing.Font("맑은 고딕", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 129);
            this.lblOther.ForeColor = System.Drawing.Color.White;
            this.lblOther.Location = new System.Drawing.Point(698, 143);
            this.lblOther.Name = "lblOther";
            this.lblOther.TabIndex = 2;
            this.lblOther.Text = "기타";
            //
            // ucCommonCode
            //
            this.ucCommonCode.Location = new System.Drawing.Point(48, 246);
            this.ucCommonCode.Name = "ucCommonCode";
            this.ucCommonCode.Size = new System.Drawing.Size(1496, 656);
            this.ucCommonCode.TabIndex = 3;
            //
            // ucProcessRule
            //
            this.ucProcessRule.Location = new System.Drawing.Point(48, 246);
            this.ucProcessRule.Name = "ucProcessRule";
            this.ucProcessRule.Size = new System.Drawing.Size(1496, 656);
            this.ucProcessRule.TabIndex = 4;
            this.ucProcessRule.Visible = false;
            //
            // UcConfig
            //
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.picBackground);
            this.Name = "UcConfig";
            this.Size = new System.Drawing.Size(1912, 1004);
            ((System.ComponentModel.ISupportInitialize)(this.picBackground)).EndInit();
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.PictureBox picBackground;
        private System.Windows.Forms.Label lblCommonCode;
        private System.Windows.Forms.Label lblProductFinishingRules;
        private System.Windows.Forms.Label lblOther;
        private PineTree.UserControls.UcCommonCode ucCommonCode;
        private PineTree.UserControls.UcProcessRule ucProcessRule;
    }
}
