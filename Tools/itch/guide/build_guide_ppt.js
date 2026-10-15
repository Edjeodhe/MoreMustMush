// 실행: GIF는 python Tools/itch/guide/clips.py <폴더> 로 만들고 이 파일 옆(HERE)에 둔다. pptxgenjs가 필요하다 (npm i pptxgenjs, NODE_PATH 지정).
// MoreMustMush 플레이 가이드 (3장) — pptxgenjs, 16:9 (10 x 5.625 in)
const pptxgen = require('pptxgenjs');
const path = require('path');
const { applyTheme } = require('C:/Users/user/AppData/Roaming/Claude/local-agent-mode-sessions/skills-plugin/af65a7be-c802-4dd7-a623-f79dd3b45027/b55869d3-c6ce-445a-9a05-95cde2e3c2a0/skills/pptx/scripts/apply_theme.js');

const ROOT = 'C:/Project/MoreMustMush';
const ART = ROOT + '/Assets/Art/Soft';
const HERE = __dirname;
const OUT = ROOT + '/Marketing/MoreMustMush_PlayGuide.pptx';

const THEME = {
  name: 'MoreMustMush Meadow',
  headFontFace: 'Malgun Gothic',
  bodyFontFace: 'Malgun Gothic',
  colors: {
    dk1: '263A2C', lt1: 'FFFFFF', dk2: '2E8B4E', lt2: 'EEF6E8',
    accent1: '2E8B4E', accent2: 'EC4C8A', accent3: '2E9BE6', accent4: 'F2A51A', accent5: '5E6F60', accent6: 'D6E9C4',
    hlink: 'E0457B', folHlink: 'A23B66',
  },
};

const pres = new pptxgen();
pres.layout = 'LAYOUT_16x9';
pres.title = 'MoreMustMush 플레이 가이드';
pres.theme = { headFontFace: THEME.headFontFace, bodyFontFace: THEME.bodyFontFace };
const C = pres.SchemeColor;

// ---------- 레이아웃
pres.defineSlideMaster({
  title: 'COVER',
  background: { color: 'FFFFFF' },
  objects: [
    { placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 2.35, w: 4.3, h: 0.7, fontSize: 32, bold: true, color: C.text1, margin: 0, valign: 'top', align: 'left' }, text: '' } },
    { placeholder: { options: { name: 'body', type: 'body', x: 0.5, y: 3.05, w: 4.1, h: 0.75, fontSize: 14, color: C.accent5, margin: 0, valign: 'top', align: 'left' }, text: '' } },
  ],
});
pres.defineSlideMaster({
  title: 'CONTENT',
  background: { color: 'FFFFFF' },
  objects: [
    { placeholder: { options: { name: 'title', type: 'title', x: 0.5, y: 0.3, w: 9.0, h: 0.6, fontSize: 28, bold: true, color: C.text2, margin: 0, valign: 'middle', align: 'left' }, text: '' } },
  ],
  slideNumber: { x: 9.2, y: 5.25, w: 0.4, h: 0.25, fontSize: 10, color: C.accent5, align: 'right' },
});

const lines = (arr, o = {}) => arr.map((t, i) => ({ text: t, options: Object.assign({}, o, i < arr.length - 1 ? { breakLine: true } : {}) }));
const shadow = () => ({ type: 'outer', color: '263A2C', opacity: 0.18, blur: 6, offset: 2, angle: 90 });

