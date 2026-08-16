# Upload Architecture Investigation
Captured: 2026-08-16

## Question: What generates http://localhost:27195/upload/Product_images/ URLs?

## 1. Web.config appSettings — Upload/Image Path Keys

### cakerstreet_CRM
  key=klarna_baseurl  value=https://api.klarna.com/
  key=crm_PhysicalApplicationPath  value=C:\inetpub\vhosts\stoicpro.co.uk\crm.stoicpro.co.uk\
  key=businesswebsite_URL  value=http://localhost:27201/
  key=Epos_websiteURL  value=https://epos.cakerstreet.com
  key=custwebsite_PhysicalApplicationPath  value=C:\Inetpub\vhosts\cakerstreet.com\httpdocs\
  key=businesswebsite_PhysicalApplicationPath  value=C:\inetpub\vhosts\stoicpro.co.uk\business.stoicpro.co.uk\
  key=siteURL  value=http://localhost:27195/
  key=siteURL_Logo  value=http://localhost:27195
  key=PhysicalApplicationPath  value=C:\inetpub\vhosts\cakerstreet.com\httpdocs\
  key=PhysicalApplicationPath_crm  value=C:\inetpub\vhosts\stoicpro.co.uk\crm.stoicpro.co.uk\
  key=PhysicalApplicationPath_fileupload  value=C://\inetpub//vhosts//cakerstreet.com//httpdocs
  key=PhysicalApplicationPath_fileupload_crm  value=C://\inetpub//vhosts//stoicpro.co.uk//crm.stoicpro.co.uk
  key=VirtualApplicationPath  value=https://crm.stoicpro.co.uk/
  key=ApplicationPath  value=\
  key=FCKeditor:BasePath  value=~/FCKeditor/
  key=continueShoppingURL  value=https://crm.stoicpro.co.uk/mailmessage.aspx
  key=Path  value=https://crm.stoicpro.co.uk/
  key=PAYPAL_REDIRECT_URL  value=https://www.paypal.com/webscr&cmd=
  key=cdnLink  value=https://cakerstreet1.s3.amazonaws.com/
  key=customImagesearch_googleApiKey  value=AIzaSyA4LdCB1FjbBhChHOGo6XFBTokTuDfIn20
  key=customImagesearch_searchEngineID  value=014696490992484563204:06b9jgzw0tg
  key=CancelPurchaseConnectUrl  value=https://crm.stoicpro.co.uk/failAuth.aspx
  key=ReturnConnectUrl  value=https://crm.stoicpro.co.uk/PaypalIPN.aspx
  key=NotifyConnectUrl  value=https://crm.stoicpro.co.uk/PaypalIPN.aspx
  key=SendToReturnURL  value=true
  key=accessoryimgpath  value=C://\CakerstreetAccessoryFTPImages//150 dpi RGB
  key=accessoryimgpath_lrg  value=C://\CakerstreetAccessoryFTPImages//300 dpi RGB//Unique_Product_Image_Library_300dpi_RGB

### cakerstreet
  key=cdnLink  value=https://cakerstreet1.s3.amazonaws.com/
  key=businesswebsite_URL  value=http://localhost:27200/
  key=CRMwebsite_URL  value=http://localhost:27200/
  key=EPOSwebsite_URL  value=http://localhost:27200/
  key=PreserveLoginUrl  value=true
  key=siteURL  value=http://localhost:27203/
  key=SiteUrls  value=http://localhost:27203/
  key=siteURL_Logo  value=http://localhost:27203
  key=PhysicalApplicationPath  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\
  key=PhysicalApplicationPath_fileupload  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet
  key=VirtualApplicationPath  value=http://localhost:27203/
  key=ApplicationPath  value=\
  key=FCKeditor:BasePath  value=~/FCKeditor/
  key=continueShoppingURL  value=http://localhost:27203/mailmessage.aspx
  key=Path  value=http://localhost:27203/
  key=websiteURL_live  value=https://www.cakerstreet.com
  key=PAYPAL_REDIRECT_URL  value=https://www.paypal.com/webscr&cmd=
  key=customImagesearch_googleApiKey  value=AIzaSyA4LdCB1FjbBhChHOGo6XFBTokTuDfIn20
  key=customImagesearch_searchEngineID  value=014696490992484563204:06b9jgzw0tg
  key=CancelPurchaseConnectUrl  value=http://localhost:27203/failAuth.aspx
  key=ReturnConnectUrl  value=http://localhost:27203/PaypalIPN.aspx
  key=NotifyConnectUrl  value=http://localhost:27203/PaypalIPN.aspx
  key=SendToReturnURL  value=true
  key=accessoryimgpath  value=C://\CakerstreetAccessoryFTPImages//150 dpi RGB
  key=accessoryimgpath_lrg  value=C://\CakerstreetAccessoryFTPImages//300 dpi RGB//Unique_Product_Image_Library_300dpi_RGB
  key=websiteLink_liveforImages  value=https://www.cakerstreet.com
  key=LocalDev_UseLiveImageFallback  value=true
  key=LocalDev_LiveImageBaseUrl  value=https://www.cakerstreet.com/

