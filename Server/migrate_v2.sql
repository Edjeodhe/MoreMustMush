-- 배포된 D1(moremustmosh_itch-logs)을 새 구조로 바꾼다. D1 Console에 위에서부터 한 번 붙여 실행.
-- 지금 배포본: logs(id, ts, game, version, platform, session, event, data) + 인덱스 i_event_ts
-- 1) 인덱스 삭제 (쓰기 한도 절약: 로그 1줄이 2행으로 계산되던 것)
DROP INDEX IF EXISTS i_event_ts;

-- 2) 지금 테이블은 지우지 않고 이름만 바꿔 남긴다 (uid·eid가 없던 시험 로그)
ALTER TABLE logs RENAME TO logs_v1;

-- 3) 새 테이블 (schema.sql과 같다)
CREATE TABLE IF NOT EXISTS logs (
  eid      TEXT PRIMARY KEY,
  ts       INTEGER NOT NULL,
  cts      INTEGER,
  uid      TEXT NOT NULL,
  game     TEXT NOT NULL,
  version  TEXT,
  platform TEXT,
  session  TEXT,
  event    TEXT NOT NULL,
  t        REAL,
  data     TEXT
) WITHOUT ROWID;

CREATE TABLE IF NOT EXISTS stage_end (
  eid      TEXT PRIMARY KEY,
  ts       INTEGER NOT NULL,
  cts      INTEGER,
  uid      TEXT NOT NULL,
  game     TEXT NOT NULL,
  version  TEXT,
  platform TEXT,
  session  TEXT,
  t        REAL,
  stage    INTEGER NOT NULL,
  result   TEXT NOT NULL,
  boss     INTEGER NOT NULL,
  score    REAL,
  gain     REAL,
  spent    REAL,
  bal      REAL,
  etc      REAL,
  boss_hp  REAL,
  sec      REAL,
  up       TEXT,
  codex    TEXT,
  data     TEXT
) WITHOUT ROWID;

-- 4) 확인
SELECT name, type FROM sqlite_master WHERE type IN ('table', 'index') ORDER BY name;

-- (선택) 시험 로그가 필요 없으면: DROP TABLE logs_v1;
