param([switch]$Database)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../bin/Debug/PineTree.exe')))
$flags = [Reflection.BindingFlags]'Instance, NonPublic'
$script:checks = 0
function Check($condition, $message) {
    if (!$condition) { throw $message }
    $script:checks++
}
function Field($target, $name) { return ,$target.GetType().GetField($name, $flags).GetValue($target) }
function SetField($target, $name, $value) { $target.GetType().GetField($name, $flags).SetValue($target, $value) }
function Call($target, $name) { $target.GetType().GetMethod($name, $flags).Invoke($target, @()) }
function Rule($name, $action, $json) {
    $rule = [Activator]::CreateInstance($assembly.GetType('PineTree.Classes.ProcessRuleData'))
    $rule.RuleID = 123
    $rule.ProcessName = $name
    $rule.ActionCode = $action
    $rule.ParamsJSON = $json
    return $rule
}
$punch = Rule '타공' 'ADD_PUNCH_MARK' '{"punchMode":"FOUR_SIDES","punchCount":4,"diameterMm":35,"unit":"mm"}'
Check ($punch.BuildSummary() -eq "타공 · 타공마크 생성`r`n사방타공 · 개수 4개 · 직경 35mm") 'Punch screenshot summary'
$wood = Rule '각목' 'ADD_WOOD_BAR_LR' '{"leftMm":100,"rightMm":100,"unit":"mm"}'
Check ($wood.BuildSummary() -eq "각목 · 좌우각목`r`n좌 100mm / 우 100mm") 'Wood screenshot summary'
$spacing = Rule '간격타공' 'ADD_PUNCH_MARK' '{"punchMode":"CUSTOM_SPACING","punchCount":6,"diameterMm":5.25,"spacingMm":300,"unit":"mm"}'
Check ($spacing.BuildSummary().Contains('간격 300mm')) 'Custom spacing retained'
foreach ($action in @('ADD_MARGIN','ADD_CUT_LINE','ADD_GUIDE_LINE','ADD_WOOD_BAR_TB','ADD_POLE_HEM_TB','ADD_POLE_HEM_LR')) {
    $rule = Rule '테스트' $action '{"topMm":3.125,"bottomMm":0,"leftMm":2.5,"rightMm":10,"unit":"mm"}'
    $summary = $rule.BuildSummary()
    Check (!$summary.Contains('미입력')) "$action valid parameters"
    if ($action.EndsWith('_TB')) { Check (!$summary.Contains('좌 ')) 'Vertical-only margins' }
    if ($action.EndsWith('_LR')) { Check (!$summary.Contains('상 ')) 'Horizontal-only margins' }
}
$legacy = Rule '각목' 'ADD_WOOD_BAR_LR' ''
Check ($legacy.BuildSummary().Contains('좌 미입력 / 우 미입력')) 'Missing legacy values are explicit'
$bad = Rule '깨진 규칙' 'ADD_PUNCH_MARK' '{"diameterMm":"invalid"}'
$rejected = $false
try { $null = $bad.BuildSummary() } catch { $rejected = $true }
Check $rejected 'Invalid JSON values rejected'
$picker = [Activator]::CreateInstance($assembly.GetType('PineTree.Dialogs.DialogProcessRuleList'))
try {
    $grid = Field $picker 'FGrid1'
    Check ((Field $picker 'c1TextBox1').ReadOnly) 'Summary is read-only'
    Check (!(Field $picker 'btnOK').Enabled) 'Empty picker cannot confirm'
    # Supply an in-memory preview to exercise the returned ID, text and step copies without DB access.
    SetField $picker '_binding' $true
    $table = New-Object Data.DataTable
    $null = $table.Columns.Add('RuleID', [long])
    $null = $table.Rows.Add([long]123)
    $grid.DataSource = $table
    $grid.Row = $grid.Rows.Fixed
    $steps = [Activator]::CreateInstance([System.Collections.Generic.List``1].MakeGenericType($punch.GetType()))
    $steps.Add($punch)
    $steps.Add($wood)
    SetField $picker '_previewRuleID' ([long]123)
    SetField $picker '_previewSteps' $steps
    (Field $picker 'c1TextBox1').Text = $punch.BuildSummary() + "`r`n`r`n" + $wood.BuildSummary()
    SetField $picker '_binding' $false
    Call $picker 'SelectCurrentRule'
    Check ($picker.DialogResult -eq [Windows.Forms.DialogResult]::OK) 'Confirm result'
    Check ($picker.SelectedRuleID -eq 123) 'Returned RuleID'
    Check ($picker.SelectedSummary.Contains('직경 35mm') -and $picker.SelectedSummary.Contains('좌 100mm')) 'Multi-step returned summary'
    Check ($picker.SelectedSteps.Count -eq 2) 'All steps returned'
    $picker.SelectedSteps[0].ProcessName = 'changed copy'
    Check ($punch.ProcessName -eq '타공') 'Returned steps do not mutate preview'
    Call $picker 'ClearPreview'
    Check (!(Field $picker 'btnOK').Enabled -and (Field $picker 'c1TextBox1').Text -eq '') 'Clear stale preview'
    if ($Database) {
        $repository = $assembly.GetType('PineTree.Classes.ProcessRuleRepository')
        $probe = $repository.GetMethod('GetRuleList').Invoke($null, @('', ''))
        Call $picker 'LoadRules'
        Check ($grid.Cols.Contains('ProcessName') -and !$grid.Cols['RuleID'].Visible) 'Live grid schema'
        $repository = $assembly.GetType('PineTree.Classes.ProcessRuleRepository')
        $categories = $repository.GetMethod('GetRuleCategories').Invoke($null, @())
        foreach ($category in $categories.Rows) {
            $rows = $repository.GetMethod('GetRuleList').Invoke($null, @([string]$category.Category, ''))
            foreach ($row in $rows.Rows) { Check ($row.Category -eq $category.Category) 'Category filter' }
        }
        $rows = $repository.GetMethod('GetRuleList').Invoke($null, @('', "' AND 1=0 --"))
        Check ($rows.Rows.Count -eq 0) 'Search is parameterized'
        if ($grid.Rows.Count -gt $grid.Rows.Fixed) {
            $grid.Row = $grid.Rows.Fixed
            $id = $grid[$grid.Row, 'RuleID']
            $liveSteps = $repository.GetMethod('GetRuleSteps').Invoke($null, @([long]$id))
            Check (@($liveSteps | Where-Object RuleID -ne $id).Count -eq 0) 'Details match selected master'
            Check ((Field $picker 'c1TextBox1').Text.Length -gt 0) 'Live details displayed'
        }
    }
} finally { $picker.Dispose() }
Write-Output "PASS: $script:checks checks (Database=$Database)"
