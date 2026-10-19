# -*- coding: utf-8 -*-
"""2026-10-19 다국어 검수 결과(loc/검수_*.md)의 치명·높음 항목을 String 시트에 넣을 JSON으로 만든다.
db.json(현재 값)을 읽어 BalanceData/db/loc_fix_set.json(칸 바꾸기)·loc_fix_rows.json(새 줄)·loc/수정내역.tsv(전후 비교)를 쓴다.
쓰기는 db_write.py가 한다: set / rows"""
import io
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DB = json.load(io.open(os.path.join(ROOT, "Assets", "Resources", "DB", "db.json"), encoding="utf-8"))["String"]
BY = {str(r["Str_ID"]): r for r in DB}
LANGS = ["EN", "JA", "RU", "UK"]
fix = {l: {} for l in LANGS}      # 언어 -> {ID: 새 글}


def cur(l, i):
    return fix[l].get(i, BY[i].get("Str_" + l) or "")


def sub(l, ids, pairs, guard=None):
    """ids(None이면 전체)의 현재 글에 정규식 치환. 바뀐 행만 fix에 넣는다."""
    for i in (ids if ids is not None else list(BY)):
        if i not in BY:
            sys.exit("없는 ID " + i)
        if guard and not guard(BY[i]):
            continue
        t = cur(l, i)
        n = t
        for a, b in pairs:
            n = re.sub(a, b, n)
        if n != t:
            fix[l][i] = n


def put(l, d):
    for i, t in d.items():
        if i not in BY:
            sys.exit("없는 ID " + i)
        fix[l][i] = t


# ===================== 4개 언어 공통 =====================
KO_UP = "한 라운드의 제한시간을 늘려요."
assert BY["201006"]["Str_KR"].startswith(KO_UP)
put("EN", {
    "201006": "Increases a round's time limit. Rounds last 10 seconds in Stages 1-4, then get 0.5 seconds longer each stage after that (up to +10 seconds). This bonus is added on top.",
    "900153": "Unlocks the skill{0}",
    "900154": ": 10% trigger chance",
    "901080": "Zone Capacity +1",
    "303033": "Rare Spores",
    "203033": "Epic-or-higher mushrooms sprout more often. Max it out to reach the forest's natural rate.",
})
put("JA", {
    "201006": "1ラウンドの制限時間を延ばします。ラウンドはステージ1〜4は10秒、それ以降は10秒からステージごとに0.5秒ずつ(最大+10秒)長くなり、そこにこの分が加わります。",
    "900153": "解放すると{0}使用開始",
    "900154": "発動率10%で",
    "901080": "エリアの定員 +1",
    "303033": "希少な胞子",
    "203033": "エピック以上のキノコがよく生えるようになります。最大まで上げると、森本来の頻度になります。",
})
put("RU", {
    "201006": "Увеличивает лимит времени раунда. На этапах 1-4 раунд длится 10 с, дальше - 10 с плюс 0,5 с за каждый этап (максимум +10 с); бонус добавляется сверху.",
    "900153": "Открывает навык{0}",
    "900154": ": шанс срабатывания 10%",
    "901080": "Мест в зоне +1",
    "303033": "Редкие споры",
    "203033": "Эпические и более редкие грибы появляются чаще. На максимуме частота станет естественной для леса.",
})
put("UK", {
    "201006": "Збільшує ліміт часу раунду. На етапах 1-4 раунд триває 10 с, далі - 10 с плюс 0,5 с за кожен етап (макс. +10 с); цей бонус додається зверху.",
    "900153": "Відкриває навичку{0}",
    "900154": ": шанс спрацювання 10%",
    "901080": "Місткість зони +1",
    "303033": "Рідкісні спори",
    "203033": "Епічні й рідкісніші гриби з'являються частіше. На максимумі - природна частота лісу.",
})

