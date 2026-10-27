param([string]$PreviewPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../bin/Debug/PineTree.exe'))
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
function Field($instance, $name) { return ,($instance.GetType().GetField($name, $flags).GetValue($instance)) }
function InvokePrivate($instance, $name, [object[]]$arguments = @()) {
    $instance.GetType().GetMethod($name, $flags).Invoke($instance, $arguments)
}
function AssertEqual($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message : expected=$expected actual=$actual" }
}
# Bypass the list constructor so this regression check never connects to the database.
$pickerType = $assembly.GetType('PineTree.Dialogs.DialogPriceItemList', $true)
$picker = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($pickerType)
[void][Windows.Forms.Form].GetConstructor([Type]::EmptyTypes).Invoke($picker, @())
InvokePrivate $picker 'InitializeComponent'
$order = [Activator]::CreateInstance($assembly.GetType('PineTree.Dialogs.DialogOrderItem', $true))
try {
    $table = New-Object System.Data.DataTable
    foreach ($column in @('SeqNo', 'PriceItemPriceId', 'ItemCode', 'PriceItemName', 'SpecText', 'BillingBasis', 'UnitName', 'PriceName', 'CurrentUnitPrice', 'CustCode', 'PriceCategory', 'CategoryName', 'ApplyFrom')) {
        [void]$table.Columns.Add($column)
    }
    [void]$table.Rows.Add(@('101', '202', 'ITEM01', '테스트 품목', '기본규격', '면적', '회배', '기본단가', '1000', '', '출력', '현수막', '2026-10-19'))
    $grid = Field $picker 'FGrid1'
    $grid.DataSource = $table
    InvokePrivate $picker 'InitGrid'
    $grid.Row = $grid.Rows.Fixed
    InvokePrivate $picker 'ConfirmSelectedPriceItem'
    AssertEqual $picker.DialogResult ([Windows.Forms.DialogResult]::OK) 'Picker result'
    AssertEqual $picker.SelectedPriceItemSeqNo 101 'Selected item ID'
    AssertEqual $picker.SelectedPriceItemPriceId 202 'Selected price ID'
    AssertEqual $picker.SelectedPriceCategory '출력' 'Dedicated item category'
    InvokePrivate $order 'ApplySelectedPriceItem' @($picker)
    (Field $order 'tbWidth').Value = [decimal]1000
    (Field $order 'tbHeight').Value = [decimal]2000
    (Field $order 'tbQty').Value = [decimal]3
    AssertEqual (Field $order 'tbArea').Value 6 'Area calculation'
    AssertEqual (Field $order 'tbNetPrice').Value 6000 'Supply amount'
    AssertEqual (Field $order 'tbVat').Value 600 'VAT'
    AssertEqual (Field $order 'tbSum').Value 6600 'Total'
    (Field $order 'tbItemName').Text = '입력한 품목명'
    (Field $order 'tbCustomName').Text = '수정한 주문명'
    InvokePrivate $order 'ConfirmSelectedOrderItem'
    AssertEqual $order.SelectedOrderItem.CustomName '수정한 주문명' 'Edited order name'
    AssertEqual $order.SelectedOrderItem.PriceItemName '입력한 품목명' 'Original product name'
    AssertEqual $order.SelectedOrderItem.AreaQty 6 'Returned area'
    AssertEqual $order.SelectedOrderItem.PriceItemSeqNo 101 'Returned item ID'
    AssertEqual $order.DialogResult ([Windows.Forms.DialogResult]::OK) 'Order confirmation'
    # Check the entire in-memory save/load mapping without database access.
    $parentType = $assembly.GetType('PineTree.Dialogs.DialogOrder', $true)
    $parent = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($parentType)
    [void][Windows.Forms.Form].GetConstructor([Type]::EmptyTypes).Invoke($parent, @())
    InvokePrivate $parent 'InitializeComponent'
    try {
        InvokePrivate $parent 'InitOrderItemTable'
        $order.SelectedOrderItem.Remark = '테스트 적요'
        InvokePrivate $parent 'AddOrderItem' @($order.SelectedOrderItem, $null)
        $rows = Field $parent 'm_orderItems'
        AssertEqual $rows.Rows[0]['ItemName'] '입력한 품목명' 'Grid product name'
        AssertEqual $rows.Rows[0]['CustomName'] '수정한 주문명' 'Grid custom name'
        $saved = InvokePrivate $parent 'CreateOrderItemSaveDataList'
        AssertEqual $saved[0].ItemName '입력한 품목명' 'Saved product name'
        AssertEqual $saved[0].CustomName '수정한 주문명' 'Saved custom name'
        $loaded = InvokePrivate $parent 'CreateDialogOrderItem' @($rows.Rows[0])
        AssertEqual $loaded.CustomName '수정한 주문명' 'Reload custom name'
        AssertEqual $loaded.PriceItemName '입력한 품목명' 'Reload product name'
        $repositoryType = $assembly.GetType('PineTree.Classes.OrderRepository', $true)
        $parameters = $repositoryType.GetMethod('CreateOrderItemParameters', [Reflection.BindingFlags]'Static, NonPublic').Invoke($null, @([long]1, [long]2, $saved[0]))
        AssertEqual ($parameters | Where-Object ParameterName -eq '@CustomName').Value '수정한 주문명' 'CustomName SQL parameter'
        AssertEqual ($parameters | Where-Object ParameterName -eq '@ItemName').Value '입력한 품목명' 'ItemName SQL parameter'
        $editor = [Activator]::CreateInstance($assembly.GetType('PineTree.Dialogs.DialogOrderInfoEdit', $true))
        try {
            InvokePrivate $editor 'ApplyOrderItemToInputs' @($loaded)
            AssertEqual (Field $editor 'tbItemName').Text '입력한 품목명' 'Edit product name'
            AssertEqual (Field $editor 'tbCustomName').Text '수정한 주문명' 'Edit custom name'
            (Field $editor 'tbItemName').Text = '수정창에서 바꾼 품목명'
            (Field $editor 'tbCustomName').Text = '수정창에서 바꾼 주문명'
            $edited = InvokePrivate $editor 'CreateOrderItem'
            AssertEqual $edited.CustomName '수정창에서 바꾼 주문명' 'Edited custom name'
            AssertEqual $edited.PriceItemName '수정창에서 바꾼 품목명' 'Edited product name'
            $updateData = InvokePrivate $editor 'CreateOrderItemSaveData' @($edited)
            $updateParameters = $repositoryType.GetMethod('CreateOrderItemParameters', [Reflection.BindingFlags]'Static, NonPublic').Invoke($null, @([long]1, [long]2, $updateData))
            AssertEqual ($updateParameters | Where-Object ParameterName -eq '@ItemName').Value '수정창에서 바꾼 품목명' 'Update ItemName SQL parameter'
            AssertEqual ($updateParameters | Where-Object ParameterName -eq '@CustomName').Value '수정창에서 바꾼 주문명' 'Update CustomName SQL parameter'
            InvokePrivate $parent 'UpdateOrderItemRow' @($rows.Rows[0], $edited)
            $updated = InvokePrivate $parent 'CreateOrderItemSaveDataList'
            AssertEqual $updated[0].ItemName '수정창에서 바꾼 품목명' 'Parent save after editing product'
            AssertEqual $updated[0].CustomName '수정창에서 바꾼 주문명' 'Parent save after editing custom name'
        } finally { $editor.Dispose() }
        $parentGrid = Field $parent 'FGrid1'
        $totalIndex = $parentGrid.Rows.Count - 1
        AssertEqual $parentGrid.Rows[$totalIndex].IsNode $true 'Subtotal is a separate grid row'
        $visibleColumns = @($parentGrid.Cols | Where-Object Visible | ForEach-Object Name)
        AssertEqual ($visibleColumns -join ',') 'LineNo,CustomName,Remark,SpecText,UnitName,Qty,AreaQty,UnitPrice,SupplyAmount,VatAmount,TotalAmount,ItemName' 'Configured display column order'
        AssertEqual $parentGrid[$parentGrid.Rows.Fixed, 'Remark'] '테스트 적요' 'Remark stays in its named column after reorder'
        AssertEqual $parentGrid[$parentGrid.Rows.Fixed, 'ItemName'] '수정창에서 바꾼 품목명' 'ItemName stays in its named column after reorder'
        AssertEqual $saved[0].ItemDescription '테스트 적요' 'Remark SQL save mapping after reorder'
        $rows.Columns['Remark'].SetOrdinal(0)
        $rows.Columns['ItemName'].SetOrdinal($rows.Columns.Count - 1)
        $reorderedItem = InvokePrivate $parent 'CreateDialogOrderItem' @($rows.Rows[0])
        AssertEqual $reorderedItem.PriceItemName '수정창에서 바꾼 품목명' 'Load ItemName by name regardless of source column order'
        AssertEqual $reorderedItem.Remark '테스트 적요' 'Load Remark by name regardless of source column order'
        InvokePrivate $parent 'InitOrderItemGrid'
        InvokePrivate $parent 'UpdateOrderItemTotals'
        $totalIndex = $parentGrid.Rows.Count - 1
        AssertEqual $parentGrid[$totalIndex, 'CustomName'] '금액 합계' 'Subtotal caption in leading name column'
        AssertEqual $parentGrid[$totalIndex, 'SupplyAmount'] 6000 'One item supply total'
        AssertEqual $parentGrid[$totalIndex, 'VatAmount'] 600 'One item VAT total'
        AssertEqual $parentGrid[$totalIndex, 'TotalAmount'] 6600 'One item grand total'
        $secondItem = [Activator]::CreateInstance($assembly.GetType('PineTree.Dialogs.DialogPriceItemOrderItem', $true))
        $secondItem.PriceItemName = '두 번째 품목'
        $secondItem.CustomName = '두 번째 주문'
        $secondItem.SupplyAmount = 10000
        $secondItem.VatAmount = 1000
        $secondItem.TotalAmount = 11000
        InvokePrivate $parent 'AddOrderItem' @($secondItem, $null)
        $totalIndex = $parentGrid.Rows.Count - 1
        AssertEqual $parentGrid[$totalIndex, 'SupplyAmount'] 16000 'Two item supply total'
        AssertEqual $parentGrid[$totalIndex, 'VatAmount'] 1600 'Two item VAT total'
        AssertEqual $parentGrid[$totalIndex, 'TotalAmount'] 17600 'Two item grand total'
        AssertEqual $rows.Rows.Count 2 'Subtotal excluded from source data'
        $parentGrid.Row = $totalIndex
        InvokePrivate $parent 'btnRemove_Click' @($null, [EventArgs]::Empty)
        AssertEqual $rows.Rows.Count 2 'Subtotal cannot delete an item'
        $parentGrid.Row = $parentGrid.Rows.Fixed + 1
        InvokePrivate $parent 'btnRemove_Click' @($null, [EventArgs]::Empty)
        $totalIndex = $parentGrid.Rows.Count - 1
        AssertEqual $parentGrid[$totalIndex, 'TotalAmount'] 6600 'Total after deleting an item'
        $rows.Rows[0]['SupplyAmount'] = 2000
        $rows.Rows[0]['VatAmount'] = 200
        $rows.Rows[0]['TotalAmount'] = 2200
        InvokePrivate $parent 'UpdateOrderItemTotals'
        $totalIndex = $parentGrid.Rows.Count - 1
        AssertEqual $parentGrid[$totalIndex, 'TotalAmount'] 2200 'Total after editing an item'
        AssertEqual $parentGrid.Rows.Count ($rows.Rows.Count + $parentGrid.Rows.Fixed + 1) 'No duplicate subtotal rows'
        if ($PreviewPath) {
            $parent.ShowInTaskbar = $false
            $parent.StartPosition = [Windows.Forms.FormStartPosition]::Manual
            $parent.Location = New-Object Drawing.Point(-32000, -32000)
            $parent.Show()
            [Windows.Forms.Application]::DoEvents()
            $parentBitmap = New-Object Drawing.Bitmap($parent.Width, $parent.Height)
            try {
                $parent.DrawToBitmap($parentBitmap, (New-Object Drawing.Rectangle(0, 0, $parent.Width, $parent.Height)))
                $parentBitmap.Save(($PreviewPath + '.parent.png'), [Drawing.Imaging.ImageFormat]::Png)
            } finally { $parentBitmap.Dispose() }
        }
    } finally { $parent.Dispose() }
    # Selecting an ordinary unit must clear the previous area and calculate by quantity.
    $table.Rows[0]['UnitName'] = '장'
    $table.Rows[0]['PriceItemPriceId'] = [DBNull]::Value
    InvokePrivate $picker 'ConfirmSelectedPriceItem'
    InvokePrivate $order 'ApplySelectedPriceItem' @($picker)
    AssertEqual (Field $order 'tbCustomName').Text '수정한 주문명' 'Preserve custom name when selecting another product'
    AssertEqual (Field $order 'tbItemName').Text '테스트 품목' 'Display selected product name'
    AssertEqual (Field $order 'tbArea').Value 0 'Clear previous area'
    AssertEqual (Field $order 'tbNetPrice').Value 3000 'Quantity based amount'
    AssertEqual $order.SelectedPriceItemPriceId $null 'Nullable price ID'
    AssertEqual ($order.GetType().GetField('FGrid1', $flags)) $null 'Removed order grid'
    AssertEqual ($picker.GetType().GetField('c1DockingTab1', $flags)) $null 'Removed list tabs'
    if ($PreviewPath) {
        $order.DialogResult = [Windows.Forms.DialogResult]::None
        $order.ShowInTaskbar = $false
        $order.StartPosition = [Windows.Forms.FormStartPosition]::Manual
        $order.Location = New-Object Drawing.Point(-32000, -32000)
        $order.Show()
        [Windows.Forms.Application]::DoEvents()
        $order.PerformLayout()
        $bitmap = New-Object Drawing.Bitmap($order.Width, $order.Height)
        try {
            $order.DrawToBitmap($bitmap, (New-Object Drawing.Rectangle(0, 0, $order.Width, $order.Height)))
            $bitmap.Save($PreviewPath, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $bitmap.Dispose() }
    }
    # Exercise the exclusive picker transfer and existing-customer save path without database access.
    $exclusiveType = $assembly.GetType('PineTree.Dialogs.DialogPriceItemListExclusive', $true)
    $exclusive = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($exclusiveType)
    [void][Windows.Forms.Form].GetConstructor([Type]::EmptyTypes).Invoke($exclusive, @())
    InvokePrivate $exclusive 'InitializeComponent'
    $customerOrder = [Activator]::CreateInstance($order.GetType(), [object[]]@('', 'CUST01', '전용 거래처'))
    try {
        AssertEqual (Field $order 'btnSelectProductExclusive').Enabled $false 'Disable exclusive picker without customer'
        AssertEqual (Field $customerOrder 'btnSelectProductExclusive').Enabled $true 'Enable exclusive picker for customer'
        $exclusiveTable = $table.Copy()
        $exclusiveTable.Rows[0]['SeqNo'] = '303'
        $exclusiveTable.Rows[0]['CustCode'] = 'CUST01'
        $exclusiveGrid = Field $exclusive 'FGrid1'
        $exclusiveGrid.DataSource = $exclusiveTable
        InvokePrivate $exclusive 'InitGrid'
        $exclusiveGrid.Row = $exclusiveGrid.Rows.Fixed
        InvokePrivate $exclusive 'ConfirmSelectedPriceItem'
        InvokePrivate $customerOrder 'ApplySelectedExclusivePriceItem' @($exclusive)
        AssertEqual $customerOrder.SelectedPriceItemSeqNo 303 'Exclusive product ID transfer'
        AssertEqual (Field $customerOrder '_selectedCustCode') 'CUST01' 'Exclusive customer transfer'
        AssertEqual (Field $customerOrder '_selectedPriceCategory') '출력' 'Exclusive category transfer'
        AssertEqual (Field $customerOrder '_selectedCategoryName') '현수막' 'Exclusive subcategory transfer'
        AssertEqual (Field $customerOrder 'tbItemName').Text '테스트 품목' 'Exclusive product display'
        AssertEqual (Field $customerOrder 'tbPrice').Value 1000 'Exclusive unit price transfer'
        (Field $customerOrder 'chkCustItem').Checked = $true
        InvokePrivate $customerOrder 'ConfirmSelectedOrderItem'
        AssertEqual $customerOrder.SelectedOrderItem.PriceItemSeqNo 303 'Reuse existing exclusive product without duplicate insert'
        AssertEqual $customerOrder.DialogResult ([Windows.Forms.DialogResult]::OK) 'Exclusive order confirmation'
        # Missing customer must clear results before any repository query.
        InvokePrivate $exclusive 'LoadPriceItems'
        AssertEqual $exclusiveGrid.DataSource $null 'No exclusive results without customer'
    } finally {
        $customerOrder.Dispose()
        $exclusive.Dispose()
    }
    Write-Output 'PASS: picker data transfer, area/quantity calculations, VAT, order result, CustomName save/load/SQL mapping and removed controls.'
} finally {
    $order.Dispose()
    $picker.Dispose()
}