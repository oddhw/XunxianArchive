const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

const els = {
  systemSearch: $('#systemSearch'), systemGrid: $('#systemGrid'), emptyState: $('#emptyState'),
  groupFilters: $('#groupFilters'), stageTitle: $('#stageTitle'), filteredCount: $('#filteredCount'),
  systemCount: $('#systemCount'), updateCount: $('#updateCount'), materialCount: $('#materialCount'), headerState: $('#headerState'),
  drawer: $('#detailDrawer'), backdrop: $('#drawerBackdrop'), closeDrawer: $('#closeDrawer'),
  drawerIcon: $('#drawerIcon'), drawerGroup: $('#drawerGroup'), drawerName: $('#drawerName'), drawerDescription: $('#drawerDescription'),
  drawerStages: $('#drawerStages'), drawerMaterials: $('#drawerMaterials'), drawerLatest: $('#drawerLatest'),
  detailSearch: $('#detailSearch'), detailResultCount: $('#detailResultCount'), detailList: $('#detailList'), toastWrap: $('#toastWrap'),
};

const state = { systems: [], groups: [], group: 'all', query: '', selected: null, section: 'route', detailQuery: '', guide: null };
let searchTimer;
let detailTimer;

const icons = {
  blade: '<svg viewBox="0 0 24 24"><path d="m5 19 4-4m-2 6-4-4m5-3L18 4l2 2-10 10m4-8 2 2"/></svg>',
  armor: '<svg viewBox="0 0 24 24"><path d="m8 4 4 2 4-2 4 3-2 4v9H6v-9L4 7l4-3Zm0 0v6m8-6v6M9 14h6"/></svg>',
  pet: '<svg viewBox="0 0 24 24"><path d="M8 11c-2 1-3 3-2 5 1 3 4 4 6 4s5-1 6-4c1-2 0-4-2-5-2-1-2 1-4 1s-2-2-4-1ZM7 8c1 0 2-1 2-3S8 2 7 3 5 5 6 7c0 1 0 1 1 1Zm10 0c-1 0-2-1-2-3s1-3 2-2 2 2 1 4c0 1 0 1-1 1Z"/></svg>',
  mount: '<svg viewBox="0 0 24 24"><path d="M4 17c3-1 4-4 5-7 2 2 4 2 7 1l3-3 1 5-3 2 1 5m-9-4-1 4m3-11 2-3 4 1"/></svg>',
  scroll: '<svg viewBox="0 0 24 24"><path d="M7 5h11v14H7a3 3 0 0 1 0-6h11M7 5a3 3 0 0 0 0 6h8M9 8h6"/></svg>',
  yinyang: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><path d="M12 3a4.5 4.5 0 1 1 0 9 4.5 4.5 0 1 0 0 9"/><circle cx="12" cy="7.5" r=".7"/><circle cx="12" cy="16.5" r=".7"/></svg>',
  relic: '<svg viewBox="0 0 24 24"><path d="m12 3 6 4v7l-6 7-6-7V7l6-4Z"/><path d="m9 9 3-2 3 2-1 5-2 2-2-2-1-5Z"/></svg>',
  orb: '<svg viewBox="0 0 24 24"><circle cx="12" cy="11" r="6"/><path d="M8 19h8M9 17l-2 3m8-3 2 3M9 9c1-2 3-3 5-2"/></svg>',
  rune: '<svg viewBox="0 0 24 24"><path d="M12 3v18M5 7l7 4 7-4M5 17l7-4 7 4M7 4l10 16"/></svg>',
  flame: '<svg viewBox="0 0 24 24"><path d="M13 3c1 4-3 5-1 9 1-2 3-2 4-4 3 4 3 8 1 11-3 3-8 3-11-1-2-4 0-8 4-11 0 3 1 4 3 5-1-4 3-5 2-9Z"/></svg>',
  plate: '<svg viewBox="0 0 24 24"><circle cx="12" cy="12" r="9"/><circle cx="12" cy="12" r="4"/><path d="M12 3v5m0 8v5M3 12h5m8 0h5"/></svg>',
  skill: '<svg viewBox="0 0 24 24"><path d="m12 3 2 5 5 2-5 2-2 5-2-5-5-2 5-2 2-5Zm6 13 1 2 2 1-2 1-1 2-1-2-2-1 2-1 1-2Z"/></svg>',
  rank: '<svg viewBox="0 0 24 24"><path d="M5 20h14M7 17h10M9 14h6l2-8-3 2-2-5-2 5-3-2 2 8Z"/></svg>',
  home: '<svg viewBox="0 0 24 24"><path d="m3 11 9-7 9 7M5 10v10h14V10M9 20v-6h6v6"/></svg>',
  guild: '<svg viewBox="0 0 24 24"><path d="M4 21V7l8-4 8 4v14M8 21v-5h8v5M8 9h2m4 0h2M8 13h2m4 0h2"/></svg>',
  book: '<svg viewBox="0 0 24 24"><path d="M4 5c3-1 6 0 8 2v14c-2-2-5-3-8-2V5Zm16 0c-3-1-6 0-8 2v14c2-2 5-3 8-2V5Z"/></svg>',
  fashion: '<svg viewBox="0 0 24 24"><path d="m8 4 4 2 4-2 4 4-3 3v9H7v-9L4 8l4-4Z"/><path d="M9 5c0 2 1 3 3 3s3-1 3-3"/></svg>',
  craft: '<svg viewBox="0 0 24 24"><path d="m14 5 5 5M13 6l2-2 5 5-2 2M4 20l7-7 3 3-7 7-3-3Zm1-8 3-3m-1-2 3 3"/></svg>',
};

