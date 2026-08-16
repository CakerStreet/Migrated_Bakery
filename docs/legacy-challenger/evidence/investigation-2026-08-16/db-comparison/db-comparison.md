# Database Comparison — Active vs Backup
Captured: 2026-08-16

Active: db_cakerstreet_live (contaminated — 28 BatchThree records)
Backup: db_cakerstreet_live_may13_26.bak (not yet restored — VERIFYONLY confirms valid)

## Active DB: db_cakerstreet_live — Schema Object Counts

```
Tables|456
Views|1
StoredProcs|375
Functions|18

(4 rows affected)
```

## Active DB: Key Table Counts and ID Ranges

```
Msg 208, Level 16, State 1, Server LAPTOP-VFM9JOD4, Line 2
Invalid object name 'tbl_Handicraft'.
```

## BatchThree Records — Isolation Query

```
1000000|2026-06-07 16:38:28.313|0
1000001|2026-06-07 16:42:27.963|0
1000002|2026-06-07 17:22:34.167|0
1000003|2026-06-07 17:26:51.867|0
1000004|2026-06-07 17:37:09.117|0
1000005|2026-06-07 17:39:52.330|0
1000006|2026-06-07 17:41:31.717|0
1000007|2026-06-07 17:44:15.323|0
1000008|2026-06-07 17:45:29.957|0
1000009|2026-06-07 22:02:14.123|0
1000010|2026-06-07 22:21:42.023|0
1000011|2026-06-07 22:22:33.997|0
1000012|2026-06-07 22:22:51.667|0
1000013|2026-06-07 22:23:26.470|0
1000014|2026-06-07 22:23:49.943|0
1000015|2026-06-07 23:09:24.480|0
1000016|2026-06-09 12:26:41.830|0

(17 rows affected)
```

## Records Newer Than 2026-05-13 (post-backup records)

Post-backup legitimate CRFs:
```
9

(1 rows affected)
```

## CRF Date Range
```
2017-06-16 13:58:00.000|2026-05-13 12:41:56.297

(1 rows affected)
```

## Stored Procedures List (first 50)
```
addselectedPrd2tags_all_default15
AddUpdateBrand
addupdatebrandSEO
AddUpdateBusinessType
AddUpdateCategory
AddUpdateCategoryBulkUpload
addupdatecategorySEO
AddUpdateCoupon
AddUpdateGiftProduct
addUpdateHeaderContent
AddUpdateLinkProductForBulkupload
AddUpdatelinkProductwithCategoryForBulkupload
AddUpdateNewsletter
addupdatePageSEO
AddUpdateParameter
AddUpdatePrdReview
AddUpdatePriceBand
AddUpdateProduct
AddUpdateProductBuyButton
AddUpdateProductOption
addupdateproductSEO
AddUpdateRegion
AddUpdateSpecial
AddUpdateSpecialPrice
AddUpdateSpecialProductForBulkupload
AddUpdateTestimonial
AddUpdateWeightBand
addUpdShippingCost
AddUpdTrackingOrder
changePasswordByID
cloneNewprdAttributesbyOldPrdID
customerLoginChk
customerLoginChk_crm
CustomInsert
CustomUpdate
DeleteandinsertProductAttriutes_ByTempleteID
DeletebyID
DeletebyWhere
delLinkOpt2Prd
delLinkPrd2Brand
delLinkPrd2Cat
delLinkPrd2content
delLinkPrdOption
findandreplaceIngredient
getActiveBrand_level_1
getActiveCategories_level_1
getActiveCategories_level_1_2_franchise
getActiveCategories_level_1_ForWholesaleCustomer
getActiveCategories_level_1WithproductFilter
getActiveCategories_level_1WithproductFilter_all

(50 rows affected)
```

## Backup File Info
Path: G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\databases\cakerstreet db may 13 bak\db_cakerstreet_live_may13_26.bak
Size: 11.84 GB | Date: 2026-05-13
VERIFYONLY result: EXIT 0 (confirmed valid)

## Proposed Restore Command (DO NOT EXECUTE — awaiting challenger approval)
```sql
RESTORE DATABASE [db_cakerstreet_live_legacy_uat]
FROM DISK = N'G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\databases\cakerstreet db may 13 bak\db_cakerstreet_live_may13_26.bak'
WITH
  MOVE N'db_cakerstreet' TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\db_cakerstreet_live_legacy_uat.mdf',
  MOVE N'db_cakerstreet_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\db_cakerstreet_live_legacy_uat_log.ldf',
  STATS = 10;
```
Note: Creates NEW database, does NOT modify or drop db_cakerstreet_live.
