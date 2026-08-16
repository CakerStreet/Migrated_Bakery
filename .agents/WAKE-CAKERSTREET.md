# WAKE-CAKERSTREET — Legacy Runtime Authority

**Trigger:** `#wakecakerstreet`

When this trigger is used, read this file completely before taking any action.
Re-establish the full legacy runtime/challenger context. Do not proceed until
all sections below are loaded into working context.

---

## 1. PURPOSE

This is the challenger audit runtime for the original CakerStreet legacy
Web Forms applications. The objective is to make the legacy runtime a
**trustworthy source of truth** before migration begins.

Do NOT perform migration work unless explicitly asked.
Do NOT copy, rename, or patch files without source → destination proof and
challenger approval.

---

## 2. FIVE LEGACY APPLICATIONS — RUNTIME AUTHORITY

| Port | IIS Site Name | Physical Path (served by IIS Express) | App Pool |
|---|---|---|---|
| **27195** | `cakerstreet_crm` | `G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM` | Clr4IntegratedAppPool |
| **27201** | `business_portal` | `G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source` | Clr4IntegratedAppPool |
| **27203** | `cakerstreet` | `G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet` | Clr4IntegratedAppPool |
| **27210** | `epos_terminal` | `G:\AI-Projects\CRM-Recovery\epos_2026\epos.cakerstreet.com` | Clr4IntegratedAppPool |
| **27211** | `epos_admin` | `G:\AI-Projects\CRM-Recovery\epos_2026\eposadmin.cakerstreet.com` | Clr4IntegratedAppPool |

**IIS config:** `%USERPROFILE%\Documents\IISExpress\config\applicationhost.config`

**To start all five sites:**
```powershell
$IIS_EXE = "$env:ProgramFiles\IIS Express\iisexpress.exe"
$IIS_CFG = "$env:USERPROFILE\Documents\IISExpress\config\applicationhost.config"
foreach ($site in @('cakerstreet_crm','business_portal','cakerstreet','epos_terminal','epos_admin')) {
    Start-Process -FilePath $IIS_EXE -ArgumentList "/config:`"$IIS_CFG`" /site:$site /systray:false" -WindowStyle Hidden
}
```
Wait 12s then probe ports 27195/27201/27203/27210/27211.

---

## 3. DATABASE CONNECTIONS (runtime web.configs)

| App | Key DB | Server | Notes |
|---|---|---|---|
| CRM | `db_cakerstreet_live` | localhost | **CONTAMINATED** — 28 BatchThree records (IDs ≥ 1,000,000) |
| CRM | `db_cakerstreet_crm` | localhost | CRM-specific tables |
| CRM | `db_cakerstreet_business` | localhost | Inventory/business |
| Business Portal | `db_cakerstreet_live` | localhost | Same live DB |
| Business Portal | `db_cakerstreet_staffAssessment` | localhost | |
| Frontend | `db_cakerstreet_live` | localhost | Same live DB |
| Frontend | `db_kart` | localhost | |
| EPOS Terminal | `db_cakerstreet_epos` | localhost | |
| EPOS Terminal | `db_cakerstreet_franchise` | localhost | |
| EPOS Admin | `db_cakerstreet_franchise` | localhost | |

**All databases use Windows Integrated Security — localhost only.**

---

## 4. DUPLICATE SOURCE TREES — SEARCH BEFORE CONCLUDING MISSING

Before declaring any file missing, search ALL of these locations:

```
G:\AI-Projects\Dev\kiro-cakerstreet-uk\
  ├── cakerstreet_CRM\                      # IIS-served CRM (patched, PATCH-001)
  ├── 114_server 1\114_server\              # Largest server copy (46,193 files) — ApplicationBlocks PRESENT
  ├── 114_server 1\114_server_safe\         # Safe snapshot
  ├── 114_server_business\114_server_business\  # Business server copy
  ├── 114_server_epos\114_server_epos\      # EPOS server copy
  ├── recovered-business-portal-source\    # IIS-served Business Portal
  ├── vs-test\cakerstreet\                 # IIS-served Frontend
  ├── Cakerstreet.com\                     # Alternative frontend copy
  ├── cakerstreet-live-backup\             # May-13 live backup
  ├── cakerstreet_CRM_backup_pre_recovery\ # Pre-recovery CRM snapshot
  └── recovery-snapshots\                  # Recovery snapshots

G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\
  ├── crm\          # Migration reference CRM (40,597 files)
  ├── frontend\     # Migration reference frontend
  ├── business-portal\
  ├── epos-terminal\
  └── epos-admin\

G:\AI-Projects\CRM-Recovery\
  ├── 114_server_crm_full\          # 4,311 files
  ├── 114_server_crm_full_runtime\  # 41,362 files — ApplicationBlocks EXCLUDED
  └── epos_2026\                    # EPOS runtime

G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy-server-intake\
  └── crm-2026-08-15\              # Server intake staging area (empty — awaiting server copy)
