/* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */

$(document).ready(function () {
    session = $.cookie("_sess");

    $.cookie("_lang", '§lang_code§');
    $('#content').css('top', ($('#header').height() + 20) + 'px');
    $('#content').css('margin-bottom', ($('#footer').height() + 20) + 'px');

    //if (!$.browser.webkit) {
    //    $('#main').html(`<div class=".warning">§error_webkit§</div>${$('#main').html()}`);
    //}

    if (§ssl§/* server-generated */)
        $('#ssl_warning').css('display', 'none');
    else
        $('#ssl_warning a').attr('href', https_uri + '§url§');

    $('#noscript').css('display', 'none');
    $('.blurred').removeClass('blurred');
    $(window).resize(function () {
        $('#ssl_warning').css('top', $('#header').height() + 20);
    });
    $('#_login input[type=submit]').click(function () {
        try {
            var guid = $('#_guid').val();
            var pass = $('#_password').val();
            var user = ajax("user_by_guid", `guid=${guid}`).Data;

            if (user.IsBlocked) {
                alert("§login_blocked§");

                return;
            }

            var id = user.ID;
            var salt = ajax("auth_salt", `id=${id}`).Data;
            var hash = gethash(pass, salt);
            var res = ajax("auth_login", `id=${id}&hash=${hash}`).Data;

            if (res.Success) {
                session = res.Session;

                $.cookie("_sess", session, { expires: 3600 });

                window.location.assign('§protocol§://§host§:§port§§url§');
            }
            else
                throw null;
        }
        catch (ex) {
            alert("§login_invalid§");
        }
    });
    $('#_login form').submit(function (event) { event.preventDefault(); });
    $('#_search input[type=text]').on('change keyup paste', function () {
        var query = $('#_search input[type=text]').val();
        var listview = $('#_search #_guids');

        listview.html('');

        ajax('user_by_name', 'name=' + query).Data.forEach(function (item) {
            var user = item.Item1;

            listview.html(`${listview.html()}
<div class="${user.IsAdmin ? `admin` : ``} ${user.IsBlocked ? `blocked` : ``}">
    <div>
        <b>${user.Name}${user.IsAdmin ? ` [§search_admin§]` : ``}${user.IsBlocked ? ` [§search_blocked§]` : ``}</b> &#160; 
        <i class="code">{${user.UUID.toUpperCase()}}</i><br/>
        §search_membersince§: <span class="code">${user.MemberSince}</span>
    </div>
</div>`);
        });
    });
});

function ajax(operation, params) {
    var result;

    $.ajax({
        url: `${api_uri}&session=${session}&operation=${operation}&${params}`,
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
        url: `${api_uri}&operation=available_lang&session=${session}`,
        async: false
    }).done(function () {
        langs = $(this).Data;
    });

    return langs;
}
