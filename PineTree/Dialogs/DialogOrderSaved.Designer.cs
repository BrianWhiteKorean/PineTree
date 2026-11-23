namespace PineTree.Dialogs
{
    partial class DialogOrderSaved
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelMessage = new System.Windows.Forms.Label();
            this.btnNewWork = new System.Windows.Forms.Button();
            this.btnManagement = new System.Windows.Forms.Button();
            this.SuspendLayout();
            this.labelMessage.AutoSize = true;
            this.labelMessage.Location = new System.Drawing.Point(24, 30);
            this.labelMessage.Name = "labelMessage";
            this.labelMessage.Text = "저장되었습니다. 새로운 작업을 시작할까요?";
            this.btnNewWork.Location = new System.Drawing.Point(94, 90);
            this.btnNewWork.Name = "btnNewWork";
            this.btnNewWork.Size = new System.Drawing.Size(90, 30);
            this.btnNewWork.TabIndex = 0;
            this.btnNewWork.Text = "예";
            this.btnNewWork.UseVisualStyleBackColor = true;
            this.btnManagement.Location = new System.Drawing.Point(196, 90);
            this.btnManagement.Name = "btnManagement";
            this.btnManagement.Size = new System.Drawing.Size(260, 30);
            this.btnManagement.TabIndex = 1;
            this.btnManagement.Text = "아니오(관리모드로 돌아가기)";
            this.btnManagement.UseVisualStyleBackColor = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(480, 146);
            this.Controls.Add(this.labelMessage);
            this.Controls.Add(this.btnNewWork);
            this.Controls.Add(this.btnManagement);
            this.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DialogOrderSaved";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "주문 저장";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.Button btnNewWork;
        private System.Windows.Forms.Button btnManagement;
    }
}
