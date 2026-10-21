// 분석 로그(Telemetry.cs)용 웹 브라우저 기능
//  · MMM_Beacon: 탭이 닫혀도 끝까지 가는 navigator.sendBeacon으로 로그 묶음을 보낸다 (text/plain이라 CORS 사전 요청이 없다)
//  · MMM_HookVisibility: 탭이 숨거나 닫힐 때(visibilitychange·pagehide) · 다시 보일 때 C#을 부른다
//  · MMM_RunSet/Get: "실행 중" 표시. PlayerPrefs(IndexedDB) 저장은 탭이 닫히면 끝나지 못해 localStorage에 바로 쓴다
mergeInto(LibraryManager.library, {
  MMM_Beacon: function (url, body) {
    try {
      return navigator.sendBeacon(UTF8ToString(url), new Blob([UTF8ToString(body)], { type: "text/plain" })) ? 1 : 0;
    } catch (e) { return 0; }
  },

  MMM_HookVisibility: function (onHidden, onShown) {
    var hidden = function () { {{{ makeDynCall('v', 'onHidden') }}}(); };
    var shown = function () { {{{ makeDynCall('v', 'onShown') }}}(); };
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "hidden") hidden(); else shown();
    });
    window.addEventListener("pagehide", hidden);
  },

  MMM_RunSet: function (on) {
    try { localStorage.setItem("mmm_tlm_run", on ? "1" : "0"); } catch (e) {}
  },

  MMM_RunGet: function () {
    try { return localStorage.getItem("mmm_tlm_run") === "1" ? 1 : 0; } catch (e) { return 0; }
  },
});
