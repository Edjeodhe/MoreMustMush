// 세이브 사본용 브라우저 저장소 (BrowserStore.cs)
//  웹 빌드의 persistentDataPath(IndexedDB 폴더)는 페이지 URL마다 달라서, itch.io처럼 올릴 때마다 주소가 바뀌면 새 버전이 빈 폴더를 본다.
//  localStorage는 같은 사이트(origin)면 주소 경로가 달라도 같으므로 여기에 세이브 사본·기기 ID를 둔다.
//  · MMM_StoreGet: 없으면 null(0)  · MMM_StoreSet: 성공 1 / 실패(용량 초과 등) 0  · MMM_StoreDel
mergeInto(LibraryManager.library, {
  MMM_StoreGet: function (key) {
    try {
      var v = localStorage.getItem(UTF8ToString(key));
      if (v === null) return 0;
      var n = lengthBytesUTF8(v) + 1;
      var buf = _malloc(n);
      stringToUTF8(v, buf, n);
      return buf;
    } catch (e) { return 0; }
  },

  MMM_StoreSet: function (key, value) {
    try { localStorage.setItem(UTF8ToString(key), UTF8ToString(value)); return 1; } catch (e) { return 0; }
  },

  MMM_StoreDel: function (key) {
    try { localStorage.removeItem(UTF8ToString(key)); } catch (e) {}
  },
});
