# BREAK-DB-006 — db_cakerstreet_live BatchThree Contamination

## Observed Symptom
CRM CRF list at http://localhost:27195/quotations?pendinglead=1 shows CRF IDs
1000015 and 1000016 — synthetic BatchThree test fixtures, not real historical data.

## Playwright Evidence
Manual browser screenshot from 2026-08-14 shows BatchThree records in the list.

## Active Database
Server: localhost
Database: db_cakerstreet_live
Auth: Windows Integrated Security

## Contamination Details
- 28 CRF records with IDs >= 1,000,000 (BatchThree synthetic fixtures)
- Real historical CRFs have IDs in the range ~1–60,000+
- MAX historical CRF ID: ~51,216 (representative CRF used for evidence)

## Backup Available
Path: G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\databases\cakerstreet db may 13 bak\db_cakerstreet_live_may13_26.bak
VERIFYONLY result: EXIT 0 (backup is valid and restorable)
Backup date: 2026-05-13

## Root-Cause Hypothesis (HYPOTHESIS)
A BatchThree testing phase inserted synthetic CRF records directly into
db_cakerstreet_live rather than a separate test database. These records were
never cleaned up before the backup was taken, OR the contamination happened
after the May-13 backup and was introduced locally during testing.

## Proposed Fix
RESTORE DATABASE [db_cakerstreet_live_legacy_uat]
FROM DISK = N'G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\databases\cakerstreet db may 13 bak\db_cakerstreet_live_may13_26.bak'
WITH
  MOVE N'db_cakerstreet' TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\db_cakerstreet_live_legacy_uat.mdf',
  MOVE N'db_cakerstreet_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\db_cakerstreet_live_legacy_uat_log.ldf',
  STATS = 10;

Then update CRM web.config: db_handicraftEntities -> db_cakerstreet_live_legacy_uat

## Risk of Fix
LOW — creates a NEW database, does not modify or drop db_cakerstreet_live.
Rollback: revert web.config connectionString.

## Rollback
Revert web.config connection string to db_cakerstreet_live.