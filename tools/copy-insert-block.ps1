<#!
.SYNOPSIS
  Copy a block of text from a source file and insert it into a target file.

.DESCRIPTION
  Extracts a block from SourcePath (by line range or start/end marker patterns) and inserts it into TargetPath
  (by line number or before/after an anchor pattern).

  Designed to support “mechanical copy/paste” workflows so you can consolidate content into a single file first,
  then refactor, without rewriting from memory.

  Notes:
  - Line numbers are 1-based.
  - By default, creates a timestamped backup of the target file next to it.
  - Preserves the target file's newline style (CRLF vs LF) when writing.

.PARAMETER SourcePath
  Path to the file to copy the block from.

.PARAMETER TargetPath
  Path to the file to insert the block into.

.PARAMETER SourceStartLine
  1-based start line (inclusive) of the block to copy.

.PARAMETER SourceEndLine
  1-based end line (inclusive) of the block to copy.

.PARAMETER SourceStartPattern
  A line-matching pattern that marks the start of the block (inclusive by default).

.PARAMETER SourceEndPattern
  A line-matching pattern that marks the end of the block (inclusive by default).

.PARAMETER SourcePatternIsRegex
  Treat SourceStartPattern/SourceEndPattern as regex patterns instead of literal substring matches.

.PARAMETER SkipSourceStartMatchLine
  If set, the copied block starts on the line after the SourceStartPattern match.

.PARAMETER SkipSourceEndMatchLine
  If set, the copied block ends on the line before the SourceEndPattern match.

.PARAMETER InsertAtLine
  1-based target line at which to insert the block.
  Insertion occurs BEFORE the specified line. Use (TargetLineCount + 1) to append.

.PARAMETER InsertBeforePattern
  Insert before the line that matches this pattern.

.PARAMETER InsertAfterPattern
  Insert after the line that matches this pattern.

.PARAMETER TargetPatternIsRegex
  Treat InsertBeforePattern/InsertAfterPattern as regex patterns instead of literal substring matches.

.PARAMETER Occurrence
  Which match occurrence to use for insertion when using an anchor pattern. Default: 1.

.PARAMETER NoBackup
  Disable creating a backup of the target file.

.PARAMETER PassThru
  Output the extracted block text to the pipeline.

.EXAMPLE
  # Copy lines 10-42 from a source file and insert at line 5 of a target file
  ./tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartLine 10 -SourceEndLine 42 -InsertAtLine 5

.EXAMPLE
  # Copy a block between markers (literal matches), and insert after an anchor line
  ./tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md \
    -SourceStartPattern "BEGIN" -SourceEndPattern "END" \
    -InsertAfterPattern "# Insert Here" -Occurrence 1

.EXAMPLE
  # Use regex markers and preview with WhatIf
  ./tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartPattern "^##\s+Section" -SourceEndPattern "^##\s+" -SourcePatternIsRegex -InsertBeforePattern "^#\s+Appendix" -TargetPatternIsRegex -WhatIf