# ===================== 영어 =====================
put("EN", {
    "901157": "Not enough EXP (offer Granary mushrooms or complete Quests)",
    "900203": "Complete {n} daily quests",
    "900327": "Yellow Coral Mushroom",
    "900385": "Sea Coral Mushroom",
    "900002": "Fairy Ring Mushroom",
    "900247": "Cinnabar Polypore",
    "900222": "Artist's Conk",
    "900061": "When Little Ember picks mushrooms, Mycelium Stone +{0}",
    "900069": "Construction time −{0} when Little Pebble builds",
    "900094": "Little Lava", "900095": "Little Neon", "900096": "Little Snowflake", "900097": "Little Moneybags",
    "900098": "Little Galaxy", "900099": "Little Cherry Blossom", "900100": "Little Sunny", "900101": "Little Cloud",
    "900102": "Little Gem", "900103": "Little Strawberry Choco",
    "302053": "Combo Factor II",
    "900156": "Speed Up and Clear Spore Veil last {0}, Sharpen {1} hits",
    "900172": "Power Up: Attack +{0}% · trigger chance +{1}%",
    "301029": "Power Up Boost",
    "900184": "Payout Gold ×{0}",
    "901155": "Tree Rings Lv.{0}! Tree Mushrooms grow faster",
    "901156": "Grow your Tree Rings to Lv.{0} first",
    "900977": "Round Harvester speed +{0}",
    "901193": "A village covered in giant mushrooms: the Harvester of a seasoned mushroom hunter",
    "901194": "Grab the Harvester, pull back, and release to launch!",
    "301004": "Harvester Speed",
    "900554": "Skill · Chance to trigger when hitting mushrooms",
    "900555": "Skill · Chance to trigger when picking mushrooms",
    "900556": "Skill · Chance to trigger when bouncing off walls",
    "900557": "Skill · Chance to pierce when hitting hard mushrooms",
})
sub("EN", ["900641"], [(r"requests", "Quests")])
sub("EN", [str(i) for i in range(900452, 900471, 2)], [(r"^(.+) Little One$", r"Little \1")])
sub("EN", ["900453", "900469"], [(r"(?:the )?(Ember|Pebble) Little One", r"Little \1")])
sub("EN", ["900580"], [(r"Mushroom Cabin", "Mushroom Cottage")])
sub("EN", ["302054"], [(r"Auto-Harvest", "Auto Harvest")])
sub("EN", None, [(r"Settlement", "Payout"), (r"settlement", "payout")])
sub("EN", None, [(r"Growth Ring", "Tree Rings")])
sub("EN", ["901195"], [(r"the pinball in a round", "your Harvesters in a round")])
sub("EN", None, [(r"proc rates", "trigger chances"), (r"proc rate", "trigger chance"), (r"Proc Rate", "Trigger Chance"),
                 (r"trigger rate", "trigger chance")])

# ===================== 일본어 =====================
put("JA", {
    "301032": "果てなき闘志",
    "303032": "果てなき豊作",
    "900247": "ヒイロタケ",
})
sub("JA", ["201021"], [(r"果てなき闘志", "果てなき闘志")])
sub("JA", ["900061", "900069"], [(r"火種ちび(?!っこ)", "ほのおちびっこ"), (r"小石ちび(?!っこ)", "こいしちびっこ")])
sub("JA", None, [(r"ちび(?!っこ)", "ちびっこ")], guard=lambda r: "꼬마" in r["Str_KR"])

# ===================== 러시아어 =====================
put("RU", {
    "900747": "Бонусное золото <size=70%><color=#8a6a4a>(сборщик-монета)</color></size>",
    "900589": "Свободных малышей: {0}/{1}",
    "900628": "Свободных малышей <b>{0}/{1}</b>",
    "900138": "Шанс {0} для каждого гриба",
    "900222": "Трутовик плоский",
    "900204": "Навозник белый",
    "900433": "Панеолус",
    "900247": "Трутовик киноварный",
    "900506": "{0}: все ×{1}",
    "900527": "Золотой вариант: {0}!",
    "900752": "Золото: {0}",
    "900538": "Появился гигант: {0}!",
    "900760": 'Вызов боссу выключен. Пофарми на этом этапе, а потом снова брось вызов кнопкой "Вызов боссу" в Древе мицелия',
    "900872": "* Никогда не ешь и не трогай их в реальной жизни",
})
sub("RU", None, [(r"Чистая спорная завеса", "Очищающая завеса"), (r"Чистой спорной завесы", "Очищающей завесы"),
                 (r"спорных облаков", "споровых облаков"), (r"спорный взрыв", "споровый взрыв"),
                 (r"Спорный взрыв", "Споровый взрыв"),
                 (r"спорного взрыва", "спорового взрыва")])
sub("RU", None, [(r"Жёлудёвый", "Желудёвый"), (r"жёлудёвый", "желудёвый")])
sub("RU", ["900882"], [(r"Стройте", "Строй")])
sub("RU", ["901049"], [(r"к вам присоединяется", "к тебе присоединяется")])
sub("RU", ["901068"], [(r"вы собрали", "ты собрал")])
sub("RU", ["901127"], [(r"все ваши украшения", "все твои украшения")])
sub("RU", ["901163"], [(r"Вы принесли", "Ты принёс")])
sub("RU", ["201021"], [(r"Бесконечный боевой дух", "Неукротимый дух")])

