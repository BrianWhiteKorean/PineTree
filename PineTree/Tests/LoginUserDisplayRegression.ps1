$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../PineTreeLauncher/bin/Debug/PineTree.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
$globalType = $assembly.GetType('PineTree.Classes.Global', $true)
function SetUser($userID, $auth, $company) {
    $globalType.GetProperty('UserID').SetValue($null, $userID, $null)
    $globalType.GetProperty('UserType').SetValue($null, $auth, $null)
    $globalType.GetProperty('CompanyName').SetValue($null, $company, $null)
}
function CheckText($control, $fieldName, $expected) {
    $actual = $control.GetType().GetField($fieldName, $flags).GetValue($control).Text
    if ($actual -ne $expected) { throw "$fieldName expected='$expected' actual='$actual'" }
}
SetUser $null $null $null
$menu = [Activator]::CreateInstance($assembly.GetType('PineTree.UserControls.UcMenu', $true))
$topBar = [Activator]::CreateInstance($assembly.GetType('PineTree.UserControls.UcTopBar', $true))
try {
    CheckText $topBar '_userButton' ''
    CheckText $menu '_userAvatar' ''
    CheckText $menu '_userName' ''
    CheckText $menu '_userRole' ''
    Write-Output 'PASS: No administrator label before login'
    $cases = @(
        @{ ID='b'; Auth='관리자'; Company='소나무그래픽'; Badge='관'; Title='관리자'; Detail='소나무그래픽 · 전체권한' },
        @{ ID='wi'; Auth='사용자'; Company='소나무그래픽'; Badge='사'; Title='사용자'; Detail='소나무그래픽' },
        @{ ID='test'; Auth='사용자'; Company='베너톡'; Badge='사'; Title='사용자'; Detail='베너톡' },
        @{ ID='b'; Auth='관리자'; Company=$null; Badge='관'; Title='관리자'; Detail='전체권한' },
        @{ ID='test'; Auth=$null; Company=$null; Badge='사'; Title='사용자'; Detail='' }
    )
    foreach ($case in $cases) {
        SetUser $case.ID $case.Auth $case.Company
        $menu.RefreshUserInfo()
        $topBar.RefreshUserInfo()
        CheckText $topBar '_userButton' $case.Badge
        CheckText $menu '_userAvatar' $case.Badge
        CheckText $menu '_userName' $case.Title
        CheckText $menu '_userRole' $case.Detail
        Write-Output "PASS: User=$($case.ID), Auth=$($case.Auth), Company=$($case.Company)"
    }
} finally {
    $menu.Dispose()
    $topBar.Dispose()
    SetUser $null $null $null
}
