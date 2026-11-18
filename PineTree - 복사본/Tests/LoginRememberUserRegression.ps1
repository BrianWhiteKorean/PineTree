$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../PineTreeLauncher/bin/Debug/PineTree.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$dialogType = $assembly.GetType('PineTree.Dialogs.LoginDlg', $true)
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
$registryPath = 'Software\PineTree\Tests\LoginRemember-' + [Guid]::NewGuid().ToString('N')
function NewLogin {
    $dialog = [Activator]::CreateInstance($dialogType, [object[]]@($null))
    $dialogType.GetField('RegKeyPath', $flags).SetValue($dialog, $registryPath)
    return $dialog
}
function LoadLogin($dialog) {
    [void]$dialogType.GetMethod('LoginDlg_Load', $flags).Invoke($dialog, @($null, [EventArgs]::Empty))
}
function CheckUser($dialog, $expected) {
    $actual = $dialogType.GetField('tbUserID', $flags).GetValue($dialog).Text
    if ($actual -ne $expected) { throw "Expected '$expected', got '$actual'." }
    if ($dialogType.GetField('tbPassword', $flags).GetValue($dialog).Text -ne '') { throw 'Password must not be restored.' }
}
try {
    $dialog = NewLogin
    try {
        LoadLogin $dialog
        CheckUser $dialog ''
        Write-Output 'PASS: First launch without saved ID'
        [void]$dialogType.GetMethod('SaveUserIDToRegistry', $flags).Invoke($dialog, @('first-user'))
    } finally { $dialog.Dispose() }
    $dialog = NewLogin
    try {
        LoadLogin $dialog
        CheckUser $dialog 'first-user'
        Write-Output 'PASS: Successful ID restored on next dialog'
        [void]$dialogType.GetMethod('SaveUserIDToRegistry', $flags).Invoke($dialog, @('last-user'))
    } finally { $dialog.Dispose() }
    $dialog = NewLogin
    try {
        LoadLogin $dialog
        CheckUser $dialog 'last-user'
        Write-Output 'PASS: Most recent ID replaces previous ID; password stays empty'
    } finally { $dialog.Dispose() }
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($registryPath, $true)
    try { $key.SetValue('LastLoginUser', 123, [Microsoft.Win32.RegistryValueKind]::DWord) } finally { $key.Dispose() }
    $dialog = NewLogin
    try {
        LoadLogin $dialog
        CheckUser $dialog ''
        Write-Output 'PASS: Invalid saved value does not interrupt login'
    } finally { $dialog.Dispose() }
} finally {
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($registryPath, $false)
}
