# CRM Smoke Test — Post BREAK-RUNTIME-002 Fix

Captured: 2026-08-16T15:42:40.127Z
Fix applied: Microsoft.ApplicationBlocks.Data.dll.exclude → .dll (SHA 8AA2BAE7...)

## Console
[ERROR] Failed to load resource: the server responded with a status of 500 (Internal Server Error)

## Failed Requests
FAIL GET http://localhost:27195/quotations — net::ERR_ABORTED
FAIL GET http://localhost:27195/crm/crf.aspx?id=51216 — net::ERR_ABORTED

## All Responses (first 50)
500 http://localhost:27195/crflist_forsalesperson.aspx