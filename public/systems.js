const $ = (selector) => document.querySelector(selector);
const $$ = (selector) => [...document.querySelectorAll(selector)];

const els = {
  systemSearch: $('#systemSearch'), systemGrid: $('#systemGrid'), emptyState: $('#emptyState'),
  groupFilters: $('#groupFilters'), stageTitle: $('#stageTitle'), filteredCount: $('#filteredCount'),
  systemCount: $('#systemCount'), updateCount: $('#updateCount'), materialCount: $('#materialCount'), headerState: $('#headerState'),
  drawer: $('#detailDrawer'), backdrop: $('#drawerBackdrop'), closeDrawer: $('#closeDrawer'),
  drawerIcon: $('#drawerIcon'), drawerGroup: $('#drawerGroup'), drawerName: $('#drawerName'), drawerDescription: $('#drawerDescription'),
  drawerUpdates: $('#drawerUpdates'), drawerMaterials: $('#drawerMaterials'), drawerLatest: $('#drawerLatest'),
  timelineTab: $('#timelineTab'), materialsTab: $('#materialsTab'), detailSearch: $('#detailSearch'),
  detailResultCount: $('#detailResultCount'), detailList: $('#detailList'), detailPagination: $('#detailPagination'),
  detailPrev: $('#detailPrev'), detailNext: $('#detailNext'), detailPage: $('#detailPage'), toastWrap: $('#toastWrap'),
};

const state = { systems: [], groups: [], group: 'all', query: '', selected: null, section: 'timeline', detailQuery: '', page: 1, pages: 1 };
let searchTimer;
let detailTimer;

