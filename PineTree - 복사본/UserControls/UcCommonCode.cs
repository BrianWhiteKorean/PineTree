using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using PineTree.Classes;
using PineTree.Dialogs;

namespace PineTree.UserControls
{
    public partial class UcCommonCode : UserControl
    {
        private const string AllText = "[전체]";
        private DataTable _commonCodes;
        private bool _isLoading;
        private readonly HashSet<long> _checkedSeqNos = new HashSet<long>();

        public UcCommonCode()
        {
            InitializeComponent();

            cbGroupName.Items.Add(AllText);
            cbGroupName.SelectedIndex = 0;
            commonCodeGrid.Columns.Add(new PagedGridColumn("Selected", "선택", 70, ContentAlignment.MiddleCenter));
            commonCodeGrid.Columns.Add(new PagedGridColumn("GroupName", "그룹명", 280, ContentAlignment.MiddleLeft));
            commonCodeGrid.Columns.Add(new PagedGridColumn("ItemName", "항목명", 0, ContentAlignment.MiddleLeft));
            UpdatePageSize();
            commonCodeGrid.TotalText = "전체 0건";
            commonCodeGrid.PageChanged += delegate { BindCommonCodePage(); };
            commonCodeGrid.CellClicked += CommonCodeGrid_CellClicked;
            commonCodeGrid.Resize += delegate
            {
                UpdatePageSize();
                commonCodeGrid.PageIndex = commonCodeGrid.PageIndex;
                BindCommonCodePage();
            };
            Load += UcCommonCode_Load;
            btnSelect.Click += btnSelect_Click;
            btnAdd.Click += BtnAdd_Click;
            btnEdit.Click += BtnEdit_Click;
            btnDelete.Click += BtnDelete_Click;
        }

        private void UpdatePageSize()
        {
            commonCodeGrid.PageSize = Math.Max(1, (commonCodeGrid.Height - 36 - 52) / 41);
        }

        private async void UcCommonCode_Load(object sender, EventArgs e)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
            {
                return;
            }

            SetLoading(true);
            try
            {
                DataTable groups = await Task.Run(() => CommonCodeRepository.GetGroupNames());
                if (IsDisposed)
                {
                    return;
                }

                cbGroupName.Items.Clear();
                cbGroupName.Items.Add(AllText);
                foreach (DataRow row in groups.Rows)
                {
                    cbGroupName.Items.Add(Convert.ToString(row["GroupName"]));
                }
                cbGroupName.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show("그룹 목록을 불러오지 못했습니다.\r\n" + ex.Message, "공통코드", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetLoading(false);
                }
            }
        }

        private async void btnSelect_Click(object sender, EventArgs e)
        {
            await LoadCommonCodesAsync();
        }

        private async Task LoadCommonCodesAsync(long? selectedSeqNo = null)
        {
            if (_isLoading)
            {
                return;
            }

            SetLoading(true);
            try
            {
                string groupName = cbGroupName.SelectedIndex == 0 ? null : cbGroupName.Text;
                DataTable codes = await Task.Run(() => CommonCodeRepository.GetCommonCodes(groupName));
                if (IsDisposed)
                {
                    return;
                }

                _commonCodes = codes;
                _checkedSeqNos.Clear();
                commonCodeGrid.Columns.Clear();
                commonCodeGrid.Columns.Add(new PagedGridColumn("Selected", "선택", 70, ContentAlignment.MiddleCenter));
                foreach (DataColumn column in codes.Columns)
                {
                    if (column.ColumnName == "SeqNo")
                    {
                        continue;
                    }

                    string caption = column.ColumnName == "GroupName" ? "그룹명"
                        : column.ColumnName == "ItemName" ? "항목명" : column.ColumnName;
                    int width = column.ColumnName == "GroupName" ? 280 : 0;
                    commonCodeGrid.Columns.Add(new PagedGridColumn(column.ColumnName, caption, width,
                        ContentAlignment.MiddleLeft));
                }
                commonCodeGrid.TotalCount = codes.Rows.Count;
                int selectedIndex = -1;
                if (selectedSeqNo.HasValue)
                {
                    for (int index = 0; index < codes.Rows.Count; index++)
                    {
                        if (Convert.ToInt64(codes.Rows[index]["SeqNo"]) == selectedSeqNo.Value)
                        {
                            selectedIndex = index;
                            break;
                        }
                    }
                }
                commonCodeGrid.PageIndex = selectedIndex < 0 ? 1 : selectedIndex / commonCodeGrid.PageSize + 1;
                commonCodeGrid.TotalText = string.Format("전체 {0:#,##0}건", codes.Rows.Count);
                BindCommonCodePage();
                if (selectedIndex >= 0)
                    commonCodeGrid.SelectedRowIndex = selectedIndex % commonCodeGrid.PageSize;
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    MessageBox.Show("공통코드를 조회하지 못했습니다.\r\n" + ex.Message, "조회 오류", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetLoading(false);
                }
            }
        }

