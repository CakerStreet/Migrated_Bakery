# BREAK-RUNTIME-002 — Deep Evidence
Captured: 2026-08-16

## 1. Authentication State
CRM uses Windows Integrated Security (NTLM/Kerberos). Headless Playwright cannot pass NTLM
credentials in the same way a browser with logged-in OS session can. The 403 response
is the Windows auth challenge — NOT a compilation error.

The compilation error occurs AFTER authentication, on postback of 'Add New Suggestion'.
Manual browser evidence from session 2026-08-14 captured the compilation error dialog.

## 2. Exact Root Cause: Namespace Not Found

All 49 App_Code .cs files contain:
```csharp
using Microsoft.ApplicationBlocks.Data;
```
When ASP.NET dynamically compiles App_Code, it cannot resolve this namespace because
Microsoft.ApplicationBlocks.Data.dll is NOT loadable (renamed to .dll.exclude).
ASP.NET only loads assemblies with the .dll extension from the bin\ folder.

## 3. Files Directly Referencing ApplicationBlocks

App_Code directory: G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\App_Code
Total .cs files: 108
Files referencing ApplicationBlocks: 49

| File | Has ApplicationBlocks reference |
|---|---|
| AbstractPage.cs | no |
| AutoComplete.cs | no |
| AWSWrapper.cs | YES — references namespace |
| BackgroundWorker.cs | no |
| braintree_Payment.cs | no |
| braintreerefund.cs | no |
| BundleConfig.cs | no |
| businesscommissionhelper.cs | YES — references namespace |
| cart.cs | no |
| ChatEngine.cs | no |
| ChatMessage.cs | no |
| ChatRoom.cs | no |
| ChatUser.cs | no |
| ClientInfoUtil.cs | no |
| cls_addpostalcakes.cs | YES — references namespace |
| cls_addpricefromtemplate.cs | YES — references namespace |
| cls301Redirect.cs | YES — references namespace |
| clsCategory.cs | YES — references namespace |
| clsChat.cs | YES — references namespace |
| clsCommon.cs | no |
| clsContactUs.cs | YES — references namespace |
| clsCoupon.cs | YES — references namespace |
| clsCustomDelete.cs | YES — references namespace |
| clsCustomers.cs | YES — references namespace |
| clsCustomGetValue.cs | YES — references namespace |
| clsdbKart.cs | YES — references namespace |
| clsDeliveryAddress.cs | YES — references namespace |
| clsEmailNewsletter.cs | YES — references namespace |
| clsencryption.cs | no |
| clsEpos.cs | no |
| clsFacebook.cs | YES — references namespace |
| clsformSubscribe.cs | YES — references namespace |
| clsFriends.cs | YES — references namespace |
| clsglobalfunction.cs | no |
| clsglobaltext.cs | YES — references namespace |
| clsgooglesearch.cs | YES — references namespace |
| clsHeaderContent.cs | YES — references namespace |
| clsInventoryManagement.cs | YES — references namespace |
| clskoolcake.cs | YES — references namespace |
| clsMail.cs | YES — references namespace |
| ClsMSACSV.cs | no |
| clsNewsletter.cs | YES — references namespace |
| clsOrder.cs | YES — references namespace |
| clsorderlog.cs | YES — references namespace |
| clsParameter.cs | YES — references namespace |
| clspaypal.cs | no |
| clsPOHelper.cs | no |
| clsPost.cs | YES — references namespace |
| clsPostLink.cs | YES — references namespace |
| clsPriceBand.cs | YES — references namespace |
| clsProductBuyButton.cs | YES — references namespace |
| clsProductOption.cs | YES — references namespace |
| clsProducts.cs | YES — references namespace |
| clsQuickQuotes.cs | YES — references namespace |
| ClsReadHtmlFile.cs | no |
| clsRefundOrderDetail.cs | YES — references namespace |
| clsReminder.cs | YES — references namespace |
| clsReviews.cs | YES — references namespace |
| clsShipping.cs | YES — references namespace |
| clsShippingCost.cs | YES — references namespace |
| clsSocial.cs | no |
| clsSpecialPrice.cs | YES — references namespace |
| ClsSQLCSV.cs | no |
| clsSubscribe.cs | YES — references namespace |
| ClsTestimonial.cs | YES — references namespace |
| clsUserAuthorization.cs | no |
| clsUsers.cs | YES — references namespace |
| Common.cs | no |
| CompressedViewStatePage.cs | no |
| Configuration.cs | no |
| crmadminloghelper.cs | YES — references namespace |
| cslCRF.cs | YES — references namespace |
| CustomerDelivery.cs | no |
| CustomFunction.cs | no |
| EPOSadminModel.cs | no |
| EPOSModel.cs | no |
| franchise_service.cs | no |
| HttpModule.cs | no |
| includes.cs | no |
| JsonCompressionModule.cs | no |
| MobileClass.cs | no |
| model_business.cs | no |
| model_crm.cs | no |
| Model.cs | no |
| oAuthFacebook.cs | no |
| OrderHub.cs | no |
| OrderNotification.cs | no |
| partyaccessorymodel.cs | YES — references namespace |
| partythememodel.cs | YES — references namespace |
| PayPalHelper.cs | no |
| paypalrefund.cs | no |
| RecaptchaV2.cs | no |
| ScriptCompressor.cs | no |
| SearchBakeries.cs | no |
| SearchImages.cs | no |
| Service.cs | YES — references namespace |
| SetProperties.cs | no |
| Shop.cs | no |
| StaticCache.cs | no |
| StringHelper.cs | no |
| StripeMsg.cs | no |
| StripeRefund.cs | no |
| Thumbnails.cs | no |
| webpgeneratetor.cs | no |
| webscrapper_model.cs | no |
| WhitespaceFilter.cs | no |
| WhitespaceModule.cs | no |
| wspaypal.cs | no |