```

---

## 5. KNOWN BREAKS

### BREAK-RUNTIME-002 — CRM Add New Suggestion ✅ RESOLVED 2026-08-16
- **Original hypothesis:** `Microsoft.ApplicationBlocks.Data.dll.exclude` was blocking namespace resolution
- **Actual root cause:** Stale/corrupt ASP.NET temp cache (`%TEMP%\Temporary ASP.NET Files\root\66f5cfd2\`) had a poisoned compile state
- **Real DLL provider:** `msSQLDLL.dll` (13,312 bytes, v1.0.0.0) exports `Microsoft.ApplicationBlocks.Data.SqlHelper` and `SqlHelperParameterCache` — it IS the namespace provider
- **Fix applied:** Cleared 4 ASP.NET temp cache folders (`181ad724`, `43db38b2`, `66f5cfd2`, `c148442f`). Restarted cakerstreet_crm IIS Express.
- **Verified:** `crflist_forsalesperson.aspx` — HTTP 200, title 'Customer Requirement Form', zero compilation errors
- **Note:** The `.dll.exclude` for `Microsoft.ApplicationBlocks.Data.dll` is INTENTIONAL — `msSQLDLL.dll` is the correct provider.

### BREAK-DB-006 — BatchThree data contamination (PARTIAL — UAT RESTORED)
- **Active DB:** `db_cakerstreet_live` — **17** synthetic CRF records (IDs 1,000,000–1,000,016, created 2026-06-07 to 06-09)
- **Correction:** Previous count of 28 was wrong. Actual count = 17.
- **UAT DB:** `db_cakerstreet_live_legacy_uat` ✅ RESTORED 2026-08-16 (1551136 pages, 318 sec, SQL v915→v998)
- **UAT DB BatchThree count:** 0 ✅ (clean backup)
- **Legitimate post-May-13 CRFs in active:** 9 (would be lost if active were replaced)
- **Active has 1 extra table:** `tbl_staffTiming` (added post-backup)
- **CRM web.config NOT changed** — applications still point to `db_cakerstreet_live`
- **Status:** UAT DB available for comparison. No production DB replacement executed.

### BREAK-CONTENT-001 — CRM missing `/upload/Product_images/` (OPEN)
- **Finding:** Frontend (27203) loads product images from `http://localhost:27195/upload/Product_images/`
- **CRM physical path has no `/upload/` folder** → all product images return 404/blocked
- **Candidate source:** `G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\upload\Product_images\` (446 files)
- **Status:** Awaiting challenger approval

### BREAK-BIN-001 — Business Portal msSQLDLL ✅ CLOSED 2026-08-16
- **Finding:** BP bin contains `Microsoft.ApplicationBlocks.Data.dll` (32,768 bytes, SHA `8AA2BAE7...`) — the FULL correct binary
- **BP compiles clean** — HTTP 200 on all tested pages without any DLL change
- **No fix needed.** The earlier `.dll.exclude` observation was for a different file. BP is healthy.

### BREAK-CRM-003 — CRF Detail Page DataBinding Error (OPEN — needs Challenger decision)
- **URL:** `http://localhost:27195/quotations/51216`
- **HTTP:** 500
- **Error:** `DataBinding: AnonymousType does not contain property 'CakeShapeTitle'`
- **Data check:** CRF 51216 has `CRF_ShapeID = 13` ("As Shown") — NOT null. `tbl_CakeShape` has row 13.
- **Root cause:** LINQ anonymous type projection in the detail code path omits `CakeShapeTitle`. The 29-field anonymous type is built from a query that doesn't JOIN `tbl_CakeShape`. The CRF list view works; the detail view (with ID parameter) uses a different query.
- **Impact:** CRF detail page fails for any CRF. CRF list (`/quotations`) works fine.
- **Status:** Awaiting Challenger decision — fix null-guard or trace query projection.

### BREAK-FRONT-001 — Frontend Category Routing (`checkredirection.aspx` MISSING)
- **Affected:** ALL `/category/*` URLs on Frontend (27203)
- **HTTP:** 404 "The resource cannot be found"
- **Route defined:** `category/{categoryName}` → `~/checkredirection.aspx` in Frontend Global.asax
- **File status:** `checkredirection.aspx` is ABSENT from ALL source trees:
  - `vs-test\cakerstreet` ❌, `114_server 1\114_server` ❌, `cakerstreet-live-backup` ❌, `Cakerstreet.com` ❌
- **DB:** Category SEO URLs ARE present (`birthday-cakes` ID=1, `wedding-engagement-cakes` ID=2, etc.)
- **Impact:** All product category browsing broken. Home, Quote, Contact, Login still work.
- **Question for Challenger:** Was checkredirection.aspx compiled into a DLL? Served from a different location? Does the original server have this file?

---

## 6. BIN / DLL AUDIT REFERENCE

