namespace PineTree
{
    partial class Form1
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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            this.c1TextBox1 = new C1.Win.C1Input.C1TextBox();
            this.c1NumericEdit1 = new C1.Win.C1Input.C1NumericEdit();
            this.c1InputPanel1 = new C1.Win.C1InputPanel.C1InputPanel();
            this.c1Button1 = new C1.Win.C1Input.C1Button();
            ((System.ComponentModel.ISupportInitialize)(this.c1TextBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1NumericEdit1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1InputPanel1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1Button1)).BeginInit();
            this.SuspendLayout();
            // 
            // c1TextBox1
            // 
            this.c1TextBox1.Location = new System.Drawing.Point(68, 109);
            this.c1TextBox1.Name = "c1TextBox1";
            this.c1TextBox1.Size = new System.Drawing.Size(100, 19);
            this.c1TextBox1.TabIndex = 0;
            this.c1TextBox1.Tag = null;
            // 
            // c1NumericEdit1
            // 
            this.c1NumericEdit1.ImagePadding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            this.c1NumericEdit1.Location = new System.Drawing.Point(459, 165);
            this.c1NumericEdit1.Name = "c1NumericEdit1";
            this.c1NumericEdit1.Size = new System.Drawing.Size(200, 19);
            this.c1NumericEdit1.TabIndex = 1;
            this.c1NumericEdit1.Tag = null;
            // 
            // c1InputPanel1
            // 
            this.c1InputPanel1.AutoSizeElement = C1.Framework.AutoSizeElement.Both;
            this.c1InputPanel1.Font = new System.Drawing.Font("맑은 고딕", 9F);
            this.c1InputPanel1.Location = new System.Drawing.Point(203, 270);
            this.c1InputPanel1.Name = "c1InputPanel1";
            this.c1InputPanel1.Size = new System.Drawing.Size(240, 150);
            this.c1InputPanel1.TabIndex = 2;
            // 
            // c1Button1
            // 
            this.c1Button1.Location = new System.Drawing.Point(242, 89);
            this.c1Button1.Name = "c1Button1";
            this.c1Button1.Size = new System.Drawing.Size(75, 23);
            this.c1Button1.TabIndex = 3;
            this.c1Button1.Text = "c1Button1";
            this.c1Button1.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.c1Button1);
            this.Controls.Add(this.c1InputPanel1);
            this.Controls.Add(this.c1NumericEdit1);
            this.Controls.Add(this.c1TextBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.c1TextBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1NumericEdit1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1InputPanel1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.c1Button1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private C1.Win.C1Input.C1TextBox c1TextBox1;
        private C1.Win.C1Input.C1NumericEdit c1NumericEdit1;
        private C1.Win.C1InputPanel.C1InputPanel c1InputPanel1;
        private C1.Win.C1Input.C1Button c1Button1;
    }
}

