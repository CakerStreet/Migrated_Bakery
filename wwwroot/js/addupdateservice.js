$(document).ready(function () {

    $(".float").keypress(function (event) {
        if ((event.which != 46 || $(this).val().indexOf('.') != -1) && (event.which < 48 || event.which > 57)) {
            event.preventDefault();
        }
    });
    $(".number").keypress(function (e) {
        //if the letter is not digit then display error and don't type anything
        if (e.which != 8 && e.which != 0 && (e.which < 48 || e.which > 57)) {
            return false;
        }
    });

    $("body").on("click", "#rbRecurring", function () {

        if ($(this).is(":checked")) {

            $("#div_RecurringFrequency").show();
            $("#ddlRecurringFrequency").val("0");
        }
    });

    $("body").on("click", "#rbOnline", function () {

        if ($(this).is(":checked")) {

            $("#div_RecurringFrequency").hide();
            $("#ddlRecurringFrequency").val("0");
        }
    });

    $("body").on("click", "#btnSave", function () {

        var alerttext = "";

        if ($("#ddlCat").val() == "-1") {
            alerttext += "Please select website category.\n\r";
            $("#ddlCat").addClass("redcolored");
        }

        if ($("#ddlSubCat").val() == "-1") {
            alerttext += "Please select website sub category.\n\r";
            $("#ddlSubCat").addClass("redcolored");
        }

        if ($("#txtPrdTitle").val() == "") {
            alerttext += "Please enter Service title.\n\r";
            $("#txtPrdTitle").addClass("redcolored");
        }

        if ($("#txtWSPrice").val() == "") {
            alerttext += "Please enter WS Price.\n\r";
            $("#txtWSPrice").addClass("redcolored");
        } else {
            if (isNaN($("#txtWSPrice").val())) {
                alerttext += "Please enter valid WS Price.\n\r";
                $("#txtWSPrice").addClass("redcolored");
            }
        }

        if ($("#txtPrice").val() == "") {
            alerttext += "Please enter Price.\n\r";
            $("#txtPrice").addClass("redcolored");
        } else {
            if (isNaN($("#txtPrice").val())) {
                alerttext += "Please enter valid Price.\n\r";
                $("#txtPrice").addClass("redcolored");
            }
        }

        if ($("#rbRecurring").is(":checked") && $("#ddlRecurringFrequency").val() == "0") {

            alerttext += "Please select Recurring Frequency.\n\r";
            $("#ddlRecurringFrequency").addClass("redcolored");
        }

        if (alerttext != "") {
            alert(alerttext);
        }
        else {
            //alert($("#chkRecomended").is(":checked"));
            showpop_modalpop();
            var prdList = {};
            prdList.product_ID = $("#hfPrdID").val();
            prdList.product_catID = $("#ddlSubCat").val();
            prdList.product_Name = $("#txtPrdTitle").val();
            prdList.product_desc = CKEDITOR.instances.txtDescription.getData();
            prdList.product_startingtPrice = $("#txtWSPrice").val();
            prdList.product_marketPrice = $("#txtPrice").val();
            prdList.product_isActive = $("#chkRecomended").is(":checked");

            if ($("#rbRecurring").is(":checked")) {
                prdList.shapeid = 1;
                prdList.typeid = $("#ddlRecurringFrequency").val();
            }
            else {
                prdList.shapeid = 2;
                prdList.typeid = 0;
            }

            var arr_prdImage = new Array();
            intcounter = 0;
            var intcounter_image = 0;
            $(".prdImgbox_outer.imgboxouter .prdImgbox").each(function () {
                if ($(this).attr("data-tid").toString() != "0") {
                    var prdImage = {};
                    intcounter_image += 1;
                    prdImage.productImage_imagename = $(this).attr("data-tid").toString();
                    prdImage.productImage_isnew = (($(this).attr("data-bid").toString() == "1") ? true : false);
                    if ($(this).find('span.spn_radioouter input[type="radio"]').filter(':checked').length > 0) {
                        prdList.product_image1 = prdImage.productImage_imagename;
                        prdList.product_isnewImage = prdImage.productImage_isnew;
                        prdImage.productImage_isdefaultimage = true;
                    } else {
                        prdImage.productImage_isdefaultimage = false;
                    }
                    arr_prdImage[intcounter] = prdImage;
                    intcounter += 1;
                }
            });

            $.ajax({
                type: "POST",
                url: $("#hdGlobalUrl").val() + "/webservices.aspx/AddUpdateService",
                data: "{prdList:" + JSON.stringify(prdList) + ",prdImage:" + JSON.stringify(arr_prdImage) + "}",
                contentType: "application/json; charset=utf-8",
                dataType: "json",
                async: false,
                cache: true,
                success: function (msg) {
                    var city = msg.d;
                    hidepop_modalpop();
                    if (city.data_ID == 1) {

                        location.href = 'manageservices';

                    } else {
                        alert(city.data_optionalstr);
                    }
                },
                error: function (x, e) {
                    alert("The call to the server side failed. " + x.responseText);
                }
            });
        }
    });

    $("body").on('keypress', 'input.redcolored', function () {
        $(this).removeClass("redcolored");
    });
    $("body").on('keypress', 'textarea.redcolored', function () {
        $(this).removeClass("redcolored");
    });
    $("body").on('change', 'select.redcolored', function () {
        $(this).removeClass("redcolored");
    });



    $('.div_catdrps_ListingOuter').on('change', 'select.form-control', function () {
        showCategoriesbyCatID($(this).val(), parseInt($(this).attr("data-tid").toString()));
    });

    $(".prdImgbox_outer.addnewctrl").click(function () {
        var data_id = ($(".prdImgbox_outer.imgboxouter").length + 1).toString();
        if (data_id <= 10) {
            var newidchk = strReplaceAll(guid().toString(), "-", "");
            var newidinput = strReplaceAll(guid().toString(), "-", "");
            $(this).before("<div class='prdImgbox_outer imgboxouter'  data-id='" + data_id + "'></div>");
            $(".prdImgbox_outer.imgboxouter[data-id='" + data_id + "']").prepend("<div data-bid='0' data-tid='0' class='prdImgbox' ></div>");
            $(".prdImgbox_outer.imgboxouter[data-id='" + data_id + "'] .prdImgbox").append("<div class='PrdImg displaynone' >&nbsp;</div><div class='addPrdImg' ><input type='file' id='" + newidinput + "' name='" + newidinput + "'><img class='loading displaynone' src='images/loading_wall.gif'></div><a data-bid='0' class='edit displaynone' >&nbsp;</a> <a data-bid='0' class='remove displaynone' >&nbsp;</a><span class='spn_radioouter displaynone'><input type='radio' value='" + newidchk + "' name='grp_prdImage' id='" + newidchk + "'><label for='" + newidchk + "'>Main Image</label></span>");
        }
        if (data_id >= 10) {
            $(this).hide();
        }
    });

    $("#ul_uploadImages").on('click', '.edit.displaynone', function () {
        if ($(this).attr("data-bid").toString() == "0") {
            $(this).attr("data-bid", "1");
            var id = $(this).parent("div.prdImgbox");
            var fuID = $(id).find("input[type='file']").attr("id").toString();
            $(id).find(".addPrdImg").show();
            $(id).find("input[type='file']").remove();
            $(id).find(".addPrdImg").append("<input id='" + fuID + "' name='" + fuID + "' type='file'>");
            $(id).find(".PrdImg").hide();
            $(id).find("input[type='file']").change(function () {
                uploadImage(id, fuID, this);
            });
        } else {
            $(this).attr("data-bid", "0");
            var id = $(this).parent("div.prdImgbox");
            $(id).find(".addPrdImg").hide();
            $(id).find(".PrdImg").show();
        }
    });
    $("#ul_uploadImages").on('click', '.remove.displaynone', function () {
        if (confirm("Are your sure to remove this image?")) {
            var id = $(this).parent("div.prdImgbox");
            $(id).attr("data-tid", "0");
            $(id).find("a.remove").hide();
            $(id).find("a.edit").hide();
            $(id).find("span.spn_radioouter").hide();
            $(id).find("span.spn_radioouter").prop('checked', false);
            $(id).find(".PrdImg").css("background-image", "url('')");
            var fuID = $(id).find("input[type='file']").attr("id").toString();
            $(id).find(".addPrdImg").show();
            $(id).find("input[type='file']").remove();
            $(id).find(".addPrdImg").append("<input id='" + fuID + "' name='" + fuID + "' type='file'>");
            $(id).find(".PrdImg").hide();
            $(id).find("input[type='file']").change(function () {
                uploadImage(id, fuID, this);
            });
            updateImageval();
        }
    });
    $("#ul_uploadImages").on('change', '.addPrdImg input[type="file"]', function () {
        uploadImage($(this).parent(".addPrdImg").parent("div.prdImgbox"), $(this).attr("id").toString(), this);
    });
    updateImageval();

});

