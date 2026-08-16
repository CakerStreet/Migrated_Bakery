# DB Comparison — Active (db_cakerstreet_live) vs UAT (db_cakerstreet_live_legacy_uat)
Captured: 2026-08-16 16:42 BST

Restore summary: 1551136 pages in 318 seconds (38.1 MB/sec). SQL upgrade from v915 to v998.

## 1. Schema Object Counts

| Object type | db_cakerstreet_live (ACTIVE/CONTAMINATED) | db_cakerstreet_live_legacy_uat (MAY-13 BACKUP) |
|---|---|---|
| Tables | 456 | 455 |
| Views | 1 | 1 |
| Procedures | 375 | 375 |
| Functions | 18 | 18 |

## 2. Key Table Counts and ID Ranges

| Table | ACTIVE rows | ACTIVE min_id | ACTIVE max_id | UAT rows | UAT min_id | UAT max_id |
|---|---|---|---|---|---|---|
| tbl_CRF | 76881 | 11488 | 1000016 | 76864 | 11488 | 89264 |
| tbl_Handicraft | ERR | ERR | ERR | ERR | ERR | ERR |
| tbl_Orders | ERR | ERR | ERR | ERR | ERR | ERR |
| tbl_Category | 287 | 1 | 325 | 287 | 1 | 325 |
| tbl_Tags | ERR | ERR | ERR | ERR | ERR | ERR |
| tbl_Users | ERR | ERR | ERR | ERR | ERR | ERR |
| tbl_Newsletter | 1 | 1 | 1 | 1 | 1 | 1 |

## 3. BatchThree Contamination in Active DB
Active DB BatchThree records (CRF_ID >= 1000000):
```
1000000|2026-06-07 16:38:28.313
1000001|2026-06-07 16:42:27.963
1000002|2026-06-07 17:22:34.167
1000003|2026-06-07 17:26:51.867
1000004|2026-06-07 17:37:09.117
1000005|2026-06-07 17:39:52.330
1000006|2026-06-07 17:41:31.717
1000007|2026-06-07 17:44:15.323
1000008|2026-06-07 17:45:29.957
1000009|2026-06-07 22:02:14.123
1000010|2026-06-07 22:21:42.023
1000011|2026-06-07 22:22:33.997
1000012|2026-06-07 22:22:51.667
1000013|2026-06-07 22:23:26.470
1000014|2026-06-07 22:23:49.943
1000015|2026-06-07 23:09:24.480
1000016|2026-06-09 12:26:41.830

(17 rows affected)
```
UAT DB BatchThree records count: 0

## 4. Post-May-13 Records (legitimate, would be lost if restoring active from backup)
Legitimate post-May-13 CRFs in active DB: 9

## 5. CRF Date Range

| DB | Oldest CRF | Newest CRF (excl. BatchThree) |
|---|---|---|
| ACTIVE | 2017-06-16 13:58:00.000 | 2026-05-13 12:41:56.297 |
| UAT | 2017-06-16 13:58:00.000 | 2026-05-13 12:41:56.297 |

## 6. Schema Diff — Tables in ACTIVE not in UAT
Tables in ACTIVE not in UAT: 1
  - tbl_staffTiming

Tables in UAT not in ACTIVE: 0
