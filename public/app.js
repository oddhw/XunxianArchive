const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

const els = {
  searchInput: $('#searchInput'),
  clearSearch: $('#clearSearch'),
  yearFilter: $('#yearFilter'),
  serverOptions: $('#serverOptions'),
  sortSelect: $('#sortSelect'),
  resetFilters: $('#resetFilters'),
  resultList: $('#resultList'),
  resultHeading: $('#resultHeading'),
  resultCount: $('#resultCount'),
  activeFilters: $('#activeFilters'),
  pagination: $('#pagination'),
  prevPage: $('#prevPage'),
  nextPage: $('#nextPage'),
  pageInfo: $('#pageInfo'),
  archiveCount: $('#archiveCount'),
  allServerCount: $('#allServerCount'),
  topStatus: $('#topStatus'),
  syncButton: $('#syncButton'),
  fullSyncButton: $('#fullSyncButton'),
  syncCard: $('#syncCard'),
  syncTime: $('#syncTime'),
  syncMessage: $('#syncMessage'),
  syncProgress: $('#syncProgress'),
  readerPanel: $('#readerPanel'),
  readerClose: $('#readerClose'),
  readerBackdrop: $('#readerBackdrop'),
  readerEmpty: $('#readerEmpty'),
  readerContent: $('#readerContent'),
  readerTags: $('#readerTags'),
  readerDate: $('#readerDate'),
  readerTitle: $('#readerTitle'),
  articleBody: $('#articleBody'),
  originalLink: $('#originalLink'),
  toastRegion: $('#toastRegion'),
};

const state = {
  query: '',
  year: 'all',
  server: 'all',
  sort: 'newest',
  page: 1,
  pages: 1,
  selectedId: null,
  loading: false,
  stats: null,
  lastSyncRunning: false,
};

let searchTimer;

function escapeHtml(value = '') {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function escapeRegex(value = '') {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function highlight(value, query = state.query) {
  const safe = escapeHtml(value);
  const terms = query.trim().split(/\s+/).filter(Boolean).sort((a, b) => b.length - a.length);
  if (!terms.length) return safe;
  const expression = new RegExp(`(${terms.map(escapeRegex).join('|')})`, 'gi');
  return safe.replace(expression, '<mark>$1</mark>');
}

function formatDate(value) {
  if (!value) return '尚未同步';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit',
  }).format(date);
}

function toast(message, type = '') {
  const node = document.createElement('div');
  node.className = `toast ${type}`;
  node.textContent = message;
  els.toastRegion.append(node);
  setTimeout(() => node.remove(), 3600);
}

async function request(url, options) {
  const response = await fetch(url, options);
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.error || `请求失败（${response.status}）`);
  return data;
}

function renderSkeletons() {
  els.resultList.innerHTML = Array.from({ length: 5 }, () => '<div class="result-skeleton"></div>').join('');
}

function resultTemplate(item, index) {
  const selected = item.id === state.selectedId ? ' selected' : '';
  const version = item.version ? `<span class="tag version">v${escapeHtml(item.version)}</span>` : '';
  return `
    <button class="result-item${selected}" type="button" data-id="${item.id}" style="animation-delay:${Math.min(index, 7) * 28}ms">
      <div class="result-item-head">
        <span class="tag">${escapeHtml(item.server)}</span>
        ${version}
        <time datetime="${escapeHtml(item.date)}">${escapeHtml(item.date)}</time>
      </div>
      <h3>${highlight(item.title)}</h3>
      <p>${highlight(item.snippet)}</p>
    </button>`;
}

function renderEmpty() {
  const syncing = state.lastSyncRunning;
  els.resultList.innerHTML = `
    <div class="empty-results">
      <svg viewBox="0 0 24 24"><circle cx="11" cy="11" r="6.5"/><path d="m16 16 4 4"/></svg>
      <h3>${syncing ? '资料正在从官网赶来' : '没有找到匹配的公告'}</h3>
      <p>${syncing ? '首次整理需要一点时间，完成后结果会自动出现。' : '换个更短的关键词，或重置筛选条件试试。'}</p>
    </div>`;
}

function renderActiveFilters() {
  const chips = [];
  if (state.query) chips.push(`关键词：${escapeHtml(state.query)}`);
  if (state.year !== 'all') chips.push(`${escapeHtml(state.year)} 年`);
  if (state.server !== 'all') chips.push(escapeHtml(state.server));
  els.activeFilters.classList.toggle('visible', Boolean(chips.length));
  els.activeFilters.innerHTML = chips.map((label) => `<span class="filter-chip">${label}</span>`).join('');
}