### recovered-business-portal-source
  key=Epos_websiteURL  value=http://localhost:27202
  key=custwebsite_PhysicalApplicationPath  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\
  key=businesswebsite_PhysicalApplicationPath  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source\
  key=crmwebsite_PhysicalApplicationPath  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\
  key=siteURL  value=http://localhost:27201/
  key=siteURL_Logo  value=http://localhost:27201
  key=PhysicalApplicationPath  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\
  key=PhysicalApplicationPath_fileupload  value=G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet
  key=VirtualApplicationPath  value=http://localhost:27201/
  key=ApplicationPath  value=\
  key=FCKeditor:BasePath  value=~/FCKeditor/
  key=continueShoppingURL  value=http://localhost:27201/mailmessage.aspx
  key=Path  value=http://localhost:27201/
  key=PAYPAL_REDIRECT_URL  value=https://www.paypal.com/webscr&cmd=
  key=cdnLink  value=https://cakerstreet1.s3.amazonaws.com/
  key=customImagesearch_googleApiKey  value=AIzaSyA4LdCB1FjbBhChHOGo6XFBTokTuDfIn20
  key=customImagesearch_searchEngineID  value=014696490992484563204:06b9jgzw0tg
  key=CancelPurchaseConnectUrl  value=http://localhost:27201/failAuth.aspx
  key=ReturnConnectUrl  value=http://localhost:27201/PaypalIPN.aspx
  key=NotifyConnectUrl  value=http://localhost:27201/PaypalIPN.aspx
  key=SendToReturnURL  value=true
  key=accessoryimgpath  value=C://\CakerstreetAccessoryFTPImages//150 dpi RGB
  key=accessoryimgpath_lrg  value=C://\CakerstreetAccessoryFTPImages//300 dpi RGB//Unique_Product_Image_Library_300dpi_RGB

## 2. App_Code — Image URL construction