# ===================== 우크라이나어 =====================
put("UK", {
    "900747": "Бонусне золото <size=70%><color=#8a6a4a>(Монетний збирач)</color></size>",
    "900802": "Головне меню",
    "900893": "У головне меню",
    "900433": "Панеолус",
    "201021": "Додає найбільше з усіх. Якщо прокачати до кінця, відкриється «Незламний дух».",
    "203032": "Навіть після максимуму «Бали за удар III+» золото виплати (золото за бали) продовжує рости. Максимуму немає.",
    "900447": "Бали за удар",
    "900496": "Мандрівник: колонія блукає полем (бали ×1,5)",
    "900497": "Найбільше балів за раунд",
    "900520": "Жолудь світиться! Бали ×2",
    "900615": "{0} бали ×{1}",
    "900729": "{0} Щедрий урожай! Бали й збір ×{1}",
    "900746": "Отримані бали → золото <size=70%><color=#8a6a4a>(×{0})</color></size>",
    "900830": "Бали ×1,5",
    "900833": "Золото виплати за раунд <size=60%><color=#8a6a4a>(золото за бали + бонус)</color></size>",
    "900834": "Отримані бали → золото",
    "901322": "Бали",
    "901196": "Покращення Дерева міцелію",
    "900773": "Кожен збирач має свою <color=#3a8a2a>унікальну здатність</color>. На кожного постійного збирача припадає один <color=#3a8a2a>слот</color>, а кожен увімкнений збирач займає один слот (лише по одному кожного виду). Решту слотів заповнює Базовий збирач, а збирачі від Розділення чи Двійника є двійниками Базового збирача (20% атаки).",
    "900527": "Золотий різновид: {0}!",
    "900752": "Золотий: {0}",
    "900538": "З'явився гігант: {0}!",
})
sub("UK", ["900061"], [(r"Іскринка", "Жаринка")])
sub("UK", ["202002"], [(r"На 4 рівні розмір початковий\.", "На 4 рівні поле має звичайний (повний) розмір.")])

# ===================== 누락 키 19개 (새 줄) =====================
NEW = [
    # KR, EN, JA, RU, UK
    ("언어", "Language", "言語", "Язык", "Мова"),
    ("소리 설정", "Sound Settings", "サウンド設定", "Настройки звука", "Налаштування звуку"),
    ("효과음", "Sound Effects", "効果音", "Звуковые эффекты", "Звукові ефекти"),
    ("배경음", "Music", "BGM", "Музыка", "Музика"),
    ("메인 메뉴", "Main Menu", "メインメニュー", "Главное меню", "Головне меню"),
    ("음소거: 켜짐", "Mute: On", "ミュート:オン", "Без звука: вкл.", "Без звуку: увімк."),
    ("음소거: 꺼짐", "Mute: Off", "ミュート:オフ", "Без звука: выкл.", "Без звуку: вимк."),
    ("에픽 이상 가중치 ×{0}", "Epic and above weight ×{0}", "エピック以上の重み ×{0}", "Вес эпических и выше ×{0}", "Вага епічних і вище ×{0}"),
    ("얻은 점수 → 골드 (×{0})", "Score Earned → Gold (×{0})", "獲得スコア→ゴールド (×{0})", "Очки → золото (×{0})", "Отримані бали → золото (×{0})"),
    ("신기록! {0} {1}", "New record! {0} {1}", "新記録!{0} {1}", "Новый рекорд! {0} {1}", "Новий рекорд! {0} {1}"),
    ("보스 {0} 처치! 스테이지 {1} 열림", "Boss {0} defeated! Stage {1} unlocked", "ボス{0}を倒した!ステージ{1}が解放されました", "Босс {0} побеждён! Открыт этап {1}", "Боса {0} переможено! Відкрито етап {1}"),
    ("보스 {0}에게 졌어요… 골드 −{1}", "Defeated by Boss {0}… Gold −{1}", "ボス{0}に負けました…ゴールド −{1}", "Босс {0} победил… Золото −{1}", "Бос {0} переміг… Золото −{1}"),
    ("보스 {0}에게 졌어요… 골드 −{1} (−{2}%)", "Defeated by Boss {0}… Gold −{1} (−{2}%)", "ボス{0}に負けました…ゴールド −{1} (−{2}%)", "Босс {0} победил… Золото −{1} (−{2}%)", "Бос {0} переміг… Золото −{1} (−{2}%)"),
    ("눌러서 확인해 보세요", "Tap to check it out", "タップして確認してみましょう", "Нажми, чтобы посмотреть", "Натисни, щоб подивитися"),
    ("{0} 완성! 강화 재료로 보관했어요", "{0} complete! Stored as upgrade material", "{0}ができあがり!強化素材として保管しました", "Готово: {0}! Сохранено как материал для улучшения", "Готово: {0}! Збережено як матеріал для вдосконалення"),
    ("오늘 즉시 완료를 다 썼어요", "You've used up all instant finishes today", "今日の即完了はすべて使い切りました", "Мгновенные завершения на сегодня закончились", "Миттєві завершення на сьогодні закінчилися"),
    ("꼬마가 합류하면 열려요", "Opens when a Little One joins", "ちびっこが仲間入りしたら開きます", "Откроется, когда присоединится малыш", "Відкриється, коли приєднається малюк"),
    ("{0} 합류 시 해금", "Unlocks when {0} joins", "{0}が仲間入りしたら解放", "Откроется, когда присоединится {0}", "Відкриється, коли приєднається {0}"),
    ("빈 칸", "Empty slot", "空き枠", "Пустой слот", "Порожній слот"),
]
have_kr = {r["Str_KR"] for r in DB}
max_id = max(int(r["Str_ID"]) for r in DB if str(r["Str_ID"]).startswith("90"))
max_grp = max(int(r["Str_Group_ID"]) for r in DB if str(r["Str_ID"]).startswith("90"))
rows = []
for kr, en, ja, ru, uk in NEW:
    if kr in have_kr:
        sys.exit("이미 있는 키: " + kr)
    max_id += 1
    max_grp += 1
    rows.append({"Str_ID": max_id, "Str_Group_ID": max_grp, "Str_Name": "ui", "Str_KR": kr,
                 "Str_EN": en, "Str_JA": ja, "Str_RU": ru, "Str_UK": uk})

