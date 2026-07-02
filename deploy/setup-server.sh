#!/usr/bin/env sh
set -eu

APP_NAME="xunxian-archive"
REPO_ROOT="${REPO_ROOT:-$HOME/repos}"
APP_ROOT="${APP_ROOT:-$HOME/apps}"
BARE_REPO="$REPO_ROOT/$APP_NAME.git"
WORK_TREE="$APP_ROOT/$APP_NAME"

if ! command -v git >/dev/null 2>&1; then
  echo "缺少 git，请先安装。" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
  echo "缺少 Docker Compose。建议使用腾讯云轻量应用服务器的 Docker CE 镜像。" >&2
  exit 1
fi

mkdir -p "$REPO_ROOT" "$APP_ROOT" "$WORK_TREE"

if [ ! -d "$BARE_REPO" ]; then
  git init --bare "$BARE_REPO"
fi

cat > "$BARE_REPO/hooks/post-receive" <<EOF
#!/usr/bin/env sh
set -eu
GIT_DIR="$BARE_REPO"
WORK_TREE="$WORK_TREE"
while read oldrev newrev refname; do
  [ "\$refname" = "refs/heads/main" ] || continue
  mkdir -p "\$WORK_TREE"
  git --git-dir="\$GIT_DIR" --work-tree="\$WORK_TREE" checkout -f main
  cd "\$WORK_TREE"
  if [ ! -f .env ]; then
    cp .env.deploy.example .env
  fi
  if [ ! -f data/announcements.json ]; then
    echo "首次部署：正在建立完整公告索引…"
    docker compose build app
    docker compose run --rm app node server.js --sync-all
  fi
  docker compose up -d --build --remove-orphans
  docker image prune -f >/dev/null 2>&1 || true
  echo "寻仙志已更新：\$(date '+%Y-%m-%d %H:%M:%S')"
done
EOF

chmod +x "$BARE_REPO/hooks/post-receive"
echo "READY:$BARE_REPO"
