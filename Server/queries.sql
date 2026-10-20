-- MMM 로그 분석 쿼리 (D1 Console에서 실행). 테이블: logs(범용) · stage_end(스테이지 종료 전용). eid가 PRIMARY KEY라 중복은 이미 빠져 있다.
-- 읽기 한도(하루 500만 행)는 "읽은 행 수"로 센다. 인덱스 i_logs_event_cts(event, cts) · i_stage_cts(cts)를 타도록
--   모든 쿼리는 최근 7일(cts >= 7일 전)만 읽는다. 그보다 오래된 기간은 따로 필요할 때만 '-7 days'를 바꿔 쓴다.
--   logs는 event 조건을 꼭 같이 건다 (event 없이 cts만 걸면 인덱스를 못 탄다).
-- 시각: cts = 기기 시각(로그가 생긴 때), ts = 서버가 받은 시각(묶어 보내 늦게 올 수 있다). 날짜는 cts 기준 한국 시간(+9시간)
-- 묶인 줄: 세계수 반복 레벨업·tree_act, 21스테이지부터의 보스 구간, 엔딩 뒤(post = 1)는 여러 건이 한 줄이다. 건수는 SUM(IFNULL(c, 1))로 센다

-- 1) 하루 접속 유저(DAU)·세션 수
SELECT date((cts + 32400000) / 1000, 'unixepoch') AS day, COUNT(DISTINCT uid) AS dau, COUNT(*) AS sessions
FROM logs WHERE event = 'session_start' AND cts >= strftime('%s', 'now', '-7 days') * 1000
GROUP BY day ORDER BY day;

-- 2) 신규 유저 (처음 실행 first = 1 이고 세이브가 없던 기기)
SELECT date((cts + 32400000) / 1000, 'unixepoch') AS day, COUNT(DISTINCT uid) AS new_users
FROM logs WHERE event = 'session_start' AND cts >= strftime('%s', 'now', '-7 days') * 1000
  AND json_extract(data, '$.first') = 1 AND json_extract(data, '$.bal') IS NULL
GROUP BY day ORDER BY day;

-- 3) 다음날 복귀율(D1): 처음 실행한 날의 다음 날에도 접속한 비율 (가장 최근 날은 아직 다음 날이 없어 낮게 나온다)
WITH s AS (SELECT uid, date((cts + 32400000) / 1000, 'unixepoch') AS d, json_extract(data, '$.first') AS f
           FROM logs WHERE event = 'session_start' AND cts >= strftime('%s', 'now', '-7 days') * 1000),
     u AS (SELECT uid, MIN(CASE WHEN f = 1 THEN d END) AS d0 FROM s GROUP BY uid),
     b AS (SELECT DISTINCT s.uid FROM s JOIN u ON u.uid = s.uid AND s.d = date(u.d0, '+1 day'))
SELECT u.d0, COUNT(*) AS new_users, COUNT(b.uid) AS back_d1, ROUND(100.0 * COUNT(b.uid) / COUNT(*), 1) AS pct
FROM u LEFT JOIN b ON b.uid = u.uid WHERE u.d0 IS NOT NULL GROUP BY u.d0 ORDER BY u.d0;

-- 4) 스테이지 도달 깔때기: 유저별 최고 클리어 스테이지 분포 (최근 7일에 플레이한 유저)
SELECT max_stage, COUNT(*) AS users
FROM (SELECT uid, MAX(stage) AS max_stage FROM stage_end
      WHERE cts >= strftime('%s', 'now', '-7 days') * 1000 AND result = 'clear' GROUP BY uid)
GROUP BY max_stage ORDER BY max_stage;

-- 5) 스테이지별 결과 (보스 실패율·중도 포기). 21스테이지부터 일반 판은 보스 줄에 합쳐 오므로 판 수는 plays(SUM c)로 본다. 엔딩 뒤 줄은 뺀다
SELECT stage, boss, COUNT(*) AS rows_, SUM(IFNULL(json_extract(data, '$.c'), 1)) AS plays,
       SUM(result = 'clear') AS clear, SUM(result = 'fail') AS fail, SUM(result = 'early_exit') AS early_exit,
       ROUND(AVG(CASE WHEN result != 'clear' AND boss = 1 THEN boss_hp END), 1) AS avg_boss_hp_left
FROM stage_end
WHERE cts >= strftime('%s', 'now', '-7 days') * 1000 AND json_extract(data, '$.post') IS NULL
GROUP BY stage, boss ORDER BY stage;