# ===================== 검사 =====================
PH = re.compile(r"\{[0-9a-z]+(?::[^}]*)?\}")
TAG = re.compile(r"<[^>]+>")
HANGUL = re.compile(r"[가-힣]")
bad = []


def check(kr, t, where, special=False):
    if special:
        return
    if sorted(PH.findall(kr)) != sorted(PH.findall(t)):
        bad.append("%s 자리표시 불일치: %r -> %r" % (where, kr, t))
    if sorted(TAG.findall(kr)) != sorted(TAG.findall(t)):
        bad.append("%s 태그 불일치: %r -> %r" % (where, kr, t))
    if HANGUL.search(t):
        bad.append("%s 한글 섞임: %r" % (where, t))
    if "\t" in t or ("\n" in t) != ("\n" in kr):
        bad.append("%s 줄바꿈/탭" % where)


for l in LANGS:
    for i, t in fix[l].items():
        kr = BY[i]["Str_KR"]
        # 900153/154는 코드가 {0}에 900154를 끼워 넣는 짝이라 자리표시 개수가 원문과 같아야 하는 건 900153만
        check(kr, t, "%s %s" % (l, i), special=(i == "900154"))
for r in rows:
    for l in LANGS:
        check(r["Str_KR"], r["Str_" + l], "NEW %s %s" % (l, r["Str_ID"]))
# 남은 문제어 확인
leftover = []
for i, r in BY.items():
    ru, uk, en = cur("RU", i), cur("UK", i), cur("EN", i)
    if re.search(r"(?i)спорн", ru):
        leftover.append("RU спорн " + i)
    if re.search(r"(?i)proc rate|trigger rate|settlement|growth ring|pinball", en):
        leftover.append("EN 옛 용어 " + i)
if bad or leftover:
    print("\n".join(bad + leftover))
    sys.exit(1)

# ===================== 쓰기 =====================
cells = {}
for l in LANGS:
    for i, t in fix[l].items():
        cells.setdefault(i, {})["Str_" + l] = t
out = os.path.dirname(os.path.abspath(__file__))
io.open(os.path.join(out, "loc_fix_set.json"), "w", encoding="utf-8").write(
    json.dumps({"String": cells}, ensure_ascii=False, indent=1))
io.open(os.path.join(out, "loc_fix_rows.json"), "w", encoding="utf-8").write(
    json.dumps({"String": rows}, ensure_ascii=False, indent=1))
log = io.open(os.path.join(ROOT, "loc", "수정내역.tsv"), "w", encoding="utf-8")
log.write("ID\t언어\t이전\t이후\n")
for i in sorted(cells, key=int):
    for col, t in cells[i].items():
        old = (BY[i].get(col) or "").replace("\n", "\\n")
        log.write("%s\t%s\t%s\t%s\n" % (i, col[4:], old, t.replace("\n", "\\n")))
for r in rows:
    log.write("%s\t(새 줄)\t\t%s\n" % (r["Str_ID"], r["Str_KR"]))
log.close()
print("칸 %d개(행 %d개) 수정, 새 줄 %d개" % (sum(len(v) for v in cells.values()), len(cells), len(rows)))
for l in LANGS:
    print(l, len(fix[l]))
