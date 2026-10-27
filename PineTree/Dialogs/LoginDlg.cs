using Microsoft.Win32;
using PineTree.Classes;
using PineTree.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PineTree.Dialogs
{
    public partial class LoginDlg : Form
    {
        private CancellationTokenSource loginCancellation;

        // 레지스트리 저장 경로 정의
        private readonly string RegKeyPath = @"Software\PineTree\LoginUsers";
        private readonly string LastUserKeyName = "LastLoginUser";

        public LoginDlg(FormMain frm)
        {
            InitializeComponent();
            Owner = frm;
            tbUserID.KeyDown += tbUserID_KeyDown;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private async void btnOK_Click(object sender, EventArgs e)
        {
            if (loginCancellation != null)
                return;

            // 👈 tbUserID.Text에서 cbUserID.Text로 변경
            if (string.IsNullOrEmpty(tbUserID.Text.Trim()))
            {
                MSG.ShowError("아이디를 입력해주세요");
                return;
            }
            if (tbPassword.Text == "")
            {
                MSG.ShowError("패스워드를 입력해주세요");
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            loginCancellation = cancellation;
            btnOK.Enabled = false;
            tbUserID.Enabled = false;
            tbPassword.Enabled = false;
            UseWaitCursor = true;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Version assemblyVersion = assembly.GetName().Version;

                // 👈 cbUserID에서 텍스트를 가져옴
                string sUserID = tbUserID.Text.Trim();
                string sPassword = tbPassword.Text;

                // --- Database Authentication (Synchronous) ---
                DataTable dtUserInfo = await Task.Run(() => DB.MySqlQueryRead(
                    "SELECT UserID, UserName, Pwd, Auth, CompanyName FROM UserInfo WHERE UserID = @UserID",
                    new[] { new ParamValuePair("@UserID", sUserID) }));
                cancellation.Token.ThrowIfCancellationRequested();

                if (dtUserInfo.Rows.Count == 0)
                {
                    MSG.ShowError("사용자 정보가 존재하지 않습니다");
                    DialogResult = DialogResult.Abort;
                    return;
                }

                DataRow rUserInfo = dtUserInfo.Rows[0];

                // --- Password Check ---
                if (sPassword != rUserInfo["Pwd"].ToString())
                {
                    MSG.ShowError("비밀번호가 일치하지 않습니다");
                    DialogResult = DialogResult.Abort;
                    return;
                }

                // --- Login Successful: Get IP and Log ---
                NetworkUtility util = new NetworkUtility();
                string sMyIP = await util.GetPublicIPAddressAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();

                await Task.Run(() => DB.Execute(
                    "CALL sp_Insert_LoginHistory(@UserID, @IPAddress, @Version)",
                    new[]
                    {
                        new ParamValuePair("@UserID", rUserInfo["UserID"].ToString()),
                        new ParamValuePair("@IPAddress", sMyIP),
                        new ParamValuePair("@Version", assemblyVersion.ToString())
                    }));
                cancellation.Token.ThrowIfCancellationRequested();

                Global.UserID = rUserInfo["UserID"].ToString();
                Global.UserName = rUserInfo["UserName"].ToString();
                Global.UserType = rUserInfo["Auth"].ToString().Trim();
                Global.CompanyName = rUserInfo["CompanyName"].ToString().Trim();

                // 👈 [추가] 로그인 성공 시 아이디를 레지스트리에 저장
                SaveUserIDToRegistry(sUserID);

                // --- Final Success ---
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (!cancellation.IsCancellationRequested)
                {
                    MSG.ShowError($"로그인 중 오류가 발생했습니다: {ex.Message}");
                    DialogResult = DialogResult.Abort;
                }
            }
            finally
            {
                loginCancellation = null;
                cancellation.Dispose();
                if (!IsDisposed && !Disposing)
                {
                    UseWaitCursor = false;
                    btnOK.Enabled = true;
                    tbUserID.Enabled = true;
                    tbPassword.Enabled = true;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel)
                loginCancellation?.Cancel();
        }

        private void LoginDlg_Load(object sender, EventArgs e)
        {
            LoadLastUserID();
            tbUserID.Focus();

            ReadConfig();
        }

        private void LoginDlg_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        }

        private void tbPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnOK.PerformClick();
            }
        }

        // 👈 [수정] tbUserID_KeyDown에서 cbUserID_KeyDown으로 변경 (UI 속성창에서 연결 필요)
        private void tbUserID_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // 엔터 입력 시 패스워드 입력란으로 포커스 이동
                this.BeginInvoke(new Action(() => { tbPassword.Focus(); }));
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }


        private void LoadLastUserID()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKeyPath))
                {
                    tbUserID.Text = key?.GetValue(LastUserKeyName) as string ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"마지막 로그인 아이디 읽기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 성공한 로그인 아이디를 레지스트리에 저장합니다.
        /// </summary>
        /// <param name="userID">로그인 성공한 사용자 ID</param>
        private void SaveUserIDToRegistry(string userID)
        {
            try
            {
                // CreateSubKey는 경로가 없으면 생성하고, 있으면 엽니다.
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegKeyPath))
                {
                    if (key != null)
                    {
                        // 값 이름과 데이터 모두 userID로 저장하여 중복을 방지합니다.
                        key.SetValue(userID, userID);
                        // 마지막 로그인 사용자 저장
                        key.SetValue(LastUserKeyName, userID);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레지스트리 저장 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 레지스트리에 저장된 로그인 아이디를 삭제합니다.
        /// </summary>
        /// <param name="userID">삭제할 사용자 ID</param>
        /// <returns>삭제 여부</returns>
        private bool DeleteUserIDFromRegistry(string userID)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegKeyPath, true))
                {
                    if (key == null)
                        return false;

                    string registryUserID = key.GetValueNames()
                        .FirstOrDefault(id => string.Equals(id, userID, StringComparison.OrdinalIgnoreCase));

                    if (string.IsNullOrEmpty(registryUserID))
                        return false;

                    key.DeleteValue(registryUserID, false);

                    string lastUser = key.GetValue(LastUserKeyName)?.ToString();
                    if (string.Equals(lastUser, registryUserID, StringComparison.OrdinalIgnoreCase))
                    {
                        string nextUser = key.GetValueNames()
                            .FirstOrDefault(id => id != LastUserKeyName);

                        if (string.IsNullOrEmpty(nextUser))
                            key.DeleteValue(LastUserKeyName, false);
                        else
                            key.SetValue(LastUserKeyName, nextUser);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"레지스트리 삭제 실패: {ex.Message}");
                MSG.ShowError($"아이디 삭제 중 오류가 발생했습니다: {ex.Message}");
                return false;
            }
        }


        private bool ReadConfig()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config.ini");
                if (!File.Exists(filePath))
                    return false;

                iniUtil ini = new iniUtil(filePath);
                Global.DeptName = ini.GetIniValue("부서", "부서명");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"설정 파일 읽기 실패: {ex.Message}");
            }
            return false;
        }


    }
}
