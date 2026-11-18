$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$assemblyPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../PineTreeLauncher/bin/Debug/PineTree.dll'))
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
function Call($target, $name, $arguments = @()) {
    $target.GetType().GetMethod($name, $flags).Invoke($target, $arguments)
}
function Field($target, $name) { $target.GetType().GetField($name, $flags).GetValue($target) }
function SetField($target, $name, $value) { $target.GetType().GetField($name, $flags).SetValue($target, $value) }
function Check($condition, $message) { if (!$condition) { throw $message } }
function Bare($name, $baseType) {
    $target = [Runtime.Serialization.FormatterServices]::GetUninitializedObject($assembly.GetType($name, $true))
    [void]$baseType.GetConstructor([Type]::EmptyTypes).Invoke($target, @())
    return $target
}
function NewEditor {
    $editor = Bare 'PineTree.UserControls.UcOrderDesignPad' ([Windows.Forms.UserControl])
    [void](Call $editor 'InitializeComponent')
    [void](Call $editor 'InitOrderItemTable')
    [void](Call $editor 'InitOrderEditor')
    [void](Call $editor 'InitPreviewEvent')
    $editor.ResetNewOrder()
    return $editor
}
# Initialize UI without querying or writing the production database.
$main = Bare 'PineTree.Forms.FormMain' ([Windows.Forms.Form])
[void](Call $main 'InitializeComponent')
$management = Bare 'PineTree.UserControls.UcContainer' ([Windows.Forms.UserControl])
$management.Dock = 'Fill'
$editor = NewEditor
$designType = $assembly.GetType('PineTree.UserControls.UcDesignModeContainer', $true)
$constructor = $designType.GetConstructor($flags, $null, @($editor.GetType()), $null)
$design = $constructor.Invoke(@($editor))
$panel = Field $main 'panel1'
$panel.Controls.Add($management)
$panel.Controls.Add($design)
$design.Visible = $false
SetField $main '_managementContainer' $management
SetField $main '_designModeContainer' $design
SetField $main '_isManagementMode' $true
[void](Call $main 'InitializeModeControls')
$hostForm = New-Object Windows.Forms.Form
$hostForm.ClientSize = New-Object Drawing.Size(1600, 960)
$hostForm.StartPosition = 'Manual'
$hostForm.Location = New-Object Drawing.Point(-4000, -4000)
$hostForm.ShowInTaskbar = $false
$hostForm.Controls.Add($panel)
try {
    $hostForm.Show()
    [Windows.Forms.Application]::DoEvents()
    $button = Field $main '_modeButton'
    $commonPanel = Field $main '_modeSwitchPanel'
    Check ($button.Text -eq '관리모드' -and $management.Visible -and !$design.Visible) 'Must start in management mode'
    Check ($commonPanel.Parent -eq $panel) 'Toggle must belong to FormMain common area'
    $button.PerformClick()
    Check ($button.Text -eq '디자인모드' -and !$management.Visible -and $design.Visible) 'Must switch to design container'
    Check ($editor.Top -ge 60 -and $editor.Bottom -eq $design.ClientSize.Height) 'Editor must fill below common header'
    $remark = Field $editor 'tbRemark'
    $remark.Text = 'Keep this draft'
    $button.PerformClick()
    $button.PerformClick()
    Check ([object]::ReferenceEquals((Field $design '_orderDesigner'), $editor)) 'Designer instance must be reused'
    Check ($remark.Text -eq 'Keep this draft') 'Draft must survive mode switches'
    $hostForm.ClientSize = New-Object Drawing.Size(1400, 900)
    [Windows.Forms.Application]::DoEvents()
    Check ($commonPanel.Right -eq $panel.ClientSize.Width) 'Common toggle must remain right anchored'
    Check ($commonPanel.Visible -and $button.Visible) 'Common toggle must remain visible'
    Check ($panel.Controls.GetChildIndex($commonPanel) -eq 0) 'Common toggle must stay above both containers'
    $savedHandler = Field $editor 'OrderSaved'
    $savedHandler.DynamicInvoke(@($editor, [EventArgs]::Empty))
    Check ($remark.Text -eq '' -and (Field $editor 'm_orderItems').Rows.Count -eq 0) 'Saved order must prepare a clean draft'
    Check ($design.Visible -and $button.Text -eq '디자인모드') 'Save must keep design mode open'
    $output = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../tmp/design-mode-ui'))
    [void][IO.Directory]::CreateDirectory($output)
    $bitmap = New-Object Drawing.Bitmap($panel.Width, $panel.Height)
    try {
        $panel.DrawToBitmap($bitmap, $panel.ClientRectangle)
        $commonPanel.DrawToBitmap($bitmap, $commonPanel.Bounds)
        $bitmap.Save((Join-Path $output 'design-mode.png'))
    } finally { $bitmap.Dispose() }
    Write-Output 'PASS: management default, common toggle, exclusive containers, draft retention, resize, continuous entry'
    $modal = Bare 'PineTree.Dialogs.DialogOrder' ([Windows.Forms.Form])
    try {
        [void](Call $modal 'InitializeComponent')
        [void](Call $modal 'InitOrderItemTable')
        [void](Call $modal 'InitOrderEditor')
        [void](Call $modal 'InitPreviewEvent')
        Check ($null -ne (Field $modal 'FGrid1')) 'DialogOrder must own its order grid'
        Check ($modal.GetType().GetField('_designer', $flags) -eq $null) 'DialogOrder must not embed UcOrderDesigner'
        Check ($modal.StartPosition -eq 'CenterParent' -and $modal.KeyPreview) 'Original modal window settings must be restored'
        [void](Call $modal 'btnCancel_Click' @($null, [EventArgs]::Empty))
        Check ($modal.DialogResult -eq 'Cancel') 'Management modal cancellation must remain supported'
        Check (!$editor.IsDisposed) 'Closing the modal must not affect the design-mode editor'
        Write-Output 'PASS: independent DialogOrder layout, initialization, and modal cancellation'
    } finally { $modal.Dispose() }
} finally {
    $hostForm.Dispose()
    $main.Dispose()
}