-- 6) 보스 첫 처치까지 도전 횟수 (유저별·스테이지별, 처치한 유저만)
WITH b AS (SELECT uid, stage, cts, result,
                  MIN(CASE WHEN result = 'clear' THEN cts END) OVER (PARTITION BY uid, stage) AS first_clear
           FROM stage_end WHERE cts >= strftime('%s', 'now', '-7 days') * 1000 AND boss = 1 AND json_extract(data, '$.post') IS NULL)
SELECT stage, COUNT(*) AS users, ROUND(AVG(tries), 1) AS avg_tries, MAX(tries) AS max_tries
FROM (SELECT uid, stage, COUNT(*) AS tries FROM b WHERE first_clear IS NOT NULL AND cts <= first_clear GROUP BY uid, stage)
GROUP BY stage ORDER BY stage;

-- 7) 초반 구간(FTUE) 퍼널: 최근 7일 신규 유저가 각 단계에 도달한 수
--    0 첫 실행 · 1 새 게임(시작 버튼) · 2 첫 발사 · 3 첫 판 끝 · 4 첫 업그레이드 · 5 첫 세계수
--    한 번만 훑도록 uid별 플래그로 모은다 (조인하면 인덱스를 반복해서 타 읽기가 수십 배가 된다)
WITH u AS (SELECT uid,
                  MAX(event = 'session_start' AND json_extract(data, '$.first') = 1 AND json_extract(data, '$.bal') IS NULL) AS nw,
                  MAX(event = 'ftue' AND json_extract(data, '$.step') = 1) AS s1, MAX(event = 'ftue' AND json_extract(data, '$.step') = 2) AS s2,
                  MAX(event = 'ftue' AND json_extract(data, '$.step') = 3) AS s3, MAX(event = 'ftue' AND json_extract(data, '$.step') = 4) AS s4,
                  MAX(event = 'ftue' AND json_extract(data, '$.step') = 5) AS s5
           FROM logs WHERE event IN ('session_start', 'ftue') AND cts >= strftime('%s', 'now', '-7 days') * 1000 GROUP BY uid)
SELECT SUM(nw) AS s0_first_run, SUM(nw * s1) AS s1_new_game, SUM(nw * s2) AS s2_first_launch, SUM(nw * s3) AS s3_first_round,
       SUM(nw * s4) AS s4_first_upgrade, SUM(nw * s5) AS s5_first_tree
FROM u;

-- 8) 에러 상위 (메시지별)
SELECT json_extract(data, '$.msg') AS msg, json_extract(data, '$.at') AS at, COUNT(*) AS n, COUNT(DISTINCT uid) AS users
FROM logs WHERE event = 'error' AND cts >= strftime('%s', 'now', '-7 days') * 1000
GROUP BY msg, at ORDER BY n DESC LIMIT 30;

-- 9) 크래시 (플랫폼·버전·마지막 스테이지별). 0.0.6 이하 웹은 탭 닫기·새로고침도 crash로 찍혔다 (0.0.7부터 고침)
SELECT platform, version, json_extract(data, '$.stage') AS stage, COUNT(*) AS n
FROM logs WHERE event = 'crash' AND cts >= strftime('%s', 'now', '-7 days') * 1000
GROUP BY platform, version, stage ORDER BY n DESC;

-- 10) 플레이 시간: 세션 안 연속된 로그 사이가 5분 이하인 구간만 더한다 (탭을 띄워 두고 자리를 비운 시간 제외)
WITH e AS (SELECT session, uid, t FROM logs WHERE event IN ('session_start', 'ftue', 'tree_up', 'tree_build', 'tree_act', 'quest_clear') AND cts >= strftime('%s', 'now', '-7 days') * 1000
           UNION ALL SELECT session, uid, t FROM stage_end WHERE cts >= strftime('%s', 'now', '-7 days') * 1000),
     g AS (SELECT session, uid, t - LAG(t) OVER (PARTITION BY session ORDER BY t) AS gap FROM e)
SELECT COUNT(DISTINCT session) AS sessions, ROUND(SUM(CASE WHEN gap <= 300 THEN gap END) / COUNT(DISTINCT session) / 60, 1) AS avg_active_min
FROM g;

