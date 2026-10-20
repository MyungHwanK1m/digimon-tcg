# 내장 카드 생성 도구

`StarterDecks.GetAllCatalogCards()`의 120장은 아래 두 자료를 합쳐 생성했다.

- `ko.tsv` — 공식 한글판 카드 이미지에서 옮겨 적은 이름 · 메인 효과 · 진화원 효과 (`|` = 줄바꿈)
- DigimonCard.io API 응답 JSON (세트별 `real_ST1.json` 등) — 레벨 · 코스트 · DP · 진화 조건 · 등급

```powershell
# API JSON 받기 (분당 15회 제한 — 세트마다 몇 초 쉬기)
Invoke-WebRequest "https://digimoncard.io/api-public/search?card=ST1-&series=Digimon%20Card%20Game" -OutFile api\real_ST1.json

# C# 카드 정의 생성 → StarterDecks.cs 의 cards 목록에 붙여 넣기
.\gen_cs.ps1 -ApiDir api -KoTsv ko.tsv -Out cards.cs.txt
```

- `dump_real.ps1` — 번호 목록의 실제 카드 데이터를 표로 출력 (검수용)
- `ocr.ps1` — Windows 한국어 OCR로 카드 이미지 글자 읽기 (오타가 섞이므로 검수 필요)
- 키워드(블로커 · 시큐리티 어택)와 조건 없는 진화원 DP 보너스는 영어 효과 문장의 **맨 앞**에 있을 때만 자동 설정한다.
