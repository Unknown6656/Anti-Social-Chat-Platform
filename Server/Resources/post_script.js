/* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */

// I cannot use the ES6-backtick syntax, because of incompatiblity with older browsers -__-

if (!Date.now) {
    Date.now = function now() {
        return new Date().getTime();
    };
}

$(document).ready(function () {
    $('#noscript').css('display', 'none');

    inner = $('#header, #content, #footer');
    inner.removeClass('blurred');

    session = $.cookie("_sess");

    $.cookie("_lang", '§lang_code§');
    $('#content').css('top', ($('#header').height() + 20) + 'px');
    $('#content').css('margin-bottom', ($('#footer').height() + 20) + 'px');

    //if (!$.browser.webkit) {
    //    $('#main').html('<div class=".warning">§error_webkit§</div>' + $('#main').html());
    //}

    if (§ssl§/* server-generated */)
        $('#ssl_warning').css('display', 'none');
    else
        $('#ssl_warning a').attr('href', https_uri + '§url§');

    $(window).resize(function () {
        $('#ssl_warning').css('top', $('#header').height() + 20);
    });

    $('#title').click(function () {
        window.location.href = base_uri;
    });
    $('a[href*="#"]:not([href="#"])').click(function () {
        if ((location.pathname.replace(/^\//, '') == this.pathname.replace(/^\//, '')) && (location.hostname == this.hostname)) {
            var target = $(this.hash);

            target = target.length ? target : $('[name=' + this.hash.slice(1) + ']');

            if (target.length) {
                $('html, body').animate({
                    scrollTop: target.offset().top
                }, 1000);

                return false;
            }
        }
    });
    $('#_login input[type=submit]').click(function () {
        try {
            var guid = $('#_guid').val();
            var pass = $('#_password').val();
            var user = ajax("user_by_guid", 'guid=' + guid).Data;

            if (user.IsBlocked) {
                alert("§login_blocked§");

                return;
            }

            var id = user.ID;
            var salt = ajax("auth_salt", 'id=' + id).Data;
            var hash = gethash(pass, salt);
            var res = ajax("auth_login", 'id=' + id + '&hash=' + hash).Data;

            if (res.Success) {
                session = res.Session;

                $.cookie("_sess", session, { expires: 3600 });

                window.location.assign('§protocol§://§host§:§port§§url§');
            }
            else
                throw null;
        }
        catch (ex) {
            printerror('§login_invalid§');
        }
    });
    $('#_login form').submit(function (event) { event.preventDefault(); });
    $('#_search input[type=text]').on('change keyup paste', function () {
        var query = $('#_search input[type=text]').val();
        var listview = $('#_search #_guids');

        listview.html('');

        ajax('user_by_name', 'name=' + query).Data.forEach(function (item) {
            var user = item.Item1;

            listview.html(listview.html() + '\
<div class="' + (user.IsAdmin ? 'admin ' : ' ') + (user.IsBlocked ? 'locked' : '') + '">\
    <div>\
        <b>' + user.Name + (user.IsAdmin ? ' [§search_admin§]' : '') + (user.IsBlocked ? ' [§search_locked§]' : '') + '</b> &#160;\
        <i class="code">{' + user.UUID.toUpperCase() + '}</i><br/>\
        §search_membersince§:\
        <span class="code">' + user.MemberSince + '</span>\
    </div>\
</div>');
        });
    });
    $('#flag').click(function () {
        $(document).keyup(esc_lang);
        $('#lang_change_bg, #lang_change_form').css('display', 'block');
        $('#lang_change_form > div.lang_container').height(270 - $('#lang_change_form > span').height());
        inner.addClass('blurred');
    });
    $('#lang_change_bg').click(function () {
        try {
            $(document).unbind(esc_lang);
        } catch (e) { }

        $('#lang_change_bg, #lang_change_form').css('display', 'none');
        inner.removeClass('blurred');
    });
    $('#globalmessage input[type=button]').click(function () {
        try {
            $(document).unbind(esc_msg);
        } catch (e) { }

        $('#globalmessage').removeClass('open').removeClass('red').removeClass('green');
        inner.removeClass('blurred');
    });

    avail_lang.forEach(function (lang) {
        var nfo = ajax('lang_info', 'code=' + lang);

        if (nfo.Success)
            $('#lang_change_form > div.lang_container').html($('#lang_change_form > div.lang_container').html() +
'<div class="lang_option" data-lang="' + lang + '" style="background: url(\'res~ico_' + lang + '~image/png\') no-repeat #666;">\
    ' + lang.toUpperCase() + '/' + nfo.Data.Name + ' - ' + nfo.Data.EnglishName + ' ' + (nfo.Data.IsBeta ? ' &#160; <i>(beta)</i>' : '') +
'</div>');
    });
    $('#lang_change_form > div.lang_container > div.lang_option').click(function () {
        var lang = $(this).attr('data-lang');

        $.cookie("_lang", lang);

        window.location.reload();
    });

    if ((user != undefined) && (user != null)) {
        $('#footer').css('display', 'block');
        $('#_logout').click(gotomainsite);
        $('#_userinfo').html('<b>' + user.Name + ' <code>{' + user.UUID + '}</code></b>');
    }

    if ($('#_ecountdown').length > 0) {
        var stop = $('#_error input[type=button]');
        var sec = 10;
        var id = setInterval(function () {
            --sec;

            $('#_ecountdown').html(sec);

            if (sec == 0)
                gotomainsite();
        }, 1000);

        stop.click(function () {
            clearInterval(id);

            stop.css('display', 'none');
            stop.parent().css('display', 'none');
        });
    }

    $('#_register form').submit(function (event) {
        event.preventDefault();

        var name = $('#_regname').val();
        var pass = $('#_regpw1').val();

        if (!ajax('can_use_name', 'name=' + name).Data)
            printerror('§login_register_invalname§');
        else if (pass != $('#_regpw2').val())
            printerror('§login_register_pwnonequal§');
        else if ($('.g-recaptcha textarea').val() == "")
            printerror('§login_register_fillcaptcha§');
        else {
            var formdata = $(this).serialize();
            var response = (function () {
                var result;

                $.ajax({
                    url: api_uri + '&operation=auth_register&name=' + name,
                    type: 'get',
                    async: false,
                    cache: false,
                    data: formdata,
                    dataType: 'json',
                    success: function (dat) {
                        result = dat;
                    }
                });

                return result;
            })();

            if (response.Success)
                try {
                    deletecookie('_sess');

                    response = response.Data;
                    var hash = gethash(pass, response.Salt);

                    if (ajax("auth_change_pw", 'id=' + response.ID + '&hash=' + response.Hash + '&newhash=' + hash))
                        printreginfo('\
<span>\
    <span style="font-size: 1.7em;">§login_register_ok_title§</span><br/><br/>\
    §login_register_ok_pretext§ "' + name + '" §login_register_ok_posttext§<br/>\
    §login_register_ok_guid§:<span class="code">{' + response.UUID + '}</span>\
    §login_register_ok_salt§:<span class="code">' + response.Salt + '</span>\
</span>');
                    else
                        throw null;
                } catch (e) {
                    ajax("delete_tmp", 'id=' + response.ID + '&hash=' + response.Hash);   
                }
        }

        return false;
    });

    upatesession();

    if (!is_main_page)
        updatetimeoffs();
});

function deletecookie(name) {
    $.cookie(name, null, { path: '/' });
}

function printreginfo(resp) {
    $('#globalmessage').addClass('open green');

    printcommon(resp);
}

function printerror(msg) {
    $('#globalmessage').addClass('open red');

    printcommon('<h2 style="margin: 0; font-size: 32pt;">§error_error§</h2><br/>' + msg);
}

function printcommon(html) {
    $('#globalmessage span').html(html);

    inner.addClass('blurred');

    $(document).keyup(esc_msg);
}

function ajax(operation, params) {
    clearInterval(ival_sess);

    var uri = api_uri + '&session=' + session + '&operation=' + operation + '&' + params;
    var result;

    $.ajax({
        url: uri,
        async: false,
        cache: false,
        dataType: 'json',
        success: function (dat) {
            result = dat;
        }
    });

    try {
        if ((result.Session != null) && (result.Session != undefined))
            $.cookie('_sess', session = result.Session);
    } catch (e) {
    }

    upatesession();

    return result;
}

function gethash(password, salt) {
    return sha512(password + '+' + salt.toUpperCase());
}

function getlanguages() {
    var langs;

    $.ajax({
        url: api_uri + '&operation=available_lang&session=' + session,
        async: false
    }).done(function () {
        langs = $(this).Data;
    });

    return langs;
}

function gotomainsite() {
    $.cookie("_sess", session = "");

    window.location.href = base_uri;
}

function upatesession() {
    clearInterval(ival_sess);

    ival_sess = setInterval(function () {
        ajax('auth_refr_session', null);
    }, 120000);
}

function updatetimeoffs() {
    var id = setInterval(function () {
        try {
            var time_req = Date.now();
            var resp = ajax("auth_verify_sesion", "");
            var time_res = Date.now();

            time_res -= time_req;
            time_res /= 2;
            server_offs = resp.TimeStamp.SinceUnix - time_req - time_res;

            if ((user != undefined) && (user != null))
                if (resp.Success && (resp.Data == true)) {

                }
                else {
                    clearInterval(id);

                    $('#globalmessage input[type=button]').click(gotomainsite);
                    printerror('§error_messages_session§<br/>§error_messages_logout§');
                }
        } catch (e) {
            clearInterval(id);

            $('#globalmessage input[type=button]').click(gotomainsite);
            printerror('§error_messages_offline§<br/>§error_messages_logout§');
        }
    }, 2000);
}

function esc_lang(e) {
    if (e.keyCode == 27)
        $('#lang_change_bg').click();
}

function esc_msg(e) {
    if ((e.keyCode == 27) /* | (e.keyCode == 13) */)
        $('#globalmessage input[type=button]').click();
}
