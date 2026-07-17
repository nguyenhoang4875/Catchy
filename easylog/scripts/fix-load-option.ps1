$f = 'src\EasyLog.App\ViewModels\MainWindowViewModel.cs'
$c = [System.IO.File]::ReadAllText($f)

$oldMarker = 'private LogLoadMode? ShowLogLoadOptionDialog()'
$idx = $c.IndexOf($oldMarker)
if ($idx -lt 0) { Write-Host "ERROR: ShowLogLoadOptionDialog not found."; exit 1 }

# Find the summary comment above (3 lines before)
$summaryStart = $c.LastIndexOf('/// <summary>', $idx)
if ($summaryStart -lt 0 -or ($idx - $summaryStart) -gt 500) { Write-Host "ERROR: summary not found near method."; exit 1 }

# Find the closing brace of this method
# Count braces from the opening {
$methodStart = $c.IndexOf('{', $idx)
$braceCount = 0
$pos = $methodStart
while ($pos -lt $c.Length) {
    if ($c[$pos] -eq '{') { $braceCount++ }
    elseif ($c[$pos] -eq '}') { $braceCount--; if ($braceCount -eq 0) { break } }
    $pos++
}
$methodEnd = $pos + 1

Write-Host "Found method from $summaryStart to $methodEnd (length $($methodEnd - $summaryStart))"

$newMethod = @"
    /// <summary>
    /// Shows an in-app overlay for log load option selection.
    /// Full: load all logs, Filtered: load only filtered logs, null: cancelled.
    /// </summary>
    private async Task<LogLoadMode?> ShowLogLoadOptionAsync()
    {
        _loadOptionTcs = new TaskCompletionSource<LogLoadMode?>();
        IsLoadOptionVisible = true;
        var result = await _loadOptionTcs.Task.ConfigureAwait(true);
        _loadOptionTcs = null;
        return result;
    }
"@

$result = $c.Substring(0, $summaryStart) + $newMethod + $c.Substring($methodEnd)
[System.IO.File]::WriteAllText($f, $result)
Write-Host "SUCCESS: ShowLogLoadOptionDialog replaced with ShowLogLoadOptionAsync."