#>

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
  [Parameter(Mandatory = $true)]
  [string]$SourcePath,

  [Parameter(Mandatory = $true)]
  [string]$TargetPath,

  [Parameter(Mandatory = $false)]
  [int]$SourceStartLine,

  [Parameter(Mandatory = $false)]
  [int]$SourceEndLine,

  [Parameter(Mandatory = $false)]
  [string]$SourceStartPattern,

  [Parameter(Mandatory = $false)]
  [string]$SourceEndPattern,

  [Parameter(Mandatory = $false)]
  [switch]$SourcePatternIsRegex,

  [Parameter(Mandatory = $false)]
  [switch]$SkipSourceStartMatchLine,

  [Parameter(Mandatory = $false)]
  [switch]$SkipSourceEndMatchLine,

  [Parameter(Mandatory = $false)]
  [int]$InsertAtLine,

  [Parameter(Mandatory = $false)]
  [string]$InsertBeforePattern,

  [Parameter(Mandatory = $false)]
  [string]$InsertAfterPattern,

  [Parameter(Mandatory = $false)]
  [switch]$TargetPatternIsRegex,

  [Parameter(Mandatory = $false)]
  [ValidateRange(1, 1000000)]
  [int]$Occurrence = 1,

  [Parameter(Mandatory = $false)]
  [switch]$NoBackup,

  [Parameter(Mandatory = $false)]
  [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-Newline {
  param([string]$Text)

  if ($null -eq $Text -or $Text.Length -eq 0) {
    return "`r`n"
  }

  if ($Text.Contains("`r`n")) { return "`r`n" }
  return "`n"
}

function Split-Lines {
  param([string]$Text)

  # Split on CRLF or LF; produces an array of lines without newline characters.
  if ($null -eq $Text) { return @() }
  return [regex]::Split($Text, "\r?\n", [System.Text.RegularExpressions.RegexOptions]::None)
}

function Find-LineMatchIndices {
  param(
    [string[]]$Lines,
    [string]$Pattern,
    [bool]$IsRegex
  )

  $indices = New-Object System.Collections.Generic.List[int]

  for ($i = 0; $i -lt $Lines.Count; $i++) {
    $line = $Lines[$i]

    $matched = $false
    if ($IsRegex) {
      $matched = [regex]::IsMatch($line, $Pattern)
    } else {
      if ([string]::IsNullOrEmpty($Pattern)) { $matched = $false }
      else { $matched = $line.Contains($Pattern) }
    }

    if ($matched) {
      [void]$indices.Add($i)
    }
  }

  return ,$indices.ToArray()
}

if (-not (Test-Path -LiteralPath $SourcePath)) {
  throw "SourcePath not found: $SourcePath"
}

if (-not (Test-Path -LiteralPath $TargetPath)) {
  throw "TargetPath not found: $TargetPath"
}

$sourceText = Get-Content -LiteralPath $SourcePath -Raw
$targetText = Get-Content -LiteralPath $TargetPath -Raw

$targetNewline = Resolve-Newline -Text $targetText

$sourceLines = Split-Lines -Text $sourceText
$targetLines = Split-Lines -Text $targetText

# If the file ends with a newline, Split-Lines will include a trailing empty line.
# That's OK; we preserve exact line arrays and write joined with target newline.

$blockStartIndex = $null
$blockEndIndex = $null

$hasSourceLines = ($PSBoundParameters.ContainsKey('SourceStartLine') -or $PSBoundParameters.ContainsKey('SourceEndLine'))
$hasSourcePatterns = ($PSBoundParameters.ContainsKey('SourceStartPattern') -or $PSBoundParameters.ContainsKey('SourceEndPattern'))

if ($hasSourceLines -and $hasSourcePatterns) {
  throw "Choose exactly one source selector: line range OR start/end patterns (not both)."
}

if ($hasSourceLines) {
  if (-not ($PSBoundParameters.ContainsKey('SourceStartLine') -and $PSBoundParameters.ContainsKey('SourceEndLine'))) {
    throw "When selecting source by lines, both SourceStartLine and SourceEndLine are required."
  }
  if ($SourceStartLine -lt 1 -or $SourceEndLine -lt 1) {
    throw "SourceStartLine/SourceEndLine must be >= 1."
  }
  if ($SourceEndLine -lt $SourceStartLine) {
    throw "SourceEndLine must be >= SourceStartLine."
  }

  $maxLine = $sourceLines.Count
  if ($SourceStartLine -gt $maxLine -or $SourceEndLine -gt $maxLine) {
    throw "Source line range out of bounds. Source has $maxLine lines."
  }

  $blockStartIndex = $SourceStartLine - 1
  $blockEndIndex = $SourceEndLine - 1
}
elseif ($hasSourcePatterns) {
  if ([string]::IsNullOrWhiteSpace($SourceStartPattern) -or [string]::IsNullOrWhiteSpace($SourceEndPattern)) {
    throw "When selecting source by patterns, both SourceStartPattern and SourceEndPattern are required."
  }

  $startMatches = Find-LineMatchIndices -Lines $sourceLines -Pattern $SourceStartPattern -IsRegex ([bool]$SourcePatternIsRegex)
  if ($startMatches.Count -lt 1) {
    throw "SourceStartPattern not found in source file."
  }

  $startIndex = $startMatches[0]

  $endMatchesAll = Find-LineMatchIndices -Lines $sourceLines -Pattern $SourceEndPattern -IsRegex ([bool]$SourcePatternIsRegex)
  $endMatches = @($endMatchesAll | Where-Object { $_ -ge $startIndex })
  if ($endMatches.Count -lt 1) {
    throw "SourceEndPattern not found in source file after the start marker."
  }

  $endIndex = $endMatches[0]

  if ($SkipSourceStartMatchLine) { $startIndex++ }
  if ($SkipSourceEndMatchLine) { $endIndex-- }

  if ($startIndex -lt 0 -or $endIndex -ge $sourceLines.Count -or $endIndex -lt $startIndex) {
    throw "Computed block range is invalid after applying Skip* switches."
  }

  $blockStartIndex = $startIndex
  $blockEndIndex = $endIndex
}
else {
  throw "You must specify a source selector: (-SourceStartLine and -SourceEndLine) OR (-SourceStartPattern and -SourceEndPattern)."
}

$blockLines = @()
for ($i = $blockStartIndex; $i -le $blockEndIndex; $i++) {
  $blockLines += $sourceLines[$i]
}

$blockText = ($blockLines -join $targetNewline)

# Determine insertion index in target
$insertIndex = $null
$insertAtLineProvided = $PSBoundParameters.ContainsKey('InsertAtLine')
$insertBeforeProvided = $PSBoundParameters.ContainsKey('InsertBeforePattern')
$insertAfterProvided = $PSBoundParameters.ContainsKey('InsertAfterPattern')

$insertModes = @(@($insertAtLineProvided, $insertBeforeProvided, $insertAfterProvided) | Where-Object { $_ })
if ($insertModes.Count -ne 1) {
  throw "You must specify exactly one insertion method: -InsertAtLine OR -InsertBeforePattern OR -InsertAfterPattern."
}

if ($insertAtLineProvided) {
  if ($InsertAtLine -lt 1) {
    throw "InsertAtLine must be >= 1."
  }

  $maxTargetLine = $targetLines.Count + 1
  if ($InsertAtLine -gt $maxTargetLine) {
    throw "InsertAtLine out of bounds. Target has $($targetLines.Count) lines; use 1..$maxTargetLine."
  }

  $insertIndex = $InsertAtLine - 1
}
elseif ($insertBeforeProvided) {
  if ([string]::IsNullOrWhiteSpace($InsertBeforePattern)) {
    throw "InsertBeforePattern cannot be empty."
  }

  $matches = Find-LineMatchIndices -Lines $targetLines -Pattern $InsertBeforePattern -IsRegex ([bool]$TargetPatternIsRegex)
  if ($matches.Count -lt $Occurrence) {
    throw "InsertBeforePattern not found $Occurrence time(s) in target file."
  }

  $insertIndex = $matches[$Occurrence - 1]
}
else {
  if ([string]::IsNullOrWhiteSpace($InsertAfterPattern)) {
    throw "InsertAfterPattern cannot be empty."
  }

  $matches = Find-LineMatchIndices -Lines $targetLines -Pattern $InsertAfterPattern -IsRegex ([bool]$TargetPatternIsRegex)
  if ($matches.Count -lt $Occurrence) {
    throw "InsertAfterPattern not found $Occurrence time(s) in target file."
  }

  $insertIndex = $matches[$Occurrence - 1] + 1
}

# Create updated target lines by inserting block lines.
$newTargetLines = @()
for ($i = 0; $i -lt $targetLines.Count; $i++) {
  if ($i -eq $insertIndex) {
    $newTargetLines += $blockLines
  }
  $newTargetLines += $targetLines[$i]
}

# Append if insertIndex is at the end
if ($insertIndex -eq $targetLines.Count) {
  $newTargetLines += $blockLines
}

$newTargetText = ($newTargetLines -join $targetNewline)

$summary = "Copy block [$($blockStartIndex + 1)-$($blockEndIndex + 1)] from '$SourcePath' and insert into '$TargetPath' at index $($insertIndex + 1)."

if ($PSCmdlet.ShouldProcess($TargetPath, $summary)) {
  if (-not $NoBackup) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = "$TargetPath.bak.$timestamp"
    Copy-Item -LiteralPath $TargetPath -Destination $backupPath -Force
    Write-Verbose "Backup created: $backupPath"
  }

  Set-Content -LiteralPath $TargetPath -Value $newTargetText -NoNewline

  Write-Host $summary
  Write-Host "Inserted $($blockLines.Count) line(s)."
}

if ($PassThru) {
  $blockText
}