        private void SetLoading(bool loading)
        {
            _isLoading = loading;
            btnSelect.Enabled = !loading;
            btnAdd.Enabled = !loading;
            btnEdit.Enabled = !loading;
            btnDelete.Enabled = !loading;
            commonCodeGrid.Enabled = !loading;
            cbGroupName.Enabled = !loading;
        }

        private async void BtnAdd_Click(object sender, EventArgs e)
        {
            if (_isLoading) return;
            if (cbGroupName.SelectedIndex <= 0)
            {
                MessageBox.Show(this, "항목을 추가할 그룹을 먼저 선택해주세요.",
                    "공통코드", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (DialogCommonCode dialog = new DialogCommonCode(cbGroupName.Text))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    await LoadCommonCodesAsync(dialog.SavedSeqNo);
            }
        }

        private async void BtnEdit_Click(object sender, EventArgs e)
        {
            if (_isLoading) return;
            int rowIndex = commonCodeGrid.SelectedRowIndex;
            int index = (commonCodeGrid.PageIndex - 1) * commonCodeGrid.PageSize + rowIndex;
            if (_commonCodes == null || rowIndex < 0 || index < 0 || index >= _commonCodes.Rows.Count)
            {
                MessageBox.Show(this, "수정할 항목의 행을 선택해주세요.",
                    "공통코드", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            long seqNo = Convert.ToInt64(_commonCodes.Rows[index]["SeqNo"]);
            using (DialogCommonCode dialog = new DialogCommonCode(seqNo))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    await LoadCommonCodesAsync(dialog.SavedSeqNo);
            }
        }

        private async void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_isLoading) return;
            if (_checkedSeqNos.Count == 0)
            {
                MessageBox.Show(this, "삭제할 항목의 선택 체크박스를 체크해주세요.",
                    "공통코드", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            long[] seqNos = new long[_checkedSeqNos.Count];
            _checkedSeqNos.CopyTo(seqNos);
            if (MessageBox.Show(this, string.Format("선택한 공통코드 {0}개를 삭제하시겠습니까?", seqNos.Length),
                "공통코드 삭제", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;

            bool deleted = false;
            SetLoading(true);
            try
            {
                await Task.Run(() => CommonCodeRepository.DeleteItems(seqNos));
                deleted = true;
                _checkedSeqNos.Clear();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show(this, "공통코드를 삭제하지 못했습니다.\r\n" + ex.Message,
                        "공통코드 삭제", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed) SetLoading(false);
            }
            if (deleted && !IsDisposed)
                await LoadCommonCodesAsync();
        }

        private void CommonCodeGrid_CellClicked(object sender, PagedGridCellClickEventArgs e)
        {
            commonCodeGrid.SelectedRowIndex = e.RowIndex;

            if (e.ColumnKey != "Selected")
            {
                return;
            }

            long seqNo = Convert.ToInt64(e.Row["SeqNo"].Text);
            if (e.Row["Selected"].Checked)
            {
                _checkedSeqNos.Add(seqNo);
            }
            else
            {
                _checkedSeqNos.Remove(seqNo);
            }
        }

        private void BindCommonCodePage()
        {
            List<Dictionary<string, PagedGridCell>> rows = new List<Dictionary<string, PagedGridCell>>();
            if (_commonCodes != null)
            {
                int startIndex = (commonCodeGrid.PageIndex - 1) * commonCodeGrid.PageSize;
                int endIndex = Math.Min(startIndex + commonCodeGrid.PageSize, _commonCodes.Rows.Count);
                for (int index = startIndex; index < endIndex; index++)
                {
                    long seqNo = Convert.ToInt64(_commonCodes.Rows[index]["SeqNo"]);
                    Dictionary<string, PagedGridCell> row = new Dictionary<string, PagedGridCell>
                    {
                        { "SeqNo", new PagedGridCell(seqNo.ToString()) },
                        { "Selected", new PagedGridCell(string.Empty)
                            {
                                CellType = PagedGridCellType.CheckBox,
                                Checked = _checkedSeqNos.Contains(seqNo)
                            }
                        }
                    };
                    foreach (PagedGridColumn column in commonCodeGrid.Columns)
                    {
                        if (column.Key == "Selected")
                        {
                            continue;
                        }

                        object value = _commonCodes.Rows[index][column.Key];
                        string text = value is DateTime
                            ? ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss")
                            : Convert.ToString(value);
                        row.Add(column.Key, new PagedGridCell(text));
                    }
                    rows.Add(row);
                }
            }
            commonCodeGrid.SetRows(rows);
        }
    }
}
