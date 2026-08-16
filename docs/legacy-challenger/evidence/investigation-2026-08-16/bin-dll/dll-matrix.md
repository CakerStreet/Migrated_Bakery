# BIN/DLL Forensic Audit
**Captured:** 2026-08-16 16:06 BST

## DLL Presence Matrix

| DLL | CRM (27195) BizPortal (27201) Frontend (27203) EPOSTerm (27210) EPOSAdmin (27211)
| --- | --- --- --- --- ---
| `Microsoft.ApplicationBlocks.Data.dll` | 🔴 EXCLUDED | ✅ v2.0.0.0 | ✅ v2.0.0.0 | ✅ v2.0.0.0 | ✅ v2.0.0.0 |
| `AjaxControlToolkit.dll` | ✅ v18.1.0.0 | ✅ v18.1.0.0 | ✅ v18.1.0.0 | ❌ MISSING | ✅ v4.1.7.1213 |
| `Twilio.dll` | ✅ v5.6.4.0 | ✅ v7.11.3.0 | ❌ MISSING | ❌ MISSING | ❌ MISSING |
| `Newtonsoft.Json.dll` | ✅ v9.0.1.19813 | ✅ v13.0.3.27908 | ✅ v13.0.1.25517 | ✅ v13.0.1.25517 | ✅ v13.0.1.25517 |
| `EntityFramework.dll` | ✅ v6.1.40302.0 | ✅ v6.400.420.21404 | ✅ v6.400.420.21404 | ✅ v6.400.420.21404 | ✅ v6.1.40302.0 |
| `System.Web.Optimization.dll` | ✅ v1.1.40211.0 | ✅ v1.1.40211.0 | ✅ v1.1.30515.0 | ✅ v1.0.0.0 | ❌ MISSING |
| `WebGrease.dll` | ✅ v1.5.2.14234 | ✅ v1.5.2.14234 | ✅ v1.3.0.0 | ✅ v1.0.0.0 | ❌ MISSING |
| `Antlr3.Runtime.dll` | ✅ v3.4.1.9004 | ✅ v3.4.1.9004 | ✅ v3.3.1.7705 | ✅ v3.3.1.7705 | ❌ MISSING |
| `msSQLDLL.dll` | ✅ v1.0.0.0 | 🔴 EXCLUDED | ❌ MISSING | ❌ MISSING | ❌ MISSING |
| `Microsoft.Web.Infrastructure.dll` | ✅ v1.0.20105.407 | ✅ v1.0.20105.407 | ✅ v1.0.20105.407 | ✅ v1.0.20105.407 | ❌ MISSING |
| `System.Web.Helpers.dll` | ❌ MISSING | ❌ MISSING | ✅ v2.0.20710.0 | ✅ v3.0.61128.0 | ❌ MISSING |
| `System.Web.Mvc.dll` | ❌ MISSING | ❌ MISSING | ✅ v4.0.20710.0 | ✅ v5.2.61128.0 | ❌ MISSING |
| `System.Web.Razor.dll` | ❌ MISSING | ❌ MISSING | ✅ v2.0.20713.0 | ✅ v3.0.61128.0 | ❌ MISSING |

## Detailed BIN Inventory per Application

### CRM (27195)
**BIN path:** `G:\AI-Projects\Dev\kiro-cakerstreet-uk\cakerstreet_CRM\bin`
**DLL count:** 79
**Excluded count:** 6

#### Excluded Files (.exclude)
| Filename | Size | Date |
|---|---|---|
| AjaxControlToolkit.dll.3.exclude | 6522880 bytes | 2016-12-27 |
| AjaxControlToolkit.dll.exclude | 262144 bytes | 2016-12-27 |
| AjaxControlToolkit.dll.refresh.3.exclude | 146 bytes | 2016-12-27 |
| AjaxControlToolkit.dll.refresh.exclude | 142 bytes | 2016-12-27 |
| Microsoft.ApplicationBlocks.Data.dll.exclude | 32768 bytes | 2016-12-27 |
| Microsoft.ApplicationBlocks.Data.dll.refresh.exclude | 124 bytes | 2016-12-27 |

