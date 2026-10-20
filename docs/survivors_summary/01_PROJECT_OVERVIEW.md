# 01. 프로젝트 개요 및 아키텍처 (Project Overview & Architecture)

## 1. 프로젝트 개요
* **프로젝트명**: Digimon Survivors (디지몬 서바이버즈)
* **장르**: 2D 탑다운 로그라이크 액션 서바이벌 (Vampire Survivors 스타일)
* **플랫폼 / 프레임워크**: Windows Desktop (.NET 8.0, WPF, Direct2D 가속 DrawingVisual)
* **목적**: 디지몬 카드의 속성(Vaccine, Data, Virus)과 진화 계통도를 반영하여, 사방에서 몰려오는 적 디지몬을 퇴치하고 생존하며 진화하는 실시간 액션 게임.

---

## 2. 기술 스택 및 렌더링 파이프라인
* **언어 및 런타임**: C# 12, .NET 8.0 Windows Desktop
* **렌더링 파이프라인**: 
  * WPF의 `FrameworkElement.OnRender` 기반 가속 드로잉
  * `CompositionTarget.Rendering`을 통한 60 FPS 고정 델타 타임 루프
  * UI 오버레이와 인게임 월드 렌더링의 완벽한 분리 (Camera Transform Matrix)
* **디지털 월드 시각 연출**:
  * 네온 사이버 회로 그리드 바닥 및 펄스 파동
  * 절차적 발광(Radial Bloom) 및 전자기 아크 플라즈마 연출
* **데이터 관리**:
  * `DigimonTcg.Core` 모듈과의 연동을 통한 카드 ID 및 일러스트 자원 공유

---

## 3. 디렉터리 구조

```
D:\Project\DigimonSurvivors\
├── Assets\
│   ├── Cards\               # 공식 TCG 카드 이미지
│   ├── Environment\         # 디지털 월드 바닥 타일 및 환경 텍스처
│   ├── Skills\              # 44종 고유 스킬 아이콘 및 고해상도 VFX 스프라이트
│   ├── Sprites\             # 24종 디지몬 캐릭터 스탠디 스프라이트
│   └── UI\                  # 속성 뱃지(VAC/DATA/VIRUS) 및 프레임
├── Audio\                   # 인게임 BGM 및 사운드 시스템
├── Data\
│   └── GameDatabase.cs      # 디지몬 프로필, 진화 트리, 44종 스킬 및 TCG 카드 데이터
├── Engine\
│   ├── GameEngine.cs        # 메인 게임 루프, 스폰, 충돌 판정, 레벨업 및 보스 타이머
│   ├── ParticleSystem.cs    # 원소별 발광 파티클 및 궤적 시스템
│   └── SoundSystem.cs       # 사운드 이펙트 합성 및 재생기
├── Entities\
│   ├── AreaEffectEntity.cs  # 지속 장판 스킬 엔티티
│   ├── EnemyEntity.cs       # 적 몬스터 및 보스 엔티티
│   ├── ExpOrbEntity.cs      # 경험치 비트 오브
│   ├── MonolithEntity.cs    # 필드 파괴 가능 오브젝트 (모놀리스)
│   ├── PlayerEntity.cs      # 플레이어 캐릭터 엔티티
│   └── ProjectileEntity.cs  # 스킬 투사체 엔티티
├── Models\
│   ├── DigimonProfile.cs    # 디지몬 스탯, 속성, 진화 루트 모델
│   └── SkillDefinition.cs   # 스킬 데미지, 쿨다운, 투사체 속성 모델
├── Rendering\
│   ├── AssetManager.cs      # 스프라이트/카드/스킬 캐싱 및 투명화 로더
│   └── GameCanvas.cs        # 60 FPS 전용 드로잉 캔버스 (VFX & HUD)
├── Skills\
│   └── WeaponSystem.cs      # 자동 공격 조준, 쿨다운 관리 및 투사체 발사 시스템
├── Systems\
│   └── CardUpgradeManager.cs# 레벨업 및 모놀리스 파괴 시 3택 카드 업그레이드 매니저
├── Tools\
│   ├── DigimonTokenGenerator.cs # 스탠디 캐릭터 스프라이트 생성기
│   ├── NukkiProcessor.cs    # 투명 배경 알파 추출기
│   └── SkillEffectGenerator.cs # 스킬 VFX 에셋 생성기
├── docs\                    # 개발 종합 문서 폴더
├── publish\                 # 최종 배포 실행 파일
├── App.xaml / App.xaml.cs
└── MainWindow.xaml / .cs    # 메인 윈도우 UI 및 입력 이벤트 핸들러
```