-- 11) 재화 정합성 검사: 직전 bal + gain − spent + etc = 이번 bal 이 안 맞는 stage_end (uid별 기기 시각순)
--     새 게임(스테이지가 줄어듦)은 빼고, 아주 큰 골드의 소수점 오차는 허용한다. 반복·큰 차이만 의심한다
WITH s AS (SELECT uid, eid, stage, gain, spent, etc, bal, cts,
                  LAG(bal) OVER (PARTITION BY uid ORDER BY cts, t) AS prev_bal,
                  LAG(stage) OVER (PARTITION BY uid ORDER BY cts, t) AS prev_stage
           FROM stage_end WHERE cts >= strftime('%s', 'now', '-7 days') * 1000)
SELECT uid, eid, stage, prev_bal, gain, spent, etc, bal, bal - (prev_bal + gain - spent + IFNULL(etc, 0)) AS diff
FROM s
WHERE prev_bal IS NOT NULL AND stage >= prev_stage
  AND ABS(bal - (prev_bal + gain - spent + IFNULL(etc, 0))) > MAX(1, 1e-9 * ABS(bal))
ORDER BY uid, cts;

-- 12) 의심 uid 요약 (11번이 3번 이상 어긋난 uid). 지표에서 뺄 때 NOT IN (...)으로 쓴다
WITH s AS (SELECT uid, stage, bal,
                  bal - (LAG(bal) OVER (PARTITION BY uid ORDER BY cts, t) + gain - spent + IFNULL(etc, 0)) AS diff,
                  LAG(stage) OVER (PARTITION BY uid ORDER BY cts, t) AS prev_stage
           FROM stage_end WHERE cts >= strftime('%s', 'now', '-7 days') * 1000)
SELECT uid, COUNT(*) AS mismatches, MAX(ABS(diff)) AS worst
FROM s WHERE diff IS NOT NULL AND stage >= prev_stage AND ABS(diff) > MAX(1, 1e-9 * ABS(bal))
GROUP BY uid HAVING COUNT(*) >= 3 ORDER BY mismatches DESC;

-- 13) 플랫폼·버전별 유저 수 (0.0.6까지는 모두 0.0.2로 찍혔다)
SELECT platform, version, COUNT(DISTINCT uid) AS users
FROM logs WHERE event = 'session_start' AND cts >= strftime('%s', 'now', '-7 days') * 1000 GROUP BY platform, version;

-- ===== 세계수 지표 (최근 7일 활동 기준) =====
-- 세계수 진입 유저 = ftue 5단계 또는 세계수 로그가 하나라도 있는 유저 (5단계는 10-16~0.0.6에서 빠져 있었다)

-- 14) 꼬마 퍼널: 세계수 유저 → 해금(세계수 단계 ≥ 합류 단계) → 보유(구매·강화·각성 중 하나) → 구매 로그(unlock_) → 강화(klv_) → 각성(awaken_)
--     보유는 구매 로그가 생기기 전(10-16 이전) 자동 합류한 꼬마까지 센다. leaf는 무료 자동 합류라 보유 = 세계수 유저
WITH k(kid, lv) AS (VALUES ('leaf',1),('dew',2),('wind',3),('fire',4),('rock',5),('spark',6),('moon',7),('star',8)),
e AS MATERIALIZED (SELECT uid, json_extract(data,'$.id') id, json_extract(data,'$.lv') lv, json_extract(data,'$.out') out, json_extract(data,'$.out_gem') gem, IFNULL(json_extract(data,'$.c'), 1) c
                   FROM logs WHERE event IN ('tree_up', 'tree_build', 'tree_act', 'quest_clear', 'ftue') AND cts >= strftime('%s', 'now', '-7 days') * 1000
                     AND (event != 'ftue' OR json_extract(data,'$.step') = 5)),
st AS (SELECT uid, MAX(CASE WHEN id = 'stage' THEN lv ELSE 1 END) stage FROM e GROUP BY uid),
ids AS (SELECT id, COUNT(DISTINCT uid) users, SUM(c) n, SUM(out) gold, SUM(gem) gem, MAX(lv) maxlv FROM e WHERE id IS NOT NULL GROUP BY id),
own AS (SELECT substr(id, instr(id, '_') + 1) kid, COUNT(DISTINCT uid) users FROM e
        WHERE id GLOB 'unlock_*' OR id GLOB 'klv_*' OR id GLOB 'awaken_*' GROUP BY kid)
