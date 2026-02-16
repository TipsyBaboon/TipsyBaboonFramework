# Tools

## copy-insert-block.ps1

Copies a block of text from one file and inserts it into another.

Selection (source):
- Line range: `-SourceStartLine` / `-SourceEndLine`
- Marker lines: `-SourceStartPattern` / `-SourceEndPattern` (literal by default; add `-SourcePatternIsRegex` for regex)

Insertion (target):
- At a line: `-InsertAtLine` (inserts *before* that line; use `TargetLineCount + 1` to append)
- Before an anchor line: `-InsertBeforePattern`
- After an anchor line: `-InsertAfterPattern`

Safety:
- Supports `-WhatIf` / `-Confirm`.
- Creates a timestamped backup next to the target by default (disable with `-NoBackup`).

Examples:

- Copy lines 10–42 from one file and insert before line 5 in another:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartLine 10 -SourceEndLine 42 -InsertAtLine 5`

- Copy a block between markers (literal matches) and insert after the first matching anchor:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartPattern "BEGIN" -SourceEndPattern "END" -InsertAfterPattern "# Insert Here" -Occurrence 1`

- Preview without writing:
  - `powershell -NoProfile -ExecutionPolicy Bypass -File tools/copy-insert-block.ps1 -SourcePath a.md -TargetPath b.md -SourceStartLine 1 -SourceEndLine 5 -InsertAtLine 1 -WhatIf`
