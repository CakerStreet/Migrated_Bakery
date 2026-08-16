# Source Tree Matrix — Duplicate / Recovery Copy Evidence

Captured: 2026-08-16

| Tree | IIS Served | File Count | Has ASPX | Has bin | Has web.config | Has upload | Has Product_images | Notes |
|---|---|---|---|---|---|---|---|---|
| cakerstreet_CRM | YES (27195) | 41027 | YES (261) | YES | YES | NO | NO | Patched (PATCH-001 bool.Parse). ApplicationBlocks EXCLUDED |
| 114_server\114_server | NO | 45656 | YES (384) | YES | YES | NO | NO | Largest server copy. ApplicationBlocks PRESENT |
| 114_server\114_server_safe | NO | 537 | YES (1) | YES | YES | NO | NO | Safe snapshot |
| 114_server_business\crm | NO | 185 | NO | YES | YES | NO | NO | ApplicationBlocks EXCLUDED |
| CRM_backup_pre_recovery | NO | 2314 | YES (210) | YES | YES | YES | YES | Pre-recovery snapshot |
| cakerstreet-live-backup | NO | 1848 | YES (5) | YES | NO | NO | NO | May-13 live backup |
| 114_server_crm_full | NO | 4311 | YES (260) | YES | YES | NO | NO | ApplicationBlocks EXCLUDED |
| 114_server_crm_full_runtime | NO | 41362 | YES (261) | YES | YES | NO | NO | ApplicationBlocks EXCLUDED |
| migration\legacy\crm | NO | 40597 | YES (261) | YES | YES | NO | NO | Migration reference. ApplicationBlocks PRESENT |
| recovered-business-portal | YES (27201) | 2091 | YES (119) | YES | YES | YES | NO | msSQLDLL EXCLUDED. Twilio EXCLUDED (newer present) |
| vs-test\cakerstreet | YES (27203) | 2333 | YES (2) | YES | YES | YES | YES | Contains Product_images (446 files) |
| Cakerstreet.com | NO | 2101 | YES (166) | YES | YES | NO | NO | Alternative frontend copy |
| epos.cakerstreet.com | YES (27210) | 2371 | YES (38) | YES | YES | NO | YES | ApplicationBlocks PRESENT. AjaxControlToolkit MISSING |
| eposadmin.cakerstreet.com | YES (27211) | 1918 | YES (57) | YES | YES | YES | YES | ApplicationBlocks PRESENT. Multiple DLLs missing |
| migration\legacy\frontend | NO | 2137 | YES (166) | YES | YES | NO | NO | Migration reference |
| 114_server_epos | NO | 501 | NO | YES | YES | NO | NO | EPOS server copy |

## Challenger Question
IIS is serving cakerstreet_CRM (41,027 files, patched, ApplicationBlocks EXCLUDED).
114_server/114_server has 46,193 files and ApplicationBlocks PRESENT.
Is the IIS-served copy the most complete and authoritative, or did we pick the wrong tree?
