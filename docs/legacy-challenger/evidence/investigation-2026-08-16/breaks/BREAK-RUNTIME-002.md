# BREAK-RUNTIME-002 — CRM Add New Suggestion Compilation Error

## Observed Symptom
Clicking "Add New Suggestion" on the CRM CRF detail page returns an ASP.NET HTML
"Compilation Error" page instead of executing the action.

## Playwright Evidence
- CRM navigates to 403 Windows auth wall (headless Playwright cannot log in)
- Manual browser evidence from session 2026-08-14 confirmed compilation error dialog
- Screenshot from earlier session: evidence/legacy-runtime/screenshots/ (crm-crf-list-before.png)

## Runtime Error (known)
ASP.NET compilation fails because Microsoft.ApplicationBlocks.Data namespace
cannot be resolved. 49 files in App_Code reference this namespace.

## Source Files Involved
- All 49 files in: G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\App_Code\
- Representative: clsEmailNewsletter.cs, msSQLHelper.cs

## Current IIS Physical Path
G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM

## Current BIN State
- Microsoft.ApplicationBlocks.Data.dll.exclude (32,768 bytes, 2016-12-27)
- Microsoft.ApplicationBlocks.Data.dll.refresh.exclude (124 bytes, 2016-12-27)
- DLL is NOT loadable — .exclude suffix prevents ASP.NET from loading it

## Other Candidate Copies Found
See bin-dll/applicationblocks-deep-audit.md for full cross-estate audit.
Best candidate: G:\AI-Projects\Dev\kiro-cakerstreet-uk\114_server 1\114_server\bin\Microsoft.ApplicationBlocks.Data.dll

## Root-Cause Hypothesis (HYPOTHESIS — not yet proven in production context)
The .dll.exclude suffix was applied during a development/recovery process to prevent
the DLL from being loaded (possibly because a newer or conflicting version was
expected). When the CRM was recovered locally, this exclusion was never reverted.
The DLL itself is correct — byte-identical SHA-256 confirms it is the same binary
that works in Business Portal, Frontend, EPOS Terminal, and EPOS Admin.

## Proposed Fix
Option A (preferred): Rename G:\...\cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll.exclude
                   to G:\...\cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll
Option B:           Copy from G:\...\114_server 1\114_server\bin\Microsoft.ApplicationBlocks.Data.dll
Both options produce an identical binary (SHA: 8AA2BAE7...)

## Risk of Fix
ZERO — same binary already loaded and working in all other 4 applications on this machine

## Rollback
Rename .dll back to .dll.exclude. IIS Express does not need to restart (next request
triggers recompilation).