/* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */

$(document).ready(function () {
    session = $.cookie("_session");

    $('#content').css('top', ($('#header').height() + 20) + 'px');

    if (ssl)
        $('#ssl_warning').css('display', 'none');
    else
        $('#ssl_warning a').attr('href', https_uri + '§url§');

    $('#noscript').css('display', 'none');
    $('.blurred').removeClass('blurred');
    $(window).resize(function () {
        $('#ssl_warning').css('top', $('#header').height() + 20);
    });
    $('#_login button').click(function () {
        try {
            var guid = $('#_guid').val();
            var pass = $('#_password').val();
            var id = ajax("user_by_guid", "guid=" + guid).Data.ID;
            var salt = ajax("auth_salt", "id=" + id).Data;
            var hash = gethash(pass, salt);
            var res = ajax("auth_login", "id=" + id + "&hash=" + hash).Data;

            if (res.Success) {
                alert(res.Session);

                session = res.Session;

                $.cookie("_session", session, { expires: 3600 });

                window.location.reload();
            }
            else
                throw null;
        }
        catch (ex) {
            alert("§login_invalid§");
        }
    });
    $('#_login form').submit(function (event) { event.preventDefault(); });
});

function ajax(operation, params) {
    var result;

    $.ajax({
        url: api_uri + "&session=" + session + "&operation=" + operation + "&" + params,
        async: false,
        cache: false,
        dataType: 'json',
        success: function (dat) {
            result = dat;
        }
    });

    return result;
}

function gethash(password, salt) {
    return sha512(password + '+' + salt.toUpperCase());
}

function getlanguages() {
    var langs;

    $.ajax({
        url: api_uri + "&operation=available_lang&session=" + session,
        async: false
    }).done(function () {
        langs = $(this).Data;
    });

    return langs;
}
