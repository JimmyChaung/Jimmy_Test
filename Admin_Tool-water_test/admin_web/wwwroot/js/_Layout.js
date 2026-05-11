function showLoading(text) {
    var displayText = text ? text : '';
    $('body').append('<div id="loadingOverlay" class="loading-overlay"><div class="spinner-container"><div class="loader">Load&nbsp;ng</div>' + displayText + '</div></div>');
}

function hideLoading() {
    $('#loadingOverlay').remove();
}

function scrollFunction() {
    if (document.body.scrollTop > 20 || document.documentElement.scrollTop > 20) {
        document.getElementById("back-to-top").style.display = "block";
    } else {
        document.getElementById("back-to-top").style.display = "none";
    }
}

function topFunction() {
    document.body.scrollTop = 0;
    document.documentElement.scrollTop = 0;
}