-- MMM 로그 D1 스키마 (새로 만들 때). 이미 배포된 DB를 바꿀 때는 migrate_v2.sql을 쓴다.
-- 설계: HANDOFF.md 3·4·6절. 쓰기 한도(하루 10만 행, 인덱스 쓰기 포함)를 아끼려고
--   · 별도 인덱스를 두지 않는다
--   · eid(로그 고유 번호)를 PRIMARY KEY로 둔 WITHOUT ROWID 테이블 → 행마다 B-tree 하나만 쓴다. 같은 eid는 INSERT OR IGNORE로 무시(재전송 중복 방지)
-- stage_end는 분석이 많아 칸을 나눈 전용 테이블, 나머지 이벤트는 범용 logs 테이블(data는 JSON 문자열, 최대 4,000자)

CREATE TABLE IF NOT EXISTS logs (
  eid      TEXT PRIMARY KEY,      -- 클라 세션ID-일련번호
  ts       INTEGER NOT NULL,      -- 서버가 받은 시각 (ms)
  cts      INTEGER,               -- 클라 기기 시각 (ms, 큐에 오래 있었으면 ts와 다르다)
  uid      TEXT NOT NULL,         -- 기기 단위 무작위 ID (32자리 16진수)
  game     TEXT NOT NULL,
  version  TEXT,
  platform TEXT,
  session  TEXT,
  event    TEXT NOT NULL,         -- session_start · tree_up · tree_build · tree_act · quest_clear · ftue · crash · error · game_clear
  t        REAL,                  -- 세션 시작 후 경과 초
  data     TEXT                   -- 이벤트별 내용 JSON
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
  result   TEXT NOT NULL,         -- clear · fail(보스, 시간 내 미처치) · early_exit(정산하고 나가기)
  boss     INTEGER NOT NULL,      -- 0/1
  score    REAL,
  gain     REAL,                  -- 획득 재화 (클라 필드 in)
  spent    REAL,                  -- 직전 업그레이드에 쓴 골드 합계 (클라 필드 out)
  bal      REAL,                  -- 정산 후 보유 골드
  etc      REAL,                  -- 업그레이드·이번 판 말고 골드가 바뀐 몫 (직전 bal + gain − spent + etc = bal)
  boss_hp  REAL,                  -- 보스 남은 체력 % (보스 fail·early_exit일 때만)
  sec      REAL,                  -- 나간 시점 (early_exit일 때만)
  up       TEXT,                  -- 업그레이드 변경분 [[Upg_ID, 레벨], ...]
  codex    TEXT,                  -- 새 도감 [버섯 번호, ...] (식용 1·약용 2·독 3 × 100 + 번호)
  data     TEXT                   -- 나머지 필드(out_gem·gem 등) JSON
) WITHOUT ROWID;

-- 기간 조회용 인덱스 (2026-10-20 적용). 분석 쿼리는 cts(기기 시각)로 최근 7일만 읽는다. logs는 event 조건을 같이 걸어야 탄다
CREATE INDEX IF NOT EXISTS i_logs_event_cts ON logs(event, cts);
CREATE INDEX IF NOT EXISTS i_stage_cts ON stage_end(cts);
