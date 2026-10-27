$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../bin/Debug/PineTree.exe')))
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../dll/MySql.Data.dll')))
$conn = $assembly.GetType('PineTree.Classes.DB').GetMethod('CreateConnection').Invoke($null,@())
try {
$conn.Open()
foreach ($table in @('OrderInfo','OrderItems','OrderFiles','ProcessRule','ProcessRuleStep')) {
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'SHOW CREATE TABLE ' + $table
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { Write-Output $reader.GetString(1) }
$reader.Dispose(); $cmd.Dispose()
}
$cmd = $conn.CreateCommand(); $cmd.CommandText = "SHOW TABLES LIKE '%Process%'"
$reader = $cmd.ExecuteReader(); while ($reader.Read()) { Write-Output $reader.GetString(0) }; $reader.Dispose(); $cmd.Dispose()
} finally { $conn.Dispose() }