#### Full DLL Inventory
| Filename | Size | Date | Version |
|---|---|---|---|
| Affirma.ThreeSharp.dll | 45056 | 2012-10-28 | 1.1.0.0 |
| AjaxControlToolkit.dll | 5485056 | 2018-01-12 | 18.1.0.0 |
| AjaxControlToolkit.HtmlEditor.Sanitizer.dll | 10240 | 2018-01-12 | 18.1.0.0 |
| AjaxControlToolkit.StaticResources.dll | 7168 | 2018-01-12 | 18.1.0.0 |
| AjaxMin.dll | 434176 | 2016-12-27 | 4.97.4951.28478 |
| AmazonProductAdvtApiClient.dll | 324096 | 2016-12-27 | 1.0.0.0 |
| Antlr3.Runtime.dll | 102912 | 2013-02-22 | 3.4.1.9004 |
| ASPNET_SDK_Samples_AA.dll | 45056 | 2016-12-27 | 1.0.0.0 |
| ASPNET_SDK_Samples_AP.dll | 53248 | 2016-12-27 | 1.0.0.0 |
| ASPNetSpell.dll | 88576 | 2013-12-11 | 4.0.3.24577 |
| ASPSnippets.GoogleAPI.dll | 10240 | 2014-07-22 | 1.0.0.0 |
| awsmailmanager.dll | 6144 | 2020-05-21 | 1.0.0.0 |
| AWSSDK.Core.dll | 1072640 | 2020-05-21 | 3.3.106.26 |
| AWSSDK.dll | 1861120 | 2016-12-27 | 1.3.3.1 |
| AWSSDK.SimpleEmail.dll | 236032 | 2020-05-21 | 3.3.101.152 |
| Base_AA.dll | 65536 | 2016-12-27 | 1.0.0.0 |
| Base_AP.dll | 110592 | 2016-12-27 | 1.0.0.0 |
| Base_Common.dll | 45056 | 2016-12-27 | 1.0.0.0 |
| Braintree.dll | 572416 | 2021-11-15 | 5.10.0.0 |
| CKEditor.NET.dll | 119296 | 2016-12-27 | 3.6.4.0 |
| CKFinder.dll | 86016 | 2016-12-27 | 2.3.0.2394 |
| ClosedXML.dll | 796160 | 2015-01-09 | 0.69.1.0 |
| DocumentFormat.OpenXml.dll | 5233512 | 2015-01-09 | 2.0.5022.0 |
| EntityFramework.dll | 4976848 | 2018-02-24 | 6.1.40302.0 |
| EntityFramework.SqlServer.dll | 601808 | 2018-02-24 | 6.1.40302.0 |
| Fizzler.dll | 34816 | 2018-01-31 | 1.2.0.0 |
| FredCK.FCKeditorV2.dll | 45056 | 2016-12-27 | 2.6.3.22451 |
| GCheckout.dll | 126976 | 2016-12-27 | 1.3.0.0 |
| HtmlAgilityPack.dll | 134656 | 2016-12-27 | 1.4.6.0 |
| htmlrender_class.dll | 6144 | 2013-07-06 | 1.0.0.0 |
| ImageProcessor.dll | 189952 | 2020-07-29 | 2.9.1.00225 |
| ImageProcessor.Plugins.WebP.dll | 915456 | 2019-12-18 | 1.3.0.00152 |
| InstamojoAPI.dll | 25088 | 2019-04-15 | 1.0.0.0 |
| Ionic.Zip.dll | 492032 | 2016-12-27 | 1.9.1.8 |
| JWT.dll | 8704 | 2018-02-24 | 1.3.3.0 |
| log4net.dll | 270336 | 2016-12-27 | 1.2.10.0 |
| Microsoft.AspNet.SignalR.Client.dll | 107192 | 2018-02-24 | 1.1.20801.0 |
| Microsoft.AspNet.SignalR.Core.dll | 274104 | 2017-01-10 | 1.1.21022.0 |
| Microsoft.AspNet.SignalR.Owin.dll | 51896 | 2017-01-10 | 1.1.21022.0 |
| Microsoft.AspNet.SignalR.SystemWeb.dll | 19128 | 2017-01-10 | 1.1.21022.0 |
| Microsoft.Bcl.AsyncInterfaces.dll | 21064 | 2019-11-15 | 4.700.19.56404 |
| Microsoft.Data.Edm.dll | 655432 | 2016-12-27 | 5.2.0.51212 |
| Microsoft.Data.OData.dll | 1316944 | 2016-12-27 | 5.2.0.51212 |
| Microsoft.Owin.Host.SystemWeb.dll | 119968 | 2017-01-10 | 1.0.20312.147 |
| Microsoft.Web.Infrastructure.dll | 45416 | 2018-02-24 | 1.0.20105.407 |
| Microsoft.WindowsAzure.Configuration.dll | 17560 | 2016-12-27 | 1.8.0.0 |
| Microsoft.WindowsAzure.Storage.dll | 732816 | 2016-12-27 | 2.1.0.4 |
| msSQLDLL.dll | 13312 | 2018-10-17 | 1.0.0.0 |
| Newtonsoft.Json.dll | 526336 | 2018-02-24 | 9.0.1.19813 |
| Owin.dll | 4608 | 2017-01-10 | 1.0 |
| PayPal.dll | 302592 | 2018-02-09 | 1.7.4 |
| PayPalAdaptivePaymentsSDK.dll | 131072 | 2016-12-27 | 2.14.117.0 |
| PayPalCoreSDK.dll | 97280 | 2018-07-20 | 1.7.1 |
| RestSharp.dll | 168960 | 2016-12-27 | 105.2.3.0 |
| ScrapySharp.Core.dll | 63488 | 2017-11-01 |  |
| ScrapySharp.dll | 82944 | 2017-11-01 | 2.0.0.0 |
| ScriptReferenceProfiler.dll | 14336 | 2016-12-27 | 1.1.0.0 |
| Stripe.net.dll | 2394624 | 2024-01-04 | 43.9.0.0 |
| Svg.dll | 537600 | 2020-01-13 | 3.0.102.39411 |
| System.Net.Http.dll | 191152 | 2017-01-03 | 2.2.29.0 |
| System.Net.Http.WebRequest.dll | 27352 | 2017-01-03 | 2.2.29.0 |
| System.Reflection.dll | 21168 | 2018-03-26 | 4.7.3062.0 |
| System.Reflection.Extensions.dll | 20704 | 2018-03-26 | 4.7.3062.0 |
| System.Resources.ResourceManager.dll | 20712 | 2018-03-26 | 4.7.3062.0 |
| System.Runtime.CompilerServices.Unsafe.dll | 23600 | 2018-09-18 | 4.6.26919.02 |
| System.Runtime.dll | 28840 | 2018-03-26 | 4.7.3062.0 |
| System.Runtime.InteropServices.dll | 23784 | 2018-03-26 | 4.7.3062.0 |
| System.Runtime.InteropServices.RuntimeInformation.dll | 33256 | 2016-11-04 | 4.6.24705.01 |
| System.Spatial.dll | 126016 | 2016-12-27 | 5.2.0.51212 |
| System.Threading.dll | 21168 | 2018-03-26 | 4.7.3062.0 |
| System.Threading.Tasks.Extensions.dll | 33008 | 2018-11-29 | 4.6.27129.04 |
| System.ValueTuple.dll | 20656 | 2018-03-26 | 4.7.3062.0 |
| System.Web.Optimization.dll | 70864 | 2016-11-20 | 1.1.40211.0 |
| Twilio.dll | 1257984 | 2018-02-24 | 5.6.4.0 |
| WebGrease.dll | 1276568 | 2016-11-20 | 1.5.2.14234 |
| WebMarkupMin.AspNet.Common.dll | 20480 | 2018-04-16 | 2.4.0.0 |
| WebMarkupMin.AspNet4.Common.dll | 15872 | 2018-04-16 | 2.4.0.0 |
| WebMarkupMin.AspNet4.WebForms.dll | 11776 | 2018-04-16 | 2.4.0.0 |
| WebMarkupMin.Core.dll | 140288 | 2018-04-16 | 2.4.0.0 |

