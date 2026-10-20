# 디지몬 서바이버즈 (Digimon Survivors) 개발 종합 문서

디지몬 IP 기반의 탑다운 뱀파이어 서바이버즈(Vampire Survivors) 스타일 로그라이크 액션 게임의 개발 현황, 아키텍처, 시스템 명세 및 개선 히스토리를 정리한 문서 모음입니다.

---

## 📚 문서 색인 (Documentation Index)

| 번호 | 문서명 | 주요 내용 |
| :---: | :--- | :--- |
| **01** | [**01_PROJECT_OVERVIEW.md**](file:///D:/Project/DigimonSurvivors/docs/01_PROJECT_OVERVIEW.md) | 프로젝트 총괄 개요, 기술 스택, 핵심 아키텍처 및 디렉터리 구조 |
| **02** | [**02_CHARACTER_AND_EVOLUTION.md**](file:///D:/Project/DigimonSurvivors/docs/02_CHARACTER_AND_EVOLUTION.md) | 스타터 8종 디지몬, 다단계 진화 트리, 스킬 승계 및 비주얼 스탠디 개편 |
| **03** | [**03_SKILL_AND_VFX_ENGINE.md**](file:///D:/Project/DigimonSurvivors/docs/03_SKILL_AND_VFX_ENGINE.md) | 그림판 스타일 완전 탈피 및 11종 고품질 플라즈마/블룸 절차적 VFX 명세 |
| **04** | [**04_GAMEPLAY_AND_SYSTEMS.md**](file:///D:/Project/DigimonSurvivors/docs/04_GAMEPLAY_AND_SYSTEMS.md) | 모놀리스 파괴/드롭, TCG 서브 카드 업그레이드, 파티클 및 HUD 시스템 |
| **05** | [**05_DEVELOPMENT_CHANGELOG.md**](file:///D:/Project/DigimonSurvivors/docs/05_DEVELOPMENT_CHANGELOG.md) | 사용자 피드백 반영 타임라인 및 주요 기능 변경 이력 (Changelog) |

---

## 🚀 빠른 시작 (Quick Start)

### 실행 방법
1. 빌드 완료된 실행 파일 경로:
   ```
   D:\Project\DigimonSurvivors\publish\DigimonSurvivors.exe
   ```
2. 바로 더블 클릭하여 실행하거나 터미널에서 다음 명령어로 구동할 수 있습니다.
   ```powershell
   & "D:\Project\DigimonSurvivors\publish\DigimonSurvivors.exe"
   ```

### 조작 키 (Controls)
* **이동**: `W`, `A`, `S`, `D` 또는 방향키 (`↑`, `←`, `↓`, `→`)
* **긴급 대시 (회피/무적)**: `Space` (쿨다운: 2.5초)
* **공격 / 스킬**: 자동 발사 (Auto-targeting)
* **일시 정지 / 스킬 창**: `ESC`
* **모놀리스 파괴**: 주변에 스킬 투사체를 적중시켜 파괴 후 보상 카드 획득
