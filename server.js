import http from 'node:http';
import { readFile, writeFile, mkdir, rename } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { SYSTEM_GUIDES } from './knowledge.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC_DIR = path.join(__dirname, 'public');
const DATA_DIR = path.join(__dirname, 'data');
const DATA_FILE = path.join(DATA_DIR, 'announcements.json');
const TEMP_FILE = path.join(DATA_DIR, 'announcements.tmp');
const PORT = Number(process.env.PORT || 4173);
const HOST = process.env.HOST || '127.0.0.1';
const BASE_URL = 'https://xx.qq.com';
const LIST_PATH = '/webplat/info/news_version3/154/2233/3889/m2702/list_';
const USER_AGENT = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) XunxianArchive/1.0';

const SYSTEM_DEFINITIONS = [
  { id: 'weapon', name: '武器系统', short: '武器', group: '战力养成', icon: 'blade', color: '#75e4b1', description: '武器等级、武魂品阶、阵纹孔洞与相关兑换材料。', keywords: ['武器', '武魂', '阵纹'] },
  { id: 'equipment', name: '装备系统', short: '装备', group: '战力养成', icon: 'armor', color: '#72c9e9', description: '防具、首饰、套装、强化、精炼、镶嵌与宝石迭代。', keywords: ['装备', '防具', '首饰', '套装', '强化', '精炼', '镶嵌', '宝石'] },
  { id: 'attendant-pet', name: '侍宠 · 宝宝', short: '侍宠', group: '宠物伙伴', icon: 'pet', color: '#e2b872', description: '侍宠获取、四维成长、融合、技能与魂器养成。', keywords: ['侍宠', '宝宝', '宠物', '魂器'] },
  { id: 'mount-pet', name: '骑宠系统', short: '骑宠', group: '宠物伙伴', icon: 'mount', color: '#ef8f91', description: '骑宠获取、进化、被动属性、技能和骑宠装备。', keywords: ['骑宠', '坐骑'] },
  { id: 'mental', name: '心法系统', short: '心法', group: '角色修炼', icon: 'scroll', color: '#b8a2ef', description: '各职业心法等级、装备效果、增强道具与调整记录。', keywords: ['心法'] },
  { id: 'yin-yang-jade', name: '阴阳玉系统', short: '阴阳玉', group: '角色修炼', icon: 'yinyang', color: '#c9d5d0', description: '阴阳玉珏的用途、兑换节点和历史获取方式。', keywords: ['阴阳玉', '阴阳玉珏'] },
  { id: 'magic-weapon', name: '法宝系统', short: '法宝', group: '战力养成', icon: 'relic', color: '#f0cd67', description: '法宝阶位、天赋、强化、兑换和绛紫石等材料。', keywords: ['法宝', '绛紫石'] },
  { id: 'spiritual-treasure', name: '灵宝系统', short: '灵宝', group: '角色修炼', icon: 'orb', color: '#69d8d0', description: '灵宝等级、技能、炼化与相关养成材料。', keywords: ['灵宝'] },
  { id: 'mystic-arts', name: '玄法系统', short: '玄法', group: '角色修炼', icon: 'rune', color: '#8eb7ef', description: '玄法品阶、词条、升级消耗和产出途径。', keywords: ['玄法'] },
  { id: 'battle-soul', name: '战魂系统', short: '战魂', group: '战力养成', icon: 'flame', color: '#ee936e', description: '战魂成长、属性、升阶和材料来源。', keywords: ['战魂'] },
  { id: 'treasure-plate', name: '宝盘系统', short: '宝盘', group: '战力养成', icon: 'plate', color: '#d4a3e9', description: '宝盘升级、属性、助力礼包与消耗道具。', keywords: ['宝盘'] },
  { id: 'skills', name: '门派 · 技能', short: '技能', group: '职业成长', icon: 'skill', color: '#7bd7a2', description: '职业技能、门派调整、天赋与神通的版本演进。', keywords: ['门派', '技能', '天赋', '神通'] },
  { id: 'immortal-rank', name: '仙阶 · 仙职', short: '仙阶', group: '角色修炼', icon: 'rank', color: '#e6c77d', description: '仙阶晋升、仙职能力、任务和对应奖励。', keywords: ['仙阶', '仙职'] },
  { id: 'home', name: '家园系统', short: '家园', group: '休闲社交', icon: 'home', color: '#e2a978', description: '家园建设、装饰、生产、互动和材料来源。', keywords: ['家园'] },
  { id: 'guild', name: '仙盟系统', short: '仙盟', group: '休闲社交', icon: 'guild', color: '#87b5e8', description: '仙盟玩法、建筑、贡献、商店与活动奖励。', keywords: ['仙盟'] },
  { id: 'chronicle', name: '风物志 · 称号', short: '风物志', group: '收集外观', icon: 'book', color: '#d6c393', description: '风物志收集、称号奖励、图卷和获取地点。', keywords: ['风物志', '称号'] },
  { id: 'fashion', name: '时装 · 外观', short: '外观', group: '收集外观', icon: 'fashion', color: '#ec9fbd', description: '时装、相框、仙衣华裳和其他外观获取。', keywords: ['时装', '外观', '仙衣华裳', '相框'] },
  { id: 'crafting', name: '生产 · 合成', short: '合成', group: '资源经济', icon: 'craft', color: '#e2a76f', description: '生活技能、配方、道具合成、材料转换和制造。', keywords: ['生活技能', '配方', '合成', '生产'] },
];