const icons = {
  blade: '<svg viewBox="0 0 24 24"><path d="m5 19 4-4m-2 6-4-4m5-3L18 4l2 2-10 10m4-8 2 2"/></svg>',
  armor: '<svg viewBox="0 0 24 24"><path d="m8 4 4 2 4-2 4 3-2 4v9H6v-9L4 7l4-3Zm0 0v6m8-6v6M9 14h6"/></svg>',
  pet: '<svg viewBox="0 0 24 24"><path d="M8 11c-2 1-3 3-2 5 1 3 4 4 6 4s5-1 6-4c1-2 0-4-2-5-2-1-2 1-4 1s-2-2-4-1ZM7 8c1 0 2-1 2-3S8 2 7 3 5 5 6 7c0 1 0 1 1 1Zm10 0c-1 0-2-1-2-3s1-3 2-2 2 2 1 4c0 1 0 1-1 1ZM12 7c1 0 2-1 2-3s-1-3-2-2-2 2-2 3 1 2 2 2Z"/></svg>',
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

function escapeHtml(value = '') { return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#039;'); }
function escapeRegex(value = '') { return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'); }
function mark(value, words = []) { const safe = escapeHtml(value); const terms = words.filter(Boolean).sort((a,b)=>b.length-a.length); return terms.length ? safe.replace(new RegExp(`(${terms.map(escapeRegex).join('|')})`, 'gi'), '<mark>$1</mark>') : safe; }
function icon(name) { return icons[name] || icons.orb; }
async function request(url) { const response = await fetch(url); const data = await response.json().catch(()=>({})); if (!response.ok) throw new Error(data.error || '请求失败'); return data; }
function toast(message) { const node=document.createElement('div'); node.className='toast'; node.textContent=message; els.toastWrap.append(node); setTimeout(()=>node.remove(),2800); }

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
    <button class="system-card" type="button" data-system-id="${system.id}" style="--system-color:${system.color}">
      <div class="card-top"><span class="system-icon">${icon(system.icon)}</span><span class="card-group">${escapeHtml(system.group)}</span></div>
      <h3>${mark(system.name, [state.query])}</h3><p>${mark(system.description, [state.query])}</p>
      <div class="card-stats"><span><b>${system.updateCount}</b>次迭代</span><span><b>${system.materialCount}</b>条材料</span><span class="card-arrow"><svg viewBox="0 0 20 20"><path d="m7 4 6 6-6 6"/></svg></span></div>
    </button>`).join('');
}

async function loadSystems() {
  try {
    const [data, stats] = await Promise.all([request('/api/systems'), request('/api/stats')]);
    state.systems = data.systems; state.groups = data.groups;
    const updates = data.systems.reduce((sum,item)=>sum+item.updateCount,0);
    const materials = data.systems.reduce((sum,item)=>sum+item.materialCount,0);
    els.systemCount.textContent=data.systems.length; els.updateCount.textContent=updates.toLocaleString('zh-CN'); els.materialCount.textContent=materials.toLocaleString('zh-CN');
    els.headerState.textContent=`${stats.earliest.slice(0,4)}—${stats.latest.slice(0,4)} · ${stats.count} 篇寻仙1公告`;
    renderGroups(); renderSystems();
    const hash=location.hash.slice(1); if(hash && state.systems.some(item=>item.id===hash)) openSystem(hash);
  } catch(error) { toast(error.message); }
}

function setDrawerSummary(system) {
  els.drawer.style.setProperty('--system-color', system.color); els.drawerIcon.innerHTML=icon(system.icon);
  els.drawerGroup.textContent=system.group; els.drawerName.textContent=system.name; els.drawerDescription.textContent=system.description;
  els.drawerUpdates.textContent=system.updateCount; els.drawerMaterials.textContent=system.materialCount; els.drawerLatest.textContent=system.latestDate?.slice(0,7) || '—';
}

async function openSystem(id, section = 'timeline') {
  state.selected=id; state.section=section; state.page=1; state.detailQuery=''; els.detailSearch.value='';
  const system=state.systems.find(item=>item.id===id); if(!system)return;
  setDrawerSummary(system); updateTabs(); els.drawer.classList.add('open'); els.backdrop.classList.add('visible'); els.drawer.setAttribute('aria-hidden','false'); document.body.style.overflow='hidden'; location.hash=id;
  await loadDetail();
}

function closeDrawer() { els.drawer.classList.remove('open'); els.backdrop.classList.remove('visible'); els.drawer.setAttribute('aria-hidden','true'); document.body.style.overflow=''; history.replaceState(null,'',location.pathname); }
function updateTabs() { $$('.drawer-tabs button').forEach(button=>button.classList.toggle('active',button.dataset.section===state.section)); }

async function loadDetail() {
  els.detailList.innerHTML='<div class="detail-empty">正在整理资料…</div>';
  const params=new URLSearchParams({section:state.section,q:state.detailQuery,page:String(state.page),limit:'14'});
  try {
    const data=await request(`/api/systems/${state.selected}?${params}`); state.pages=data.pages;
    const keywords=data.system.keywords; els.detailResultCount.textContent=`${data.total} 条`;
    els.detailPage.textContent=`${data.page} / ${data.pages}`; els.detailPrev.disabled=data.page<=1; els.detailNext.disabled=data.page>=data.pages; els.detailPagination.hidden=data.total===0;
    if(!data.items.length){els.detailList.innerHTML='<div class="detail-empty">这一部分暂时没有可提取的官网记录。</div>';return}
    els.detailList.innerHTML=data.items.map(item=>`
      <article class="detail-item">
        <div class="detail-item-head"><span class="detail-item-tag">${state.section==='materials'?'材料线索':'版本迭代'}</span>${item.version?`<span>v${escapeHtml(item.version)}</span>`:''}<time>${escapeHtml(item.date)}</time></div>
        <h3>${mark(item.title,[state.detailQuery])}</h3>
        <p>${mark(item.snippet,[...keywords,state.detailQuery])}</p>
        <footer><a href="${escapeHtml(item.url)}" target="_blank" rel="noopener noreferrer">查看公告出处 <svg viewBox="0 0 20 20"><path d="M7 4h9v9M16 4l-9 9"/></svg></a></footer>
      </article>`).join('');
    els.drawer.scrollTo({top:280,behavior:'smooth'});
  } catch(error){els.detailList.innerHTML=`<div class="detail-empty">${escapeHtml(error.message)}</div>`}
}

els.groupFilters.addEventListener('click',(event)=>{const button=event.target.closest('[data-group]');if(!button)return;state.group=button.dataset.group;renderGroups();renderSystems()});
els.systemGrid.addEventListener('click',(event)=>{const card=event.target.closest('[data-system-id]');if(card)openSystem(card.dataset.systemId)});
els.systemSearch.addEventListener('input',()=>{state.query=els.systemSearch.value.trim();clearTimeout(searchTimer);searchTimer=setTimeout(renderSystems,180)});
$$('[data-system]').forEach(button=>button.addEventListener('click',()=>openSystem(button.dataset.system)));
$$('.drawer-tabs button').forEach(button=>button.addEventListener('click',()=>{state.section=button.dataset.section;state.page=1;updateTabs();loadDetail()}));
els.detailSearch.addEventListener('input',()=>{state.detailQuery=els.detailSearch.value.trim();state.page=1;clearTimeout(detailTimer);detailTimer=setTimeout(loadDetail,220)});
els.detailPrev.addEventListener('click',()=>{if(state.page>1){state.page--;loadDetail()}});els.detailNext.addEventListener('click',()=>{if(state.page<state.pages){state.page++;loadDetail()}});
els.closeDrawer.addEventListener('click',closeDrawer);els.backdrop.addEventListener('click',closeDrawer);
document.addEventListener('keydown',(event)=>{if(event.key==='/'&&!['INPUT','TEXTAREA'].includes(document.activeElement.tagName)){event.preventDefault();els.systemSearch.focus()}if(event.key==='Escape')closeDrawer()});

loadSystems();
