export const SYSTEM_GUIDES = {
  weapon: {
    stages: [
      { id: 'obtain', name: '武器本体', description: '通过副本、世界首领或材料兑换取得对应等级武器。' },
      { id: 'soul', name: '武魂升阶', description: '使用当前等级段强化材料提升武魂品阶。' },
      { id: 'pattern', name: '阵纹天机', description: '提升阵纹孔洞和武器天机等级。' },
    ],
    materials: [
      { name: '真昊玄金锭', stage: 'obtain', icon: 'ingot', amount: '按兑换档位', purpose: '兑换 260 级武魂武器礼包', source: '本区及跨区世界 BOSS 掉落' },
      { name: '青玄陨铁', stage: 'obtain', icon: 'ore', amount: '100 块', purpose: '合成 1 个 170 级武魂武器礼包', source: '对应版本首领与活动产出' },
      { name: '七星神钢锭', stage: 'obtain', icon: 'ingot', amount: '100 枚', purpose: '兑换 190 级武魂武器礼包', source: '世界 BOSS 产出' },
      { name: '灼焱水晶', stage: 'obtain', icon: 'crystal', amount: '1,000 枚', purpose: '与七星神钢锭共同兑换 190 级礼包', source: '副本与活动奖励' },
      { name: '天曜石', stage: 'soul', icon: 'gem', amount: '按武魂等阶', purpose: '提升高等级武器武魂品阶', source: '车迟国副本、联盟积分及活动礼包' },
      { name: '金晶玉', stage: 'pattern', icon: 'jade', amount: '101—110 重每次 1 个；111—120 重每次 2 个', purpose: '升级武器阵纹孔洞', source: '金币集市，单价 3 砖流通金' },
      { name: '天机夔龙玉佩', stage: 'pattern', icon: 'pendant', amount: '3—4 个合成/兑换上级玉佩', purpose: '点化武器天机', source: '合成界面、仙友会多宝仙人' },
    ],
  },
  equipment: {
    stages: [
      { id: 'obtain', name: '装备获取', description: '收集图纸、卷轴和副本材料取得当前等级装备。' },
      { id: 'refine', name: '强化精炼', description: '强化装备基础属性并提升精炼档次。' },
      { id: 'wash', name: '洗炼镶嵌', description: '洗炼属性类型，安装和升级装备灵石。' },
    ],
    materials: [
      { name: '碧玺玄玉', stage: 'wash', icon: 'jade', amount: '洗炼按次消耗；回收可得 1—4 枚', purpose: '洗炼星陨及星陨·禋装备属性类型', source: '回收对应装备；活动与副本奖励' },
      { name: '翠棱晶石', stage: 'refine', icon: 'crystal', amount: '按强化阶段', purpose: '装备强化与阶段养成', source: '回归任务、副本与活动礼包' },
      { name: '精炼飘羽仙丹', stage: 'refine', icon: 'pill', amount: '按精炼次数', purpose: '装备精炼', source: '副本奖励、回归活动与礼包' },
      { name: '装备灵石兑换凭证', stage: 'wash', icon: 'token', amount: '按灵石档位', purpose: '兑换装备灵石', source: '活动副本与周年奖励' },
      { name: '金丝玛瑙', stage: 'obtain', icon: 'gem', amount: '按部位兑换', purpose: '兑换残缺的星陨圣卷腰、鞋部位', source: '车迟国普通副本' },
      { name: '寻仙周年印记', stage: 'obtain', icon: 'token', amount: '按图纸兑换', purpose: '兑换周年装备图纸', source: '周年副本首领额外掉落' },
    ],
  },
  'attendant-pet': {
    stages: [
      { id: 'obtain', name: '侍宠获取', description: '从副本、活动或礼包取得侍宠与宠物蛋。' },
      { id: 'growth', name: '四维成长', description: '提升四维、技能与宠物融合能力。' },
      { id: 'soul', name: '魂器进阶', description: '魂器从灵炁进入真灵、灵法阶段。' },
    ],
    materials: [
      { name: '真灵图样', stage: 'soul', icon: 'blueprint', amount: '进阶时消耗', purpose: '灵炁魂器满级后开启真灵阶段', source: '灵炁图录在道具合成界面生成' },
      { name: '灵法图录', stage: 'soul', icon: 'scroll', amount: '进阶时消耗；500 枚神威印记可兑换', purpose: '真灵魂器满级后进入灵法阶段', source: '邪魔杀阵奖励、仙友会多宝仙人' },
      { name: '魂器灵玉', stage: 'soul', icon: 'jade', amount: '生肖印记 1 枚兑换 3 个', purpose: '侍宠魂器养成', source: '诛仙剑阵掉落、生肖印记兑换' },
      { name: '昊灵精魄', stage: 'growth', icon: 'essence', amount: '按成长档位', purpose: '侍宠成长与四维相关养成', source: '仙缘宝鉴及活动礼包' },
      { name: '农政仙书', stage: 'growth', icon: 'book', amount: '按成长阶段', purpose: '宠物相关成长养成', source: '活动礼包、仙缘宝鉴' },
      { name: '侍宠兑换券', stage: 'obtain', icon: 'ticket', amount: '按目标侍宠', purpose: '兑换经典或活动侍宠', source: '节日活动与礼包' },
    ],
  },
  'mount-pet': {
    stages: [
      { id: 'obtain', name: '骑宠获取', description: '通过印记、兑换券和凭证取得骑宠。' },
      { id: 'growth', name: '成长进化', description: '提升骑宠属性、进化与技能等级。' },
      { id: 'gear', name: '骑宠装备', description: '配置骑宠装备并强化特殊能力。' },
    ],
    materials: [
      { name: '神威印记', stage: 'obtain', icon: 'token', amount: '常见兑换 20 枚', purpose: '在骑宠商人处兑换指定骑宠', source: '活动、副本和仙缘宝鉴' },
      { name: '骑宠兑换券', stage: 'obtain', icon: 'ticket', amount: '按目标骑宠', purpose: '兑换活动或经典骑宠', source: '合区奖励、节日礼包' },
      { name: '假日骑宠兑换券', stage: 'obtain', icon: 'ticket', amount: '1 张', purpose: '节日骑宠兑换', source: '节日上线礼包' },
      { name: '乐购凭证碎片', stage: 'obtain', icon: 'fragment', amount: '10 个合成 1 张完整凭证', purpose: '完整凭证兑换仙兽骑宠', source: '仙玉返还礼包' },
      { name: '聚灵晶（极）', stage: 'growth', icon: 'crystal', amount: '按进化阶段', purpose: '骑宠成长与进化', source: '礼包、凭证兑换与活动奖励' },
      { name: '新年通宝', stage: 'obtain', icon: 'coin', amount: '100 枚', purpose: '兑换旅行骑宠“大橘大利”', source: '擂台挑战·聚宝金蟾活动' },
    ],
  },
  mental: {
    stages: [
      { id: 'learn', name: '心法获取', description: '取得职业心法书并满足声望条件。' },
      { id: 'level', name: '心法升级', description: '消耗心法书、散卷或天书提升等级。' },
      { id: 'equip', name: '装备生效', description: '装备心法并获得职业属性或技能效果。' },
    ],
    materials: [
      { name: '功勋印记', stage: 'learn', icon: 'token', amount: '旧制每本 20—50 个', purpose: '兑换心法书或心法散卷', source: '功勋堂、战场与回归奖励' },
      { name: '心法散卷', stage: 'learn', icon: 'scroll', amount: '按目标心法', purpose: '兑换可学习心法、心法天书及开启道具', source: '功勋堂物资官使用各类印记兑换' },
      { name: '心法天书', stage: 'level', icon: 'book', amount: '按升级阶段', purpose: '心法升级与增强', source: '活动、礼包与功勋堂兑换' },
      { name: '元宵猜谜印记', stage: 'learn', icon: 'token', amount: '5 枚', purpose: '兑换 1 个心法天书礼包', source: '元宵猜谜活动' },
    ],
  },
  'yin-yang-jade': {
    stages: [
      { id: 'obtain', name: '玉珏获取', description: '从阴阳劫境副本或历史兑换渠道取得玉珏。' },
      { id: 'exchange', name: '七阶法宝兑换', description: '集齐玉珏和流通金兑换 120 级七阶法宝。' },
    ],
    materials: [
      { name: '阴阳玉珏', stage: 'exchange', icon: 'yinyang', amount: '20 枚', purpose: '兑换 120 级七阶法宝', source: '阴阳劫境副本；历史上可用神威印记兑换' },
      { name: '流通金', stage: 'exchange', icon: 'coin', amount: '100 砖', purpose: '与阴阳玉珏共同兑换七阶法宝', source: '游戏内流通金来源' },
      { name: '神威印记', stage: 'obtain', icon: 'token', amount: '20 枚', purpose: '历史渠道兑换阴阳玉珏', source: '仙友会多宝仙人（后续已移除）' },
      { name: '七阶绛紫石', stage: 'exchange', icon: 'stone', amount: '按强化次数', purpose: '七阶法宝强化', source: '阴阳劫境副本奖励' },
    ],
  },
  'magic-weapon': {
    stages: [
      { id: 'obtain', name: '法宝获取', description: '通过副本材料与 NPC 兑换取得各阶法宝。' },
      { id: 'strengthen', name: '法宝强化', description: '使用对应阶位绛紫石提升法宝。' },
      { id: 'talent', name: '天赋养成', description: '解锁法宝天赋并强化特殊效果。' },
    ],
    materials: [
      { name: '阴阳玉珏', stage: 'obtain', icon: 'yinyang', amount: '20 枚 + 100 砖流通金', purpose: '兑换 120 级七阶法宝', source: '阴阳劫境副本' },
      { name: '绛紫石', stage: 'strengthen', icon: 'stone', amount: '按法宝阶位', purpose: '法宝强化', source: '对应阶位副本、活动与礼包' },
      { name: '流沙灵晶', stage: 'obtain', icon: 'crystal', amount: '按合成配方', purpose: '转换流沙灵玉，兑换 260 级十四阶二代法宝', source: '车迟国普通副本' },
      { name: '返虚灵晶', stage: 'talent', icon: 'crystal', amount: '按天赋阶段', purpose: '高阶法宝相关养成', source: '仙缘宝鉴与活动奖励' },
      { name: '猎魂碎片', stage: 'talent', icon: 'fragment', amount: '按兑换项目', purpose: '法宝及相关养成兑换', source: '副本、礼包与活动奖励' },
    ],
  },
  'spiritual-treasure': {
    stages: [
      { id: 'craft', name: '仙诀生产', description: '通过方术配方生产通用或五行仙诀结晶。' },
      { id: 'exchange', name: '仙诀兑换', description: '在万仙台按品质兑换灵宝仙诀。' },
      { id: 'quality', name: '极品进阶', description: '极品仙诀额外消耗上一阶段混元结晶。' },
    ],
    materials: [
      { name: '无相皓月结晶', stage: 'exchange', icon: 'crystal', amount: '按仙诀品质', purpose: '兑换洪阶灵宝仙诀', source: '洪阶方术生产配方' },
      { name: '五行皓月结晶', stage: 'exchange', icon: 'crystal', amount: '按仙诀品质', purpose: '兑换洪阶五行仙诀', source: '洪阶方术生产配方' },
      { name: '荒阶·混元结晶', stage: 'quality', icon: 'orb', amount: '极品品质额外消耗', purpose: '兑换极品洪阶仙诀', source: '回收荒阶极品灵宝仙诀或直接购买' },
      { name: '霞光仙石', stage: 'craft', icon: 'stone', amount: '按阶段配方', purpose: '生产仙阶仙诀', source: '方术生产材料来源' },
      { name: '五行结晶', stage: 'craft', icon: 'crystal', amount: '按阶段配方', purpose: '生产五行仙诀', source: '副本、生产与活动产出' },
    ],
  },
  'mystic-arts': {
    stages: [
      { id: 'paper', name: '灵璧符纸', description: '选择符纸决定灵璧可打造的属性。' },
      { id: 'powder', name: '仙粉消耗', description: '不同符纸对应不同品阶仙粉。' },
      { id: 'craft', name: '属性打造', description: '在物品打造界面附纸并生成属性。' },
    ],
    materials: [
      { name: '玄法仙粉', stage: 'powder', icon: 'powder', amount: '按打造配方', purpose: '道法符纸对应的灵璧打造材料', source: '世界首领掉落、紫灵仙粉转换、活动奖励' },
      { name: '极地仙粉', stage: 'powder', icon: 'powder', amount: '按打造配方', purpose: '通玄符纸对应材料', source: '灵璧与活动相关产出' },
      { name: '紫灵仙粉', stage: 'powder', icon: 'powder', amount: '按转换配方', purpose: '可转换为玄法仙粉', source: '副本、首领与活动奖励' },
      { name: '湛苍仙粉', stage: 'powder', icon: 'powder', amount: '按打造配方', purpose: '玄元符纸对应材料', source: '礼包、活动和灵璧玩法' },
      { name: '灵璧符纸', stage: 'paper', icon: 'scroll', amount: '每次打造 1 张', purpose: '指定灵璧属性配方', source: '玩法产出与兑换' },
    ],
  },
  'battle-soul': {
    stages: [
      { id: 'obtain', name: '战魂获取', description: '取得不同等级战魂水晶。' },
      { id: 'refine', name: '附魂定魂', description: '通过附魂与定魂稳定并提升战魂。' },
      { id: 'recycle', name: '分解回收', description: '分解战魂水晶取得魂玉。' },
    ],
    materials: [
      { name: '战魂水晶', stage: 'obtain', icon: 'crystal', amount: '1 个起', purpose: '获得或培养宠物战魂', source: '天魔幻境、首领掉落与兑换' },
      { name: '附魂石', stage: 'refine', icon: 'stone', amount: '按附魂次数', purpose: '战魂附魂', source: '商城快捷购买与活动奖励' },
      { name: '定魂石', stage: 'refine', icon: 'stone', amount: '按定魂次数', purpose: '战魂定魂', source: '商城礼包、活动与首领掉落' },
      { name: '魂玉', stage: 'recycle', icon: 'jade', amount: '按战魂等级随机', purpose: '战魂相关强化', source: '分解战魂水晶，等级越高品质越高' },
      { name: '天魔印', stage: 'obtain', icon: 'token', amount: '配合流通金', purpose: '在乌罗处兑换战魂水晶', source: '天魔幻境' },
    ],
  },
  'treasure-plate': {
    stages: [
      { id: 'plate', name: '宝盘获取', description: '取得万象宝盘及对应卦象。' },
      { id: 'gem', name: '宝盘宝石', description: '兑换并镶嵌不同卦象宝石。' },
      { id: 'upgrade', name: '宝石升级', description: '使用阶段材料提升宝石能力。' },
    ],
    materials: [
      { name: '冰棱石', stage: 'gem', icon: 'crystal', amount: '30 枚', purpose: '任选兑换闇云、青焰、凶冥、擎雷宝盘宝石', source: '宝盘相关玩法与活动' },
      { name: '紫宸石', stage: 'gem', icon: 'stone', amount: '10 枚', purpose: '兑换天煞、断虹、极霜、冲霄宝盘宝石', source: '宝盘玩法与活动产出' },
      { name: '宝盘宝石', stage: 'upgrade', icon: 'gem', amount: '按卦象和档位', purpose: '提供宝盘属性', source: '仙友会奇石商人兑换' },
      { name: '宝盘升级礼包', stage: 'upgrade', icon: 'box', amount: '按礼包档位', purpose: '集中补充宝盘升级材料', source: '仙玉商城助力礼包' },
    ],
  },
  skills: {
    stages: [
      { id: 'learn', name: '技能学习', description: '取得技能书并学习职业技能。' },
      { id: 'level', name: '技能升级', description: '提升技能等级与效果。' },
      { id: 'talent', name: '天赋联动', description: '通过法宝天赋和门派能力强化技能。' },
    ],
    materials: [
      { name: '技能书', stage: 'learn', icon: 'book', amount: '每项技能 1 本', purpose: '学习对应职业技能', source: '副本、首领、门派与活动兑换' },
      { name: '心法天书', stage: 'level', icon: 'book', amount: '按技能或心法阶段', purpose: '增强职业能力', source: '功勋堂、活动与礼包' },
      { name: '法宝天赋点', stage: 'talent', icon: 'spark', amount: '按法宝阶位', purpose: '激活技能联动天赋', source: '法宝成长与阶位提升' },
      { name: '门派贡献', stage: 'learn', icon: 'token', amount: '按兑换项目', purpose: '兑换门派技能与物资', source: '门派任务和活动' },
    ],
  },
  'immortal-rank': {
    stages: [
      { id: 'rank', name: '仙阶晋升', description: '满足条件并消耗当前阶段材料晋升仙阶。' },
      { id: 'recipe', name: '仙诀生产', description: '方术生产当前仙阶通用或五行仙诀。' },
      { id: 'exchange', name: '仙诀兑换', description: '以结晶兑换对应品质仙诀。' },
    ],
    materials: [
      { name: '琮蕸砂', stage: 'rank', icon: 'powder', amount: '按仙阶档位', purpose: '仙阶晋升至真昊等阶段', source: '副本、礼包与活动奖励' },
      { name: '曜天阳魄', stage: 'recipe', icon: 'orb', amount: '按阳阶配方', purpose: '生产阳阶仙诀', source: '对应阶段玩法产出' },
      { name: '霞光仙石', stage: 'recipe', icon: 'stone', amount: '按仙阶配方', purpose: '生产通用与五行仙诀', source: '生产、副本和活动产出' },
      { name: '五行结晶', stage: 'recipe', icon: 'crystal', amount: '按五行配方', purpose: '生产五行仙诀', source: '生产和玩法产出' },
      { name: '混元结晶', stage: 'exchange', icon: 'orb', amount: '极品品质额外消耗', purpose: '兑换极品仙阶仙诀', source: '回收上一阶段极品仙诀或购买' },
    ],
  },
  home: {
    stages: [
      { id: 'build', name: '家园建设', description: '解锁建筑、装饰和功能区域。' },
      { id: 'produce', name: '家园生产', description: '种植、生产并取得家园资源。' },
      { id: 'decorate', name: '家具装饰', description: '收集家具图纸与装饰物。' },
    ],
    materials: [
      { name: '仙府玉珏', stage: 'build', icon: 'jade', amount: '按兑换项目', purpose: '家园及仙府相关兑换', source: '家园玩法、活动与兑换' },
      { name: '农政仙书', stage: 'produce', icon: 'book', amount: '按生产阶段', purpose: '家园和宠物生产相关养成', source: '礼包、活动与仙缘宝鉴' },
      { name: '家具图纸', stage: 'decorate', icon: 'blueprint', amount: '每件家具 1 张', purpose: '制作家园家具', source: '家园任务、活动与兑换' },
      { name: '家园币', stage: 'build', icon: 'coin', amount: '按建设项目', purpose: '家园建设与商店购买', source: '家园日常与互动玩法' },
    ],
  },
  guild: {
    stages: [
      { id: 'contribute', name: '贡献积累', description: '完成仙盟任务与活动积累贡献。' },
      { id: 'build', name: '仙盟建设', description: '提升仙盟建筑和功能等级。' },
      { id: 'shop', name: '仙盟兑换', description: '使用贡献或印记兑换物资。' },
    ],
    materials: [
      { name: '仙盟贡献', stage: 'contribute', icon: 'token', amount: '按兑换项目', purpose: '仙盟商店兑换与个人养成', source: '仙盟任务、活动和捐献' },
      { name: '仙盟资金', stage: 'build', icon: 'coin', amount: '按建筑等级', purpose: '升级仙盟建筑', source: '成员任务、活动和捐献' },
      { name: '仙盟令牌', stage: 'shop', icon: 'token', amount: '按物资档位', purpose: '兑换仙盟限定物资', source: '仙盟玩法与活动奖励' },
      { name: '神威印记', stage: 'shop', icon: 'token', amount: '按兑换项目', purpose: '仙友会与仙盟相关高阶兑换', source: '副本、活动及赛事奖励' },
    ],
  },
  chronicle: {
    stages: [
      { id: 'collect', name: '图卷收集', description: '从任务、首领和场景取得图卷。' },
      { id: 'activate', name: '风物志激活', description: '使用图卷激活对应风物志。' },
      { id: 'reward', name: '称号奖励', description: '完成组合收集后领取称号与属性。' },
    ],
    materials: [
      { name: '风物志图卷', stage: 'collect', icon: 'scroll', amount: '每项 1 张', purpose: '激活对应风物志', source: '任务、首领掉落、场景探索与活动' },
      { name: '残破图卷', stage: 'collect', icon: 'fragment', amount: '按合成配方', purpose: '合成完整图卷', source: '副本和首领掉落' },
      { name: '寻仙周年印记', stage: 'reward', icon: 'token', amount: '按周年兑换表', purpose: '兑换周年称号、图卷和外观', source: '周年副本与活动' },
      { name: '称号兑换券', stage: 'reward', icon: 'ticket', amount: '每个称号 1 张或按活动规则', purpose: '兑换活动称号', source: '赛事、节日与运营活动' },
    ],
  },
  fashion: {
    stages: [
      { id: 'collect', name: '外观获取', description: '从活动、礼包和玩法取得外观。' },
      { id: 'exchange', name: '凭证兑换', description: '收集凭证或碎片兑换指定外观。' },
      { id: 'unlock', name: '衣橱解锁', description: '解锁仙衣华裳、相框和幻化。' },
    ],
    materials: [
      { name: '乐购凭证碎片', stage: 'exchange', icon: 'fragment', amount: '常见 10 个合成 1 张凭证', purpose: '兑换限定骑宠或外观', source: '仙玉返还礼包与活动' },
      { name: '时装兑换券', stage: 'exchange', icon: 'ticket', amount: '按目标时装', purpose: '兑换指定时装', source: '节日、赛事与运营活动' },
      { name: '仙衣华裳', stage: 'unlock', icon: 'silk', amount: '满足对应收藏条件', purpose: '解锁武器或装备外观', source: '持有指定武魂武器、活动与兑换' },
      { name: '相框兑换物', stage: 'unlock', icon: 'token', amount: '每款 1 个', purpose: '兑换限定相框', source: '跨区奖励、赛事与活动' },
    ],
  },
  crafting: {
    stages: [
      { id: 'recipe', name: '配方学习', description: '取得并学习对应生产配方。' },
      { id: 'material', name: '材料准备', description: '收集矿石、结晶、仙粉等原料。' },
      { id: 'produce', name: '生产合成', description: '达到生活技能等级后进行制造。' },
    ],
    materials: [
      { name: '生产配方', stage: 'recipe', icon: 'blueprint', amount: '每项 1 张', purpose: '解锁指定道具制造', source: 'NPC、任务、副本和活动兑换' },
      { name: '霞光仙石', stage: 'material', icon: 'stone', amount: '按仙诀配方', purpose: '生产高阶仙诀', source: '副本、活动与生产玩法' },
      { name: '五行结晶', stage: 'material', icon: 'crystal', amount: '按五行配方', purpose: '生产五行仙诀', source: '玩法、生产与活动产出' },
      { name: '仙粉', stage: 'material', icon: 'powder', amount: '按灵璧符纸配方', purpose: '灵璧属性打造', source: '首领、副本、转换与活动' },
      { name: '流通金', stage: 'produce', icon: 'coin', amount: '按配方手续费', purpose: '生产、合成与兑换手续费', source: '游戏内经济系统' },
    ],
  },
};