App_Code references to upload/image paths: 285
  AWSWrapper.cs:81 — //internal static string ProfileImagePath { get; set; }
  AWSWrapper.cs:82 — //internal static string ProductImagePath { get; set; }
  AWSWrapper.cs:83 — internal static string InputImagePath { get; set; }
  AWSWrapper.cs:110 — //ProductImagePath = ConfigurationManager.AppSettings["productimagepath"];
  AWSWrapper.cs:111 — //ProfileImagePath = ConfigurationManager.AppSettings["profileimagepath"];
  AWSWrapper.cs:169 — Boolean result = uploadImagetesting(r);
  AWSWrapper.cs:199 — private static bool uploadImagetesting(DataRow dr)
  AWSWrapper.cs:205 — awsConfig.InputImagePath = ConfigurationManager.AppSettings["custwebsite_PhysicalApplicationPath"].ToString();
  AWSWrapper.cs:208 — awsConfig.InputImagePath = Path.Combine(awsConfig.InputImagePath, "upload", "homepagelinks", "resized");
  AWSWrapper.cs:212 — awsConfig.InputImagePath = Path.Combine(awsConfig.InputImagePath, "upload", "banner");
  AWSWrapper.cs:219 — return AmazonUploadStatus(awsConfig.CDNPath + awsConfig.AmazonFolderInBucket + "/" + strImageName);
  AWSWrapper.cs:223 — return AmazonUploadStatus(awsConfig.CDNPath + awsConfig.AmazonFolderInBucket+  "/" + Path.GetFileNameWithoutExtension(strImageName) + "-" + awsConfig.BannerImageSizes.Split('|')[0] + Path.GetExtension(strImageName));
  AWSWrapper.cs:237 — string imageFilePath = awsConfig.InputImagePath + "/" + strImageName;
  AWSWrapper.cs:240 — moveimagetoCDN_upload(strImageName, imageFilePath);
  AWSWrapper.cs:244 — FileInfo FI = new FileInfo(awsConfig.InputImagePath + "/" + strImageName_main_webp);
  AWSWrapper.cs:247 — moveimagetoCDN_upload(strImageName_main_webp.Replace(" ", "-"), awsConfig.InputImagePath + "/" + strImageName_main_webp);
  AWSWrapper.cs:251 — //generate .webp and upload
  AWSWrapper.cs:252 — ConvertToWebP(awsConfig.InputImagePath, strImageName);
  AWSWrapper.cs:253 — FI = new FileInfo(awsConfig.InputImagePath + "/" + strImageName_main_webp);
  AWSWrapper.cs:256 — moveimagetoCDN_upload(strImageName_main_webp.Replace(" ", "-"), awsConfig.InputImagePath + "/" + strImageName_main_webp);
  AWSWrapper.cs:274 — string outputPath = awsConfig.InputImagePath + @"\" + "resized_" + size.Resize_folderName.Replace("-", "_");
  AWSWrapper.cs:280 — moveimagetoCDN_upload(strfileName.Replace(" ", "-"), outputPath + @"\" + strImageName);
  AWSWrapper.cs:288 — moveimagetoCDN_upload(strfileName.Replace(" ", "-"), outputPath + @"\" + strImageName_webp);
  AWSWrapper.cs:292 — //generate resized .webp and upload
  AWSWrapper.cs:293 — ConvertToWebP(awsConfig.InputImagePath + @"\" + "resized_" + size.Resize_folderName.Replace("-", "_"), strImageName);
  AWSWrapper.cs:297 — moveimagetoCDN_upload(strfileName.Replace(" ", "-"), outputPath + @"\" + strImageName_webp);
  AWSWrapper.cs:323 — private static void moveimagetoCDN_upload(string strImageName, string imageFilePath)
  AWSWrapper.cs:334 — private static bool AmazonUploadStatus(string sourceImageS3Link)
  AWSWrapper.cs:369 — //dirFrom.Add(Path.Combine(images_path, "Product_images"));
  AWSWrapper.cs:370 — //dirFrom.Add(Path.Combine(images_path, "Product_images/resized_800_800"));

## 3. ASPX Pages — Hardcoded /upload/ references

Frontend ASPX references to localhost:27195 or upload/Product: 0

## 4. Database — handicraft_filename and image URL fields

```
Msg 208, Level 16, State 1, Server LAPTOP-VFM9JOD4, Line 2
Invalid object name 'tbl_Handicraft'.
```

## 5. IIS applicationhost.config — Virtual Directories and /upload paths

Virtual directories with /upload path found: NO

## 6. Anonymous vs Auth analysis

CRM web.config authentication mode: Forms
windowsAuthentication element: not in web.config (IIS default)
anonymousAuthentication element: not in web.config (IIS default)

IIS Express default: Windows Auth ENABLED, Anonymous DISABLED for .NET apps.
Static files under /upload/ would also be subject to Windows Auth unless a location element exempts them.

Location overrides for /upload: NONE

CONCLUSION:
Even if /upload/ folder physically exists, all requests to it will return 401/403 (Windows Auth)
unless the IIS Express site is configured to allow anonymous access for that path.
ERR_BLOCKED_BY_ORB is the browser consequence of receiving an opaque 401/403 for a cross-origin image request.
