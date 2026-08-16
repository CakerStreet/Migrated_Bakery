# DLL Usage Reference Audit
Captures which DLLs are referenced in source code and where.
Captured: 2026-08-16

## msSQLDLL — Source Code References
References in CRM App_Code: 332
  AWSWrapper.cs:187 — SqlHelper.ExecuteNonQuery(awsConfig.CnxnString, awsConfig.SetImageInfo, parameters);
  businesscommissionhelper.cs:28 — t.Load(SqlHelper.ExecuteReader(constr, CommandType.Text, query));
  businesscommissionhelper.cs:57 — SqlHelper.ExecuteNonQuery(constr, CommandType.Text, query, param);
  cls_addpostalcakes.cs:2666 — int i = SqlHelper.ExecuteNonQuery(constr, CommandType.StoredProcedure, "dbo.InsertLinkPrd2Cat", param);
  cls_addpostalcakes.cs:2684 — int i = SqlHelper.ExecuteNonQuery(constr, CommandType.StoredProcedure, "dbo.usp_insertPrdtopper_byprdID", param);
  cls301Redirect.cs:38 — SqlHelper.ExecuteNonQuery(constr, CommandType.StoredProcedure, "dbo.SaveRedirectUrl", param);
  clsCategory.cs:119 — return (SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getBretCrumByCatID", param)).Tables[0];
  clsCategory.cs:133 — return (SqlHelper.ExecuteScalar(constr, CommandType.StoredProcedure, "dbo.getBretCrumByCatIDForAdmin", param)).ToString();
  clsCategory.cs:150 — return SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getGiftCatByProductID", param);
  clsCategory.cs:166 — return SqlHelper.ExecuteReader(constr, CommandType.StoredProcedure, "dbo.getCategoryByID", param);
  clsCategory.cs:183 — ds = SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getSpecialProductsByCatID", param);
  clsCategory.cs:201 — t.Load(SqlHelper.ExecuteReader(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level_1_ForWholesaleCustomer", param));
  clsCategory.cs:216 — return (DataSet)SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level_1");
  clsCategory.cs:227 — return (DataSet)SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level1_tags");
  clsCategory.cs:238 — return (DataSet)SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_retailonly_level_1");
  clsCategory.cs:263 — return SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level_1WithproductFilter", param).Tables[0];
  clsCategory.cs:289 — return SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level_1WithproductFilter_all", param);
  clsCategory.cs:300 — return (DataSet)SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.USP_GetActiveCategories_Leve1l_2A");
  clsCategory.cs:314 — return (DataSet)SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.USP_GetActiveCategories_Leve1l_2A_by_cattype", param);
  clsCategory.cs:328 — return SqlHelper.ExecuteDataset(constr, CommandType.StoredProcedure, "dbo.getActiveCategories_level1_12ForWholesaleCustomer", param);

## msSQLDLL — web.config assembly bindings
### cakerstreet_CRM
  (no assembly bindings found for these DLLs)
### recovered-business-portal-source
  (no assembly bindings found for these DLLs)

## Microsoft.ApplicationBlocks.Data — Source Code References
References in CRM App_Code: 49
  AWSWrapper.cs:28 — using Microsoft.ApplicationBlocks.Data;
  businesscommissionhelper.cs:7 — using Microsoft.ApplicationBlocks.Data;
  cls_addpostalcakes.cs:10 — using Microsoft.ApplicationBlocks.Data;
  cls_addpricefromtemplate.cs:10 — using Microsoft.ApplicationBlocks.Data;
  cls301Redirect.cs:7 — using Microsoft.ApplicationBlocks.Data;
  clsCategory.cs:11 — using Microsoft.ApplicationBlocks.Data;
  clsChat.cs:11 — using Microsoft.ApplicationBlocks.Data;
  clsContactUs.cs:12 — using Microsoft.ApplicationBlocks.Data;
  clsCoupon.cs:11 — using Microsoft.ApplicationBlocks.Data;
  clsCustomDelete.cs:11 — using Microsoft.ApplicationBlocks.Data;

## ApplicationBlocks — using statements
Files with 'using Microsoft.ApplicationBlocks': 49
  AWSWrapper.cs
  businesscommissionhelper.cs
  cls_addpostalcakes.cs
  cls_addpricefromtemplate.cs
  cls301Redirect.cs
  clsCategory.cs
  clsChat.cs
  clsContactUs.cs
  clsCoupon.cs
  clsCustomDelete.cs