### BizPortal (27201)
**BIN path:** `G:\AI-Projects\Dev\kiro-cakerstreet-uk\recovered-business-portal-source\bin`
**DLL count:** 95
**Excluded count:** 8

#### Excluded Files (.exclude)
| Filename | Size | Date |
|---|---|---|
| AjaxControlToolkit.dll.3.exclude | 6522880 bytes | 2026-05-15 |
| AjaxControlToolkit.dll.exclude | 262144 bytes | 2026-05-15 |
| AjaxControlToolkit.dll.refresh.3.exclude | 146 bytes | 2026-05-15 |
| AjaxControlToolkit.dll.refresh.exclude | 142 bytes | 2026-05-15 |
| msSQLDLL.dll.exclude | 13312 bytes | 2026-05-15 |
| Twilio.dll.exclude | 1257984 bytes | 2026-05-15 |
| Twilio.dll.refresh.exclude | 86 bytes | 2026-05-15 |
| Twilio.xml.exclude | 2760413 bytes | 2026-05-15 |

#### Full DLL Inventory
| Filename | Size | Date | Version |
|---|---|---|---|
| AjaxControlToolkit.dll | 5485056 | 2026-05-15 | 18.1.0.0 |
| AjaxControlToolkit.HtmlEditor.Sanitizer.dll | 10240 | 2026-05-15 | 18.1.0.0 |
| AjaxControlToolkit.StaticResources.dll | 7168 | 2026-05-15 | 18.1.0.0 |
| AjaxMin.dll | 434176 | 2026-05-15 | 4.97.4951.28478 |
| AmazonProductAdvtApiClient.dll | 324096 | 2026-05-15 | 1.0.0.0 |
| Antlr3.Runtime.dll | 102912 | 2026-05-15 | 3.4.1.9004 |
| ASPNET_SDK_Samples_AA.dll | 45056 | 2026-05-15 | 1.0.0.0 |
| ASPNET_SDK_Samples_AP.dll | 53248 | 2026-05-15 | 1.0.0.0 |
| ASPSnippets.GoogleAPI.dll | 10240 | 2026-05-15 | 1.0.0.0 |
| AWSSDK.dll | 1861120 | 2026-05-15 | 1.3.3.1 |
| Base_AA.dll | 65536 | 2026-05-15 | 1.0.0.0 |
| Base_AP.dll | 110592 | 2026-05-15 | 1.0.0.0 |
| Base_Common.dll | 45056 | 2026-05-15 | 1.0.0.0 |
| CKEditor.NET.dll | 119296 | 2026-05-15 | 3.6.4.0 |
| CKFinder.dll | 86016 | 2026-05-15 | 2.3.0.2394 |
| ClosedXML.dll | 796160 | 2026-05-15 | 0.69.1.0 |
| CssJscriptOptimizer.dll | 26112 | 2026-05-15 | 1.0.0.0 |
| DocumentFormat.OpenXml.dll | 5233512 | 2026-05-15 | 2.0.5022.0 |
| EntityFramework.dll | 4991352 | 2026-05-15 | 6.400.420.21404 |
| EntityFramework.SqlServer.dll | 591752 | 2026-05-15 | 6.400.420.21404 |
| FacebookLoginASPnetWebForms.dll | 9216 | 2026-05-15 | 1.0.0.0 |
| FredCK.FCKeditorV2.dll | 45056 | 2026-05-15 | 2.6.3.22451 |
| FSharp.Core.dll | 1506120 | 2026-05-15 | 4.40.23020.0 |
| GCheckout.dll | 126976 | 2026-05-15 | 1.3.0.0 |
| Google.Apis.Auth.dll | 52736 | 2026-05-15 | 1.9.2.27817 |
| Google.Apis.Auth.PlatformServices.dll | 22016 | 2026-05-15 | 1.9.2.27820 |
| Google.Apis.Core.dll | 47104 | 2026-05-15 | 1.9.2.27816 |
| Google.Apis.dll | 61952 | 2026-05-15 | 1.9.2.27817 |
| Google.Apis.PlatformServices.dll | 7168 | 2026-05-15 | 1.9.2.27818 |
| Google.Apis.ShoppingContent.v2.dll | 181760 | 2026-05-15 | 1.9.2.56 |
| HtmlAgilityPack.dll | 132096 | 2026-05-15 | 1.4.9.4 |
| htmlrender_class.dll | 6144 | 2026-05-15 | 1.0.0.0 |
| ImageProcessor.dll | 189952 | 2026-05-15 | 2.9.1.00225 |
| ImageProcessor.Plugins.WebP.dll | 915456 | 2026-05-15 | 1.3.0.00152 |
| Ionic.Zip.dll | 492032 | 2026-05-15 | 1.9.1.8 |
| json-ld.net.dll | 102400 | 2026-05-15 | 1.0.5.0 |
| JWT.dll | 8704 | 2026-05-15 | 1.3.3.0 |
| libwebp.dll | 597504 | 2026-05-15 | 1.2.1 |
| log4net.dll | 299520 | 2026-05-15 | 1.2.13.0 |
| Microsoft.ApplicationBlocks.Data.dll | 32768 | 2026-05-15 | 2.0.0.0 |
| Microsoft.AspNet.SignalR.Client.dll | 107192 | 2026-05-15 | 1.1.20801.0 |
| Microsoft.AspNet.SignalR.Core.dll | 274104 | 2026-05-15 | 1.1.21022.0 |
| Microsoft.AspNet.SignalR.Owin.dll | 51896 | 2026-05-15 | 1.1.21022.0 |
| Microsoft.AspNet.SignalR.SystemWeb.dll | 19128 | 2026-05-15 | 1.1.21022.0 |
| Microsoft.Bcl.AsyncInterfaces.dll | 26904 | 2026-05-15 | 8.0.23.53103 |
| Microsoft.Bcl.Memory.dll | 51464 | 2026-05-15 | 9.0.24.52809 |
| Microsoft.Bcl.TimeProvider.dll | 32520 | 2026-05-15 | 8.0.123.58001 |
| Microsoft.Data.Edm.dll | 655432 | 2026-05-15 | 5.2.0.51212 |
| Microsoft.Data.OData.dll | 1316944 | 2026-05-15 | 5.2.0.51212 |
| Microsoft.Extensions.Logging.Abstractions.dll | 47600 | 2026-05-15 | 2.1.0.18136 |
| Microsoft.IdentityModel.Abstractions.dll | 20008 | 2026-05-15 | 8.3.1.60117 |
| Microsoft.IdentityModel.JsonWebTokens.dll | 162336 | 2026-05-15 | 8.3.1.60117 |
| Microsoft.IdentityModel.Logging.dll | 38984 | 2026-05-15 | 8.3.1.60117 |
| Microsoft.IdentityModel.Tokens.dll | 356936 | 2026-05-15 | 8.3.1.60117 |
| Microsoft.Owin.Host.SystemWeb.dll | 119968 | 2026-05-15 | 1.0.20312.147 |
| Microsoft.Threading.Tasks.dll | 37104 | 2026-05-15 | 1.0.168.0 |
| Microsoft.Threading.Tasks.Extensions.Desktop.dll | 47424 | 2026-05-15 | 1.0.168.0 |
| Microsoft.Web.Infrastructure.dll | 45416 | 2026-05-15 | 1.0.20105.407 |
| Microsoft.WindowsAzure.Configuration.dll | 17560 | 2026-05-15 | 1.8.0.0 |
| Microsoft.WindowsAzure.Storage.dll | 732816 | 2026-05-15 | 2.1.0.4 |
| Newtonsoft.Json.dll | 711952 | 2026-05-15 | 13.0.3.27908 |
| Owin.dll | 4608 | 2026-05-15 | 1.0 |
| PayPal.dll | 302592 | 2026-05-15 | 1.7.4 |
| PayPalAdaptivePaymentsSDK.dll | 131072 | 2026-05-15 | 2.14.117.0 |
| PayPalCoreSDK.dll | 110592 | 2026-05-15 | 1.6.1.0 |
| RestSharp.dll | 168960 | 2026-05-15 | 105.2.3.0 |
| ScrapySharp.Core.dll | 63488 | 2026-05-15 |  |
| ScrapySharp.dll | 82944 | 2026-05-15 | 2.0.0.0 |
| ScriptReferenceProfiler.dll | 14336 | 2026-05-15 | 1.1.0.0 |
| Stripe.net.dll | 354816 | 2026-05-15 | 10.4.0.0 |
| System.Buffers.dll | 20856 | 2026-05-15 | 4.6.28619.01 |
| System.Collections.Specialized.dll | 26872 | 2026-05-15 | 4.6.24705.01 |
| System.IdentityModel.Tokens.Jwt.dll | 90656 | 2026-05-15 | 8.3.1.60117 |
| System.Memory.dll | 142240 | 2026-05-15 | 4.6.31308.01 |
| System.Net.Http.dll | 191152 | 2026-05-15 | 2.2.29.0 |
| System.Net.Http.Extensions.dll | 22232 | 2026-05-15 | 2.2.29.0 |
| System.Net.Http.Primitives.dll | 21712 | 2026-05-15 | 2.2.29.0 |
| System.Net.Http.WebRequest.dll | 27352 | 2026-05-15 | 2.2.29.0 |
| System.Numerics.Vectors.dll | 115856 | 2026-05-15 | 4.6.26515.06 |
| System.Runtime.CompilerServices.Unsafe.dll | 18024 | 2026-05-15 | 6.0.21.52210 |
| System.Runtime.dll | 22176 | 2026-05-15 | 2.6.10.0 |
| System.Spatial.dll | 126016 | 2026-05-15 | 5.2.0.51212 |
| System.Text.Encodings.Web.dll | 79024 | 2026-05-15 | 8.0.23.53103 |
| System.Text.Json.dll | 644888 | 2026-05-15 | 8.0.1024.46610 |
| System.Threading.Tasks.dll | 35016 | 2026-05-15 | 2.6.10.0 |
| System.Threading.Tasks.Extensions.dll | 25984 | 2026-05-15 | 4.6.28619.01 |
| System.ValueTuple.dll | 25232 | 2026-05-15 | 4.6.26515.06 |
| System.Web.Optimization.dll | 70864 | 2026-05-15 | 1.1.40211.0 |
| Twilio.dll | 5875200 | 2026-05-15 | 7.11.3.0 |
| WebGrease.dll | 1276568 | 2026-05-15 | 1.5.2.14234 |
| WebMarkupMin.AspNet.Common.dll | 20480 | 2026-05-15 | 2.4.0.0 |
| WebMarkupMin.AspNet4.Common.dll | 15872 | 2026-05-15 | 2.4.0.0 |
| WebMarkupMin.AspNet4.WebForms.dll | 11776 | 2026-05-15 | 2.4.0.0 |
| WebMarkupMin.Core.dll | 140288 | 2026-05-15 | 2.4.0.0 |
| Zlib.Portable.dll | 81920 | 2026-05-15 | 1.11.0.0 |

