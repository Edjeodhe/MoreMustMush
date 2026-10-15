-- MMM 로그 분석 쿼리 (D1 Console에서 실행). 테이블: logs(범용) · stage_end(스테이지 종료 전용). eid가 PRIMARY KEY라 중복은 이미 빠져 있다.
-- 읽기 한도는 "읽은 행 수"로 센다. 인덱스가 없어 대부분 전체를 훑으니 기간(ts)으로 좁혀 쓴다. ts는 ms (서버가 받은 시각)
-- 날짜는 UTC 기준. 한국 시간으로 보려면 ts + 9*3600000

-- 1) 하루 접속 유저(DAU)·세션 수 (최근 14일)
SELECT date(ts / 1000, 'unixepoch') AS day, COUNT(DISTINCT uid) AS dau, COUNT(*) AS sessions
FROM logs WHERE event = 'session_start' AND ts > (strftime('%s', 'now') - 14 * 86400) * 1000
GROUP BY day ORDER BY day;

-- 2) 신규 유저 (uid의 첫 로그 날짜)
SELECT date(first / 1000, 'unixepoch') AS day, COUNT(*) AS new_users
FROM (SELECT uid, MIN(ts) AS first FROM logs WHERE event = 'session_start' GROUP BY uid)
GROUP BY day ORDER BY day;

-- 3) 다음날 복귀율(D1 retention): 첫날 다음 날에도 접속한 비율
WITH f AS (SELECT uid, MIN(date(ts / 1000, 'unixepoch')) AS d0 FROM logs WHERE event = 'session_start' GROUP BY uid),
     a AS (SELECT DISTINCT uid, date(ts / 1000, 'unixepoch') AS d FROM logs WHERE event = 'session_start')
SELECT f.d0, COUNT(*) AS users, SUM(EXISTS (SELECT 1 FROM a WHERE a.uid = f.uid AND a.d = date(f.d0, '+1 day'))) AS back_d1
FROM f GROUP BY f.d0 ORDER BY f.d0;

-- 4) 스테이지 도달 깔때기: 유저별 최고 클리어 스테이지 분포
SELECT max_stage, COUNT(*) AS users
FROM (SELECT uid, MAX(stage) AS max_stage FROM stage_end WHERE result = 'clear' GROUP BY uid)
GROUP BY max_stage ORDER BY max_stage;

-- 5) 스테이지별 결과 (보스 실패율·중도 이탈률)
SELECT stage, boss, COUNT(*) AS n,
       SUM(result = 'clear') AS clear, SUM(result = 'fail') AS fail, SUM(result = 'early_exit') AS early_exit,
       ROUND(AVG(CASE WHEN result != 'clear' AND boss = 1 THEN boss_hp END), 1) AS avg_boss_hp_left
FROM stage_end GROUP BY stage, boss ORDER BY stage;

-- 6) 보스 첫 처치까지 도전 횟수 (유저별, 스테이지별)
SELECT stage, COUNT(*) AS users, ROUND(AVG(tries), 1) AS avg_tries, MAX(tries) AS max_tries
FROM (SELECT uid, stage, COUNT(*) AS tries FROM stage_end WHERE boss = 1
      AND ts <= (SELECT MIN(s2.ts) FROM stage_end s2 WHERE s2.uid = stage_end.uid AND s2.stage = stage_end.stage AND s2.boss = 1 AND s2.result = 'clear')
      GROUP BY uid, stage)
GROUP BY stage ORDER BY stage;

-- 7) 초반 구간(FTUE) 통과 유저 수 (1 새 게임 · 2 첫 발사 · 3 첫 판 끝 · 4 첫 업그레이드 · 5 첫 세계수)
SELECT json_extract(data, '$.step') AS step, COUNT(DISTINCT uid) AS users
FROM logs WHERE event = 'ftue' GROUP BY step ORDER BY step;

-- 8) 에러 상위 (메시지별)
SELECT json_extract(data, '$.msg') AS msg, json_extract(data, '$.at') AS at, COUNT(*) AS n, COUNT(DISTINCT uid) AS users
FROM logs WHERE event = 'error' GROUP BY msg, at ORDER BY n DESC LIMIT 30;

-- 9) 크래시 (버전·마지막 스테이지별)
SELECT version, json_extract(data, '$.stage') AS stage, COUNT(*) AS n
FROM logs WHERE event = 'crash' GROUP BY version, stage ORDER BY n DESC;

-- 10) 플레이 시간(대략): 세션별 마지막 로그의 t (종료 이벤트가 없어 마지막 로그 시각으로 계산)
SELECT uid, session, MAX(t) AS sec
FROM (SELECT uid, session, t FROM logs UNION ALL SELECT uid, session, t FROM stage_end)
GROUP BY uid, session ORDER BY sec DESC LIMIT 50;

-- 11) 재화 정합성 검사: 직전 bal + gain − spent + etc = 이번 bal 이 안 맞는 stage_end (uid별 시간순)
--     전송 실패로 로그가 빠지면 한두 번 끊겨 보일 수 있다. 반복·큰 차이만 의심한다
SELECT uid, eid, stage, prev_bal, gain, spent, etc, bal, bal - (prev_bal + gain - spent + IFNULL(etc, 0)) AS diff
FROM (SELECT uid, eid, stage, gain, spent, etc, bal, ts,
             LAG(bal) OVER (PARTITION BY uid ORDER BY ts, t) AS prev_bal
      FROM stage_end)
WHERE prev_bal IS NOT NULL AND ABS(bal - (prev_bal + gain - spent + IFNULL(etc, 0))) > 1
ORDER BY uid, ts;

-- 12) 의심 uid 요약 (11번이 여러 번 어긋난 uid). 지표에서 뺄 때 NOT IN (...)으로 쓴다
SELECT uid, COUNT(*) AS mismatches, MAX(ABS(diff)) AS worst
FROM (SELECT uid, bal - (LAG(bal) OVER (PARTITION BY uid ORDER BY ts, t) + gain - spent + IFNULL(etc, 0)) AS diff FROM stage_end)
WHERE diff IS NOT NULL AND ABS(diff) > 1
GROUP BY uid HAVING COUNT(*) >= 3 ORDER BY mismatches DESC;

-- 13) 플랫폼·버전별 유저 수
SELECT platform, version, COUNT(DISTINCT uid) AS users FROM logs WHERE event = 'session_start' GROUP BY platform, version;
