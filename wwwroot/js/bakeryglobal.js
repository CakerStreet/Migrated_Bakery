function RemoveQuotationfrombakery(crfid) {
    showpop_modalpop();
    $.ajax({
        type: "POST",
        url: $("#hdGlobalUrl").val() + "/webservices.aspx/RemoveQuotationfrombakery",
        data: "{crfid:" + crfid + "}",
        contentType: "application/json; charset=utf-8",
        dataType: "json",
        async: true,
        cache: false,
        success: function (msg) {
            $(".boxouter[data-id='" + crfid + "']").remove();
            hidepop_modalpop();
        },
        error: function (x, e) {
            hidepop_modalpop();
            alert("The call to the server side failed. " + x.responseText);
        }
    });
}