### Frontend (27203)
**BIN path:** `G:\AI-Projects\Dev\kiro-cakerstreet-uk\vs-test\cakerstreet\bin`
**DLL count:** 51
**Excluded count:** 0

#### Full DLL Inventory
| Filename | Size | Date | Version |
|---|---|---|---|
| AjaxControlToolkit.dll | 5485056 | 2026-05-13 | 18.1.0.0 |
| Antlr3.Runtime.dll | 105544 | 2026-05-13 | 3.3.1.7705 |
| ASPNET_SDK_Samples_AA.dll | 45056 | 2026-05-13 | 1.0.0.0 |
| ASPNET_SDK_Samples_AP.dll | 53248 | 2026-05-13 | 1.0.0.0 |
| ASPSnippets.GoogleAPI.dll | 10240 | 2026-05-13 | 1.0.0.0 |
| Base_AA.dll | 65536 | 2026-05-13 | 1.0.0.0 |
| Base_AP.dll | 110592 | 2026-05-13 | 1.0.0.0 |
| Base_Common.dll | 45056 | 2026-05-13 | 1.0.0.0 |
| Braintree.dll | 572416 | 2026-05-13 | 5.10.0.0 |
| CakerstreetMVC.dll | 2727936 | 2026-05-13 | 1.0.0.0 |
| CakerstreetMVC.XmlSerializers.dll | 505344 | 2026-05-13 | 1.0.0.0 |
| DisableCompressionModule.dll | 3584 | 2026-05-16 | 0.0.0.0 |
| DotNetOpenAuth.AspNet.dll | 39936 | 2026-05-13 | 4.0.3.12163 |
| DotNetOpenAuth.Core.dll | 219136 | 2026-05-13 | 4.0.3.12163 |
| DotNetOpenAuth.OAuth.Consumer.dll | 17920 | 2026-05-13 | 4.0.3.12163 |
| DotNetOpenAuth.OAuth.dll | 55808 | 2026-05-13 | 4.0.3.12163 |
| DotNetOpenAuth.OpenId.dll | 267776 | 2026-05-13 | 4.0.3.12163 |
| DotNetOpenAuth.OpenId.RelyingParty.dll | 88576 | 2026-05-13 | 4.0.3.12163 |
| EntityFramework.dll | 4991352 | 2026-05-13 | 6.400.420.21404 |
| EntityFramework.SqlServer.dll | 591752 | 2026-05-13 | 6.400.420.21404 |
| Fizzler.dll | 34816 | 2026-05-13 | 1.2.0.0 |
| htmlrender_class.dll | 6144 | 2026-05-13 | 1.0.0.0 |
| Microsoft.ApplicationBlocks.Data.dll | 32768 | 2026-05-13 | 2.0.0.0 |
| Microsoft.Bcl.AsyncInterfaces.dll | 21064 | 2026-05-13 | 4.700.19.56404 |
| Microsoft.Web.Infrastructure.dll | 45416 | 2026-05-13 | 1.0.20105.407 |
| Microsoft.Web.WebPages.OAuth.dll | 29296 | 2026-05-13 | 2.0.20710.0 |
| Newtonsoft.Json.dll | 701992 | 2026-05-13 | 13.0.1.25517 |
| PayPal.dll | 304128 | 2026-05-13 | 1.9.1 |
| PayPalAdaptivePaymentsSDK.dll | 131072 | 2026-05-13 | 2.14.117.0 |
| PayPalCoreSDK.dll | 110592 | 2026-05-13 | 1.6.1.0 |
| RestSharp.dll | 168960 | 2026-05-13 | 105.2.3.0 |
| Stripe.net.dll | 2394624 | 2026-05-13 | 43.9.0.0 |
| Svg.dll | 537600 | 2026-05-13 | 3.0.102.39411 |
| System.Net.Http.dll | 86696 | 2026-05-13 | 4.7.3062.0 built by: NET472REL1 |
| System.Net.Http.Formatting.dll | 168520 | 2026-05-13 | 4.0.21112.0 |
| System.Net.Http.WebRequest.dll | 24784 | 2026-05-13 | 4.7.3062.0 built by: NET472REL1 |
| System.Runtime.CompilerServices.Unsafe.dll | 23600 | 2026-05-13 | 4.6.26919.02 |
| System.Runtime.InteropServices.RuntimeInformation.dll | 20792 | 2026-05-13 | 4.7.3062.0 |
| System.Threading.Tasks.Extensions.dll | 33008 | 2026-05-13 | 4.6.27129.04 |
| System.Web.Helpers.dll | 138352 | 2026-05-13 | 2.0.20710.0 |
| System.Web.Http.dll | 323168 | 2026-05-13 | 4.0.20710.0 |
| System.Web.Http.WebHost.dll | 73312 | 2026-05-13 | 4.0.20710.0 |
| System.Web.Mvc.dll | 506976 | 2026-05-13 | 4.0.20710.0 |
| System.Web.Optimization.dll | 70352 | 2026-05-13 | 1.1.30515.0 |
| System.Web.Razor.dll | 264816 | 2026-05-13 | 2.0.20713.0 |
| System.Web.WebPages.Deployment.dll | 41048 | 2026-05-13 | 2.0.20710.0 |
| System.Web.WebPages.dll | 204400 | 2026-05-13 | 2.0.20710.0 |
| System.Web.WebPages.Razor.dll | 39536 | 2026-05-13 | 2.0.20710.0 |
| WebGrease.dll | 1054792 | 2026-05-13 | 1.3.0.0 |
| WebMatrix.Data.dll | 37976 | 2026-05-13 | 2.0.20710.0 |
| WebMatrix.WebData.dll | 74840 | 2026-05-13 | 2.0.20710.0 |

