# EVIDENCE INDEX — Investigation 2026-08-16

**Investigation ID:** INV-2026-08-16
**Captured:** 2026-08-16 ~16:06–16:12 BST
**GitHub repo:** CakerStreet/Migrated_Bakery
**GitHub issue:** #1

This index links every artifact in this investigation and states what each proves.
The Challenger must be able to navigate all raw evidence from this file alone.

---

## RUNTIME AUTHORITY

| Artifact | Proves |
|---|---|
| [runtime/iis-authority.md](runtime/iis-authority.md) | Exact IIS site name, physical path, app pool, port, and live process state for all 5 apps |
| [runtime/connection-strings.md](runtime/connection-strings.md) | Exact database server, database name, and auth mode from the web.config actually loaded by IIS |
| [runtime/app-pool-config.md](runtime/app-pool-config.md) | .NET runtime version and pipeline mode for the shared app pool |
| [webconfigs/web.config.27195.xml](webconfigs/web.config.27195.xml) | Raw web.config served by CRM |
| [webconfigs/web.config.27201.xml](webconfigs/web.config.27201.xml) | Raw web.config served by Business Portal |
| [webconfigs/web.config.27203.xml](webconfigs/web.config.27203.xml) | Raw web.config served by Frontend |
| [webconfigs/web.config.27210.xml](webconfigs/web.config.27210.xml) | Raw web.config served by EPOS Terminal |
| [webconfigs/web.config.27211.xml](webconfigs/web.config.27211.xml) | Raw web.config served by EPOS Admin |

---

## BIN / DLL EVIDENCE

| Artifact | Proves |
|---|---|
| [bin-dll/dll-matrix.md](bin-dll/dll-matrix.md) | Full DLL presence/absence matrix across all 5 IIS-served bins. Lists every DLL with version and date. Identifies EXCLUDED and MISSING DLLs. |
| [bin-dll/applicationblocks-deep-audit.md](bin-dll/applicationblocks-deep-audit.md) | Every copy of Microsoft.ApplicationBlocks.Data.dll found across all 16 source trees. SHA-256 comparison proving byte-identity of all copies including the .exclude in CRM bin. |

---

## UPLOAD / CONTENT EVIDENCE

| Artifact | Proves |
|---|---|
| [upload-content/content-audit.md](upload-content/content-audit.md) | Upload folder inventory for all 5 IIS-served apps. Product_images locations across entire estate. Proves CRM has no /upload/ folder and Frontend (27203) has the most complete local copy (446 files). |

---

## SOURCE TREE COMPARISON

| Artifact | Proves |
|---|---|
| [source-trees/source-tree-matrix.md](source-trees/source-tree-matrix.md) | 16-tree comparison matrix: file count, ASPX presence, bin, web.config, upload, Product_images, IIS-served status, and known issues for each tree. Challenger can determine whether IIS is running the most complete copy. |

---

## BREAKS EVIDENCE

| Artifact | Proves |
|---|---|
| [breaks/BREAK-RUNTIME-002.md](breaks/BREAK-RUNTIME-002.md) | Root cause of Add New Suggestion compilation error. Symptom, root-cause hypothesis, proposed fix (rename .dll.exclude), risk (ZERO), rollback. |
| [breaks/BREAK-DB-006.md](breaks/BREAK-DB-006.md) | Database contamination with 28 BatchThree records. Backup path, VERIFYONLY result, exact RESTORE command, risk (LOW — new DB, original untouched). |
| [breaks/BREAK-CONTENT-001.md](breaks/BREAK-CONTENT-001.md) | Missing /upload/Product_images/ from CRM physical path. Evidence: Playwright caught ERR_BLOCKED_BY_ORB from http://localhost:27195/upload/Product_images/. Candidate source, counts, risk. |
| [breaks/BREAK-BIN-001.md](breaks/BREAK-BIN-001.md) | Business Portal msSQLDLL.dll.exclude. Candidate source, risk (LOW), rollback. |

---

## PLAYWRIGHT EVIDENCE

| App | Status | Screenshots | DOM | HAR | Console | Notes |
|---|---|---|---|---|---|---|
| CRM (27195) | HTTP 403 Windows Auth | crm-27195-fullpage.png, crm-27195-viewport.png | crm-27195-dom.html | network.har | crm-27195-console.txt | Headless cannot log in. 403 is expected/correct. |
| Business Portal (27201) | HTTP 200 (after 60s wait) | error-state.png | business-portal-27201-dom.html | network.har | console.txt | Timed out on first Playwright hit (30s). Resolved HTTP 200 on subsequent curl probe. First-hit ASP.NET compilation delay. |
| Frontend (27203) | HTTP 200 | frontend-27203-error-state.png | frontend-27203-dom.html | network.har | console.txt | Timed out on first Playwright hit. Resolved HTTP 200 / 'Birthday Cakes...' on curl. ERR_BLOCKED_BY_ORB for product images from 27195. |
| EPOS Terminal (27210) | HTTP 200 | epos-terminal-27210-fullpage.png, viewport.png | dom.html | network.har | console.txt | Clean. Title: 'Cakerstreet Franchise - Sign In' |
| EPOS Admin (27211) | HTTP 200 (302→adminlogin) | epos-admin-27211-fullpage.png, viewport.png | dom.html | network.har | console.txt | Clean. Title: 'Cakerstreet Franchise :: Admin Login' |

Playwright files: [playwright/](playwright/)

---

## PROPOSED COPY ACTIONS (all awaiting challenger approval)

| Action | Source | Destination | Risk |
|---|---|---|---|
| BREAK-RUNTIME-002 fix | ..\114_server 1\114_server\bin\Microsoft.ApplicationBlocks.Data.dll | cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll | ZERO |
| BREAK-CONTENT-001 fix | vs-test\cakerstreet\upload\ | cakerstreet_CRM\upload\ | LOW |
| BREAK-BIN-001 fix | cakerstreet_CRM\bin\msSQLDLL.dll | recovered-business-portal-source\bin\msSQLDLL.dll | LOW |
| BREAK-DB-006 fix | db_cakerstreet_live_may13_26.bak | RESTORE as db_cakerstreet_live_legacy_uat | LOW |

---

## CHALLENGER OPEN QUESTIONS

1. **Source tree authority**: Is cakerstreet_CRM (41,027 files, patched, ApplicationBlocks EXCLUDED) the correct CRM to serve, or should 114_server\114_server (46,193 files, ApplicationBlocks PRESENT) be used instead?
2. **Product_images completeness**: The 446-file set in vs-test\cakerstreet\upload\Product_images\ — is this the complete production set or a partial? The CRM pre-recovery backup had only 96.
3. **Twilio version**: CRM uses Twilio 5.6.4, Business Portal has 7.11.3. Is 5.6.4 correct for the CRM codebase?
4. **EPOS Admin missing DLLs**: System.Web.Optimization.dll, WebGrease.dll, Antlr3.Runtime.dll all missing. EPOS Admin boots anyway — does it use bundling/minification features that would break at runtime?