| DLL | CRM | Biz Portal | Frontend | EPOS Term | EPOS Admin |
|---|---|---|---|---|---|
| `Microsoft.ApplicationBlocks.Data.dll` | 🔴 EXCLUDED (.dll.exclude) | ✅ (32KB, SHA 8AA2BAE7) | ✅ | ✅ | ✅ |
| `AjaxControlToolkit.dll` | ✅ v18.1 | ✅ v18.1 | ✅ v18.1 | 🔴 MISSING | ✅ v4.1 |
| `Twilio.dll` | ✅ v5.6.4 | ✅ v7.11.3 | 🔴 MISSING | 🔴 MISSING | 🔴 MISSING |
| `EntityFramework.dll` | ✅ v6.1 | ✅ v6.4 | ✅ v6.4 | ✅ v6.4 | ✅ v6.1 |
| `Newtonsoft.Json.dll` | ✅ v9.0 | ✅ v13.0 | ✅ v13.0 | ✅ v13.0 | ✅ v13.0 |
| `msSQLDLL.dll` | ✅ (13KB wrapper) | N/A (has full DLL) | 🔴 MISSING | 🔴 MISSING | 🔴 MISSING |
| `System.Web.Optimization.dll` | ✅ | ✅ | ✅ | ✅ | 🔴 MISSING |
| `WebGrease.dll` | ✅ | ✅ | ✅ | ✅ | 🔴 MISSING |

---

## 7. CONTENT / UPLOAD PATHS

| App | `/upload/` | `Product_images/` | Notes |
|---|---|---|---|
| CRM (27195) | 🔴 MISSING | 🔴 MISSING | **Critical** — Frontend serves images via CRM URL |
| Frontend (27203) | ✅ 450 files | ✅ 446 images | Most complete local copy |
| Business Portal | ✅ (empty) | 🔴 MISSING | |
| EPOS Terminal | — | 4 files | In `cp/upload/` |

---

## 8. CHALLENGER RULES

1. **AntiGravity provides evidence — you act as challenger.** AntiGravity does not declare anything "fixed" or "correct" unilaterally.
2. **No blind copy/fix.** Every proposed file operation must state: CURRENT IIS PATH · MISSING/WRONG FILE · CANDIDATE SOURCE PATH · DESTINATION PATH · SIZE/VERSION/DATE COMPARISON · WHY THIS IS THE CORRECT COPY.
3. **Duplicate tree search first.** Before concluding a file is missing, search all trees in Section 4.
4. **Playwright screenshots mandatory** for any UI/runtime evidence. Text-only reports are not acceptable.
5. **GitHub is the durable layer.** Evidence, decisions, and approvals go to [GitHub Issue #1](https://github.com/CakerStreet/Migrated_Bakery/issues/1).
6. **No migration implementation** unless explicitly requested. This is runtime stabilisation only.
7. **Safety boundary:** localhost only. No remote server access. No credential discovery. SELECT-only SQL unless RESTORE explicitly approved.
8. **Never restart production.** All work is on local IIS Express daemons only.

---

## 9. GITHUB EVIDENCE THREAD

- **Repo:** `CakerStreet/Migrated_Bakery`
- **Issue #1:** [LEGACY CHALLENGER — Runtime Source, DLL/BIN, Uploads & Playwright Audit](https://github.com/CakerStreet/Migrated_Bakery/issues/1)
- **Screenshots:** `docs/legacy-challenger/evidence/`
- **Local evidence:** `G:\AI-Projects\Dev\antigravity-cakerstreet-migration\evidence\legacy-runtime\`
  - `breaks/` — BREAK-*.md reports
  - `screenshots/` — Playwright PNGs
  - `server-comparison/` — CRM_BIN_COMPARISON.md, CRM_SOURCE_COMPARISON.md

---

## 10. REPRESENTATIVE CRF FOR EVIDENCE CAPTURE

`CRF_ID = 51216` — 2 quotes · 10 suggestions · 4 notes · 1 reminder · 1 bakery link

Use this CRF when capturing screenshots of CRM workflow. Exclude any CRF with ID ≥ 1,000,000 (BatchThree synthetic fixtures).

---

## 11. PATCH REGISTER

| Patch ID | File | Nature | Status |
|---|---|---|---|
| PATCH-001 | `cakerstreet_CRM\crflist_forsalesperson.aspx` | 37 `bool.Parse()` fixes | UNVERIFIED_EXISTING_PATCH |

See [`RUNTIME_PATCH_REGISTER.md`](../RUNTIME_PATCH_REGISTER.md) for full diff and approval status.

---

## 12. WAKE CHECKLIST (run on every `#wakecakerstreet`)

- [ ] Verify all 5 IIS Express processes are running (ports 27195/27201/27203/27210/27211)
- [ ] Confirm active database for CRM is known (currently `db_cakerstreet_live`, contaminated)
- [ ] Load open breaks: BREAK-RUNTIME-002, BREAK-DB-006, BREAK-CONTENT-001, BREAK-BIN-001
- [ ] Check GitHub Issue #1 for any new challenger responses since last session
- [ ] Do not proceed to work until this context is established