const materialIcons = {
  ingot: '<svg viewBox="0 0 32 32"><path d="m8 12 5-5h9l4 5-4 11H10L6 18l2-6Z"/><path d="m8 12 6 4 12-4m-12 4-4 7"/></svg>',
  ore: '<svg viewBox="0 0 32 32"><path d="m6 18 4-10 9-3 7 7-2 11-10 4-8-9Z"/><path d="m10 8 6 7 10-3m-10 3-2 12"/></svg>',
  crystal: '<svg viewBox="0 0 32 32"><path d="m16 3 8 9-8 17-8-17 8-9Z"/><path d="m8 12 8 4 8-4m-8 4v13"/></svg>',
  gem: '<svg viewBox="0 0 32 32"><path d="M9 6h14l5 7-12 15L4 13l5-7Z"/><path d="m9 6 7 22L23 6M4 13h24"/></svg>',
  jade: '<svg viewBox="0 0 32 32"><circle cx="16" cy="16" r="12"/><circle cx="16" cy="16" r="5"/><path d="M16 4v7m0 10v7"/></svg>',
  pendant: '<svg viewBox="0 0 32 32"><path d="M10 4c0 6 3 8 6 8s6-2 6-8M16 12l8 7-8 10-8-10 8-7Z"/></svg>',
  pill: '<svg viewBox="0 0 32 32"><path d="M9 22a7 7 0 0 1 0-10l5-5a7 7 0 0 1 10 10l-5 5a7 7 0 0 1-10 0Z"/><path d="m11 10 11 11"/></svg>',
  token: '<svg viewBox="0 0 32 32"><path d="M9 4h14l4 7-2 17H7L5 11l4-7Z"/><path d="M11 12h10M12 17h8M14 22h4"/></svg>',
  blueprint: '<svg viewBox="0 0 32 32"><path d="M7 5h18v22H7z"/><path d="M11 10h10M11 15h5m-5 5 5-5 5 5"/></svg>',
  scroll: '<svg viewBox="0 0 32 32"><path d="M9 5h17v20H9a5 5 0 0 1 0-10h17M9 5a5 5 0 0 0 0 10h12M12 10h9"/></svg>',
  essence: '<svg viewBox="0 0 32 32"><path d="M16 3c5 7 9 11 9 17a9 9 0 0 1-18 0c0-6 4-10 9-17Z"/><path d="M12 22c2 2 5 2 7 0"/></svg>',
  book: '<svg viewBox="0 0 32 32"><path d="M4 7c5-2 9 0 12 3v19c-3-3-7-5-12-3V7Zm24 0c-5-2-9 0-12 3v19c3-3 7-5 12-3V7Z"/></svg>',
  ticket: '<svg viewBox="0 0 32 32"><path d="M5 9h22v5a3 3 0 0 0 0 6v5H5v-5a3 3 0 0 0 0-6V9Z"/><path d="M16 10v14"/></svg>',
  fragment: '<svg viewBox="0 0 32 32"><path d="m15 3 9 7-3 5 5 5-10 9-4-7-7-2 5-8 5-9Z"/><path d="m10 12 6 4 8-6m-8 6v13"/></svg>',
  coin: '<svg viewBox="0 0 32 32"><ellipse cx="16" cy="16" rx="12" ry="10"/><ellipse cx="16" cy="16" rx="7" ry="5"/><path d="M13 16h6M16 13v6"/></svg>',
  yinyang: '<svg viewBox="0 0 32 32"><circle cx="16" cy="16" r="12"/><path d="M16 4a6 6 0 1 1 0 12 6 6 0 1 0 0 12"/><circle cx="16" cy="10" r="1"/><circle cx="16" cy="22" r="1"/></svg>',
  stone: '<svg viewBox="0 0 32 32"><path d="m6 19 5-12 11 1 5 9-6 10H10L6 19Z"/><path d="m11 7 5 9 11 1m-11-1-6 11"/></svg>',
  orb: '<svg viewBox="0 0 32 32"><circle cx="16" cy="14" r="10"/><path d="M10 27h12M12 24l-3 5m11-5 3 5M11 12c2-4 6-6 10-3"/></svg>',
  powder: '<svg viewBox="0 0 32 32"><path d="M9 10h14l3 17H6L9 10Z"/><path d="M11 5h10v5H11zM10 17h12"/><circle cx="16" cy="21" r="2"/></svg>',
  box: '<svg viewBox="0 0 32 32"><path d="M5 11h22v16H5zM4 7h24v6H4zM16 7v20"/><path d="M11 7c-2-3 1-5 5 0m5 0c2-3-1-5-5 0"/></svg>',
  spark: '<svg viewBox="0 0 32 32"><path d="m16 3 3 9 9 4-9 3-3 10-3-10-9-3 9-4 3-9Z"/></svg>',
  silk: '<svg viewBox="0 0 32 32"><path d="m10 5 6 3 6-3 6 6-5 4v12H9V15l-5-4 6-6Z"/><path d="M12 6c0 4 1 6 4 6s4-2 4-6"/></svg>',
};