SELECT k.kid, k.lv AS open_stage,
       (SELECT COUNT(*) FROM st) AS tree_users,
       (SELECT COUNT(*) FROM st WHERE stage >= k.lv) AS opened,
       CASE WHEN k.lv = 1 THEN (SELECT COUNT(*) FROM st) ELSE IFNULL(o.users, 0) END AS owned,
       IFNULL(b.users, 0) AS bought_log, b.gem AS buy_gem,
       IFNULL(u.users, 0) AS leveled, IFNULL(u.n, 0) AS lvups, u.maxlv AS max_lv, u.gold AS lv_gold,
       IFNULL(a.users, 0) AS awakened, a.gem AS awaken_gem
FROM k LEFT JOIN own o ON o.kid = k.kid
       LEFT JOIN ids b ON b.id = 'unlock_' || k.kid
       LEFT JOIN ids u ON u.id = 'klv_' || k.kid
       LEFT JOIN ids a ON a.id = 'awaken_' || k.kid
ORDER BY k.lv;

-- 15) 세계수 요약: 진입·활동별 유저 수와 횟수
SELECT COUNT(DISTINCT uid) AS entered,
       COUNT(DISTINCT CASE WHEN event = 'tree_up' AND json_extract(data,'$.id') = 'ring' THEN uid END) AS ring_users,
       COUNT(DISTINCT CASE WHEN event = 'tree_up' AND json_extract(data,'$.id') = 'stage' THEN uid END) AS evolved,
       COUNT(DISTINCT CASE WHEN event = 'tree_build' THEN uid END) AS crafters, SUM(event = 'tree_build') AS crafts,
       COUNT(DISTINCT CASE WHEN event = 'tree_up' AND json_extract(data,'$.id') GLOB 'deco_*' THEN uid END) AS deco_enhancers,
       COUNT(DISTINCT CASE WHEN event = 'quest_clear' THEN uid END) AS questers, SUM(event = 'quest_clear') AS quests,
       COUNT(DISTINCT CASE WHEN json_extract(data,'$.act') = 'tribute' THEN uid END) AS tributers,
       COUNT(DISTINCT CASE WHEN json_extract(data,'$.act') = 'gift' THEN uid END) AS gifters,
       COUNT(DISTINCT CASE WHEN json_extract(data,'$.act') = 'craft_rush' THEN uid END) AS rushers
FROM logs
WHERE event IN ('tree_up', 'tree_build', 'tree_act', 'quest_clear', 'ftue') AND cts >= strftime('%s', 'now', '-7 days') * 1000
  AND (event != 'ftue' OR json_extract(data,'$.step') = 5);

-- 16) 해금 → 구매까지 걸린 시간 (분, 기기 시각 기준. 앱을 끈 시간도 들어가니 평균보다 중앙값·최소를 본다)과 구매 때 남은 균사석
--     진화 줄이 7일보다 오래됐으면 빠진다
WITH e AS MATERIALIZED (SELECT uid, cts, json_extract(data,'$.id') id, json_extract(data,'$.lv') lv, json_extract(data,'$.gem') gem
                        FROM logs WHERE event = 'tree_up' AND cts >= strftime('%s', 'now', '-7 days') * 1000)
SELECT substr(b.id, 8) AS kid, COUNT(*) AS buyers,
       ROUND(AVG((b.cts - s.cts) / 60000.0), 1) AS avg_min, ROUND(MIN((b.cts - s.cts) / 60000.0), 1) AS min_min, ROUND(MAX((b.cts - s.cts) / 60000.0), 1) AS max_min,
       ROUND(AVG(b.gem), 0) AS avg_gem_left
FROM e b JOIN e s ON s.uid = b.uid AND s.id = 'stage' AND s.lv = b.lv
WHERE b.id GLOB 'unlock_*'
GROUP BY b.id ORDER BY b.lv;

-- ===== 엔딩 뒤 =====
-- 17) 엔딩을 본 유저의 최종 스테이지 (엔딩 뒤에는 세션마다 한 줄: 마지막 스테이지 · 판 수 c · 실패·중도 포기 nf)
SELECT uid, MAX(stage) AS last_stage, SUM(IFNULL(json_extract(data, '$.c'), 1)) AS plays, SUM(IFNULL(json_extract(data, '$.nf'), 0)) AS fails,
       MAX(bal) AS bal, date((MAX(cts) + 32400000) / 1000, 'unixepoch') AS last_day
FROM stage_end WHERE cts >= strftime('%s', 'now', '-7 days') * 1000 AND json_extract(data, '$.post') = 1
GROUP BY uid ORDER BY last_stage DESC;