async function loadResults({ keepSelection = false } = {}) {
  if (state.loading) return;
  state.loading = true;
  renderSkeletons();
  const params = new URLSearchParams({
    q: state.query,
    year: state.year,
    server: state.server,
    sort: state.sort,
    page: state.page,
    limit: '18',
  });
  try {
    const data = await request(`/api/announcements?${params}`);
    state.pages = data.pages;
    if (!keepSelection && !data.items.some((item) => item.id === state.selectedId)) {
      state.selectedId = null;
      closeReader(false);
    }
    els.resultList.innerHTML = data.items.map(resultTemplate).join('');
    if (!data.items.length) renderEmpty();
    els.resultCount.textContent = `${data.total.toLocaleString('zh-CN')} 条结果`;
    els.resultHeading.textContent = state.query ? `“${state.query}” 的搜索结果` : '全部版本公告';
    els.pageInfo.textContent = `第 ${data.page} / ${data.pages} 页`;
    els.prevPage.disabled = data.page <= 1;
    els.nextPage.disabled = data.page >= data.pages;
    els.pagination.hidden = data.total === 0;
    renderActiveFilters();
  } catch (error) {
    els.resultList.innerHTML = '';
    renderEmpty();
    toast(error.message, 'error');
  } finally {
    state.loading = false;
  }
}

function updateStatsView(stats) {
  state.stats = stats;
  els.archiveCount.textContent = stats.count.toLocaleString('zh-CN');
  els.allServerCount.textContent = stats.count;
  els.topStatus.textContent = stats.count
    ? `${stats.earliest?.slice(0, 4) || '—'}—${stats.latest?.slice(0, 4) || '—'} · ${stats.count} 篇已索引`
    : '正在建立本地资料库';

  const currentYear = state.year;
  els.yearFilter.innerHTML = '<option value="all">全部年份</option>' + stats.years
    .map((year) => `<option value="${escapeHtml(year)}">${escapeHtml(year)} 年</option>`)
    .join('');
  els.yearFilter.value = stats.years.includes(currentYear) ? currentYear : 'all';
  state.year = els.yearFilter.value;

  const counts = Object.fromEntries(stats.servers.map((entry) => [entry.name, entry.count]));
  $$('#serverOptions button').forEach((button) => {
    if (button.dataset.server === 'all') button.querySelector('small').textContent = stats.count;
    else button.querySelector('small').textContent = counts[button.dataset.server] || 0;
  });
  els.syncTime.textContent = stats.updatedAt ? `上次同步 ${formatDate(stats.updatedAt)}` : '尚未完成首次同步';
}

async function loadStats() {
  try {
    updateStatsView(await request('/api/stats'));
  } catch (error) {
    toast(error.message, 'error');
  }
}

function markArticleTerms(container, query) {
  const terms = query.trim().split(/\s+/).filter(Boolean).sort((a, b) => b.length - a.length);
  if (!terms.length) return;
  const expression = new RegExp(`(${terms.map(escapeRegex).join('|')})`, 'gi');
  const walker = document.createTreeWalker(container, NodeFilter.SHOW_TEXT, {
    acceptNode(node) {
      if (!node.nodeValue.trim() || node.parentElement.closest('mark, script, style')) return NodeFilter.FILTER_REJECT;
      expression.lastIndex = 0;
      return expression.test(node.nodeValue) ? NodeFilter.FILTER_ACCEPT : NodeFilter.FILTER_REJECT;
    },
  });
  const nodes = [];
  while (walker.nextNode()) nodes.push(walker.currentNode);
  nodes.forEach((node) => {
    const fragment = document.createDocumentFragment();
    let last = 0;
    expression.lastIndex = 0;
    for (const match of node.nodeValue.matchAll(expression)) {
      fragment.append(document.createTextNode(node.nodeValue.slice(last, match.index)));
      const marker = document.createElement('mark');
      marker.textContent = match[0];
      fragment.append(marker);
      last = match.index + match[0].length;
    }
    fragment.append(document.createTextNode(node.nodeValue.slice(last)));
    node.replaceWith(fragment);
  });
}

async function openArticle(id) {
  state.selectedId = id;
  $$('.result-item').forEach((item) => item.classList.toggle('selected', item.dataset.id === id));
  els.readerEmpty.hidden = true;
  els.readerContent.hidden = false;
  els.readerContent.style.opacity = '.45';
  els.readerPanel.classList.add('open');
  els.readerBackdrop.classList.add('visible');
  document.body.style.overflow = window.innerWidth <= 980 ? 'hidden' : '';
  try {
    const item = await request(`/api/announcements/${id}`);
    els.readerTags.innerHTML = `<span>${escapeHtml(item.server)}</span>${item.version ? `<span>v${escapeHtml(item.version)}</span>` : ''}`;
    els.readerDate.textContent = item.publishedAt || item.date;
    els.readerDate.dateTime = item.publishedAt || item.date;
    els.readerTitle.innerHTML = highlight(item.title);
    els.articleBody.innerHTML = item.html || `<p>${escapeHtml(item.text || '这篇公告的正文还没有同步，请稍后再试。')}</p>`;
    markArticleTerms(els.articleBody, state.query);
    els.originalLink.href = item.url;
    els.readerContent.style.opacity = '';
    els.readerPanel.scrollTop = 0;
  } catch (error) {
    toast(error.message, 'error');
    els.readerContent.style.opacity = '';
  }
}