// ---------- 1. 표지 + 핵심 루프
pres.addSection({ title: '소개' });
let s = pres.addSlide({ masterName: 'COVER', sectionTitle: '소개' });
s.addImage({ path: ROOT + '/Art/Raw_Soft/bg_title.png', x: 4.9, y: 0, w: 5.1, h: 5.625, sizing: { type: 'cover', w: 5.1, h: 5.625 }, objectName: 'Hero background' });
s.addImage({ path: ROOT + '/Art/Raw_Soft/grandpa_full.png', x: 4.75, y: 2.35, w: 3.3, h: 3.3, objectName: 'Grandpa' });
s.addImage({ path: ROOT + '/Art/StyleRef/logo.png', x: 0.45, y: 0.35, w: 3.5, h: 1.92, objectName: 'Logo' });
s.addText('플레이 가이드', { placeholder: 'title' });
s.addText(lines(['거대 버섯에 뒤덮인 마을.', '노련한 버섯 사냥꾼이 되어 수확기를 튕겨', '버섯을 쓸어 담고, 더 크게 성장하세요.']), { placeholder: 'body' });
// 핵심 루프: 발사 → 수확 → 골드 → 강화
const loop = [['발사', ART + '/Harvesters/sam.png'], ['수확', ART + '/Mushrooms/Single/ed0.png'], ['골드', ART + '/Icons/gold.png'], ['강화', ART + '/Icons/hammer.png']];
const lx = 0.5, ly = 4.05, step = 1.08, d = 0.72;
loop.forEach(([label, icon], i) => {
  const x = lx + i * step;
  s.addShape(pres.shapes.OVAL, { x, y: ly, w: d, h: d, fill: { color: C.background2 }, line: { color: C.accent6, width: 1 }, objectName: 'Loop circle ' + (i + 1) });
  s.addImage({ path: icon, x: x + 0.13, y: ly + 0.13, w: 0.46, h: 0.46, sizing: { type: 'contain', w: 0.46, h: 0.46 }, objectName: 'Loop icon ' + label });
  s.addText(label, { x: x - 0.14, y: ly + d + 0.05, w: d + 0.28, h: 0.3, fontSize: 12, bold: true, color: C.text1, align: 'center', margin: 0, isTextBox: true, objectName: 'Loop label ' + label });
  if (i < loop.length - 1) s.addText('›', { x: x + d + 0.02, y: ly + 0.12, w: step - d - 0.04, h: 0.45, fontSize: 22, bold: true, color: C.accent2, align: 'center', margin: 0, isTextBox: true, objectName: 'Loop arrow ' + (i + 1) });
});
s.addText('↻ 반복할수록 수확이 커집니다', { x: lx, y: 5.15, w: 4.2, h: 0.28, fontSize: 11, color: C.accent2, bold: true, margin: 0, isTextBox: true, objectName: 'Loop caption' });
s.addNotes('MoreMustMush는 수확기를 튕겨 버섯을 수확하는 핀볼형 방치·성장 게임입니다. 한 판에서 버섯을 수확해 골드를 벌고, 그 골드로 강화해 다음 판을 더 크게 만드는 반복이 핵심입니다.');

// ---------- 2. 한 판의 흐름
pres.addSection({ title: '수확 라운드' });
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: '수확 라운드' });
s.addText('한 판: 당기고, 튕기고, 터뜨리기', { placeholder: 'title' });
s.addImage({ path: HERE + '/launch.gif', x: 0.5, y: 1.1, w: 5.2, h: 2.925, objectName: 'Launch clip', shadow: shadow() });
const steps = [
  ['1', '당겼다 놓아 발사', ['수확기를 잡고 뒤로 끌었다가', '놓으면 날아갑니다'], C.accent3],
  ['2', '바로 받아치기', ['마우스를 좌우로 움직여', '아래 바로 튕겨 냅니다'], C.accent1],
  ['3', '콤보 → 피버', ['연속으로 맞히면 콤보가 쌓이고', '피버가 터집니다'], C.accent2],
];
steps.forEach(([n, head, desc, col], i) => {
  const y = 1.1 + i * 1.0;
  s.addShape(pres.shapes.OVAL, { x: 6.05, y, w: 0.42, h: 0.42, fill: { color: col }, line: { color: col, width: 0 }, objectName: 'Step badge ' + n });
  s.addText(n, { x: 6.05, y, w: 0.42, h: 0.42, fontSize: 14, bold: true, color: C.background1, align: 'center', valign: 'middle', margin: 0, isTextBox: true, objectName: 'Step number ' + n });
  s.addText(head, { x: 6.6, y: y - 0.02, w: 2.9, h: 0.36, fontSize: 15, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: 'Step title ' + n });
  s.addText(lines(desc), { x: 6.6, y: y + 0.34, w: 2.9, h: 0.55, fontSize: 12, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: 'Step text ' + n });
});
// 조작 키
const keys = [['드래그 & 놓기', '발사'], ['마우스 좌우', '바 이동'], ['F / 우클릭', '배속 ×1·×2·×4'], ['Esc', '일시정지']];
const kw = 2.1, kg = 0.2;
keys.forEach(([k, v], i) => {
  const x = 0.5 + i * (kw + kg);
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y: 4.35, w: kw, h: 0.72, rectRadius: 0.12, fill: { color: C.background2 }, line: { color: C.accent6, width: 1 }, objectName: 'Key card ' + (i + 1) });
  s.addText([{ text: k, options: { bold: true, color: C.text1, fontSize: 13, breakLine: true } }, { text: v, options: { color: C.accent5, fontSize: 11 } }],
    { x: x + 0.12, y: 4.35, w: kw - 0.24, h: 0.72, align: 'center', valign: 'middle', margin: 0, isTextBox: true, objectName: 'Key text ' + (i + 1) });
});
s.addNotes('한 판은 제한 시간 동안 진행됩니다. 수확기를 끌었다 놓아 발사하고, 마우스로 아래 바를 움직여 수확기가 떨어지지 않게 받아칩니다. 버섯을 연속으로 맞히면 콤보가 오르고, 콤보가 쌓이면 피버와 슈퍼 피버가 터져 점수가 크게 늘어납니다. 판이 끝나면 점수가 골드로 바뀝니다.');

