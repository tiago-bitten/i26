; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------
I26ID001 | i26.Ids | Error | A typed id has to be partial for the generator to write into it
I26ID002 | i26.Ids | Error | A typed id prefix has to be one to three lowercase letters
I26ID003 | i26.Ids | Error | Two typed ids cannot share a prefix
I26ID004 | i26.Ids | Error | A typed id cannot be nested inside another type