function showCategoriesbyCatID(catId, intlevel) {
    $("#hfCatIDs").val("0");
    $(".div_catdrps_ListingOuter select.form-control").each(function () {
        if (parseInt($(this).attr("data-tid").toString()) > intlevel) {
            $(this).remove();
        }
    });
    $.ajax({
        type: "POST",
        url: $("#hdGlobalUrl").val() + "/webservices.aspx/getServiceCategoriesbyCatID",
        data: "{catId:" + catId + ",intlevel:" + intlevel + "}",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        async: false,
        cache: true,
        success: function (msg) {
            var city = msg.d;
            //                                    $(".divLoading_addCartBtn_PrdPage").hide();
            if (city.data_ID == 1) {
                //next level exists
                $(".div_catdrps_ListingOuter").append(city.data_str);
                $("#btn_hide_Attributes").click();
            } else {
                $("#hfCatIDs").val(((catId == "-1") ? "0" : catId));
                $("#btn_updateAttributes").click();
            }
        },
        error: function (x, e) {
            alert("The call to the server side failed. " + x.responseText);
        }
    });
}

function getNameFromPathPC(strFilepath) {
    var objRE = new RegExp(/([^\/\\]+)$/);
    var strName = objRE.exec(strFilepath);
    if (strName == null) {
        return null;
    } else {
        return strName[0];
    }
}