## 4. Exact DLL State in CRM bin\
DLL present: False
EXCLUDE present: True
EXCLUDE file: Microsoft.ApplicationBlocks.Data.dll.exclude | 32768 bytes | 2016-12-27 | SHA-256: 8AA2BAE74DB02555FEB60ED23346011BA6C213292EC4657A6D9DFECBF1AA231B

ASP.NET bin loading rule: Only files with .dll extension are loaded.
Files with any other extension (including .dll.exclude) are IGNORED by the runtime.
Therefore: Microsoft.ApplicationBlocks.Data is NOT loaded. NOT already present elsewhere.

## 5. Proof ApplicationBlocks is Not Loaded Elsewhere

Searched all bin subdirectories of cakerstreet_CRM for any copy of ApplicationBlocks:
All ApplicationBlocks files found in cakerstreet_CRM\bin: 2
  G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll.exclude (32768 bytes, Microsoft.ApplicationBlocks.Data.dll.exclude)
  G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll.refresh.exclude (124 bytes, Microsoft.ApplicationBlocks.Data.dll.refresh.exclude)

Conclusion: No functioning copy of ApplicationBlocks.Data.dll exists anywhere in the CRM's
loaded assembly path. There is no shadow copy, GAC entry, or alternative loading path.

## 6. GAC Check
GAC entries for ApplicationBlocks: 0
  NONE — ApplicationBlocks is NOT in the GAC. Must come from bin\.dll.

## 7. Proposed Fix (HYPOTHESIS — awaiting challenger approval)
Rename: cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll.exclude
    to: cakerstreet_CRM\bin\Microsoft.ApplicationBlocks.Data.dll
OR copy from: 114_server 1\114_server\bin\Microsoft.ApplicationBlocks.Data.dll
SHA-256 of both: 8AA2BAE74DB02555FEB60ED23346011BA6C213292EC4657A6D9DFECBF1AA231B (IDENTICAL)
Risk: ZERO — same binary already loaded and working in all other 4 IIS sites.
Rollback: rename .dll back to .dll.exclude.
