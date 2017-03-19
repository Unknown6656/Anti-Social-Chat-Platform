/* AUTO-GENERATED §time:yyyy-MM-dd HH:mm:ss:ffffff§ */

$(document).ready(function () {
    var div_main = $('#main');

    div_main.css('top', ($('#header').height() + 20) + 'px');

    $('#noscript').css('display', 'none');
    $('.blurred').removeClass('blurred');
});

function gethash(password, salt) {
    return sha512(password + '+' + salt.toUpperCase());
}
