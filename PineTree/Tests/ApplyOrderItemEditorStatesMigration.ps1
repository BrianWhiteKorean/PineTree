param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\dll\MySql.Data.dll')))
$assembly = [Reflection.Assembly]::LoadFrom($AssemblyPath)
$connection = $assembly.GetType('PineTree.Classes.DB').GetMethod('CreateConnection').Invoke($null, @())
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandTimeout = 30
    $command.CommandText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'OrderItemEditorStates.migration.sql'))
    [void]$command.ExecuteNonQuery()
    $command.CommandText = 'SHOW CREATE TABLE OrderItemEditorStates'
    $reader = $command.ExecuteReader()
    while ($reader.Read()) { Write-Output $reader.GetString(1) }
    $reader.Dispose()
    $command.Dispose()
} finally { $connection.Dispose() }
