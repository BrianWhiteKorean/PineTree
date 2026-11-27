$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../bin/Debug/PineTree.exe')))
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../dll/MySql.Data.dll')))
$conn = $assembly.GetType('PineTree.Classes.DB').GetMethod('CreateConnection').Invoke($null,@())
try {
    $conn.Open()
    $command = $conn.CreateCommand()
    $command.CommandText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'OrderProcessRules.migration.sql'))
    $null = $command.ExecuteNonQuery()
    $command.CommandText = 'SHOW CREATE TABLE OrderProcessRules'
    $reader = $command.ExecuteReader()
    while ($reader.Read()) { Write-Output $reader.GetString(1) }
    $reader.Dispose(); $command.Dispose()
} finally { $conn.Dispose() }