const ITERATION_WORDS = ['新增', '开放', '调整', '优化', '修正', '提升', '降低', '改为', '增加', '取消', '移除', '更新', '扩展', '重做', '改动', '开启'];
const MATERIAL_WORDS = ['获得', '获取', '掉落', '兑换', '合成', '产出', '奖励', '购买', '领取', '消耗', '需要', '用于', '可在', '出售', '收购', '概率'];

let announcements = [];
let syncState = {
  running: false,
  phase: 'idle',
  mode: null,
  current: 0,
  total: 0,
  found: 0,
  message: '等待同步',
  startedAt: null,
  updatedAt: null,
  error: null,
};

let systemCache = [];
let guideCache = new Map();

const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.ico': 'image/x-icon',
};

function decodeEntities(value = '') {
  const named = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };
  return value.replace(/&(#x?[0-9a-f]+|[a-z]+);/gi, (_, entity) => {
    if (entity[0] === '#') {
      const hex = entity[1]?.toLowerCase() === 'x';
      const code = Number.parseInt(entity.slice(hex ? 2 : 1), hex ? 16 : 10);
      return Number.isFinite(code) ? String.fromCodePoint(code) : _;
    }
    return named[entity.toLowerCase()] ?? _;
  });
}

function stripTags(value = '') {
  return decodeEntities(
    value
      .replace(/<br\s*\/?\s*>/gi, '\n')
      .replace(/<\/(p|div|li|tr|h[1-6]|blockquote)>/gi, '\n')
      .replace(/<[^>]*>/g, ' '),
  )
    .replace(/\r/g, '')
    .replace(/[\t ]+/g, ' ')
    .replace(/ *\n */g, '\n')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function cleanTitle(value = '') {
  return stripTags(value).replace(/\s+/g, ' ').trim();
}

function absoluteUrl(value = '') {
  try {
    return new URL(value, BASE_URL).toString();
  } catch {
    return '';
  }
}

function sanitizeArticle(html = '') {
  const blocked = /<(script|style|iframe|object|embed|form|input|button|svg|canvas)[^>]*>[\s\S]*?<\/\1>|<(script|style|iframe|object|embed|form|input|button|svg|canvas)[^>]*\/?\s*>/gi;
  const allowed = new Set([
    'p', 'br', 'strong', 'b', 'em', 'i', 'u', 'span', 'div', 'h1', 'h2', 'h3', 'h4',
    'ul', 'ol', 'li', 'blockquote', 'table', 'thead', 'tbody', 'tr', 'td', 'th', 'img', 'a',
  ]);
  return html.replace(/<!--([\s\S]*?)-->/g, '').replace(blocked, '').replace(/<\/?([a-z0-9]+)([^>]*)>/gi, (full, rawTag, attrs) => {
    const tag = rawTag.toLowerCase();
    if (!allowed.has(tag)) return '';
    if (full.startsWith('</')) return `</${tag}>`;
    if (tag === 'br') return '<br>';
    if (tag === 'img') {
      const src = attrs.match(/\bsrc\s*=\s*["']([^"']+)["']/i)?.[1] || '';
      const alt = cleanTitle(attrs.match(/\balt\s*=\s*["']([^"']*)["']/i)?.[1] || '公告图片');
      const url = absoluteUrl(src);
      return url ? `<img src="${escapeAttribute(url)}" alt="${escapeAttribute(alt)}" loading="lazy">` : '';
    }
    if (tag === 'a') {
      const href = attrs.match(/\bhref\s*=\s*["']([^"']+)["']/i)?.[1] || '';
      const url = absoluteUrl(href);
      return url && !/^javascript:/i.test(href)
        ? `<a href="${escapeAttribute(url)}" target="_blank" rel="noopener noreferrer">`
        : '<span>';
    }
    if (tag === 'td' || tag === 'th') {
      const colspan = attrs.match(/\bcolspan\s*=\s*["']?(\d+)/i)?.[1];
      const rowspan = attrs.match(/\browspan\s*=\s*["']?(\d+)/i)?.[1];
      return `<${tag}${colspan ? ` colspan="${colspan}"` : ''}${rowspan ? ` rowspan="${rowspan}"` : ''}>`;
    }
    return `<${tag}>`;
  }).replace(/<span>\s*<\/a>/gi, '</span>');
}

function escapeAttribute(value = '') {
  return value.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

function isVersionNotice(title) {
  return /版本更新公告|版本更新说明|更新维护公告|停机维护公告|例行维护公告|临时维护公告|版本公告|(?:正式服|体验服|测试服).{0,18}更新公告/.test(title);
}

function getVersion(title) {
  return title.match(/\b\d+(?:\.\d+){2,4}\b/)?.[0] || '';
}

function getServer(title) {
  if (/体验服|测试服/.test(title)) return '体验服';
  if (/正式服/.test(title)) return '正式服';
  return '其他';
}

function isXunxianOne(item) {
  return !item.title.includes('寻仙2') && !item.text.includes('寻仙2');
}

function hasAny(text, words) {
  return words.some((word) => text.includes(word));
}

function articleChunks(text = '') {
  return text
    .replace(/\r/g, '')
    .split(/\n+/)
    .flatMap((line) => line.length > 260 ? line.split(/(?<=[。！？；])/u) : [line])
    .map((line) => line.replace(/\s+/g, ' ').trim())
    .filter((line) => line.length >= 8 && !line.includes('寻仙2'));
}

function compactSnippet(value, max = 360) {
  const clean = value.replace(/\s+/g, ' ').trim();
  return clean.length > max ? `${clean.slice(0, max)}…` : clean;
}

function buildSystemCache() {
  systemCache = SYSTEM_DEFINITIONS.map((definition) => {
    const timeline = [];
    const materials = [];
    const materialKeys = new Set();
    for (const item of announcements) {
      const chunks = articleChunks(item.text);
      const matches = chunks.filter((chunk) => hasAny(chunk, definition.keywords));
      if (!matches.length) continue;
      const iteration = matches.find((chunk) => hasAny(chunk, ITERATION_WORDS)) || matches[0];
      timeline.push({
        id: `${definition.id}-${item.id}`,
        announcementId: item.id,
        title: item.title,
        date: item.date,
        version: item.version,
        server: item.server,
        url: item.url,
        snippet: compactSnippet(iteration),
      });
      for (const chunk of matches.filter((value) => hasAny(value, MATERIAL_WORDS)).slice(0, 4)) {
        const key = chunk.replace(/[\s，。；：、（）()\d]/g, '').slice(0, 100);
        if (materialKeys.has(key)) continue;
        materialKeys.add(key);
        materials.push({
          id: `${definition.id}-${item.id}-${materials.length}`,
          announcementId: item.id,
          title: item.title,
          date: item.date,
          version: item.version,
          url: item.url,
          snippet: compactSnippet(chunk),
        });
      }
    }
    return {
      ...definition,
      updateCount: timeline.length,
      materialCount: materials.length,
      latestDate: timeline[0]?.date || null,
      timeline,
      materials,
    };
  });
  buildGuideCache();
}

function materialEvidence(material) {
  const occurrences = [];
  for (const item of announcements) {
    if (!item.text.includes(material.name)) continue;
    const chunks = articleChunks(item.text).filter((chunk) => chunk.includes(material.name));
    for (const chunk of chunks) {
      occurrences.push({
        date: item.date,
        year: item.year,
        summary: compactSnippet(chunk, 300),
        sourceUrl: item.url,
      });
    }
  }
  occurrences.sort((a, b) => a.date.localeCompare(b.date));
  const first = occurrences[0] || null;
  const changes = [];
  const seen = new Set();
  for (const event of occurrences) {
    if (!hasAny(event.summary, [...ITERATION_WORDS, ...MATERIAL_WORDS]) && occurrences.length > 1) continue;
    const key = `${event.year}-${event.summary.replace(/[\s\d，。；：、（）()]/g, '').slice(0, 80)}`;
    if (seen.has(key)) continue;
    seen.add(key);
    changes.push(event);
  }
  const selectedChanges = (changes.length ? changes : occurrences).slice(-8).reverse();
  return {
    ...material,
    firstSeenYear: first?.year || '待考证',
    firstSeenDate: first?.date || null,
    lastChanged: selectedChanges[0]?.date || first?.date || null,
    evidenceCount: occurrences.length,
    changes: selectedChanges,
  };
}

function buildGuideCache() {
  guideCache = new Map();
  for (const system of systemCache) {
    const blueprint = SYSTEM_GUIDES[system.id];
    if (!blueprint) continue;
    const materials = blueprint.materials.map(materialEvidence);
    const stages = blueprint.stages.map((stage, index) => ({
      ...stage,
      order: index + 1,
      materials: materials.filter((material) => material.stage === stage.id),
    }));
    guideCache.set(system.id, {
      system: {
        id: system.id,
        name: system.name,
        group: system.group,
        icon: system.icon,
        color: system.color,
        description: system.description,
        latestDate: system.latestDate,
      },
      stageCount: stages.length,
      materialCount: materials.length,
      stages,
      materials,
    });
  }
}

function getSystemGuide(id) {
  return guideCache.get(id) || null;
}

function listSystems(params) {
  const query = (params.get('q') || '').trim().toLowerCase();
  const group = params.get('group') || 'all';
  const systems = systemCache.filter((system) => {
    if (group !== 'all' && system.group !== group) return false;
    if (!query) return true;
    const text = `${system.name} ${system.short} ${system.description} ${system.keywords.join(' ')}`.toLowerCase();
    return text.includes(query);
  }).map(({ timeline, materials, ...system }) => {
    const guide = guideCache.get(system.id);
    return {
      ...system,
      stageCount: guide?.stageCount || 0,
      materialItemCount: guide?.materialCount || 0,
    };
  });
  const groups = [...new Set(SYSTEM_DEFINITIONS.map((item) => item.group))];
  return { systems, groups, count: systems.length };
}

function getSystemDetail(id, params) {
  const system = systemCache.find((item) => item.id === id);
  if (!system) return null;
  const section = params.get('section') === 'materials' ? 'materials' : 'timeline';
  const query = (params.get('q') || '').trim().toLowerCase();
  const page = Math.max(1, Number(params.get('page') || 1));
  const limit = Math.min(50, Math.max(1, Number(params.get('limit') || 16)));
  const source = system[section].filter((item) => !query || `${item.title} ${item.snippet}`.toLowerCase().includes(query));
  const start = (page - 1) * limit;
  const { timeline, materials, ...summary } = system;
  return {
    system: summary,
    section,
    items: source.slice(start, start + limit),
    total: source.length,
    page,
    pages: Math.max(1, Math.ceil(source.length / limit)),
  };
}

function normalizeDate(value, title, url) {
  if (/^(?:19|20)\d{2}-\d{2}-\d{2}$/.test(value || '')) return value;
  const yearMonth = url.match(/\/(20\d{4})\//)?.[1];
  if (!yearMonth) return value || '';
  const titleDay = Number(title.match(/\d{1,2}月(\d{1,2})(?:日|号)/)?.[1] || 1);
  const day = String(Math.min(31, Math.max(1, titleDay))).padStart(2, '0');
  return `${yearMonth.slice(0, 4)}-${yearMonth.slice(4, 6)}-${day}`;
}

function listPageUrl(pageNumber, totalPages) {
  // 腾讯旧版 CMS 只保留最近 3 页的正向页码，更早页面改用倒序 n1、n2…命名。
  const filePage = pageNumber <= 3 ? String(pageNumber) : `n${totalPages - pageNumber + 1}`;
  return `${BASE_URL}${LIST_PATH}${filePage}.shtml`;
}

async function fetchGbk(url, retries = 2) {
  let lastError;
  for (let attempt = 0; attempt <= retries; attempt += 1) {
    try {
      const response = await fetch(url, {
        headers: { 'user-agent': USER_AGENT, accept: 'text/html,application/xhtml+xml' },
        signal: AbortSignal.timeout(25_000),
      });
      if (!response.ok) throw new Error(`官网返回 ${response.status}`);
      return new TextDecoder('gb18030').decode(await response.arrayBuffer());
    } catch (error) {
      lastError = error;
      if (attempt < retries) await new Promise((resolve) => setTimeout(resolve, 500 * (attempt + 1)));
    }
  }
  throw lastError;
}

function parseListPage(html) {
  const block = html.match(/<ul[^>]*class=["'][^"']*news-list[^"']*["'][^>]*>([\s\S]*?)<\/ul>/i)?.[1] || '';
  const rows = [];
  const itemPattern = /<li[^>]*>([\s\S]*?)<\/li>/gi;
  for (const item of block.matchAll(itemPattern)) {
    const body = item[1];
    const type = cleanTitle(body.match(/<a[^>]*class=["'][^"']*newsType[^"']*["'][^>]*>([\s\S]*?)<\/a>/i)?.[1] || '').replace(/[\[\]【】]/g, '') || '公告';
    const links = [...body.matchAll(/<a[^>]*href=["']([^"']+)["'][^>]*>([\s\S]*?)<\/a>/gi)];
    const articleLink = links.find((match) => !/newsType/i.test(match[0]));
    const date = body.match(/<span[^>]*>(\d{4}-\d{2}-\d{2})<\/span>/i)?.[1];
    if (!articleLink || !date) continue;
    const title = cleanTitle(articleLink[2]);
    if (!isVersionNotice(title) || title.includes('寻仙2')) continue;
    const url = absoluteUrl(articleLink[1]);
    const id = url.match(/\/(\d+)\.shtml(?:$|\?)/)?.[1];
    if (!id) continue;
    const normalizedDate = normalizeDate(date, title, url);
    rows.push({
      id,
      title,
      date: normalizedDate,
      year: normalizedDate.slice(0, 4),
      type,
      server: getServer(title),
      version: getVersion(title),
      url,
      html: '',
      text: '',
      crawledAt: null,
    });
  }
  const totalPages = Number(html.match(/var\s+pu\s*=\s*(\d+)/i)?.[1] || 1);
  return { rows, totalPages };
}

function parseArticlePage(page, fallback) {
  const title = cleanTitle(page.match(/<h3[^>]*class=["'][^"']*n_title[^"']*["'][^>]*>([\s\S]*?)<\/h3>/i)?.[1] || fallback.title);
  const publishedAt = page.match(/<div[^>]*class=["'][^"']*time[^"']*["'][^>]*>\s*<span[^>]*>(\d{4}-\d{2}-\d{2}(?:\s+\d{2}:\d{2}:\d{2})?)/i)?.[1] || fallback.date;
  const startTag = page.match(/<div[^>]*id=["']news_cnt["'][^>]*>/i);
  let source = '';
  if (startTag?.index != null) {
    const start = startTag.index + startTag[0].length;
    let end = page.indexOf('<h3 class="news_m"', start);
    if (end < 0) end = page.indexOf('<h3 class=\'news_m\'', start);
    if (end < 0) end = page.indexOf('</body>', start);
    source = page.slice(start, end).replace(/\s*<\/div>\s*$/, '');
  }
  const safeHtml = sanitizeArticle(source);
  const normalizedDate = normalizeDate(publishedAt.slice(0, 10), title, fallback.url);
  const normalizedPublishedAt = /^(?:19|20)\d{2}-/.test(publishedAt) ? publishedAt : normalizedDate;
  return {
    ...fallback,
    title,
    date: normalizedDate,
    year: normalizedDate.slice(0, 4),
    publishedAt: normalizedPublishedAt,
    server: getServer(title),
    version: getVersion(title),
    html: safeHtml,
    text: stripTags(safeHtml),
    crawledAt: new Date().toISOString(),
  };
}

async function pool(items, concurrency, task) {
  let cursor = 0;
  const workers = Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    while (cursor < items.length) {
      const index = cursor++;
      await task(items[index], index);
    }
  });
  await Promise.all(workers);
}

async function saveData() {
  await mkdir(DATA_DIR, { recursive: true });
  const payload = JSON.stringify({
    source: `${BASE_URL}${LIST_PATH}1.shtml`,
    updatedAt: new Date().toISOString(),
    announcements,
  });
  await writeFile(TEMP_FILE, payload, 'utf8');
  await rename(TEMP_FILE, DATA_FILE);
}

async function loadData() {
  try {
    const payload = JSON.parse(await readFile(DATA_FILE, 'utf8'));
    announcements = Array.isArray(payload.announcements) ? payload.announcements.filter(isXunxianOne) : [];
    buildSystemCache();
    syncState.updatedAt = payload.updatedAt || null;
    syncState.found = announcements.length;
    syncState.message = announcements.length ? `已收录 ${announcements.length} 篇版本公告` : '等待同步';
  } catch (error) {
    if (error.code !== 'ENOENT') console.warn('读取缓存失败：', error.message);
  }
}

async function runSync(mode = 'recent') {
  if (syncState.running) return;
  syncState = {
    ...syncState,
    running: true,
    phase: 'lists',
    mode,
    current: 0,
    total: 0,
    message: '正在读取官网目录…',
    startedAt: new Date().toISOString(),
    error: null,
  };
  try {
    const firstHtml = await fetchGbk(listPageUrl(1, 1));
    const first = parseListPage(firstHtml);
    const pages = mode === 'all' ? first.totalPages : Math.min(36, first.totalPages);
    syncState.total = pages;
    syncState.current = 1;
    syncState.message = `正在扫描公告目录 1 / ${pages}`;
    const collected = [...first.rows];
    const pageNumbers = Array.from({ length: Math.max(0, pages - 1) }, (_, i) => i + 2);
    await pool(pageNumbers, 12, async (pageNumber) => {
      const html = await fetchGbk(listPageUrl(pageNumber, first.totalPages));
      collected.push(...parseListPage(html).rows);
      syncState.current += 1;
      syncState.found = collected.length;
      syncState.message = `正在扫描公告目录 ${syncState.current} / ${pages}`;
    });

    const existing = new Map(announcements.map((item) => [item.id, item]));
    const merged = new Map();
    for (const item of collected) {
      const prior = existing.get(item.id);
      merged.set(item.id, prior ? {
        ...prior,
        ...item,
        html: prior.html || '',
        text: prior.text || '',
        publishedAt: /^(?:19|20)\d{2}-/.test(prior.publishedAt || '') ? prior.publishedAt : item.date,
        crawledAt: prior.crawledAt || null,
      } : item);
    }
    if (mode !== 'all') {
      for (const item of announcements) if (!merged.has(item.id)) merged.set(item.id, item);
    }
    announcements = [...merged.values()];
    const missing = announcements.filter((item) => !item.text);
    syncState.phase = 'articles';
    syncState.current = 0;
    syncState.total = missing.length;
    syncState.message = missing.length ? `正在抓取公告正文 0 / ${missing.length}` : '正文已是最新';
    let completedSinceSave = 0;
    await pool(missing, 8, async (item) => {
      try {
        const page = await fetchGbk(item.url);
        const parsed = parseArticlePage(page, item);
        Object.assign(item, parsed);
      } catch (error) {
        item.crawlError = error.message;
      }
      syncState.current += 1;
      syncState.message = `正在抓取公告正文 ${syncState.current} / ${missing.length}`;
      completedSinceSave += 1;
      if (completedSinceSave >= 40) {
        completedSinceSave = 0;
        await saveData();
      }
    });
    announcements.sort((a, b) => b.date.localeCompare(a.date) || Number(b.id) - Number(a.id));
    announcements = announcements.filter(isXunxianOne);
    await saveData();
    buildSystemCache();
    syncState = {
      ...syncState,
      running: false,
      phase: 'done',
      current: announcements.length,
      total: announcements.length,
      found: announcements.length,
      message: `同步完成，共收录 ${announcements.length} 篇版本公告`,
      updatedAt: new Date().toISOString(),
    };
  } catch (error) {
    syncState = { ...syncState, running: false, phase: 'error', error: error.message, message: `同步失败：${error.message}` };
    throw error;
  }
}

function makeSnippet(text, query) {
  const clean = text.replace(/\s+/g, ' ').trim();
  if (!clean) return '正文尚未同步，点击右上角“同步官网”即可补全。';
  const terms = query.trim().toLowerCase().split(/\s+/).filter(Boolean);
  const lower = clean.toLowerCase();
  const positions = terms.map((term) => lower.indexOf(term)).filter((index) => index >= 0);
  const start = positions.length ? Math.max(0, Math.min(...positions) - 58) : 0;
  return `${start ? '…' : ''}${clean.slice(start, start + 156)}${start + 156 < clean.length ? '…' : ''}`;
}

function search(params) {
  const query = (params.get('q') || '').trim();
  const year = params.get('year') || 'all';
  const server = params.get('server') || 'all';
  const sort = params.get('sort') || 'newest';
  const page = Math.max(1, Number(params.get('page') || 1));
  const limit = Math.min(50, Math.max(1, Number(params.get('limit') || 20)));
  const terms = query.toLowerCase().split(/\s+/).filter(Boolean);
  let rows = announcements.filter((item) => {
    if (year !== 'all' && item.year !== year) return false;
    if (server !== 'all' && item.server !== server) return false;
    if (!terms.length) return true;
    const haystack = `${item.title}\n${item.version}\n${item.text}`.toLowerCase();
    return terms.every((term) => haystack.includes(term));
  });
  if (terms.length) {
    rows = rows.map((item) => {
      const title = item.title.toLowerCase();
      const version = item.version.toLowerCase();
      const score = terms.reduce((sum, term) => sum + (title.includes(term) ? 8 : 0) + (version.includes(term) ? 5 : 0), 0);
      return { ...item, _score: score };
    });
  }
  rows.sort((a, b) => {
    if (sort === 'oldest') return a.date.localeCompare(b.date);
    if (sort === 'relevance' && terms.length && b._score !== a._score) return b._score - a._score;
    return b.date.localeCompare(a.date) || Number(b.id) - Number(a.id);
  });
  const total = rows.length;
  const start = (page - 1) * limit;
  const items = rows.slice(start, start + limit).map(({ html, text, _score, ...item }) => ({
    ...item,
    hasContent: Boolean(text),
    snippet: makeSnippet(text || '', query),
  }));
  return { items, total, page, pages: Math.max(1, Math.ceil(total / limit)), query };
}

function stats() {
  const years = [...new Set(announcements.map((item) => item.year).filter(Boolean))].sort().reverse();
  const servers = Object.entries(announcements.reduce((acc, item) => {
    acc[item.server] = (acc[item.server] || 0) + 1;
    return acc;
  }, {})).map(([name, count]) => ({ name, count }));
  return {
    count: announcements.length,
    years,
    servers,
    earliest: announcements.at(-1)?.date || null,
    latest: announcements[0]?.date || null,
    updatedAt: syncState.updatedAt,
    source: `${BASE_URL}${LIST_PATH}1.shtml`,
  };
}

function sendJson(response, status, payload) {
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store' });
  response.end(JSON.stringify(payload));
}

async function serveStatic(response, pathname) {
  const relative = pathname === '/'
    ? 'systems.html'
    : pathname === '/updates'
      ? 'index.html'
      : pathname.replace(/^\/+/, '');
  const filePath = path.resolve(PUBLIC_DIR, relative);
  if (!filePath.startsWith(path.resolve(PUBLIC_DIR))) return sendJson(response, 403, { error: '拒绝访问' });
  try {
    const content = await readFile(filePath);
    response.writeHead(200, { 'content-type': MIME[path.extname(filePath)] || 'application/octet-stream', 'cache-control': 'no-cache' });
    response.end(content);
  } catch (error) {
    if (error.code === 'ENOENT') return sendJson(response, 404, { error: '页面不存在' });
    throw error;
  }
}

async function handle(request, response) {
  const url = new URL(request.url, `http://${request.headers.host || 'localhost'}`);
  try {
    if (url.pathname === '/api/health' && request.method === 'GET') return sendJson(response, 200, { ok: true, announcements: announcements.length });
    if (url.pathname === '/api/systems' && request.method === 'GET') return sendJson(response, 200, listSystems(url.searchParams));
    if (url.pathname.match(/^\/api\/guides\/[a-z0-9-]+$/) && request.method === 'GET') {
      const id = url.pathname.split('/').pop();
      const guide = getSystemGuide(id);
      return guide ? sendJson(response, 200, guide) : sendJson(response, 404, { error: '没有找到这个系统指南' });
    }
    if (url.pathname.match(/^\/api\/systems\/[a-z0-9-]+$/) && request.method === 'GET') {
      const id = url.pathname.split('/').pop();
      const detail = getSystemDetail(id, url.searchParams);
      return detail ? sendJson(response, 200, detail) : sendJson(response, 404, { error: '没有找到这个系统' });
    }
    if (url.pathname === '/api/announcements' && request.method === 'GET') return sendJson(response, 200, search(url.searchParams));
    if (url.pathname.match(/^\/api\/announcements\/\d+$/) && request.method === 'GET') {
      const id = url.pathname.split('/').pop();
      const item = announcements.find((row) => row.id === id);
      return item ? sendJson(response, 200, item) : sendJson(response, 404, { error: '没有找到这篇公告' });
    }
    if (url.pathname === '/api/stats' && request.method === 'GET') return sendJson(response, 200, stats());
    if (url.pathname === '/api/sync/status' && request.method === 'GET') return sendJson(response, 200, syncState);
    if (url.pathname === '/api/sync' && request.method === 'POST') {
      if (syncState.running) return sendJson(response, 409, { error: '同步正在进行', state: syncState });
      let body = '';
      for await (const chunk of request) body += chunk;
      const mode = JSON.parse(body || '{}').mode === 'all' ? 'all' : 'recent';
      runSync(mode).catch((error) => console.error('同步失败：', error.message));
      return sendJson(response, 202, { accepted: true, mode });
    }
    if (url.pathname.startsWith('/api/')) return sendJson(response, 404, { error: '接口不存在' });
    return await serveStatic(response, url.pathname);
  } catch (error) {
    console.error(error);
    return sendJson(response, 500, { error: error.message || '服务器内部错误' });
  }
}

async function main() {
  await loadData();
  if (process.argv.includes('--sync-all')) {
    await runSync('all');
    console.log(syncState.message);
    return;
  }
  const server = http.createServer(handle);
  server.listen(PORT, HOST, () => {
    console.log(`寻仙志已启动：http://${HOST}:${PORT}`);
    if (!announcements.length) {
      console.log('首次运行：正在后台同步最近公告…');
      runSync('recent').catch((error) => console.error(error.message));
    }
    const syncHours = Number(process.env.SYNC_INTERVAL_HOURS || 0);
    if (syncHours > 0) {
      const syncLatest = () => {
        if (!syncState.running) runSync('recent').catch((error) => console.error('定时同步失败：', error.message));
      };
      setTimeout(syncLatest, 60_000);
      setInterval(syncLatest, syncHours * 60 * 60 * 1000);
    }
  });
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
