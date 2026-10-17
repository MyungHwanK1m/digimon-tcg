# ⚔️ DIGIMON TCG : DIGITAL COLOSSEUM (디지몬 TCG 디지털 콜로세움)

**디지몬 카드 게임(Digimon Card Game)** 공식 룰 기반의 **실전 AI 듀얼 아레나**, **1인 솔리테어 훈련소**, 그리고 **디지털 카드 도감 & 황금 비율 덱 공방**을 제공하는 C# (.NET 8.0 / WPF) 데스크톱 애플리케이션입니다.

---

## 🎮 주요 게임 모드 (3대 핵심 모드)

1. **⚔️ 실전 AI 듀얼 아레나 (Official AI Duel Arena - 메인 화면)**
   - **공식 플레이매트 대칭 레이아웃**: 상단(상대 시큐리티 & 상대 배틀 에어리어), 중앙(메모리 게이지 `-10 ~ +10` 줄다리기), 중단(내 시큐리티 5장 스택, 육성 에어리어, 내 배틀 에어리어 & 테이머 존, 메인 덱 & 트래시), 하단(내 손패)으로 구성된 실제 플레이매트 UI
   - **100% 공식 룰 시큐리티 & 메모리 시스템**:
     - 양측 모두 50장 메인 덱 셔플 후 맨 위 5장을 비공개 시큐리티로 세팅 (멀리건 1회 지원)
     - 시큐리티 오픈 시 **디지몬(시큐리티 배틀)**, **테이머(`신태일`, `매튜` 코스트 0 즉시 등장)**, **옵션(`가이아 포스`, `코큐토스 브레스`, `해머 스파크` 등 `[시큐리티]` 효과 즉시 발동)** 완벽 구현
     - 공식 메모리 게이지 줄다리기(`-1`에서 턴 종료 시 상대 `+1` 시작 등, 필드 위에 `ST1-12 신태일`이 있을 때만 최소 3메모리 보장)
   - **직관적인 마우스 드래그 앤 드롭(Drag & Drop) 조작**:
     - 손패 카드를 필드 디지몬 위에 드롭하여 **[🧬 진화 (1드로우)]** 또는 배틀 에어리어에 드롭하여 **[✨ 등장 / 옵션 발동]**
     - 육성 에어리어 디지몬을 배틀 에어리어로 드래그하여 **[🚀 배틀 이동]**
     - 배틀 에어리어 디지몬을 상단 상대 진영으로 드래그하여 **[⚔️ 시큐리티 어택 / 상대 레스트 디지몬 타격]**

2. **🎯 1인 솔리테어 훈련소 (Solo Training Lab)**
   - **Stage 1 (5턴 타임어택)**: 상대 방해 없이 5턴 이내에 Lv.3 → Lv.6 진화 체인을 완성해 시큐리티 5장을 돌파하는 빌드업 연습
   - **Stage 2 (블로커 돌파 챌린지)**: 상대 필드의 `≪블로커≫` 수비진을 제거 옵션이나 고DP 진화로 돌파하는 전술 훈련

3. **📚 디지털 카드 도감 & 덱 공방 (Card Encyclopedia & Deck Studio)**
   - **ST-1 가이아 레드 / ST-2 코큐토스 블루** 공식 카드 도감 및 고해상도 카드 디자인/효과 인스펙터
   - 초심자 권장 **황금 비율 HUD**(Lv.3 12~14장, Lv.4 10~12장, Lv.5 8장, Lv.6 4~6장 등) 실시간 검증 및 커스텀 덱 저장/불러오기

---

## 📂 프로젝트 구조

- **`src/DigimonTcg.Core`**: UI와 분리된 핵심 룰 엔진 및 데이터 레이어
  - `Models/`: `CardData`, `CardInstance`, `PlayerZones`, `GameState`, `Enums`
  - `Engine/`: `ActionValidator` (실시간 룰 검증 및 가이드), `GoldfishEngine` (공식 5단계 턴 진행, 시큐리티 효과, AI 턴 엔진)
  - `DeckBuilder/`: `DeckValidator` (50장 메인 덱 / 0~5장 디지타마 / 동일 카드 4장 제한 및 황금 비율 계산)
  - `Data/`: `StarterDecks`, `CardCatalogService` (`DigimonCard.io` API 동기화 및 로컬 오프라인 캐시)
- **`src/DigimonTcg.Wpf`**: Windows 데스크톱 WPF 클라이언트 (`DIGIMON TCG : DIGITAL COLOSSEUM`)
- **`src/DigimonTcg.Cli`**: 콘솔 시뮬레이션 및 규칙 검증 실행기
- **`tests/DigimonTcg.Core.Tests`**: 핵심 룰 엔진 및 덱 검증기 xUnit 단위 테스트

---

## 🚀 빌드 및 실행 방법

```powershell
# 1. 전체 솔루션 빌드
dotnet build DigimonTcg.sln

# 2. 단위 테스트 실행
dotnet test DigimonTcg.sln

# 3. WPF 데스크톱 클라이언트 실행
dotnet run --project src/DigimonTcg.Wpf

# 4. 단일 실행 파일(.exe) 배포 빌드
dotnet publish src/DigimonTcg.Wpf/DigimonTcg.Wpf.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish
```
