# 寻仙志 · 版本资料库

一个只面向《寻仙1 / 新寻仙》的系统资料与版本公告检索网站。它会从腾讯游戏官网读取历年更新公告，并整理成结构化的养成路线、材料图鉴、需求数量、获取方式、官网首见年份和后续变更时间轴。

当前知识库覆盖 18 套系统、53 个养成阶段、87 种核心材料和 487 个可追溯变更节点。公告只作为资料证据，不作为系统详情的主要阅读形式。

## 运行

需要 Node.js 20 或更高版本。

```powershell
cd D:\GIT\XunxianArchive
npm start
```

浏览器打开：<http://127.0.0.1:4173>

- `/`：系统资料库
- `/updates`：版本公告全文检索（次级入口）

首次启动会自动同步最近 36 页目录中的版本公告。网页右上角可以增量同步；左侧“同步全部历史公告”会扫描官网全部历史页面，耗时取决于网络状况。

## 常用命令

```powershell
# 检查 JavaScript 语法
npm run check

# 不启动网页，直接同步全部历史公告
npm run sync

# 开发模式
npm run dev
```

数据保存在 `data/announcements.json`，不需要数据库，也没有第三方 Node.js 运行依赖。《寻仙2》标题公告会在同步和读取时排除。

## 腾讯云部署

推荐使用腾讯云轻量应用服务器的 Docker CE 镜像，安全组/防火墙只需对公网开放 `80`、`443`，并为维护开放 `22`。

项目已经包含：

- `Dockerfile` 与 `docker-compose.yml`：Node.js 服务 + Caddy 反向代理。
- `Caddyfile`：填写域名后自动启用 HTTPS。
- `deploy/setup-server.sh`：在云服务器创建 Git 接收仓库和自动部署钩子。
- `scripts/publish.ps1`：提交本地修改、推送到腾讯云并触发更新。

首次在腾讯云服务器执行：

```bash
sh setup-server.sh
```

本地添加服务器远端（替换用户名和 IP）：

```powershell
git remote add production ssh://ubuntu@服务器IP/home/ubuntu/repos/xunxian-archive.git
git push production main
```

以后每次发布：

```powershell
.\scripts\publish.ps1 -Message "说明本次修改"
```

仅使用公网 IP 时，服务器 `.env` 保持 `SITE_ADDRESS=:80`。绑定域名后改为：

```text
SITE_ADDRESS=你的域名
```

## 数据来源

[《新寻仙》腾讯游戏官网公告页](https://xx.qq.com/webplat/info/news_version3/154/2233/3889/m2702/list_1.shtml)

本项目只提供检索和阅读界面，公告内容版权归原作者所有。
