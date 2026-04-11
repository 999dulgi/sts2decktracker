# CLAUDE.md — sts2decktracker

Slay the Spire 2 덱 트래커 Harmony 모드. Godot 4 + C# + HarmonyLib으로 작성됨.

---

## 프로젝트 구조

```
DeckTrackerInjectionPatch.cs   # Harmony 패치 진입점, 패널 생성/위치 관리
CardListPanel/
  CardListPanel.cs             # 드로우/디스카드 파일 표시 패널 (핵심)
  TopCardPanel.cs              # "▲ NEXT" 탑 카드 표시 패널
  TopCardTracker.cs            # 맨 위 카드 등록부 (HashSet)
SettingsUI/
  ModSettings.cs               # 설정 모델 + JSON 직렬화 + UI 진입점
  ModSettingsPanelNode.cs      # 인게임 설정 UI 노드
  ArrowInputRow.cs             # 숫자 입력 위젯 (화살표 버튼 + TextEdit)
```

---

## 핵심 아키텍처

### Harmony 패치 목록 (`DeckTrackerInjectionPatch.cs`)

| 클래스 | 대상 | 방식 | 역할 |
|---|---|---|---|
| `DeckTrackerInjectionPatch` | `NCombatUi._Ready` | Postfix | 전투 시작 시 패널 생성 |
| `DeckTrackerCombatWonPatch` | `NCombatUi.OnCombatWon` | Postfix | 전투 승리 시 패널 숨김 |
| `CardPileTopTrackPatch` | `CardPileCmd.Add` | Prefix | Top 위치 카드 등록 |
| `NGameReturnToMenuPatch` | `NGame.ReturnToMainMenu` | Prefix | 메인 메뉴 복귀 처리 |
| `ModdingScreenSettingsPatch` | `NModInfoContainer.Fill` | Postfix | 설정 UI 주입 |

### 위치 저장 흐름

```
전투 중 드래그 → _hasCustomPosition = true, _customPosition 업데이트
전투 종료 (_ExitTree) → _isReturningToMainMenu가 false면 SaveCustomPosition(static field)
다음 전투 시작 (Postfix) → _savedDrawCustomPos/DiscardCustomPos로 SetCustomPosition 복원

메인 메뉴 복귀 (OnReturnToMainMenu):
  1. _isReturningToMainMenu = true
  2. RememberCustomPosition이 true면 패널에서 직접 읽어 settings.DrawPileX/Y에 저장
  3. static 임시 위치 필드 null로 초기화
  4. 이후 _ExitTree: _isReturningToMainMenu = true이므로 저장 스킵
```

### CardListPanel 중요 구조

- `Panel` → `VBoxContainer(mainContainer)` → `MarginContainer` → `VBoxContainer(innerContainer)` → `ScrollContainer` → `VBoxContainer(_cardList)`
- `_resetBtn`은 Panel의 직접 자식, 앵커로 우측 상단에 오버레이
- 클릭은 `SetMouseIgnoreRecursive`로 전체 무시, 드래그/리셋 버튼만 수동 복원
- 스크롤은 `_UnhandledInput`에서 수동 처리 (MouseFilter=Ignore이므로 클릭 통과됨)

---

## 설정 (`ModSettings`)

**설정 파일 위치:** `%APPDATA%\SlaytheSpire2\DeckTracker.config.json`

| 필드 | 타입 | 기본값 | 설명 |
|---|---|---|---|
| `drawPileX/Y` | int | 0, 140 | 드로우 파일 기본 위치 |
| `discardPileX/Y` | int | 1670, 140 | 디스카드 파일 기본 위치 |
| `cardSize` | int | 24 | 카드/텍스트 기본 크기 |
| `idleOpacity` | float | 0.3 | 유휴 투명도 |
| `activeOpacity` | float | 1.0 | 활성 투명도 |
| `idleDelaySeconds` | float | 1.0 | 유휴 전환 대기 시간 |
| `draggable` | bool | false | 패널 드래그 가능 |
| `showCardTooltip` | bool | false | 카드 툴팁 표시 |
| `rememberCustomPosition` | bool | false | 게임 종료 후 위치 유지 |
| `cardColorMode` | int | 2 | 카드 이름 색상 모드 (0=없음, 1=강화/인챈트만, 2=희귀도 포함) |
| `scrollable` | bool | false | 스크롤 활성화 |
| `scrollableAutoHeight` | bool | true | 자동 높이 (750 - Y) |
| `scrollableHeight` | int | 400 | 수동 스크롤 높이 |

`CardColorMode` enum은 JSON 직렬화를 위해 `CardColorModeInt`(int)로 저장하고, `CardColorMode` 프로퍼티는 `[JsonIgnore]`.

---

## CardPileTopTrackPatch 동작 원리

게임이 카드를 드로우 파일 **맨 위(Top)**에 추가하는 `CardPileCmd.Add` 명령을 가로채서 해당 카드를 `TopCardTracker`에 등록. `TopCardPanel`이 매 프레임 파일을 순회해 등록된 카드를 찾아 "▲ NEXT"로 표시. 카드가 드로우 파일에서 사라지면 `PruneCards`로 등록 해제.

---

## 주요 Godot 패턴

- **클릭 통과**: `MouseFilter = Ignore` + `_UnhandledInput`에서 휠만 수동 처리
- **ScrollContainer 스크롤바 숨기기**: `VerticalScrollMode = ShowNever` 대신 `GetVScrollBar().Modulate = Colors.Transparent` (CallDeferred로 호출)
- **레이아웃 붕괴 방지**: ScrollContainer 부모들에 `SizeFlagsVertical = ExpandFill` 필수
- **오버레이 버튼**: `AnchorLeft/Right = 1f`, `OffsetLeft/Right`로 패널 우측 밖에 배치
- **노드 해제 전 처리**: `_ExitTree` 오버라이드 사용 (`_Notification`보다 안정적)

---

## 알려진 주의사항

- `SetMouseIgnoreRecursive` 호출 후 `_scrollContainer.MouseFilter = Ignore`와 `_resetBtn.MouseFilter = Stop`을 명시적으로 재설정해야 함
- `ArrowInputRow`에서 `_updatingText` 플래그 없이 텍스트를 수정하면 무한 루프 발생
- `OnReturnToMainMenu`는 Prefix(노드 해제 전)이므로 패널에서 직접 위치를 읽어야 함; static 임시 필드는 이미 스탈할 수 있음