// ---------- 3. 성장
pres.addSection({ title: '성장' });
s = pres.addSlide({ masterName: 'CONTENT', sectionTitle: '성장' });
s.addText('판이 끝나면: 강화하고 키우기', { placeholder: 'title' });
const cards = [
  ['균사 트리', ['번 골드로 노드를 사서', '수확기를 더 세고, 빠르고, 많게 만듭니다'], HERE + '/upgrade.gif', C.accent1],
  ['세계수', ['수확물로 세계수를 키우면 꼬마 버섯들이 합류해', '쉬는 동안에도 버섯을 모읍니다'], HERE + '/worldtree.gif', C.accent4],
];
cards.forEach(([head, desc, gif, col], i) => {
  const x = 0.5 + i * 4.65;
  s.addImage({ path: gif, x, y: 1.05, w: 4.35, h: 2.447, objectName: head + ' clip', shadow: shadow() });
  s.addShape(pres.shapes.OVAL, { x, y: 3.66, w: 0.22, h: 0.22, fill: { color: col }, line: { color: col, width: 0 }, objectName: head + ' dot' });
  s.addText(head, { x: x + 0.32, y: 3.6, w: 3.9, h: 0.34, fontSize: 16, bold: true, color: C.text1, margin: 0, valign: 'middle', isTextBox: true, objectName: head + ' title' });
  s.addText(lines(desc), { x, y: 3.98, w: 4.35, h: 0.5, fontSize: 12, color: C.accent5, margin: 0, valign: 'top', isTextBox: true, objectName: head + ' text' });
});
const stats = [['75종', '실존 버섯 도감', ART + '/Icons/book.png'], ['10스테이지마다', '보스 등장', ART + '/Icons/trophy.png'], ['5곳', '날씨가 다른 지역', ART + '/Icons/t_forest.png']];
stats.forEach(([big, small, icon], i) => {
  const x = [0.5, 3.2, 6.95][i];
  s.addImage({ path: icon, x, y: 4.62, w: 0.5, h: 0.5, sizing: { type: 'contain', w: 0.5, h: 0.5 }, objectName: 'Stat icon ' + (i + 1) });
  s.addText([{ text: big + ' ', options: { bold: true, color: C.accent2, fontSize: 16 } }, { text: small, options: { color: C.text1, fontSize: 12 } }],
    { x: x + 0.6, y: 4.62, w: 2.4, h: 0.5, valign: 'middle', margin: 0, isTextBox: true, objectName: 'Stat text ' + (i + 1) });
});
s.addNotes('판이 끝나면 번 골드로 균사 트리의 노드를 사서 수확기를 강화합니다. 수확물은 세계수에 바쳐 세계수를 키우고, 세계수가 자랄 때마다 꼬마 버섯이 합류해 자리를 비운 동안에도 버섯을 모아 둡니다. 도감 75종을 채우고, 10스테이지마다 나오는 보스를 쓰러뜨리며 다섯 지역을 넘어가세요.');

(async () => {
  await pres.writeFile({ fileName: OUT });
  await applyTheme(OUT, THEME);
  console.log('wrote', OUT);
})();
