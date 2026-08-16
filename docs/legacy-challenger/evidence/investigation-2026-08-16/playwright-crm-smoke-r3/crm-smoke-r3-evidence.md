# CRM Smoke Test Round 3 - Post BREAK-RUNTIME-002 fix + cache clear

Captured: 2026-08-16T15:50:14.154Z

## Page Results
| URL | HTTP | Title | CompileErr | RuntimeErr |
|---|---|---|---|---|
| http://localhost:27195/quotations | 200 | Customer Requirement Form | false | false |
| http://localhost:27195/quotations/51216 | 500 | DataBinding: '<>f__AnonymousTypeb`29[[System.DateTime, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Boolean, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Int64, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Int64, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Decimal, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089],[System.Decimal, mscorlib, Version=4.0.0.0, Culture=neutral, Publi...' does not contain a property with the name 'CakeShapeTitle'. | true | false |
| http://localhost:27195/quotations?pendinglead=1 | 200 | Customer Requirement Form | false | false |
| http://localhost:27195/crflist_forsBakery.aspx | 404 | The resource cannot be found. | true | false |
| http://localhost:27195/login | 200 | Caker Street :: CRM Control Panel | false | false |
| http://localhost:27195/ | 403 |  | false | false |

## Console
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 500 (Internal Server Error)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 403 (Forbidden)

## Failed Requests (first 20)
FAIL GET http://localhost:27195/ckeditor/ckeditor.js?t=C6HH5UF
FAIL GET http://localhost:27195/ckeditor/ckeditor.js?t=C6HH5UF