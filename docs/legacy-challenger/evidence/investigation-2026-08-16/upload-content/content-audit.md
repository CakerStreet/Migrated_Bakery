# Upload and Content Forensic Audit

Key finding: Frontend (27203) loads product images from http://localhost:27195/upload/Product_images/
CRM physical path has NO /upload/ folder. All product images blocked/404.

## Upload Folder Inventory

### CRM-27195
Root: G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM

| Folder | Exists | Files | Size MB |
|---|---|---|---|
| upload | NO | - | - |
| uploads | NO | - | - |
| Product_images | NO | - | - |
| App_Data | YES | 1 | 0 |
| images | YES | 157 | 1.37 |
| content | YES | 222 | 0.84 |
| temp | NO | - | - |

### BizPortal-27201
Root: G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source

| Folder | Exists | Files | Size MB |
|---|---|---|---|
| upload | YES | 0 | 0 |
| uploads | NO | - | - |
| Product_images | NO | - | - |
| App_Data | NO | - | - |
| images | NO | - | - |
| content | YES | 222 | 0.84 |
| temp | YES | 0 | 0 |

### Frontend-27203
Root: G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet

| Folder | Exists | Files | Size MB |
|---|---|---|---|
| upload | YES | 450 | 21.77 |
| uploads | NO | - | - |
| Product_images | NO | - | - |
| App_Data | NO | - | - |
| images | YES | 174 | 1.51 |
| content | YES | 16 | 0.11 |
| temp | NO | - | - |

### EPOSTerm-27210
Root: G:\AI-Projects\CRM-Recovery\epos_2026\epos.cakerstreet.com

| Folder | Exists | Files | Size MB |
|---|---|---|---|
| upload | NO | - | - |
| uploads | NO | - | - |
| Product_images | NO | - | - |
| App_Data | YES | 0 | 0 |
| images | YES | 16 | 0.03 |
| content | NO | - | - |
| temp | NO | - | - |

### EPOSAdmin-27211
Root: G:\AI-Projects\CRM-Recovery\epos_2026\eposadmin.cakerstreet.com

| Folder | Exists | Files | Size MB |
|---|---|---|---|
| upload | YES | 8 | 2.23 |
| uploads | NO | - | - |
| Product_images | NO | - | - |
| App_Data | NO | - | - |
| images | YES | 27 | 0.1 |
| content | YES | 124 | 1.98 |
| temp | NO | - | - |

## Product_images Found Across All Source Trees

| Location | Files | IIS Serving? |
|---|---|---|
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\upload\Product_images | 446 | YES at 27203 |
| G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM_backup_pre_recovery\upload\Product_images | 96 | NO |
| G:\AI-Projects\CRM-Recovery\epos_2026\epos.cakerstreet.com\cp\upload\Product_images | 4 | NO |
| G:\AI-Projects\CRM-Recovery\epos_2026\eposadmin.cakerstreet.com\upload\Product_images | 4 | NO |
