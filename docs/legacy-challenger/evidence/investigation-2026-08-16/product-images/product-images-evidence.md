# Product Images — HTTP Status and Coverage Analysis
Captured: 2026-08-16

## HTTP Probe of Known Image URLs

| URL | HTTP Status | Content-Type | Response Size | Notes |
|---|---|---|---|---|
| http://localhost:27195/upload/Product_images/resized_300_300/cars-cake-7769.jpg | 404 | - | - | Response status code does not indicate success: 404 (Not Found). |
| http://localhost:27195/upload/Product_images/ | 404 | - | - | Response status code does not indicate success: 404 (Not Found). |
| http://localhost:27195/upload/ | 404 | - | - | Response status code does not indicate success: 404 (Not Found). |
| http://localhost:27203/upload/Product_images/resized_300_300/cars-cake-7769.jpg | 200 | image/jpeg | 30215 bytes | OK |

## Product_images Folder Inventory — All Candidates

| Location | Total Files | Total Size MB | Oldest File | Newest File | Resized_300 count |
|---|---|---|---|---|---|
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\upload\Product_images | 446 | 21.56 | 2026-05-14 | 2026-06-19 | 193 |
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM_backup_pre_recovery\upload\Product_images | 96 | 4.33 | 2026-05-18 | 2026-05-21 | 47 |
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet-live-backup\upload\Product_images | NOT FOUND | - | - | - | - |
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\114_server 1\114_server\upload\Product_images | NOT FOUND | - | - | - | - |
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\Cakerstreet.com\upload\Product_images | NOT FOUND | - | - | - | - |
| G:\AI-Projects\CRM-Recovery\epos_2026\epos.cakerstreet.com\cp\upload\Product_images | 4 | 0.93 | 2021-05-05 | 2021-05-05 | 0 |
| G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\crm\upload\Product_images | NOT FOUND | - | - | - | - |
| G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\frontend\upload\Product_images | NOT FOUND | - | - | - | - |

## DB Product Sample (100 products from db_cakerstreet_live)

```
Msg 208, Level 16, State 1, Server LAPTOP-VFM9JOD4, Line 2
Invalid object name 'dbo.tbl_Handicraft'.
```

## Image Coverage Analysis
Frontend candidate has 446 image files.
(Full coverage analysis requires mapping DB filenames to folder names — see raw data above.)
