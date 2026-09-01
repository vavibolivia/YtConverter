namespace WatchYtConverter.Services;

/// <summary>
/// youtube.com 페이지에 주입하는 스크립트.
/// 기본 동작은 유튜브 그대로다 — 썸네일을 누르면 영상이 재생된다.
/// 스크립트는 (1) 지금 보고 있는 영상을 호스트에 알리고,
/// (2) '바로 변환' 옵션이 켜져 있을 때만 썸네일 클릭을 가로챈다.
/// </summary>
public static class YouTubeBridge
{
    public const string HomeUrl = "https://www.youtube.com/";

    /// <summary>
    /// 문서 생성 시점마다 실행된다. YouTube 는 SPA 라 페이지 전환에도
    /// 리스너가 유지되도록 document 에 캡처 단계로 한 번만 건다.
    /// </summary>
    public const string BridgeScript = """
(function () {
    // 문서 생성 시마다 실행되므로 기본값을 여기서 잡아둔다.
    // (호스트가 NavigationCompleted 에서 실제 토글 값을 다시 밀어넣는다)
    if (typeof window.__ytcInstantConvert === 'undefined') window.__ytcInstantConvert = false;
    if (window.__ytcHooked) return;
    window.__ytcHooked = true;

    var WATCH = /[?&]v=([A-Za-z0-9_-]{11})/;

    function post(o) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify(o));
        }
    }

    // ---- 지금 보고 있는 영상을 호스트에 보고 ----
    // YouTube 는 history API 로 이동하므로 네비게이션 이벤트만으로는 놓친다.
    // URL 과 제목을 함께 키로 삼아 변화가 있을 때만 보낸다.
    var lastKey = null;

    function reportNav() {
        var m = WATCH.exec(location.href);
        var id = m ? m[1] : null;
        var title = (document.title || '').replace(/\s*-\s*YouTube\s*$/, '').trim();
        var key = (id || location.pathname) + '|' + title;
        if (key === lastKey) return;
        lastKey = key;
        post({
            type: 'nav',
            videoId: id,
            url: id ? 'https://www.youtube.com/watch?v=' + id : null,
            title: title
        });
    }

    setInterval(reportNav, 500);
    window.addEventListener('yt-navigate-finish', reportNav);
    window.addEventListener('popstate', reportNav);
    reportNav();

    // ---- '바로 변환' 이 켜졌을 때만 썸네일 클릭을 가로챈다 ----
    function findVideoLink(el) {
        var a = el && el.closest ? el.closest('a[href]') : null;
        while (a) {
            var href = a.getAttribute('href') || '';
            if (WATCH.test(href) || WATCH.test(a.href || '')) return a;
            a = a.parentElement ? a.parentElement.closest('a[href]') : null;
        }
        return null;
    }

    function titleFor(a) {
        // 썸네일 앵커에는 제목이 없으므로 같은 카드 안에서 제목을 찾는다.
        var card = a.closest(
            'ytd-rich-item-renderer, ytd-video-renderer, ytd-compact-video-renderer, ' +
            'ytd-grid-video-renderer, ytd-playlist-video-renderer, yt-lockup-view-model, ' +
            'ytm-shorts-lockup-view-model, ytd-reel-item-renderer'
        ) || a.parentElement;
        if (card) {
            var t = card.querySelector('#video-title, h3 a, .yt-lockup-metadata-view-model__title, yt-formatted-string#video-title');
            if (t) {
                var s = (t.getAttribute('title') || t.textContent || '').trim();
                if (s) return s;
            }
        }
        return (a.getAttribute('title') || a.getAttribute('aria-label') || '').trim();
    }

    document.addEventListener('click', function (e) {
        if (!window.__ytcInstantConvert) return;          // 기본값: 유튜브 원래 동작
        if (e.button !== 0) return;                       // 좌클릭만
        if (e.ctrlKey || e.shiftKey || e.altKey || e.metaKey) return;

        var a = findVideoLink(e.target);
        if (!a) return;

        var href = a.href || a.getAttribute('href') || '';
        var m = WATCH.exec(href);
        if (!m) return;
        if (!(window.chrome && window.chrome.webview)) return;

        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();

        post({
            type: 'play',
            videoId: m[1],
            title: titleFor(a),
            url: 'https://www.youtube.com/watch?v=' + m[1]
        });
    }, true);
})();
""";

    /// <summary>'썸네일 바로 변환' on/off.</summary>
    public static string SetInstantConvertScript(bool enabled) =>
        $"window.__ytcInstantConvert = {(enabled ? "true" : "false")};";

    /// <summary>MP3 재생을 시작할 때 유튜브 영상 소리가 겹치지 않도록 일시정지한다.</summary>
    public const string PauseVideoScript =
        "(function(){var v=document.querySelector('video');if(v&&!v.paused){v.pause();}})();";
}
