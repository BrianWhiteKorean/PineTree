using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.Classes
{
    class ClassDefines
    {
    }

    public enum DialogMode
    {
        New,
        Edit,
    }

    public static class MSG
    {
        public static void ShowOK(string sCaption, string sMsg)
        {
            MessageBox.Show(sCaption, sMsg, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void ShowOK(string sMsg)
        {
            MessageBox.Show(sMsg, "알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static DialogResult ShowYesNo(string sCaption, string sMsg)
        {
            return MessageBox.Show(sCaption, sMsg, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        public static DialogResult ShowYesNo(string sMsg)
        {
            return MessageBox.Show(sMsg, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        public static DialogResult ShowError(string sCaption, string sMsg)
        {
            return MessageBox.Show(sCaption, sMsg, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static DialogResult ShowError(string sMsg)
        {
            return MessageBox.Show(sMsg, "에러", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static DialogResult ShowWarning(string sMsg)
        {
            return MessageBox.Show(sMsg, "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static DialogResult Confirm(string sMsg)
        {
            return MessageBox.Show(sMsg, "확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }

        public static DialogResult Confirm(string sMsg, string sCaption)
        {
            return MessageBox.Show(sMsg, sCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        }
    }

    public static class Global
    {
        public static string DBName { get; set; }
        public static string UserID { get; set; }
        public static string UserType { get; set; }
        public static string UserName { get; set; }
        public static string DeptName { get; set; }
        public static string CompanyName { get; set; }

        public static bool IsAdministrator
        {
            get { return !string.IsNullOrWhiteSpace(UserID) && string.Equals(UserType, "관리자", StringComparison.Ordinal); }
        }

        public static string UserBadge
        {
            get { return string.IsNullOrWhiteSpace(UserID) ? string.Empty : (IsAdministrator ? "관" : "사"); }
        }
    }
}
