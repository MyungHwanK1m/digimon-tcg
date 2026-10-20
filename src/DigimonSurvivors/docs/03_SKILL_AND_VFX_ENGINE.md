# 03. 스킬 및 절차적 VFX 엔진 (Skills & Procedural VFX Engine)

## 1. 개편 배경 (그림판 스타일 탈피)
* **초기 문제점**:
  * 초기 스킬 드로잉 방식은 WPF `DrawingContext`에서 다각형(Polygon)과 펜(Pen) 외곽선을 직접 사용하여, 파란 물고기 실루엣에 흰 눈동자 점을 찍거나, 직사각형과 삼각형으로 미사일을 그리는 등 **그림판(MS Paint)에서 낙서한 듯한 유치한 인상**을 주었습니다.
* **혁신 방향**:
  * 인위적인 외곽선 펜(Solid Pen)을 전면 제거.
  * **다층 방사형 소프트 블룸 (Bloom & Glow)** 구조 도입:
    * `Layer 1`: 대기 굴절 및 환경 후광 (Ambient Low-Alpha Spread)
    * `Layer 2`: 원소 본체 그라데이션 및 유체역학 리본 (Dynamic Energy Ribbons)
    * `Layer 3`: 백열광 초고열 코어 (Incandescent White-Hot Nucleus)
    * `Layer 4`: 렌즈 플레어 및 십자 성흔 (Star Glint & Flare)

---

## 2. 11종 핵심 스킬 투사체 렌더러 명세 ([`GameCanvas.cs`](file:///D:/Project/DigimonSurvivors/Rendering/GameCanvas.cs))

### 1) 화염 혜성 (DrawFlameProjectile)
* **적용 스킬**: `baby_flame` (아구몬), `mega_flame` (그레이몬), `spit_fire`
* **시각 연출**:
  * 광역 화염 대기 왜곡 후광 (주황/적색 방사형 블룸)
  * 다층 유선형 플라즈마 혜성 꼬리 (외곽 홍염 + 내부 황금빛 불길 혀의 베지어 곡선)
  * 전면 초신성 백색 발광 핵 (White-Hot Flare)
  * 비행 궤적에 흩날리는 백열 불티 파티클 방출

### 2) 초음속 공기팡 (DrawAirShockwaveProjectile)
* **적용 스킬**: `air_shot` (파닥몬), `sparking_air`, `spiral_twister`
* **시각 연출**:
  * 대기 굴절 포톤 오라 (Soft Refraction Atmospheric Bloom)
  * 후방으로 뻗어 나가는 3중 초음속 마하 콘 압축 기류 리본 (Mach Ribbons)
  * 고압 진공 렌즈 핵 (High-Pressure Atmospheric Sphere)
  * 전면 음속 돌파 십자 성흔 섬광 (Sonic Breaking Star Glint)

### 3) 수룡 파도와 빛의 수령 (DrawWaterWaveProjectile)
* **적용 스킬**: `surfing_fish` (고마몬 - 물고기 행진), `water_breath`, `bubble_blow`
* **시각 연출**:
  * 심해 아쿠아 발광 후광 (Aquamarine Hydro Aura)
  * 출렁이는 해일 파도 크레스트 (Surging Tidal Torrents)
  * **2기의 빛나는 청록 수령(Water Spirits)**: 유선형 빛의 정령 몸체가 파도 속을 교차 유영하는 신비로운 물의 마법 연출 (만화 물고기 완전 배제)
  * 비산하는 투명 물방울 거품 파티클

### 4) 실시간 프랙탈 벼락 (DrawLightningProjectile)
* **적용 스킬**: `petit_thunder` (텐토몬), `hammer_spark`, `mega_blaster`
* **시각 연출**:
  * 고전압 청록 플라즈마 후광 오라
  * **2단계(Pass) 지터 벼락**: 외곽 청록 발광 코로나(5.5px) + 내부 순백 방전 심선(1.8px)이 매 프레임 무작위로 꺾이며 사방으로 갈라지는 지터 아크
  * 중심부 초고열 볼 라이트닝 구체

