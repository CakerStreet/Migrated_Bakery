# BREAK-CONTENT-001 — CRM Missing /upload/Product_images/

## Observed Symptom
Playwright captured failed network requests from Frontend (27203):
  net::ERR_BLOCKED_BY_ORB
  http://localhost:27195/upload/Product_images/resized_300_300/cars-cake-7769.jpg

Product images on the Frontend are loaded via the CRM's URL (port 27195).
The CRM physical path has NO /upload/ folder.

## Current IIS Physical Path (CRM)
G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM

## Missing Content
G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\upload\ (DOES NOT EXIST)
G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\upload\Product_images\ (DOES NOT EXIST)

## Candidate Source
G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\upload\Product_images\
  - 446 image files
  - This is the Frontend (27203) IIS-served tree
  - These files were likely copied from the live server into the Frontend source tree

## Root-Cause Hypothesis (HYPOTHESIS)
When legacy files were recovered from the server, the /upload/Product_images/ folder
was placed into the Frontend source tree (vs-test\cakerstreet\) rather than into
the CRM source tree (cakerstreet_CRM\). IIS serves these images via the CRM URL
on production, so they should be in the CRM physical path.

## Challenger Question
Is the 446-file set in vs-test\cakerstreet\upload\Product_images\ the complete
production set, or does the live server have more images? The CRM pre-recovery
backup has only 96 images — suggesting the 446 set is not complete either.

## Proposed Fix
CURRENT IIS PATH: G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\
CANDIDATE SOURCE: G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\upload\
DESTINATION:      G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\upload\
METHOD:           robocopy /E /COPYALL
FILE COUNT:       446 files
RISK:             LOW (read-only content, no code change)
STATUS:           AWAITING CHALLENGER APPROVAL

## Rollback
Delete G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\upload\