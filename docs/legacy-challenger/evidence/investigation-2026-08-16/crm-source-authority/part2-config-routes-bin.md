# CRM Source Tree Authority — Part 2: Config, Routes, Master Pages, BIN, PATCH-001
Captured: 2026-08-16

## 3. Global.asax / Route Registration

### cakerstreet_CRM
**Global.asax SHA-256:** 6E0CFB67ACA217A167441F30747E0A8D271041A68037D7B28327E440522CBA5E
**Routes/handlers found:**
  - BundleConfig.RegisterBundles(BundleTable.Bundles);
  - // RouteTable.Routes.MapHubs();
  - RegisterRoutes(RouteTable.Routes);
  - void RegisterRoutes(RouteCollection routes)
  - routes.MapPageRoute(
  - "",             // Route name
  - "enhancedimages",   // Route URL
  - "~/enhancedimages.aspx"        // Web page to handle route
  - routes.MapPageRoute(
  - ������������������������������������ "",������������ // Route name

### 114_server
**Global.asax SHA-256:** AA5DA6A973BAFD2B9D15DA0CEA1B9EB70841CE017EC367ECBE6C5F0A8534AD4C
**Routes/handlers found:**
**Global.asax.cs SHA-256:** BCD259BA1078F6784A7E21D73CC7A78A5F1701DC1FC8B7F3C75601E84AF4F1CE

### 114_server_crm_full_runtime
**Global.asax SHA-256:** 6E0CFB67ACA217A167441F30747E0A8D271041A68037D7B28327E440522CBA5E
**Routes/handlers found:**
  - BundleConfig.RegisterBundles(BundleTable.Bundles);
  - // RouteTable.Routes.MapHubs();
  - RegisterRoutes(RouteTable.Routes);
  - void RegisterRoutes(RouteCollection routes)
  - routes.MapPageRoute(
  - "",             // Route name
  - "enhancedimages",   // Route URL
  - "~/enhancedimages.aspx"        // Web page to handle route
  - routes.MapPageRoute(
  - ������������������������������������ "",������������ // Route name

### legacy_crm
**Global.asax SHA-256:** 6E0CFB67ACA217A167441F30747E0A8D271041A68037D7B28327E440522CBA5E
**Routes/handlers found:**
  - BundleConfig.RegisterBundles(BundleTable.Bundles);
  - // RouteTable.Routes.MapHubs();
  - RegisterRoutes(RouteTable.Routes);
  - void RegisterRoutes(RouteCollection routes)
  - routes.MapPageRoute(
  - "",             // Route name
  - "enhancedimages",   // Route URL
  - "~/enhancedimages.aspx"        // Web page to handle route
  - routes.MapPageRoute(
  - ������������������������������������ "",������������ // Route name

## 4. Master Pages

| Master Page | cakerstreet_CRM | 114_server | 114_server_crm_full_runtime | legacy_crm |
|---|---|---|---|---|
| AdminMaster.master | 0A722D925E | 0A722D925E | 0A722D925E | 0A722D925E |
| BakeryMaster.master | MISSING | 652E05D7CE | MISSING | MISSING |
| FranchiseMaster.master | 32B049EA2B | 32B049EA2B | 32B049EA2B | 32B049EA2B |
| Home_default.master | 03D67AAB3F | 03D67AAB3F | 03D67AAB3F | 03D67AAB3F |
| Home.master | 689FA9635E | 689FA9635E | 689FA9635E | 689FA9635E |
| staffmaster.master | C0007E7286 | C0007E7286 | C0007E7286 | C0007E7286 |
| User.master | 1960F45247 | 1960F45247 | 1960F45247 | 1960F45247 |

## 5. Web.config appSettings Key Comparison

| Key | cakerstreet_CRM | 114_server | 114_server_crm_full_runtime | legacy_crm |
|---|---|---|---|---|
| accessorycustid | ✅ | MISSING | ✅ | ✅ |
| accessoryimgpath | ✅ | MISSING | ✅ | ✅ |
| accessoryimgpath_lrg | ✅ | MISSING | ✅ | ✅ |
| accessorywebstoreid | ✅ | MISSING | ✅ | ✅ |
| AccountEmail | ✅ | MISSING | ✅ | ✅ |
| adminCookieName | ✅ | MISSING | ✅ | ✅ |
| adminCRMuserIDs | ✅ | MISSING | ✅ | ✅ |
| amazonaccesskey | ✅ | MISSING | ✅ | ✅ |
| amazonbucket | ✅ | MISSING | ✅ | ✅ |
| amazonfolder | ✅ | MISSING | ✅ | ✅ |
| amazonlink | ✅ | MISSING | ✅ | ✅ |
| amazonlink_aws | ✅ | MISSING | ✅ | ✅ |
| amazonsecretkey | ✅ | MISSING | ✅ | ✅ |
| APC_EmailId | ✅ | MISSING | ✅ | ✅ |
| APC_EmailId_stockport | ✅ | MISSING | ✅ | ✅ |
| APC_Label_Endpoint | ✅ | MISSING | ✅ | ✅ |
| APC_OrderCancel_Endpoint | ✅ | MISSING | ✅ | ✅ |
| APC_Orders_Endpoint | ✅ | MISSING | ✅ | ✅ |
| APC_OrderTracking_Endpoint | ✅ | MISSING | ✅ | ✅ |
| APC_Password | ✅ | MISSING | ✅ | ✅ |
| APC_Password_stockport | ✅ | MISSING | ✅ | ✅ |
| APC_ServiceAvailability_Endpoint | ✅ | MISSING | ✅ | ✅ |
| API_AUTHENTICATION_MODE | ✅ | MISSING | ✅ | ✅ |
| API_PASSWORD | ✅ | MISSING | ✅ | ✅ |
| API_REQUESTFORMAT | ✅ | MISSING | ✅ | ✅ |
| API_RESPONSEFORMAT | ✅ | MISSING | ✅ | ✅ |
| API_SIGNATURE | ✅ | MISSING | ✅ | ✅ |
| API_USERNAME | ✅ | MISSING | ✅ | ✅ |
| apiDESTINATION | ✅ | MISSING | ✅ | ✅ |
| APPLICATION-ID | ✅ | MISSING | ✅ | ✅ |
| ApplicationPath | ✅ | MISSING | ✅ | ✅ |
| aspnet:MaxHttpCollectionKeys | ✅ | MISSING | ✅ | ✅ |
| aspnet:MaxJsonDeserializerMembers | ✅ | MISSING | ✅ | ✅ |
| Associatetag | ✅ | MISSING | ✅ | ✅ |
| AWS_ID | ✅ | MISSING | ✅ | ✅ |
| AWS_SECRET | ✅ | MISSING | ✅ | ✅ |
| AwsEmailAccessKeyID | ✅ | MISSING | ✅ | ✅ |
| AwsEmailSecretKey | ✅ | MISSING | ✅ | ✅ |
| bakeryCookieName | ✅ | MISSING | ✅ | ✅ |
| bannerthumbsize | ✅ | MISSING | ✅ | ✅ |
| bccToMail | ✅ | MISSING | ✅ | ✅ |
| BraintreeEnvironment | ✅ | MISSING | ✅ | ✅ |
| BraintreeMerchantId | ✅ | MISSING | ✅ | ✅ |
| BraintreePrivateKey | ✅ | MISSING | ✅ | ✅ |
| BraintreePublicKey | ✅ | MISSING | ✅ | ✅ |
| business_website | ✅ | MISSING | ✅ | ✅ |
| business_websiteLogo | ✅ | MISSING | ✅ | ✅ |
| BusinessEmail | ✅ | MISSING | ✅ | ✅ |
| BusinessPhone | ✅ | MISSING | ✅ | ✅ |
| businesswebsite_PhysicalApplicationPath | ✅ | MISSING | ✅ | ✅ |
| businesswebsite_URL | ✅ | MISSING | ✅ | ✅ |
| CancelPurchaseConnectUrl | ✅ | MISSING | ✅ | ✅ |
| ccToMail | ✅ | MISSING | ✅ | ✅ |
| cdnLink | ✅ | MISSING | ✅ | ✅ |
| ckCakerStreetUserLocation | ✅ | MISSING | ✅ | ✅ |
| ckCakerStreetUserOccasion | ✅ | MISSING | ✅ | ✅ |
| ckwebstorecustid | ✅ | MISSING | ✅ | ✅ |
| ckwebstoreid | ✅ | MISSING | ✅ | ✅ |
| Company_Address | ✅ | MISSING | ✅ | ✅ |
| Company_Name | ✅ | MISSING | ✅ | ✅ |
| Company_No | ✅ | MISSING | ✅ | ✅ |
| Company_postcode | ✅ | MISSING | ✅ | ✅ |
| constr | ✅ | MISSING | ✅ | ✅ |
| constr_crm | ✅ | MISSING | ✅ | ✅ |
| constr_epos | ✅ | MISSING | ✅ | ✅ |
| constr_eposadmin | ✅ | MISSING | ✅ | ✅ |
| constr_InventoryManagement | ✅ | MISSING | ✅ | ✅ |
| constr_koolcake | ✅ | MISSING | ✅ | ✅ |
| continueShoppingURL | ✅ | MISSING | ✅ | ✅ |
| crm_PhysicalApplicationPath | ✅ | MISSING | ✅ | ✅ |
| CRMCookieName | ✅ | MISSING | ✅ | ✅ |
| currency | ✅ | MISSING | ✅ | ✅ |
| CurrencyCode | ✅ | MISSING | ✅ | ✅ |
| customer_website | ✅ | MISSING | ✅ | ✅ |
| customer_websiteLogo | ✅ | MISSING | ✅ | ✅ |
| customImagesearch_googleApiKey | ✅ | MISSING | ✅ | ✅ |
| customImagesearch_searchEngineID | ✅ | MISSING | ✅ | ✅ |
| custwebsite_PhysicalApplicationPath | ✅ | MISSING | ✅ | ✅ |
| deviceId | ✅ | MISSING | ✅ | ✅ |
| DHL_LoginID | ✅ | MISSING | ✅ | ✅ |
| DHL_MeterNo | ✅ | MISSING | ✅ | ✅ |
| DHL_Password | ✅ | MISSING | ✅ | ✅ |
| encryptionStringkey | ✅ | MISSING | ✅ | ✅ |
| ENDPOINT | ✅ | MISSING | ✅ | ✅ |
| Epos_websiteURL | ✅ | MISSING | ✅ | ✅ |
| FacebookAppId | ✅ | MISSING | ✅ | ✅ |
| FacebookAppSecret | ✅ | MISSING | ✅ | ✅ |
| FB_AppID | ✅ | MISSING | ✅ | ✅ |
| FB_AppKey | ✅ | MISSING | ✅ | ✅ |
| fbwhatsapp_accesstoken | ✅ | MISSING | ✅ | ✅ |
| fbwhatsapp_BusinessAccountID | ✅ | MISSING | ✅ | ✅ |
| fbwhatsapp_PhonenumberID | ✅ | MISSING | ✅ | ✅ |
| fbwhatsapp_testnumber | ✅ | MISSING | ✅ | ✅ |
| FCKeditor:BasePath | ✅ | MISSING | ✅ | ✅ |
| FCKeditor:ToolbarSet | ✅ | MISSING | ✅ | ✅ |
| franchiseuserCookieName | ✅ | MISSING | ✅ | ✅ |
| fromMail | ✅ | MISSING | ✅ | ✅ |
| fromName | ✅ | MISSING | ✅ | ✅ |
| fromOrderMail | ✅ | MISSING | ✅ | ✅ |
| GoogleApiKey | ✅ | MISSING | ✅ | ✅ |
| GoogleClientID | ✅ | MISSING | ✅ | ✅ |
| GoogleClientSecret | ✅ | MISSING | ✅ | ✅ |
| googlecloneprd_cupcake | ✅ | MISSING | ✅ | ✅ |
| googleGemini_apikey | ✅ | MISSING | ✅ | ✅ |
| GoogleRecaptchaSecretKey | ✅ | MISSING | ✅ | ✅ |
| GoogleRecaptchaSiteKey | ✅ | MISSING | ✅ | ✅ |
| guestCookieName | ✅ | MISSING | ✅ | ✅ |
| GuestCRF_CookieName | ✅ | MISSING | ✅ | ✅ |
| IconSolutions_APIKey | ✅ | MISSING | ✅ | ✅ |
| infoMail | ✅ | MISSING | ✅ | ✅ |
| ipAddress | ✅ | MISSING | ✅ | ✅ |
| islocal | ✅ | MISSING | ✅ | ✅ |
| klarna_baseurl | ✅ | MISSING | ✅ | ✅ |
| klarna_password | ✅ | MISSING | ✅ | ✅ |
| klarna_username | ✅ | MISSING | ✅ | ✅ |
| language_googleApiKey | ✅ | MISSING | ✅ | ✅ |
| Mailto_dynamic | ✅ | MISSING | ✅ | ✅ |
| merchantID | ✅ | MISSING | ✅ | ✅ |
| merchantKey | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpBounceEmail | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpClient | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpEmail | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpinfoEmail | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpisSSL | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpport | ✅ | MISSING | ✅ | ✅ |
| newslettersmtpPwd | ✅ | MISSING | ✅ | ✅ |
| NotifyConnectUrl | ✅ | MISSING | ✅ | ✅ |
| Path | ✅ | MISSING | ✅ | ✅ |
| PAYPAL_REDIRECT_URL | ✅ | MISSING | ✅ | ✅ |
| paypalclientID | ✅ | MISSING | ✅ | ✅ |
| paypalclientSecret | ✅ | MISSING | ✅ | ✅ |
| PaypalEmail | ✅ | MISSING | ✅ | ✅ |
| paypalmode | ✅ | MISSING | ✅ | ✅ |
| paypalmode_atIPN | ✅ | MISSING | ✅ | ✅ |
| PaypalPayMode | ✅ | MISSING | ✅ | ✅ |
| PaypalPrimaryEmailID | ✅ | MISSING | ✅ | ✅ |
| paypalTSD | ✅ | MISSING | ✅ | ✅ |
| paypalTSDPlusfixed | ✅ | MISSING | ✅ | ✅ |
| PhysicalApplicationPath | ✅ | MISSING | ✅ | ✅ |
| PhysicalApplicationPath_crm | ✅ | MISSING | ✅ | ✅ |
| PhysicalApplicationPath_fileupload | ✅ | MISSING | ✅ | ✅ |
| PhysicalApplicationPath_fileupload_crm | ✅ | MISSING | ✅ | ✅ |
| PostcodeAccount | ✅ | MISSING | ✅ | ✅ |
| postcodeanywhere.lookup | ✅ | MISSING | ✅ | ✅ |
| PostcodeDefaltPostcode | ✅ | MISSING | ✅ | ✅ |
| PostcodeLicence | ✅ | MISSING | ✅ | ✅ |
| prodthumbsize | ✅ | MISSING | ✅ | ✅ |
| profilethumbsize | ✅ | MISSING | ✅ | ✅ |
| Prompt_Pin | ✅ | MISSING | ✅ | ✅ |
| recentview_CookieName | ✅ | MISSING | ✅ | ✅ |
| ReturnConnectUrl | ✅ | MISSING | ✅ | ✅ |
| reviewver | ✅ | MISSING | ✅ | ✅ |
| SendToReturnURL | ✅ | MISSING | ✅ | ✅ |
| siteURL | ✅ | MISSING | ✅ | ✅ |
| siteURL_Logo | ✅ | MISSING | ✅ | ✅ |
| sms_accountSid | ✅ | MISSING | ✅ | ✅ |
| sms_authToken | ✅ | MISSING | ✅ | ✅ |
| sms_fromNumber | ✅ | MISSING | ✅ | ✅ |
| smtpClient | ✅ | MISSING | ✅ | ✅ |
| smtpClient_Order | ✅ | MISSING | ✅ | ✅ |
| smtpEmail | ✅ | MISSING | ✅ | ✅ |
| smtpEmail_Order | ✅ | MISSING | ✅ | ✅ |
| smtpinfoEmail | ✅ | MISSING | ✅ | ✅ |
| smtpisSSL | ✅ | MISSING | ✅ | ✅ |
| smtpisSSL_Order | ✅ | MISSING | ✅ | ✅ |
| smtpport | ✅ | MISSING | ✅ | ✅ |
| smtpport_Order | ✅ | MISSING | ✅ | ✅ |
| smtpPwd | ✅ | MISSING | ✅ | ✅ |
| smtpPwd_Order | ✅ | MISSING | ✅ | ✅ |
| smtpserver | ✅ | MISSING | ✅ | ✅ |
| StipePublishablekey | ✅ | MISSING | ✅ | ✅ |
| StipeSecretkey | ✅ | MISSING | ✅ | ✅ |
| Stripe_CakerstreetAccID | ✅ | MISSING | ✅ | ✅ |
| stripeTSD | ✅ | MISSING | ✅ | ✅ |
| stripeTSDPlusfixed | ✅ | MISSING | ✅ | ✅ |
| title | ✅ | MISSING | ✅ | ✅ |
| TrustAll | ✅ | MISSING | ✅ | ✅ |
| trustpilotmailbcc | ✅ | MISSING | ✅ | ✅ |
| UserAuthorizationCookieName | ✅ | MISSING | ✅ | ✅ |
| userCookieName | ✅ | MISSING | ✅ | ✅ |
| UseSandbox | ✅ | MISSING | ✅ | ✅ |
| vat_number | ✅ | MISSING | ✅ | ✅ |
| vat_orderpercentage | ✅ | MISSING | ✅ | ✅ |
| vendorEmail | ✅ | MISSING | ✅ | ✅ |
| vendorEncPwd | ✅ | MISSING | ✅ | ✅ |
| vendorEncPwdSimulator | ✅ | MISSING | ✅ | ✅ |
| vendorName | ✅ | MISSING | ✅ | ✅ |
| VirtualApplicationPath | ✅ | MISSING | ✅ | ✅ |
| websiteLink | ✅ | MISSING | ✅ | ✅ |
| websiteName | ✅ | MISSING | ✅ | ✅ |
| websiteNamewithExt | ✅ | MISSING | ✅ | ✅ |
| Whatsapp_fromNumber | ✅ | MISSING | ✅ | ✅ |
| Whatsapp_HRname | ✅ | MISSING | ✅ | ✅ |
| Whatsapp_HRphone | ✅ | MISSING | ✅ | ✅ |
| Whatsapp_toNumber_admin | ✅ | MISSING | ✅ | ✅ |
| ws_paypalclientID | ✅ | MISSING | ✅ | ✅ |
| ws_paypalclientSecret | ✅ | MISSING | ✅ | ✅ |
| ws_paypalmode | ✅ | MISSING | ✅ | ✅ |
| ws_paypalmode_atIPN | ✅ | MISSING | ✅ | ✅ |

## 6. PATCH-001: bool.Parse Changes

Search for bool.Parse in crflist_forsalesperson.aspx across all trees:

### cakerstreet_CRM (G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\crflist_forsalesperson.aspx)
bool.Parse matches: 36
  Line 355: <%#((Convert.ToBoolean(Eval("CRF_isKart")))?"":DateTime.Parse(Eval("CRF_datetime").ToString()).ToString("h:mm ttt")+" - "+DateTime.Parse(Eval("CRF_datetime").ToString()).AddHours(2).ToString("h:mm ttt"))%></span>
  Line 487: <div class='<%#"col-sm-4"+((Convert.ToBoolean(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 527: <div class='<%#"col-sm-4"+((!Convert.ToBoolean(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 572: runat="server" visible='<%#Convert.ToBoolean(Eval("CRF_isdelivery").ToString())%>'>
  Line 576: <div class="col-sm-12 deliveroption" runat="server" visible='<%#Convert.ToBoolean(Eval("CRF_iscollection").ToString())%>'>

### 114_server (G:\AI-Projects\Dev\kiro-cakerstreet-uk\114_server 1\114_server\docs\legacy-source\cakerstreet_CRM\crflist_forsalesperson.aspx)
bool.Parse matches: 36
  Line 355: <%#((bool.Parse(Eval("CRF_isKart").ToString()))?"":DateTime.Parse(Eval("CRF_datetime").ToString()).ToString("h:mm ttt")+" - "+DateTime.Parse(Eval("CRF_datetime").ToString()).AddHours(2).ToString("h:mm ttt"))%></span>
  Line 487: <div class='<%#"col-sm-4"+((bool.Parse(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 527: <div class='<%#"col-sm-4"+((!bool.Parse(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 572: runat="server" visible='<%#bool.Parse(Eval("CRF_isdelivery").ToString())%>'>
  Line 576: <div class="col-sm-12 deliveroption" runat="server" visible='<%#bool.Parse(Eval("CRF_iscollection").ToString())%>'>

### 114_server_crm_full_runtime (G:\AI-Projects\CRM-Recovery\114_server_crm_full_runtime\crflist_forsalesperson.aspx)
bool.Parse matches: 36
  Line 355: <%#((bool.Parse(Eval("CRF_isKart").ToString()))?"":DateTime.Parse(Eval("CRF_datetime").ToString()).ToString("h:mm ttt")+" - "+DateTime.Parse(Eval("CRF_datetime").ToString()).AddHours(2).ToString("h:mm ttt"))%></span>
  Line 487: <div class='<%#"col-sm-4"+((bool.Parse(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 527: <div class='<%#"col-sm-4"+((!bool.Parse(Eval("CRF_isKart").ToString()))?"":" hide") %>'>
  Line 572: runat="server" visible='<%#bool.Parse(Eval("CRF_isdelivery").ToString())%>'>
  Line 576: <div class="col-sm-12 deliveroption" runat="server" visible='<%#bool.Parse(Eval("CRF_iscollection").ToString())%>'>

### legacy_crm (G:\AI-Projects\Dev\antigravity-cakerstreet-migration\legacy\crm\crflist_forsalesperson.aspx)
bool.Parse matches: 36
  Line 355: <%#((Eval("CRF_isKart") != System.DBNull.Value && Convert.ToBoolean(Eval("CRF_isKart")))?"":DateTime.Parse(Eval("CRF_datetime").ToString()).ToString("h:mm ttt")+" - "+DateTime.Parse(Eval("CRF_datetime").ToString()).AddHours(2).ToString("h:mm ttt"))%></span>
  Line 487: <div class='<%#"col-sm-4"+((Eval("CRF_isKart") != System.DBNull.Value && Convert.ToBoolean(Eval("CRF_isKart")))?"":" hide") %>'>
  Line 527: <div class='<%#"col-sm-4"+(!(Eval("CRF_isKart") != System.DBNull.Value && Convert.ToBoolean(Eval("CRF_isKart")))?"":" hide") %>'>
  Line 572: runat="server" visible='<%#(Eval("CRF_isdelivery") != System.DBNull.Value && Convert.ToBoolean(Eval("CRF_isdelivery")))%>'>
  Line 576: <div class="col-sm-12 deliveroption" runat="server" visible='<%#(Eval("CRF_iscollection") != System.DBNull.Value && Convert.ToBoolean(Eval("CRF_iscollection")))%>'>

## 7. BIN Assembly Version Comparison

| Assembly | cakerstreet_CRM | 114_server | 114_server_crm_full_runtime | legacy_crm |
|---|---|---|---|---|
| EntityFramework.dll | v6.1.40302.0 | v6.400.420.21404 | v6.1.40302.0 | v6.1.40302.0 |
| Newtonsoft.Json.dll | v9.0.1.19813 | v13.0.1.25517 | v9.0.1.19813 | v9.0.1.19813 |
| AjaxControlToolkit.dll | v18.1.0.0 | v18.1.0.0 | v18.1.0.0 | v18.1.0.0 |
| Twilio.dll | v5.6.4.0 | v5.6.4.0 | v5.6.4.0 | v5.6.4.0 |
| msSQLDLL.dll | v1.0.0.0 | v1.0.0.0 | v1.0.0.0 | MISSING |
| Microsoft.ApplicationBlocks.Data.dll | EXCLUDED | v2.0.0.0 | EXCLUDED | v2.0.0.0 |