### EPOSTerm (27210)
**BIN path:** `G:\AI-Projects\CRM-Recovery\epos_2026\epos.cakerstreet.com\bin`
**DLL count:** 19
**Excluded count:** 0

#### Full DLL Inventory
| Filename | Size | Date | Version |
|---|---|---|---|
| Antlr3.Runtime.dll | 105528 | 2018-08-16 | 3.3.1.7705 |
| cakerstreetfranchise_mvc.dll | 678400 | 2023-05-26 | 1.0.0.0 |
| cakerstreetfranchise_mvc.XmlSerializers.dll | 520704 | 2022-02-25 | 1.0.0.0 |
| EntityFramework.dll | 4991352 | 2020-04-16 | 6.400.420.21404 |
| EntityFramework.SqlServer.dll | 591752 | 2020-04-16 | 6.400.420.21404 |
| FastMember.dll | 26624 | 2019-05-24 | 1.5.0.0 |
| Microsoft.ApplicationBlocks.Data.dll | 32768 | 2013-03-06 | 2.0.0.0 |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform.dll | 40168 | 2018-09-05 | 2.0.50905.0 |
| Microsoft.Web.Infrastructure.dll | 45416 | 2012-07-25 | 1.0.20105.407 |
| Newtonsoft.Json.dll | 701992 | 2021-03-17 | 13.0.1.25517 |
| RestSharp.dll | 168960 | 2016-12-27 | 105.2.3.0 |
| System.Web.Helpers.dll | 137144 | 2018-11-28 | 3.0.61128.0 |
| System.Web.Mvc.dll | 548280 | 2018-11-28 | 5.2.61128.0 |
| System.Web.Optimization.dll | 54912 | 2018-08-16 | 1.0.0.0 |
| System.Web.Razor.dll | 263608 | 2018-11-28 | 3.0.61128.0 |
| System.Web.Webpages.Deployment.dll | 43128 | 2018-11-28 | 3.0.61128.0 |
| System.Web.Webpages.dll | 206456 | 2018-11-28 | 3.0.61128.0 |
| System.Web.Webpages.Razor.dll | 40888 | 2018-11-28 | 3.0.61128.0 |
| WebGrease.dll | 963640 | 2018-08-16 | 1.0.0.0 |

