# CRM Source Tree Authority — Semantic Comparison
Captured: 2026-08-16

## 1. ASPX Page Counts

| Tree | ASPX count | CS count | ASPX.CS count |
|---|---|---|---|
| cakerstreet_CRM | 261 | 438 | 261 |
| 114_server | 384 | 1527 | 383 |
| 114_server_crm_full_runtime | 261 | 438 | 261 |
| legacy_crm | 261 | 429 | 261 |

### Pages in 114_server NOT in cakerstreet_CRM
- webhook.aspx
- bakeryorderdetail.aspx
- bakeryorders.aspx
- mapcustomizedcake.aspx
- addnewcake.aspx
- addnewcaketemplate.aspx
- addnewreceipe.aspx
- addnewservice.aspx
- addpurchaseorder.aspx
- addupdatedairy.aspx
- addupduploadchecks.aspx
- bakerworktime.aspx
- bakeryavailability.aspx
- BakeryFiles.aspx
- bakeryorderdetail.aspx
- bakeryorders.aspx
- bakeryusers.aspx
- businessapc.aspx
- courseAssessment.aspx
- courseDetail.aspx
- createImagefrombyte.aspx
- createsvgforpersonalisedcake.aspx
- crflist_forsBakery.aspx
- DailyDairy_checklists.aspx
- deliveryRouteDetail.aspx
- editStoreInfo.aspx
- edittheme.aspx
- foodStandards.aspx
- FranchiseBakeryorders.aspx
- franchisenotes.aspx
- haccp.aspx
- linkbakerswithcaketemplate.aspx
- linkcutter.aspx
- linkkeywordwithgroup.aspx
- linkpackage2prd.aspx
- linkproductwithfranchise.aspx
- linktopper.aspx
- manageallergenmatrix.aspx
- manageassorted.aspx
- managebakeryingredient.aspx
- managecleaningchecklist.aspx
- managecollectionpoint.aspx
- manageDailyCheck_cleaning.aspx
- manageDailyCheck_openingnClosing.aspx
- manageDeliveryRouteCharges.aspx
- managedeliveryroutes.aspx
- managedeliveryroutesPayments.aspx
- manageeeceipecategory.aspx
- manageIngredient.aspx
- manageIngredientasTag.aspx
- manageinventory.aspx
- managelocation.aspx
- manageModuleAssignment.aspx
- manageordermenifest.aspx
- managepackagingtype.aspx
- manageproductdoc.aspx
- manageproducttags.aspx
- manageproductwithfranchise.aspx
- managepurchaseorder.aspx
- managepurchaseorderitemreceived.aspx
- managereceipe_filter.aspx
- ManageReceipe_Print.aspx
- manageReceipe.aspx
- managereceipeIngredient_keywords.aspx
- manageReceipeMatrix.aspx
- managesbakeryuperviser.aspx
- managesection.aspx
- manageservice.aspx
- managesociallinks.aspx
- managespecificationbytemplate.aspx
- managespongeorderlist.aspx
- manageStaffCerificates.aspx
- managestaffdairycheck.aspx
- managestafftimingrequest.aspx
- managestockrequest.aspx
- managesupplier.aspx
- managesupplyorder.aspx
- managesupplyorderitemreceived.aspx
- managetemplatebakerfee.aspx
- managetemplateforingredients.aspx
- managetemplateprice.aspx
- managethemes.aspx
- managetopper.aspx
- managevideos.aspx
- managewebstorecategory.aspx
- mapcustomizedcake.aspx
- myaccountbalanceforbaking.aspx
- mytradeaccount.aspx
- orderspongelist.aspx
- ordertopper.aspx
- printcutters.aspx
- printorderspongelist.aspx
- sellerPaymentSettings.aspx
- staffDashboard.aspx
- staffRota.aspx
- supplierusers.aspx
- updateorderimage.aspx
- uploadorderpicture.aspx
- viewspongeorderlist.aspx
- linkproductwithfranchise.aspx
- manageproductwithfranchise.aspx

