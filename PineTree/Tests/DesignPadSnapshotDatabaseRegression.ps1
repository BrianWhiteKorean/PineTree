param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dll\MySql.Data.dll')))
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $AssemblyPath).Path)
$connection = $assembly.GetType('PineTree.Classes.DB').GetMethod('CreateConnection').Invoke($null, @())
$taskOrderId = 0
try {
    $connection.Open()
    $transaction = $connection.BeginTransaction()
    try {
        $order = [Activator]::CreateInstance($assembly.GetType('PineTree.Classes.OrderSaveData'))
        $order.OrderDate = [DateTime]::Today
        $order.OrderTitle = 'Design pad snapshot rollback regression'
        $order.ProductionStatus = '신규'
        $order.SourceChannel = '직접입력'
        $order.CreatedBy = 'regression'
        $command = $connection.CreateCommand()
        $command.Transaction = $transaction
        $command.CommandText = 'SELECT CustCode FROM Customers LIMIT 1'
        $order.CustCode = [string]$command.ExecuteScalar()
        $itemType = $assembly.GetType('PineTree.Classes.OrderItemSaveData')
        $listType = [Collections.Generic.List``1].MakeGenericType($itemType)
        $order.Items = [Activator]::CreateInstance($listType)
        $state = [Activator]::CreateInstance($assembly.GetType('PineTree.Classes.OrderDesignPadState'))
        $state.CategoryName = '현수막'
        $state.ProductName = 'snapshot test product'
        $state.UnitPrice = 4000
        $state.Editor = [Activator]::CreateInstance($assembly.GetType('PineTree.Classes.BannerOrderEditorState'))
        $state.Editor.SourceFilePath = 'C:\snapshot-test\source.ai'
        $state.Editor.WidthMm = 1000
        $state.Editor.HeightMm = 500
        $state.Editor.Quantity = 2
        $order.DesignPadStateJson = $state.ToJson()
        $flags = [Reflection.BindingFlags]'Static,NonPublic'
        $taskOrderId = $assembly.GetType('PineTree.Classes.OrderRepository').GetMethod('SaveOrderCore',$flags).Invoke($null,@($connection,$transaction,$order))
        $command.CommandText = 'SELECT StateJSON FROM OrderDesignPadStates WHERE OrderId=@OrderId'
        [void]$command.Parameters.AddWithValue('@OrderId',$taskOrderId)
        if ([string]$command.ExecuteScalar() -ne $order.DesignPadStateJson) { throw 'Snapshot persistence mismatch' }
        $command.CommandText = 'SELECT COUNT(*) FROM OrderItems WHERE OrderId=@OrderId'
        if ([int]$command.ExecuteScalar() -ne 0) { throw 'Work snapshot unexpectedly created accessory rows' }
        Write-Output 'PASS: transaction stores work snapshot independently of accessory rows'
        $order.OrderId = $taskOrderId
        $order.DesignPadStateJson = $null
        [void]$assembly.GetType('PineTree.Classes.OrderRepository').GetMethod('SaveOrderCore',$flags).Invoke($null,@($connection,$transaction,$order))
        $command.CommandText = 'SELECT StateJSON FROM OrderDesignPadStates WHERE OrderId=@OrderId'
        if ([string]$command.ExecuteScalar() -ne $state.ToJson()) { throw 'Legacy editor overwrote snapshot' }
        Write-Output 'PASS: legacy order edits preserve saved work snapshots'
        $command.Dispose()
    } finally { $transaction.Rollback(); $transaction.Dispose() }
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT COUNT(*) FROM OrderInfo WHERE OrderId=@OrderId'
    [void]$command.Parameters.AddWithValue('@OrderId',$taskOrderId)
    if ([int]$command.ExecuteScalar() -ne 0) { throw 'Rollback cleanup failed' }
    Write-Output 'PASS: test order rolled back; existing orders unchanged'
    $command.Dispose()
} finally { $connection.Dispose() }