### 5) 공허 크리스탈 단검 (DrawDarkBatProjectile)
* **적용 스킬**: `pico_dart` (피코데블몬), `night_raid`, `darkness_wave`
* **시각 연출**:
  * 칠흑의 공허 저주 미아즈마 성운 후광
  * 120도 대칭으로 고속 회전하는 3개의 마젠타/보라빛 공허 크리스탈 단검(Void Needles)
  * 심연의 붉은 눈동자 코어와 저주받은 보라색 연기 궤적

### 6) 비취 룬 혜성 (DrawEarthBluntProjectile)
* **적용 스킬**: `goburi_strike` (고블몬), `woody_smash`, `leaf_cutter`
* **시각 연출**:
  * 에메랄드/비취빛 원소 블룸
  * 황금빛 룬 크랙이 발광하는 다면체 비취 혜성 핵
  * 주위를 공전하는 3개의 회전 비취 칼날 잎사귀 파편

### 7) 초승달 플라즈마 참격 (DrawCrescentBladeProjectile)
* **적용 스킬**: `shadow_wing` (피요몬), `kaiser_nail` (워가루몬), `grey_sword` (오메가몬)
* **시각 연출**:
  * 진홍/황금 플라즈마 후광
  * 순백의 초고열 절단 엣지(White-Hot Cutting Edge)와 잔상 블러가 살아있는 곡선형 참격파
  * 칼날 첨두 4각 십자 글린트 섬광

### 8) 프리즘 빙결 창 (DrawIceCrystalProjectile)
* **적용 스킬**: `ice_arrow`, `sorrow_blue`, `cocytus_breath` (메탈가루몬)
* **시각 연출**:
  * 극지방 빙결 오로라 후광
  * 상부(순백)와 하부(사파이어)로 나뉜 다면체 프리즘 얼음 창과 중심 융기선
  * 첨두 4각 다이아몬드 섬광 및 서리 파티클

### 9) 태양광 신성 광선창 (DrawHolySpearProjectile)
* **적용 스킬**: `holy_arrow`, `heavens_knuckle` (엔젤몬), `strike_of_the_seven_stars`
* **시각 연출**:
  * 천상의 성스러운 황금 후광
  * 눈부신 태양광의 신성 광선창과 황금빛 크로스가드 링
  * 첨두에서 회전하는 천상의 8각 렌즈 플레어(Holy Cross Star)

### 10) 유선형 크루즈 미사일 (DrawMissileProjectile)
* **적용 스킬**: `giga_destroyer` (메탈그레이몬), `hyper_cannon`, `infinity_cannon`
* **시각 연출**:
  * 유선형 티타늄 복합 탄체와 전술 센서 네온 슬릿
  * 후방 추진구에서 뿜어져 나오는 초음속 다이아몬드 쇼크 제트 플레어
  * 후방 비행 안정 날개 및 연막 화염 궤적

### 11) 회전 영혼 도깨비불 (DrawOrbitingOrb)
* **적용 스킬**: `petit_fire` (파피몬), `fox_fire`
* **시각 연출**:
  * 플레이어 공전 궤도를 따라 부드럽게 감기는 푸른 성운 리본
  * 펄스 진동하는 영혼 화염 코어와 내부 백열핵

---

## 3. 고유 지속 장판 (AreaEffect) 연출
* **BlizzardZone (소로우 블루)**: 회전하는 6각 눈꽃 마법 격자와 첨두 다이아몬드 글린트, 빙결 냉기 안개
* **GaiaBurst (가이아 포스)**: 맥동하는 태양 플레어 링과 초신성 플라즈마 대폭발 오라
* **SlashStorm (참격 폭풍)**: 다각도로 교차 폭발하는 4중 참격 광선 리본
* **Lightning (낙뢰 방전)**: 지면에 방전되는 청록 발광 코로나와 백색 심선의 전자기 그리드