function closeReader(clear = true) {
  els.readerPanel.classList.remove('open');
  els.readerBackdrop.classList.remove('visible');
  document.body.style.overflow = '';
  if (clear) {
    state.selectedId = null;
    $$('.result-item.selected').forEach((item) => item.classList.remove('selected'));
    els.readerEmpty.hidden = false;
    els.readerContent.hidden = true;
  }
}

async function startSync(mode) {
  if (state.lastSyncRunning) {
    toast('同步正在进行中');
    return;
  }
  try {
    await request('/api/sync', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ mode }),
    });
    toast(mode === 'all' ? '已开始整理全部历史公告' : '已开始同步最近公告');
    await pollSync();
  } catch (error) {
    toast(error.message, 'error');
  }
}

function renderSync(sync) {
  state.lastSyncRunning = sync.running;
  els.syncButton.classList.toggle('syncing', sync.running);
  els.syncButton.disabled = sync.running;
  els.syncButton.querySelector('span').textContent = sync.running ? '同步中' : '同步官网';
  els.syncCard.classList.toggle('running', sync.running);
  els.syncMessage.textContent = sync.message || '等待同步';
  const percent = sync.total ? Math.min(100, Math.round((sync.current / sync.total) * 100)) : 0;
  els.syncProgress.style.width = `${sync.phase === 'done' ? 100 : percent}%`;
  if (sync.running) els.topStatus.textContent = sync.message;
  if (sync.error) els.syncMessage.textContent = sync.message;
}

async function pollSync() {
  try {
    const sync = await request('/api/sync/status');
    const justFinished = state.lastSyncRunning && !sync.running && sync.phase === 'done';
    renderSync(sync);
    if (justFinished) {
      await loadStats();
      await loadResults({ keepSelection: true });
      toast(sync.message);
    }
  } catch {
    // 下一轮轮询会自动重试。
  }
}

function resetAndSearch() {
  state.page = 1;
  loadResults();
}

els.searchInput.addEventListener('input', () => {
  state.query = els.searchInput.value.trim();
  els.clearSearch.classList.toggle('visible', Boolean(state.query));
  if (state.query && els.sortSelect.value === 'newest') {
    state.sort = 'relevance';
    els.sortSelect.value = 'relevance';
  } else if (!state.query && els.sortSelect.value === 'relevance') {
    state.sort = 'newest';
    els.sortSelect.value = 'newest';
  }
  clearTimeout(searchTimer);
  searchTimer = setTimeout(resetAndSearch, 240);
});

els.clearSearch.addEventListener('click', () => {
  els.searchInput.value = '';
  state.query = '';
  state.sort = 'newest';
  els.sortSelect.value = 'newest';
  els.clearSearch.classList.remove('visible');
  els.searchInput.focus();
  resetAndSearch();
});

$$('[data-query]').forEach((button) => button.addEventListener('click', () => {
  els.searchInput.value = button.dataset.query;
  els.searchInput.dispatchEvent(new Event('input'));
  els.searchInput.focus();
}));

els.yearFilter.addEventListener('change', () => {
  state.year = els.yearFilter.value;
  resetAndSearch();
});

els.serverOptions.addEventListener('click', (event) => {
  const button = event.target.closest('button[data-server]');
  if (!button) return;
  state.server = button.dataset.server;
  $$('#serverOptions button').forEach((item) => item.classList.toggle('active', item === button));
  resetAndSearch();
});

els.sortSelect.addEventListener('change', () => {
  state.sort = els.sortSelect.value;
  resetAndSearch();
});

els.resetFilters.addEventListener('click', () => {
  state.query = '';
  state.year = 'all';
  state.server = 'all';
  state.sort = 'newest';
  state.page = 1;
  els.searchInput.value = '';
  els.clearSearch.classList.remove('visible');
  els.yearFilter.value = 'all';
  els.sortSelect.value = 'newest';
  $$('#serverOptions button').forEach((item) => item.classList.toggle('active', item.dataset.server === 'all'));
  loadResults();
});

els.resultList.addEventListener('click', (event) => {
  const item = event.target.closest('.result-item[data-id]');
  if (item) openArticle(item.dataset.id);
});

els.prevPage.addEventListener('click', () => {
  if (state.page <= 1) return;
  state.page -= 1;
  loadResults();
  els.resultList.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

els.nextPage.addEventListener('click', () => {
  if (state.page >= state.pages) return;
  state.page += 1;
  loadResults();
  els.resultList.scrollIntoView({ behavior: 'smooth', block: 'start' });
});

els.syncButton.addEventListener('click', () => startSync('recent'));
els.fullSyncButton.addEventListener('click', () => startSync('all'));
els.readerClose.addEventListener('click', () => closeReader());
els.readerBackdrop.addEventListener('click', () => closeReader());

document.addEventListener('keydown', (event) => {
  if (event.key === '/' && !['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName)) {
    event.preventDefault();
    els.searchInput.focus();
  }
  if (event.key === 'Escape') {
    if (document.activeElement === els.searchInput && state.query) els.clearSearch.click();
    else closeReader();
  }
});

async function initialize() {
  renderSkeletons();
  await Promise.all([loadStats(), pollSync()]);
  await loadResults();
  setInterval(pollSync, 1200);
}

initialize();
