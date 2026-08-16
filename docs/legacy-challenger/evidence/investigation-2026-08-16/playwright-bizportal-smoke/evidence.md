# Business Portal 27201 Smoke Test

Captured: 2026-08-16T15:51:51.497Z

## Results
| URL | HTTP | Title | Error |
|---|---|---|---|
| http://localhost:27201/ | 200 | Caker Street :: Business Control Panel | false |
| http://localhost:27201/login | 200 | Caker Street :: Business Control Panel | false |
| http://localhost:27201/dashboard | 200 | Caker Street :: Business Control Panel | false |
| http://localhost:27201/orders | 404 | The resource cannot be found. | true |
| http://localhost:27201/products | 404 | The resource cannot be found. | true |
| http://localhost:27201/staff | 404 | The resource cannot be found. | true |

## Console
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)

## Failed Requests
FAIL GET http://localhost:27201/includes/clientScripts.js
FAIL GET http://localhost:27201/includes/clientScripts.js
FAIL GET http://localhost:27201/includes/clientScripts.js