function checkFileExtensionPC(file) {
    var flag = true;
    var extension = file.substr((file.lastIndexOf('.') + 1));
    switch (extension) {
        case 'jpg':
        case 'jpeg':
        case 'png':
        case 'gif':
        case 'JPG':
        case 'JPEG':
        case 'PNG':
        case 'GIF':
            flag = true;
            break;
        default:
            flag = false;
    }
    return flag;
}

function updateImageval() {
    var createtext = "0";
    $("#ul_uploadImages .prdImgbox").each(function () {
        if ($(this).attr("data-tid").toString() != "0") {
            createtext += ";" + $(this).attr("data-tid").toString();
        }
    });
    $("#hdn_Product_Images").val(createtext);
    if ($('span.spn_radioouter:visible input[type="radio"]').filter(':checked').length == 0 && $('span.spn_radioouter:visible').length > 0) {
        $('span.spn_radioouter:visible input[type="radio"]').first().prop('checked', true);
    }
}

function uploadImage(id, fuID, fuelement) {
    var fileToUpload = getNameFromPath_cakebuilder($(fuelement).val());
    var filename = fileToUpload.substr(0, (fileToUpload.lastIndexOf('.')));
    if (checkFileExtension_cakeBuilder(fileToUpload)) {
        var flag = true;
        var counter = 1;
        if (filename != "" && filename != null) {
            $(id).find(".loading").show();
            $.ajaxFileUpload({
                url: $("#hdGlobalUrl").val() + '/upload/FileUploadHandler.ashx',
                secureuri: false,
                fileElementId: fuID,
                dataType: 'json',
                success: function (data, status) {
                    $(id).find(".loading").hide();
                    if (data.error != '') {
                        $(id).find(".loading").hide();
                        alert(data.error);
                    } else {
                        $(id).attr("data-tid", data.upfile);
                        $(id).attr("data-bid", "1");
                        var filepath = $("#hdCustGlobalUrl").val() + "/upload/temp/" + data.upfile;
                        $(id).find(".PrdImg").css("background-image", "url('" + filepath + "')");
                        $(id).find(".addPrdImg").hide();
                        $(id).find(".PrdImg").show();
                        $(id).find("a.remove").show();
                        $(id).find("a.edit").show();
                        $(id).find("a.edit").attr("data-bid", "0");
                        $(id).find("span.spn_radioouter").show();
                    }
                    updateImageval();
                },
                error: function () {
                    $(id).find(".loading").hide();
                    //alert('1');
                }
            });
        }
    }
}
// check extension of file to be upload
function checkFileExtension_cakeBuilder(file) {
    var flag = true;
    var extension = file.substr((file.lastIndexOf('.') + 1));
    switch (extension) {
        case 'jpg':
        case 'jpeg':
        case 'png':
        case 'gif':
        case 'JPG':
        case 'JPEG':
        case 'PNG':
        case 'GIF':
            flag = true;
            break;
        default:
            flag = false;
    }
    return flag;
}

function getNameFromPath_cakebuilder(strFilepath) {
    var objRE = new RegExp(/([^\/\\]+)$/);
    var strName = objRE.exec(strFilepath);
    if (strName == null) {
        return null;
    } else {
        return strName[0];
    }
}

function guid() {
    function _p8(s) {
        var p = (Math.random().toString(16) + "000000000").substr(2, 8);
        return s ? "-" + p.substr(0, 4) + "-" + p.substr(4, 4) : p;
    }
    return _p8() + _p8(true) + _p8(true) + _p8();
}

function strReplaceAll(string, Find, Replace) {
    try {
        return string.replace(new RegExp(Find, "gi"), Replace);
    } catch (ex) {
        return string;
    }
}