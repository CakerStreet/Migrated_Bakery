# frontend-27203 Smoke Test
Captured: 2026-08-16T15:59:53.315Z

| URL | HTTP | Title | Error |
|---|---|---|---|
| http://localhost:27203/ | 200 | Birthday Cakes | for Kids | for Him | for Her | Ca | false |
| http://localhost:27203/category/birthday-cakes | 404 | The resource cannot be found. | true |
| http://localhost:27203/category/wedding-cakes | 404 | The resource cannot be found. | true |
| http://localhost:27203/quotation | 200 | Caker Street :: Customer Requirement Form | false |
| http://localhost:27203/login | 200 | Birthday Cakes | for Kids | for Him | for Her | Ca | false |
| http://localhost:27203/contact-us | 200 | Birthday Cakes | for Kids | for Him | for Her | Ca | false |

## Console
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[WARNING] jQuery.Deferred exception: a(...).parents(...).andSelf is not a function TypeError: a(...).parents(...).andSelf is not a function
    at Object.parse (http://localhost:27203/Scripts/jquery.validate.unobtrusive.min.js:5:2483)
    at HTMLDocument.<anonymous> (http://localhost:27203/Scripts/jquery.validate.unobtrusive.min.js:5:4557)
    at e (https://code.jquery.com/jquery-3.7.1.min.js:2:27028)
    at t (https://code.jquery.com/jquery-3.7.1.min.js:2:27330) undefined
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[WARNING] An iframe which has both allow-scripts and allow-same-origin for its sandbox attribute can escape its sandboxing.
[INFO] Loading the script 'https://www.gstatic.com/_/mss/boq-identity/_/js/k=boq-identity.IdpIFrameHttp.en_US.bkANOIYw25w.2018.O/am=BESFAw/d=1/rs=AOaEmlFDRj6EHDvyD11ULfOwQ1W3Stj8wQ/m=base' violates the following Content Security Policy directive: "script-src 'unsafe-inline' 'unsafe-eval' blob: data:". Note that 'script-src-elem' was not explicitly set, so 'script-src' is used as a fallback. The policy is report-only, so the violation has been logged but no further action has been taken.
[ERROR] requestStorageAccess: Permission denied.
[ERROR] Failed to load resource: net::ERR_CONNECTION_REFUSED
[ERROR] Failed to load resource: net::ERR_CONNECTION_REFUSED
[LOG] init Client 5
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] requestStorageAccess: Permission denied.
[ERROR] Framing 'https://www.google.com/' violates the following report-only Content Security Policy directive: "frame-ancestors 'self'". The violation has been logged, but no further action has been taken.


## Failed Requests
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/construction-birthday-cake-6604.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/strings-hearts-valentines-cake-7310-537ccfb4f.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/cars-cake-7769.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/cars-cake-7765.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/harry-potter-cake-9182.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/unicorn-theme-cake-9267.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/little-miss-cake-9581.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/hermes-cake-14518-4ae091136.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/anniversary-cake-16026-764f8700d.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/gucci-cake-20197-980eed645.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/14th-birthday-cake-for-girls-51538-0e108ad95.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/i-love-you-heart-cupcakes-59370.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/elegant-angel-wing-birthday-cupcakes-with-gold-cross-59399.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/floral-cross-confirmation-cupcakes-59474.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/pink-gift-box-birthday-cupcakes-59593.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/tiara-cake-13569-6dff0ea25.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/festive-black-white-new-year-cupcakes-59654.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/baby-booties-flowers-christening-cupcakes-for-girls-59930.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/romantic-heart-cupcakes---i-will-never-stop-loving-you-60386.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/graduation-celebration-cupcakes-with-cap-bow-58623.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/elegant-engagement-cupcakes-with-heart-toppers-59690.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/romantic-rose-heart-birthday-cupcakes-for-her-58591.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/elegant-yellow-glaze-birthday-cupcakes-with-chocolate-decor-58424.jpg
FAIL GET http://localhost:27195/upload/Product_images/resized_300_300/classic-walnut-birthday-cupcakes-58403.jpg
FAIL GET http://localhost:27203/content/autocomplete/jquery-ui.min.css
FAIL GET http://localhost:27203/Admin_Content/js/bootstrap.min.js
FAIL GET http://localhost:27203/content/autocomplete/jquery-ui.min.css
FAIL GET http://localhost:27203/Admin_Content/js/bootstrap.min.js
FAIL POST https://csp.withgoogle.com/csp/IdpIFrameHttp/fine-allowlist
FAIL GET http://localhost:8085/dist/cabl.json
FAIL GET http://localhost:8085/banner-contract.js
FAIL GET http://localhost:27203/content/autocomplete/jquery-ui.min.css
FAIL GET http://localhost:27203/Admin_Content/js/bootstrap.min.js
FAIL POST https://csp.withgoogle.com/csp/frame-ancestors/38fac9d5b82543fc4729580d18ff2d3d