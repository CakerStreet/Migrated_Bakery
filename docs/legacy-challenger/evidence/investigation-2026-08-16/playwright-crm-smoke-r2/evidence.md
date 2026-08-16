# CRM Smoke Test Round 2 - After CS0433 Fix (msSQLDLL == ApplicationBlocks, cache cleared)

Captured: 2026-08-16T15:47:48.617Z

## Console Logs
[ERROR] Failed to load resource: net::ERR_SOCKET_NOT_CONNECTED
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)
[ERROR] Failed to load resource: the server responded with a status of 404 (Not Found)

## All Failed Requests
FAIL GET https://ajax.googleapis.com/ajax/libs/jquery/1.11.0/jquery.min.js — net::ERR_SOCKET_NOT_CONNECTED
FAIL GET http://localhost:27195/ckeditor/ckeditor.js?t=C6HH5UF — net::ERR_ABORTED

## All Responses (first 30)
200 http://localhost:27195/crflist_forsalesperson.aspx
200 http://localhost:27195/js/jstoppendingalerts.js?ver=-1
200 http://localhost:27195/js/AjaxFileupload.js
200 http://localhost:27195/images/loading_wall.gif
200 http://localhost:27195/zoom/slimbox2.js
200 http://localhost:27195/images/logo.png
200 http://localhost:27195/Content/AjaxControlToolkit/Styles/Bundle?v=R9gnEpFHVr2FwcnHV48QL3EztrfoY4L7LoeDs5V9zqM1
200 http://code.jquery.com/ui/1.12.1/themes/base/jquery-ui.css
301 http://ajax.aspnetcdn.com/ajax/4.6/1/WebForms.js
301 http://ajax.aspnetcdn.com/ajax/4.6/1/MicrosoftAjaxWebForms.js
301 http://ajax.aspnetcdn.com/ajax/4.6/1/WebUIValidation.js
301 http://ajax.aspnetcdn.com/ajax/4.6/1/MicrosoftAjax.js
200 http://localhost:27195/js/jsCRFquote.js?ver=1.11
200 http://code.jquery.com/jquery-1.12.4.min.js
200 http://code.jquery.com/ui/1.12.1/jquery-ui.js
200 https://cakerstreet1.s3.amazonaws.com/crm_content/css/bootstrap.min.css
200 https://cakerstreet1.s3.amazonaws.com/crm_content/css/font-awesome.min.css?version=1.0
200 https://cakerstreet1.s3.amazonaws.com/crm_content/zoom/css/slimbox2.css?v=1.1
200 https://cakerstreet1.s3.amazonaws.com/crm_content/css/style.css?ver=1.02
200 https://cakerstreet1.s3.amazonaws.com/crm_content/img/BackToTop_Arrow.png
200 https://cakerstreet1.s3.amazonaws.com/crm_content/css/crf.css?version=1.19
200 http://localhost:27195/Admin_Content/js/bootstrap.min.js
200 http://localhost:27195/js/CustomFunction.js?version=1.124
200 http://localhost:27195/js/globaljs.js?version=1.3
200 https://cakerstreet1.s3.amazonaws.com/crm_content/images/loading_wall.gif
200 http://localhost:27195/Scripts/AjaxControlToolkit/Bundle?v=YXFBohx9Lv9-MWoC3I0SFJfAPgFZRs-JoKzMK2zrylc1
301 http://ajax.aspnetcdn.com/ajax/act/18_1_0/Scripts/AjaxControlToolkit/Release/Compat.DragDrop.js
301 http://ajax.aspnetcdn.com/ajax/act/18_1_0/Scripts/AjaxControlToolkit/Release/FloatingBehavior.js
200 https://cdnsrc.asp.net/ajax/4.6/1/WebForms.js
200 https://cdnsrc.asp.net/ajax/4.6/1/WebUIValidation.js