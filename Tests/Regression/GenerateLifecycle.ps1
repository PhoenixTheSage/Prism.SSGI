param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $root 'ClientPlugin/SSGI/SSGIPass.cs'))
$hook = [IO.File]::ReadAllText((Join-Path $root 'ClientPlugin/Anomaly/AnomalyHook.cs'))
function Extract-Block([string]$Text, [string]$Pattern) {
    $match = [regex]::Match($Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Multiline)
    if (-not $match.Success) { throw "Missing production block: $Pattern" }
    $body = $Text.IndexOf('{', $match.Index)
    $end = $body + 1
    $depth = 1
    while ($depth -gt 0) {
        if ($Text[$end] -eq '{') { $depth++ }
        elseif ($Text[$end] -eq '}') { $depth-- }
        $end++
    }
    $Text.Substring($match.Index, $end - $match.Index)
}
$methods = foreach ($name in 'Init','RegisterOwnedPasses','OnResolutionChanged','OnDeviceEnd','RecreateTargets','DisposeTargets','DisposeShaders','ReloadShaders','NoteSkip','TraceScale','PassSize') {
    Extract-Block $source "^    (?:public )?static (?:unsafe )?(?:void|float|Vector2I) $name\("
}
$struct = Extract-Block $source '^    struct DenoiserCb'
$clear = Extract-Block $hook '^    public static void ClearPublishedBuffer\('
$template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Lifecycle.fixture.txt'))
$generated = $template.Replace('/*PRODUCTION_STRUCT*/', $struct).Replace('/*PRODUCTION_METHODS*/', ($methods -join "`n")).Replace('/*PRODUCTION_CLEAR*/', $clear)
[IO.File]::WriteAllText([IO.Path]::GetFullPath($Output), $generated)
