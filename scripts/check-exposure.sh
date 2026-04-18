#!/usr/bin/env bash
# GitHub 노출 지표 확인 스크립트
# 사용: ./check-exposure.sh  (또는 bash scripts/check-exposure.sh)
set -e
REPO="vavibolivia/YtConverter"
GH="C:/Program Files/GitHub CLI/gh.exe"
[ -x "$GH" ] || GH="gh"

echo "═══ GitHub 노출 지표 — $REPO ═══"
echo ""
echo "▶ 기본 지표"
"$GH" repo view "$REPO" --json stargazerCount,watchers,forkCount,description \
  --jq '"  ⭐ Stars:    \(.stargazerCount)\n  👁 Watchers: \(.watchers.totalCount)\n  🍴 Forks:    \(.forkCount)\n  📝 설명:     \(.description)"'
echo ""
echo "▶ 릴리스 다운로드 수"
"$GH" api "repos/$REPO/releases" \
  --jq '.[] | "  \(.tag_name): " + ([.assets[] | "\(.name) = \(.download_count)회"] | join(", "))'
echo ""
echo "▶ 지난 14일 페이지뷰 / 클론"
VIEWS=$("$GH" api "repos/$REPO/traffic/views" --jq '"  총 뷰: \(.count) (고유 \(.uniques))"')
CLONES=$("$GH" api "repos/$REPO/traffic/clones" --jq '"  총 클론: \(.count) (고유 \(.uniques))"')
echo "$VIEWS"
echo "$CLONES"
echo ""
echo "▶ 인기 경로 (상위 10)"
"$GH" api "repos/$REPO/traffic/popular/paths" \
  --jq '.[] | "  \(.count)회\t\(.path)"' | head -10
echo ""
echo "▶ 리퍼러 (어디서 왔는지)"
"$GH" api "repos/$REPO/traffic/popular/referrers" \
  --jq '.[] | "  \(.count)회\t\(.referrer)"'
echo ""
echo "▶ 외부 검색 확인 — 직접 열어보세요"
echo "  Google    : https://www.google.com/search?q=site%3Agithub.com%2Fvavibolivia"
echo "  GitHub    : https://github.com/search?q=ytconverter+vavibolivia&type=repositories"
