# 24 Failed Image Filename Location Matrix
Captured: 2026-08-16

| Filename | frontend-vs-test | crm-backup-pre | live-backup | 114_server | Cakerstreet.com | epos-cp |
|---|---|---|---|---|---|---|
| construction-birthday-cake-6604.jpg | NO | NO | NO | NO | NO | NO |
| cars-cake-7765.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| unicorn-theme-cake-9267.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| little-miss-cake-9581.jpg | NO | NO | NO | NO | NO | NO |
| strings-hearts-valentines-cake-7310-537ccfb4f.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| cars-cake-7769.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| tiara-cake-13569-6dff0ea25.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| gucci-cake-20197-980eed645.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| harry-potter-cake-9182.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| classic-walnut-birthday-cupcakes-58403.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| romantic-rose-heart-birthday-cupcakes-for-her-58591.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| graduation-celebration-cupcakes-with-cap-bow-58623.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| i-love-you-heart-cupcakes-59370.jpg | YES (resized_500_500) | YES (resized_300_300) | NO | NO | NO | NO |
| elegant-angel-wing-birthday-cupcakes-with-gold-cross-59399.jpg | YES (resized_500_500) | YES (resized_300_300) | NO | NO | NO | NO |
| floral-cross-confirmation-cupcakes-59474.jpg | YES (resized_500_500) | YES (resized_300_300) | NO | NO | NO | NO |
| pink-gift-box-birthday-cupcakes-59593.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| festive-black-white-new-year-cupcakes-59654.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| elegant-engagement-cupcakes-with-heart-toppers-59690.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| romantic-heart-cupcakes---i-will-never-stop-loving-you-60386.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| hermes-cake-14518-4ae091136.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| anniversary-cake-16026-764f8700d.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| 14th-birthday-cake-for-girls-51538-0e108ad95.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| elegant-yellow-glaze-birthday-cupcakes-with-chocolate-decor-58424.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |
| baby-booties-flowers-christening-cupcakes-for-girls-59930.jpg | YES (resized_300_300) | YES (resized_300_300) | NO | NO | NO | NO |

## Summary
- Total failed images: 24
- Found in at least one tree: 22
- Not found anywhere: 2

## Architecture Recommendation
PENDING upload-architecture.md analysis. Files may exist but ERR_BLOCKED_BY_ORB
indicates the real issue is authentication, not file presence.
IIS Express at 27195 requires Windows Auth. /upload/ static files are not exempt.

Recommended architecture:
Option A: Add a <location path='upload'> element in CRM web.config granting anonymous access
          to static content under /upload/ while keeping Windows Auth for ASPX paths.
Option B: Add a separate IIS Express site (anonymous, no .NET, just static files)
          serving the upload folder on its own port.
Option C: Move product images to Frontend (27203) origin — serve from same-origin.

Challenger must approve architecture choice before any files are copied.