### Pages in cakerstreet_CRM NOT in 114_server
- (none — identical set)

## 2. App_Code File Comparison

| Filename | cakerstreet_CRM SHA | 114_server SHA | legacy_crm SHA | Match? |
|---|---|---|---|---|
| AbstractPage.cs | B33ECB953FBF | MISSING | B33ECB953FBF | - |
| AutoComplete.cs | 6054C204F425 | MISSING | 6054C204F425 | - |
| AWSWrapper.cs | 29210DB1B433 | MISSING | MISSING | - |
| BackgroundWorker.cs | A28C070B26CF | MISSING | A28C070B26CF | - |
| braintree_Payment.cs | 9717BD9C7C85 | MISSING | 9717BD9C7C85 | - |
| braintreerefund.cs | 920C42CBC813 | MISSING | MISSING | - |
| BundleConfig.cs | 27480AE12B58 | MISSING | 27480AE12B58 | - |
| businesscommissionhelper.cs | 7950633847DB | MISSING | 7950633847DB | - |
| cart.cs | 35162F28DBEE | MISSING | 35162F28DBEE | - |
| ChatEngine.cs | 79906B91B47B | MISSING | 79906B91B47B | - |
| ChatMessage.cs | 93A7ADD9BA71 | MISSING | 93A7ADD9BA71 | - |
| ChatRoom.cs | 563E7A41CF63 | MISSING | 563E7A41CF63 | - |
| ChatUser.cs | DDD6D4A19880 | MISSING | DDD6D4A19880 | - |
| ClientInfoUtil.cs | A2E58A051FD9 | MISSING | MISSING | - |
| cls_addpostalcakes.cs | B79453D6A973 | MISSING | B79453D6A973 | - |
| cls_addpricefromtemplate.cs | 08206A3AE74F | MISSING | 08206A3AE74F | - |
| cls301Redirect.cs | BA85BC41E06F | MISSING | BA85BC41E06F | - |
| clsCategory.cs | 16F904AA3F39 | MISSING | 16F904AA3F39 | - |
| clsChat.cs | B3A3DB1A09F2 | MISSING | B3A3DB1A09F2 | - |
| clsCommon.cs | 13ACBE0B8A22 | MISSING | 13ACBE0B8A22 | - |
| clsContactUs.cs | 1F771446804E | MISSING | 1F771446804E | - |
| clsCoupon.cs | C1CF53B927F7 | MISSING | C1CF53B927F7 | - |
| clsCustomDelete.cs | D6035BAEE433 | MISSING | D6035BAEE433 | - |
| clsCustomers.cs | EB577E4AAB8E | MISSING | EB577E4AAB8E | - |
| clsCustomGetValue.cs | CF3069804D7A | MISSING | CF3069804D7A | - |
| clsdbKart.cs | DC39F2410EEE | MISSING | DC39F2410EEE | - |
| clsDeliveryAddress.cs | 117CADEC24DF | MISSING | 117CADEC24DF | - |
| clsEmailNewsletter.cs | 12C2AF649900 | MISSING | 12C2AF649900 | - |
| clsencryption.cs | 0BFBAB336CBD | MISSING | 0BFBAB336CBD | - |
| clsEpos.cs | D71B6F3B338F | MISSING | D71B6F3B338F | - |
| clsFacebook.cs | E3DBA9A6ADAC | MISSING | E3DBA9A6ADAC | - |
| clsformSubscribe.cs | 82215630EA84 | MISSING | 82215630EA84 | - |
| clsFriends.cs | 53B6CD8640DE | MISSING | 53B6CD8640DE | - |
| clsglobalfunction.cs | 13C740685CD5 | MISSING | 13C740685CD5 | - |
| clsglobaltext.cs | 8CC1028DC30F | MISSING | 8CC1028DC30F | - |
| clsgooglesearch.cs | 72374583F0BC | MISSING | 72374583F0BC | - |
| clsHeaderContent.cs | 816F90E35404 | MISSING | 816F90E35404 | - |
| clsInventoryManagement.cs | 3934586D3003 | MISSING | 3934586D3003 | - |
| clskoolcake.cs | 693945E57DD6 | MISSING | 693945E57DD6 | - |
| clsMail.cs | 3B6415968AD1 | MISSING | 3B6415968AD1 | - |
| ClsMSACSV.cs | 9EF38B20BF73 | MISSING | 9EF38B20BF73 | - |
| clsNewsletter.cs | 96E1248B91A2 | MISSING | 96E1248B91A2 | - |
| clsOrder.cs | 67A24210EE4F | MISSING | 67A24210EE4F | - |
| clsorderlog.cs | 803CD31D6B89 | MISSING | 803CD31D6B89 | - |
| clsParameter.cs | 24AB36E0A60E | MISSING | 24AB36E0A60E | - |
| clspaypal.cs | 1CB8776294BA | MISSING | MISSING | - |
| clsPOHelper.cs | 247B609AE466 | MISSING | 247B609AE466 | - |
| clsPost.cs | 44295744351B | MISSING | 44295744351B | - |
| clsPostLink.cs | CA2C77204691 | MISSING | CA2C77204691 | - |
| clsPriceBand.cs | E99598297DB4 | MISSING | E99598297DB4 | - |
| clsProductBuyButton.cs | DB894D8FAA61 | MISSING | DB894D8FAA61 | - |
| clsProductOption.cs | F90FB3C53511 | MISSING | F90FB3C53511 | - |
| clsProducts.cs | DFC39D0DB862 | MISSING | DFC39D0DB862 | - |
| clsQuickQuotes.cs | 9F48AC5E9B91 | MISSING | 9F48AC5E9B91 | - |
| ClsReadHtmlFile.cs | 8CABB04BE138 | MISSING | 8CABB04BE138 | - |
| clsRefundOrderDetail.cs | 58A8246DACA4 | MISSING | 58A8246DACA4 | - |
| clsReminder.cs | F4AE8ACB85AE | MISSING | F4AE8ACB85AE | - |
| clsReviews.cs | E43AF9B52BC2 | MISSING | E43AF9B52BC2 | - |
| clsShipping.cs | 5C1C3CE19ECB | MISSING | 5C1C3CE19ECB | - |
| clsShippingCost.cs | 1FE5C6CF60FF | MISSING | 1FE5C6CF60FF | - |
| clsSocial.cs | B48417260356 | MISSING | B48417260356 | - |
| clsSpecialPrice.cs | A0B32BFEA00E | MISSING | A0B32BFEA00E | - |
| ClsSQLCSV.cs | AF6A4AE529AB | MISSING | AF6A4AE529AB | - |
| clsSubscribe.cs | 6F97AC2786C1 | MISSING | 6F97AC2786C1 | - |
| ClsTestimonial.cs | DBAA703718DA | MISSING | DBAA703718DA | - |
| clsUserAuthorization.cs | D72858A7957F | MISSING | D72858A7957F | - |
| clsUsers.cs | 8B4044850B64 | MISSING | 8B4044850B64 | - |
| Common.cs | 52C14D278490 | MISSING | 52C14D278490 | - |
| CompressedViewStatePage.cs | AC594C8DEFC6 | MISSING | AC594C8DEFC6 | - |
| Configuration.cs | C420E1CF65C5 | MISSING | C420E1CF65C5 | - |
| crmadminloghelper.cs | 066C7F9B5A11 | MISSING | 066C7F9B5A11 | - |
| cslCRF.cs | E6FB86B5016E | MISSING | E6FB86B5016E | - |
| CustomerDelivery.cs | BBF817B4DC66 | MISSING | BBF817B4DC66 | - |
| CustomFunction.cs | 8C5F0855E93D | MISSING | 8C5F0855E93D | - |
| EPOSadminModel.cs | 1606BE40EA7A | MISSING | 1606BE40EA7A | - |
| EPOSModel.cs | 26CABBAEDD26 | MISSING | 26CABBAEDD26 | - |
| franchise_service.cs | E9A98A37B4BE | MISSING | E9A98A37B4BE | - |
| HttpModule.cs | 217106922D3B | MISSING | 217106922D3B | - |
| includes.cs | EF9FDA5CA763 | MISSING | EF9FDA5CA763 | - |
| JsonCompressionModule.cs | DB06E0A42115 | MISSING | DB06E0A42115 | - |
| MobileClass.cs | 4D4A86078EEF | MISSING | 4D4A86078EEF | - |
| model_business.cs | BC1E6DBC3142 | MISSING | BC1E6DBC3142 | - |
| model_crm.cs | FAC4C66DD770 | MISSING | FAC4C66DD770 | - |
| Model.cs | 948F8AC62B51 | MISSING | 948F8AC62B51 | - |
| oAuthFacebook.cs | 2FEE6A104853 | MISSING | 2FEE6A104853 | - |
| OpsCrmLogMutationController.cs | MISSING | 09E89868D16A | MISSING | - |
| OpsCrmNotesMutationController.cs | MISSING | 0FDBBCFF5638 | MISSING | - |
| OpsReadOnlyApiControllers.cs | MISSING | 224C0B77BDD6 | MISSING | - |
| OpsReadOnlyApiControllers2.cs | MISSING | 044E0AC591DA | MISSING | - |
| OpsReadOnlyApiControllers3.cs | MISSING | CA422E548395 | MISSING | - |
| OrderHub.cs | 5F3523544FF5 | MISSING | MISSING | - |
| OrderNotification.cs | 5B1E85FBCD9B | MISSING | MISSING | - |
| partyaccessorymodel.cs | FBDB9329FAC3 | MISSING | FBDB9329FAC3 | - |
| partythememodel.cs | FB7159476DA7 | MISSING | FB7159476DA7 | - |
| PayPalHelper.cs | 0D0C5247A1F9 | MISSING | 0D0C5247A1F9 | - |
| paypalrefund.cs | 0C0DB4448EE2 | MISSING | MISSING | - |
| RecaptchaV2.cs | 99984DA327D1 | MISSING | 99984DA327D1 | - |
| ScriptCompressor.cs | 00665ABF1959 | MISSING | 00665ABF1959 | - |
| SearchBakeries.cs | D85C711922C9 | MISSING | D85C711922C9 | - |
| SearchImages.cs | D2CD3DAA3413 | MISSING | D2CD3DAA3413 | - |
| Service.cs | BC935AC88B1D | MISSING | BC935AC88B1D | - |
| SetProperties.cs | 8CE1E976D3D0 | MISSING | 8CE1E976D3D0 | - |
| Shop.cs | 414E793E3C6F | MISSING | 414E793E3C6F | - |
| StaticCache.cs | AAC6B19533EC | MISSING | AAC6B19533EC | - |
| StringHelper.cs | 1A61345BE8DD | MISSING | 1A61345BE8DD | - |
| StripeMsg.cs | 4D29E9FBD254 | MISSING | 4D29E9FBD254 | - |
| StripeRefund.cs | CBF6CE3A078B | MISSING | MISSING | - |
| Thumbnails.cs | DED7C03065B3 | MISSING | DED7C03065B3 | - |
| webpgeneratetor.cs | 01563CDE5472 | MISSING | 01563CDE5472 | - |
| webscrapper_model.cs | AAD4A30F0FD3 | MISSING | AAD4A30F0FD3 | - |
| WhitespaceFilter.cs | 8E31092CA236 | MISSING | 8E31092CA236 | - |
| WhitespaceModule.cs | 3517045376D4 | MISSING | 3517045376D4 | - |
| wspaypal.cs | 4BE78F45277C | MISSING | MISSING | - |
| wwHTTP.cs | 5A8238C9EEF5 | MISSING | 5A8238C9EEF5 | - |
| wwHttpUtils.cs | 84FAAA755D00 | MISSING | 84FAAA755D00 | - |