### EPOSAdmin (27211)
**BIN path:** `G:\AI-Projects\CRM-Recovery\epos_2026\eposadmin.cakerstreet.com\bin`
**DLL count:** 15
**Excluded count:** 0

#### Full DLL Inventory
| Filename | Size | Date | Version |
|---|---|---|---|
| AjaxControlToolkit.dll | 7361536 | 2014-05-16 | 4.1.7.1213 |
| AjaxMin.dll | 434176 | 2014-05-16 | 4.97.4951.28478 |
| CKEditor.NET.dll | 119296 | 2013-07-17 | 3.6.4.0 |
| CKFinder.dll | 86016 | 2013-07-17 | 2.3.0.2394 |
| EntityFramework.dll | 4976848 | 2018-04-20 | 6.1.40302.0 |
| EntityFramework.SqlServer.dll | 601808 | 2018-04-20 | 6.1.40302.0 |
| GCheckout.dll | 126976 | 2008-09-06 | 1.3.0.0 |
| HtmlAgilityPack.dll | 134656 | 2014-05-16 | 1.4.6.0 |
| Microsoft.ApplicationBlocks.Data.dll | 32768 | 2013-03-06 | 2.0.0.0 |
| Microsoft.Data.Edm.dll | 655432 | 2014-05-16 | 5.2.0.51212 |
| Microsoft.Data.OData.dll | 1316944 | 2014-05-16 | 5.2.0.51212 |
| Microsoft.WindowsAzure.Configuration.dll | 17560 | 2014-05-16 | 1.8.0.0 |
| Microsoft.WindowsAzure.Storage.dll | 732816 | 2014-05-16 | 2.1.0.4 |
| Newtonsoft.Json.dll | 576040 | 2021-03-17 | 13.0.1.25517 |
| System.Spatial.dll | 126016 | 2014-05-16 | 5.2.0.51212 |
