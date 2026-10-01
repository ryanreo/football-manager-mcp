param([string]$GamePath = 'C:\Games\Football Manager 26')
$ErrorActionPreference = 'Stop'
[System.Reflection.Assembly]::LoadFrom((Join-Path $GamePath 'BepInEx/core/Mono.Cecil.dll')) | Out-Null
$taskTargets = @{
    'UnityEngine.UIElementsModule' = '^(UnityEngine.UIElements.(UIDocument|VisualElement|TextElement|TextField|Button|ClickEvent|PointerDownEvent|PointerUpEvent|MouseDownEvent|MouseUpEvent|EventBase|IPanel|RuntimePanelUtils|PanelSettings))$'
    'FM.UI' = 'FMNavigationManager|GameStateCache|SimulateButtonClickEvent|FMActionButton'
    'SI.UI' = 'PanelManager|Document|ScreenManager'
    'BepInEx.Unity.IL2CPP' = 'BasePlugin'
}
foreach ($taskEntry in $taskTargets.GetEnumerator()) {
    $taskSubdir = if ($taskEntry.Key -like 'BepInEx*') { 'core' } else { 'interop' }
    $taskPath = Join-Path $GamePath ('BepInEx/' + $taskSubdir + '/' + $taskEntry.Key + '.dll')
    if (-not (Test-Path -LiteralPath $taskPath)) { continue }
    $taskAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($taskPath)
    try {
        foreach ($taskType in $taskAssembly.MainModule.Types | Where-Object { $_.FullName -match $taskEntry.Value }) {
            Write-Output ('TYPE ' + $taskType.FullName + ' : ' + $taskType.BaseType)
            $taskType.Properties | ForEach-Object { Write-Output (' PROP ' + $_.PropertyType.FullName + ' ' + $_.Name) }
            $taskType.Methods | Where-Object { $_.Name -notlike '*Native*' -and $_.Name -notlike 'get_*' -and $_.Name -notlike 'set_*' -and $_.Name -notlike '.cctor' } | ForEach-Object { Write-Output (' METHOD ' + $_.FullName) }
        }
    } finally { $taskAssembly.Dispose() }
}
