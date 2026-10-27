$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../PineTreeLauncher/bin/Debug/PineTree.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
$mainType = $assembly.GetType('PineTree.Forms.FormMain', $true)
foreach ($mode in @('Cancel', 'Escape', 'Failure', 'Success')) {
    $main = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($mainType)
    [void][Windows.Forms.Form].GetConstructor([Type]::EmptyTypes).Invoke($main, @())
    [void]$mainType.GetMethod('InitializeComponent', $flags).Invoke($main, @())
    $script:loginSeen = $false
    $script:successKeptMainOpen = $false
    $script:testError = $null
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $timer = New-Object Windows.Forms.Timer
    $timer.Interval = 100
    $timer.add_Tick({
        try {
            if ($watch.Elapsed.TotalSeconds -gt 10) { throw 'Startup login timed out.' }
            $login = @([Windows.Forms.Application]::OpenForms | Where-Object { $_.GetType().FullName -eq 'PineTree.Dialogs.LoginDlg' }) | Select-Object -First 1
            if ($null -ne $login -and !$script:loginSeen) {
                if (!$main.Visible -or $login.Owner -ne $main -or !$login.Modal) { throw 'Login must be modal over the visible main form.' }
                $script:loginSeen = $true
                if (!(Test-Path -LiteralPath (Join-Path ([AppDomain]::CurrentDomain.BaseDirectory) 'Config.ini'))) {
                    $configRead = $login.GetType().GetMethod('ReadConfig', $flags).Invoke($login, @())
                    if ($configRead) { throw 'Missing optional config unexpectedly loaded.' }
                }
                switch ($mode) {
                    'Cancel' { $login.GetType().GetField('btnCancel', $flags).GetValue($login).PerformClick() }
                    'Escape' {
                        $keyEvent = New-Object Windows.Forms.KeyEventArgs([Windows.Forms.Keys]::Escape)
                        [void]$login.GetType().GetMethod('LoginDlg_KeyDown', $flags).Invoke($login, @($null, $keyEvent.PSObject.BaseObject))
                    }
                    'Failure' { $login.DialogResult = [Windows.Forms.DialogResult]::Abort }
                    'Success' { $login.DialogResult = [Windows.Forms.DialogResult]::OK }
                }
            } elseif ($script:loginSeen -and $mode -eq 'Success' -and $null -eq $login) {
                $script:successKeptMainOpen = $main.Visible -and !$main.IsDisposed
                $timer.Stop()
                $main.Close()
            }
        } catch {
            $script:testError = $_
            $timer.Stop()
            $main.Close()
        }
    })
    try {
        $timer.Start()
        [Windows.Forms.Application]::Run($main)
        if ($script:testError) { throw $script:testError }
        if (!$script:loginSeen) { throw 'Login dialog was not shown.' }
        if (!$main.IsDisposed) { throw 'Main form did not close.' }
        if ($mode -eq 'Success' -and !$script:successKeptMainOpen) { throw 'Successful login did not keep the main form open.' }
        Write-Output "PASS: Main shown before modal login; $mode result handled"
    } finally {
        $timer.Dispose()
        $main.Dispose()
    }
}
