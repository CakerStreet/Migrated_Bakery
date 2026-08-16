# BREAK-BIN-001 — Business Portal msSQLDLL.dll Excluded

## Observed Symptom
Business Portal (27201) bin contains msSQLDLL.dll.exclude (13,312 bytes).
Any code path that calls msSQLHelper will fail at runtime.

## Current IIS Physical Path (Business Portal)
G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source

## Current BIN State
G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source\bin\msSQLDLL.dll.exclude (13,312 bytes, 2026-05-15)

## Candidate Source
G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\bin\msSQLDLL.dll
  Size: 13,312 bytes
  Version: 1.0.0.0
  Date: (in CRM bin — known present)

## Root-Cause Hypothesis (HYPOTHESIS)
Same exclusion pattern as ApplicationBlocks. During recovery, msSQLDLL.dll was
renamed to .exclude in the business portal tree, possibly to avoid a conflict
or because the recovery script excluded custom DLLs to prevent version mismatches.

## Proposed Fix
Option A: Rename recovered-business-portal-source\bin\msSQLDLL.dll.exclude -> .dll
Option B: Copy from cakerstreet_CRM\bin\msSQLDLL.dll

## Risk
LOW — small utility DLL (13KB). Same file present and working in CRM.

## Rollback
Rename .dll back to .dll.exclude.