function escapeHtml(value = '') { return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;'); }
function escapeRegex(value = '') { return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }
function highlight(value, query = '') { const safe = escapeHtml(value); if (!query) return safe; return safe.replace(new RegExp(`(${escapeRegex(query)})`, 'gi'), '<mark>$1</mark>'); }
function icon(name) { return icons[name] || icons.orb; }
function materialIcon(name) { return materialIcons[name] || materialIcons.crystal; }
async function request(url) { const response = await fetch(url); const data = await response.json().catch(() => ({})); if (!response.ok) throw new Error(data.error || '请求失败'); return data; }
function toast(message) { const node = document.createElement('div'); node.className = 'toast'; node.textContent = message; els.toastWrap.append(node); setTimeout(() => node.remove(), 2800); }

function renderGroups() {
  const groups = ['all', ...state.groups];
  els.groupFilters.innerHTML = groups.map((group) => {
    const count = group === 'all' ? state.systems.length : state.systems.filter((item) => item.group === group).length;
    const label = group === 'all' ? '全部系统' : group;
    return `<button type="button" class="${state.group === group ? 'active' : ''}" data-group="${escapeHtml(group)}"><span>${escapeHtml(label)}</span><small>${count}</small></button>`;
  }).join('');
}

function visibleSystems() {
  const query = state.query.toLowerCase();
  return state.systems.filter((system) => {
    if (state.group !== 'all' && system.group !== state.group) return false;
    return !query || `${system.name} ${system.short} ${system.description} ${system.keywords.join(' ')}`.toLowerCase().includes(query);
  });
}

function renderSystems() {
  const systems = visibleSystems();
  els.filteredCount.textContent = `${systems.length} 个系统`;
  els.stageTitle.textContent = state.group === 'all' ? (state.query ? `“${state.query}” 的结果` : '全部系统') : state.group;
  els.emptyState.hidden = systems.length > 0;
  els.systemGrid.hidden = systems.length === 0;
  els.systemGrid.innerHTML = systems.map((system) => `
    <button class="system-card ${system.reviewed ? 'reviewed' : 'pending-review'}" type="button" data-system-id="${system.id}" style="--system-color:${system.color}">
      <div class="card-top"><span class="system-icon">${icon(system.icon)}</span><span class="card-group">${escapeHtml(system.group)}</span><span class="review-badge">${system.reviewed ? '已逐项校对' : '待校对'}</span></div>
      <h3>${highlight(system.name, state.query)}</h3><p>${highlight(system.description, state.query)}</p>
      <div class="card-stats">${system.reviewed ? `<span><b>${system.stageCount}</b>代武器</span><span><b>${system.materialItemCount}</b>项材料</span><span class="card-arrow"><svg viewBox="0 0 20 20"><path d="m7 4 6 6-6 6"/></svg></span>` : '<span>资料暂不展示，等待逐项核对</span>'}</div>
    </button>`).join('');
}

async function loadSystems() {
  try {
    const [data, stats] = await Promise.all([request('/api/systems'), request('/api/stats')]);
    state.systems = data.systems; state.groups = data.groups;
    const stages = data.systems.filter((item) => item.reviewed).reduce((sum, item) => sum + item.stageCount, 0);
    const materials = data.systems.filter((item) => item.reviewed).reduce((sum, item) => sum + item.materialItemCount, 0);
    els.systemCount.textContent = data.systems.length;
    els.updateCount.textContent = stages.toLocaleString('zh-CN');
    els.materialCount.textContent = materials.toLocaleString('zh-CN');
    els.headerState.textContent = `${stats.earliest.slice(0, 4)}—${stats.latest.slice(0, 4)} · ${stats.count} 篇寻仙1资料源`;
    renderGroups(); renderSystems();
    const hash = location.hash.slice(1);
    if (hash && state.systems.some((item) => item.id === hash)) openSystem(hash);
  } catch (error) { toast(error.message); }
}

function setDrawerSummary(guide) {
  const { system } = guide;
  els.drawer.style.setProperty('--system-color', system.color);
  els.drawerIcon.innerHTML = icon(system.icon);
  els.drawerGroup.textContent = system.group;
  els.drawerName.textContent = system.name;
  els.drawerDescription.textContent = system.description;
  els.drawerStages.textContent = guide.stageCount;
  els.drawerMaterials.textContent = guide.materialCount;
  els.drawerLatest.textContent = system.latestDate?.slice(0, 7) || '—';
}

async function openSystem(id, section = 'route') {
  const metadata = state.systems.find((item) => item.id === id);
  if (metadata && !metadata.reviewed) {
    toast(`${metadata.name}尚未逐项校对，当前先只开放武器系统。`);
    return;
  }
  state.selected = id; state.section = section; state.detailQuery = ''; state.guide = null;
  els.detailSearch.value = '';
  els.detailList.innerHTML = '<div class="detail-empty">正在建立养成图谱…</div>';
  els.drawer.classList.add('open'); els.backdrop.classList.add('visible'); els.drawer.setAttribute('aria-hidden', 'false'); document.body.style.overflow = 'hidden'; location.hash = id;
  updateTabs();
  try {
    state.guide = await request(`/api/guides/${id}`);
    setDrawerSummary(state.guide);
    renderGuide();
  } catch (error) { els.detailList.innerHTML = `<div class="detail-empty">${escapeHtml(error.message)}</div>`; }
}

function closeDrawer() { els.drawer.classList.remove('open'); els.backdrop.classList.remove('visible'); els.drawer.setAttribute('aria-hidden', 'true'); document.body.style.overflow = ''; history.replaceState(null, '', location.pathname); }
function updateTabs() { $$('.drawer-tabs button').forEach((button) => button.classList.toggle('active', button.dataset.section === state.section)); }

function historyTemplate(material) {
  if (!material.changes.length) return '<div class="history-empty">官网资料中暂未检索到明确变更记录。</div>';
  return `<ol class="material-timeline">${material.changes.map((event) => `
    <li><time>${escapeHtml(event.date)}</time><p>${highlight(event.summary, state.detailQuery)}</p><a href="${escapeHtml(event.sourceUrl)}" target="_blank" rel="noopener noreferrer" aria-label="查看资料依据">依据 ↗</a></li>`).join('')}</ol>`;
}

function materialCard(material, compact = false) {
  return `<article class="material-card ${compact ? 'compact' : ''}" data-material-name="${escapeHtml(material.name)}">
    <div class="material-card-main">
      <div class="material-icon" data-kind="${escapeHtml(material.icon)}">${materialIcon(material.icon)}</div>
      <div class="material-name"><span>核心材料</span><h4>${highlight(material.name, state.detailQuery)}</h4><small>官网首见 · ${escapeHtml(material.firstSeenYear)} 年</small></div>
      <div class="material-amount"><span>需要数量</span><strong>${escapeHtml(material.amount)}</strong></div>
    </div>
    <div class="material-facts">
      <div><span>用于</span><p>${highlight(material.purpose, state.detailQuery)}</p></div>
      <div><span>获取</span><p>${highlight(material.source, state.detailQuery)}</p></div>
    </div>
    <details class="material-history">
      <summary><span>历次变化</span><small>${material.changes.length ? `${material.changes.length} 个节点` : '暂无节点'}</small><svg viewBox="0 0 20 20"><path d="m6 8 4 4 4-4"/></svg></summary>
      ${historyTemplate(material)}
    </details>
  </article>`;
}

function filteredMaterials(materials) {
  const query = state.detailQuery.toLowerCase();
  return materials.filter((material) => !query || `${material.name} ${material.amount} ${material.purpose} ${material.source}`.toLowerCase().includes(query));
}

function renderRoute() {
  const query = state.detailQuery;
  const stages = state.guide.stages.map((stage) => ({ ...stage, materials: filteredMaterials(stage.materials) })).filter((stage) => !query || stage.materials.length);
  els.detailResultCount.textContent = `${stages.length} 个阶段`;
  if (!stages.length) return '<div class="detail-empty">没有找到对应材料。</div>';
  return `<div class="route-intro"><span>养成总览</span><p>按阶段查看要准备的材料；点击“历次变化”可看首发和后续调整。</p></div><div class="guide-route">${stages.map((stage) => `
    <section class="guide-stage">
      <header><span class="stage-number">${String(stage.order).padStart(2, '0')}</span><div><h3>${escapeHtml(stage.name)}</h3><p>${escapeHtml(stage.description)}</p></div><small>${stage.materials.length} 种材料</small></header>
      <div class="stage-material-grid">${stage.materials.map((material) => materialCard(material, true)).join('')}</div>
    </section>`).join('')}</div>`;
}

function renderMaterials() {
  const materials = filteredMaterials(state.guide.materials);
  els.detailResultCount.textContent = `${materials.length} 种材料`;
  if (!materials.length) return '<div class="detail-empty">没有找到对应材料。</div>';
  return `<div class="material-legend"><span><i></i>数量为官网明确值</span><span>“按阶段”表示不同档位消耗不同，避免用单一数字误导。</span></div><div class="material-catalog">${materials.map((material) => materialCard(material)).join('')}</div>`;
}

function weaponMaterialTemplate(material, stageLabel) {
  return `<article class="weapon-material">
    <div class="weapon-material-head"><div class="material-icon">${materialIcon(material.icon)}</div><div><span>${escapeHtml(stageLabel)}</span><h4>${highlight(material.name, state.detailQuery)}</h4></div></div>
    <div class="weapon-material-amount"><span>需求数量</span><strong>${escapeHtml(material.amount)}</strong></div>
    <p>${highlight(material.note, state.detailQuery)}</p><footer><span>来源</span>${highlight(material.source, state.detailQuery)}</footer>
  </article>`;
}

function weaponEventsTemplate(events) {
  return `<ol class="weapon-event-list">${events.map((event) => `<li><time>${escapeHtml(event.date)}</time><p>${escapeHtml(event.text)}</p><a href="${escapeHtml(event.url)}" target="_blank" rel="noopener noreferrer">依据 ↗</a></li>`).join('')}</ol>`;
}

function renderWeaponEvolution() {
  const query = state.detailQuery.toLowerCase();
  const stages = state.guide.stages.filter((stage) => !query || `${stage.label} ${stage.names.join(' ')} ${stage.obtain} ${stage.materials.map((m) => `${m.name} ${m.amount}`).join(' ')}`.toLowerCase().includes(query));
  els.detailResultCount.textContent = `${stages.length} / ${state.guide.stageCount} 代`;
  if (!stages.length) return '<div class="detail-empty">没有找到对应等级或材料。</div>';
  return `<div class="weapon-audit-note"><span>已逐项校对</span><p>仅录入官网或可交叉核对的信息；公告没有公布兑换数量的材料，明确标为“未注明”。</p></div><div class="weapon-evolution">${stages.map((stage) => `
    <section class="weapon-generation">
      <div class="generation-axis"><span>${String(stage.order).padStart(2, '0')}</span><i></i></div>
      <div class="generation-card">
        <header><div><span>官网首见 · ${escapeHtml(stage.firstSeen)}</span><h3>${highlight(stage.label, state.detailQuery)}</h3></div><small>${stage.materials.length ? `${stage.materials.length} 项材料` : '直接掉落 / 配方缺失'}</small></header>
        <div class="weapon-names">${stage.names.map((name) => `<span>${highlight(name, state.detailQuery)}</span>`).join('')}</div>
        <div class="weapon-obtain"><span>取得方式</span><p>${highlight(stage.obtain, state.detailQuery)}</p></div>
        ${stage.materials.length ? `<div class="weapon-materials">${stage.materials.map((material) => weaponMaterialTemplate(material, stage.label)).join('')}</div>` : '<div class="no-recipe">现存官网公告未披露可核验的初始材料数量，不补猜测。</div>'}
        <details class="generation-history"><summary><span>版本变化</span><small>${stage.events.length} 个节点</small><svg viewBox="0 0 20 20"><path d="m6 8 4 4 4-4"/></svg></summary>${weaponEventsTemplate(stage.events)}</details>
      </div>
    </section>`).join('')}</div>`;
}

function renderWeaponMaterials() {
  const query = state.detailQuery.toLowerCase();
  const rows = state.guide.stages.flatMap((stage) => stage.materials.map((material) => ({ stage, material }))).filter(({ stage, material }) => !query || `${stage.label} ${material.name} ${material.amount} ${material.note} ${material.source}`.toLowerCase().includes(query));
  els.detailResultCount.textContent = `${rows.length} 项已核对材料`;
  if (!rows.length) return '<div class="detail-empty">没有找到对应材料。</div>';
  return `<div class="weapon-audit-note"><span>材料口径</span><p>数量以公告明确文字为准；“兑换总数未注明”不是缺省值，而是官网没有公开兑换总量。</p></div><div class="weapon-material-catalog">${rows.map(({ stage, material }) => weaponMaterialTemplate(material, stage.label)).join('')}</div>`;
}

function renderGuide() {
  if (!state.guide) return;
  if (state.guide.kind === 'weapon-evolution') {
    els.detailList.innerHTML = state.section === 'materials' ? renderWeaponMaterials() : renderWeaponEvolution();
    return;
  }
  els.detailList.innerHTML = state.section === 'materials' ? renderMaterials() : renderRoute();
}

els.groupFilters.addEventListener('click', (event) => { const button = event.target.closest('[data-group]'); if (!button) return; state.group = button.dataset.group; renderGroups(); renderSystems(); });
els.systemGrid.addEventListener('click', (event) => { const card = event.target.closest('[data-system-id]'); if (card) openSystem(card.dataset.systemId); });
els.systemSearch.addEventListener('input', () => { state.query = els.systemSearch.value.trim(); clearTimeout(searchTimer); searchTimer = setTimeout(renderSystems, 180); });
$$('[data-system]').forEach((button) => button.addEventListener('click', () => openSystem(button.dataset.system)));
$$('.drawer-tabs button').forEach((button) => button.addEventListener('click', () => { state.section = button.dataset.section; updateTabs(); renderGuide(); }));
els.detailSearch.addEventListener('input', () => { state.detailQuery = els.detailSearch.value.trim(); clearTimeout(detailTimer); detailTimer = setTimeout(renderGuide, 180); });
els.closeDrawer.addEventListener('click', closeDrawer); els.backdrop.addEventListener('click', closeDrawer);
document.addEventListener('keydown', (event) => { if (event.key === '/' && !['INPUT', 'TEXTAREA'].includes(document.activeElement.tagName)) { event.preventDefault(); els.systemSearch.focus(); } if (event.key === 'Escape') closeDrawer(); });

loadSystems();
