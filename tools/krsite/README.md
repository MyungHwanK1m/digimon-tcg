# 한국 공식 사이트 수집기 (개인 로컬 사용 전용)

[digimoncard.co.kr](https://digimoncard.co.kr) 카드 리스트와 자료실에서 공식 한글 카드 데이터 · 이미지 · 룰 자료를 받아 온다.
사이트에 "무단 사용 및 전재 금지"가 적혀 있으므로 **받은 파일은 이 PC에만 두고 git에 넣지 않는다** (`.gitignore`: `data/kr/`, `data/images_kr/`).

```powershell
.\collect_kr.ps1             # 카드 데이터 → data/kr/sets/{세트}.json + data/kr/cards_ko.json
.\collect_kr.ps1 -Images     # + 카드 이미지 → data/images_kr/{카드번호}.png (기존 data/images_ko · data/tmp는 건드리지 않음)
.\collect_kr.ps1 -Docs       # + 자료실(룰북 · 금지/제한 · Q&A 등) → data/kr/docs/
.\collect_kr.ps1 -Only 153   # 지정한 세트 카테고리만
.\collect_kr.ps1 -Refresh    # 이미 받은 세트도 다시 받기
.\report_kr.ps1              # 영어 캐시와 카드 번호 대조 → data/kr/report.md
.\collect_qna.ps1            # 카드 Q&A (세트별) → data/kr/qna/{세트}.json + qna_all.json
```

- 카드 Q&A는 qna 페이지의 세트 선택(`?mid=qna&category={세트}&page={N}`, 한 페이지 5건)으로 받는다.
- Q&A는 `tests/DigimonTcg.Core.Tests/OfficialQnaTests*.cs` 시나리오 테스트의 근거로만 쓴다 — **Q&A 문장은 코드에 옮기지 않고** 카드 번호 · Q 번호 · 기대 결과 · 직접 쓴 짧은 요약만 남긴다.

- 요청은 한 번에 하나씩, 요청 사이 1.5초(`-DelayMs`)를 쉰다. 실패하면 10 · 20 · 30초 뒤 다시 시도한다.
- 세트마다 파일 하나로 저장하므로 중간에 멈춰도 다시 실행하면 받은 세트는 건너뛴다.
- 같은 번호의 패러렐 카드는 기본판 하나로 합치고, 패러렐 이미지 주소는 `ParallelImages`에 남긴다.

## 앱에서 쓰는 방법

- `data/kr/cards_ko.json`이 있으면 `KoreanCardData`가 읽어서 `CardData.DisplayName` · `DisplayEffectText` · `DisplayInheritedEffectText` · `DisplayTraits`가 공식 한글을 돌려준다. 없으면 원래 값.
- **대전 엔진 · 문장 파서는 영어 API 데이터(`Name` · `EffectText` · `Traits`) 그대로** 판정한다. 한글 데이터는 화면 표시 전용.
- 이미지는 `data/images_ko` → `data/tmp` → `data/images_kr` 순서로 찾는다.
