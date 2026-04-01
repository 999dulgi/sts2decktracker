# Slay the Spire 2 Deck Tracker

A mod that displays your draw pile and discard pile during combat.

## Screenshot

![Deck Tracker in Action](deck_tracker_example.webp)

## Installation

1. Download the latest release from the [releases page](https://github.com/999dulgi/sts2decktracker/releases)
2. Extract the zip file
3. Copy the `sts2decktracker.dll` and `sts2decktracker.pck` file to your STS2 mods folder (usually `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\`). If the mods folder doesn't exist, create it.
4. Launch Slay the Spire 2
5. When you first install the mod and enter the game, a mod activation message will appear. Activate it to enable the mod.

## Configuration

You can configure the mod in-game or manually edit the JSON file:

**Location:** `%APPDATA%\SlaytheSpire2\DeckTracker.config.json`

The config file will be created automatically on first run with default values.

### Available Settings

```json
{
  "drawPileX": 0,
  "drawPileY": 140,
  "discardPileX": 1670,
  "discardPileY": 140,
  "cardSize": 24,
  "idleOpacity": 0.3,
  "activeOpacity": 1.0,
  "idleDelaySeconds": 1.0,
  "draggable": false,
  "showCardTooltip": false,
  "rememberCustomPosition": false,
  "cardColorMode": 2,
  "scrollable": false,
  "scrollableAutoHeight": true,
  "scrollableHeight": 400
}
```

### Settings Explanation

- **drawPileX/drawPileY**: Position of the draw pile panel (X=0 is left edge, Y=0 is top)
- **discardPileX/discardPileY**: Position of the discard pile panel
- **cardSize**: Base size for cards and all text in the tracker panels
- **idleOpacity/activeOpacity**: Opacity when cards haven't changed / when they just changed (0.0 = fully transparent, 1.0 = fully opaque)
- **idleDelaySeconds**: Seconds before the panel fades back to idle opacity after a card change
- **draggable**: Allow dragging the panel to reposition it. A small ↺ reset button appears on hover to return it to the default position.
- **showCardTooltip**: Show card info when hovering over a card image
- **rememberCustomPosition**: Save the dragged position when exiting the game (requires `draggable: true`)
- **cardColorMode**: Card name color scheme — `0` = no color (all plain), `1` = upgraded/enchanted only, `2` = full rarity colors
- **scrollable**: Enable scrolling when the card list exceeds the panel height
- **scrollableAutoHeight**: Automatically set scroll height based on panel Y position (`height = 750 - Y`). When `false`, uses `scrollableHeight`.
- **scrollableHeight**: Fixed scroll height in pixels (used when `scrollableAutoHeight` is `false`)

### Cautions

- If you delete the config file, the mod will create a new one with default values on next run.
- When you enable the mod for the first time, your save file will split into two saves. If you want to prevent this, use this mod: https://www.nexusmods.com/slaythespire2/mods/6

<br>
<br>
<br>

# 슬더스2 덱트레커

Slay the Spire 2 덱 트레커 모드입니다.

## 스크린샷

![Deck Tracker in Action](deck_tracker_example.webp)

## 설치 방법

1. [여기](https://github.com/999dulgi/sts2decktracker/releases)에서 최신 릴리스를 다운로드합니다.
2. zip 파일을 압축 해제합니다.
3. `sts2decktracker.dll`과 `sts2decktracker.pck` 파일을 STS2 mods 폴더에 복사합니다. (보통 `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\`입니다. mods 폴더가 없으면 생성하세요.)
4. Slay the Spire 2를 실행합니다.
5. 모드를 처음 설치하고 게임에 들어가면 모드 활성화 메시지가 나타납니다. 활성화하면 모드가 작동합니다.

## 설정 방법

설정은 JSON 파일에 저장되며 인게임 또는 수동으로 편집할 수 있습니다:

**위치:** `%APPDATA%\SlaytheSpire2\DeckTracker.config.json`

설정 파일은 첫 실행 시 기본값으로 자동 생성됩니다.

### 사용 가능한 설정

```json
{
  "drawPileX": 0,
  "drawPileY": 140,
  "discardPileX": 1670,
  "discardPileY": 140,
  "cardSize": 24,
  "idleOpacity": 0.3,
  "activeOpacity": 1.0,
  "idleDelaySeconds": 1.0,
  "draggable": false,
  "showCardTooltip": false,
  "rememberCustomPosition": false,
  "cardColorMode": 2,
  "scrollable": false,
  "scrollableAutoHeight": true,
  "scrollableHeight": 400
}
```

### 설정 설명

- **drawPileX/drawPileY**: 드로우 파일 패널의 위치 (X=0은 왼쪽 끝, Y=0은 상단)
- **discardPileX/discardPileY**: 디스카드 파일 패널의 위치
- **cardSize**: 덱 트래커 패널의 카드 및 텍스트 기본 크기
- **idleOpacity/activeOpacity**: 카드가 변경되지 않았을 때 / 변경되었을 때의 투명도 (0.0 = 완전 투명, 1.0 = 완전 불투명)
- **idleDelaySeconds**: 카드 변경 후 idle 투명도로 돌아오기까지의 대기 시간 (초)
- **draggable**: 패널을 드래그해서 위치를 변경할 수 있게 합니다. 패널에 마우스를 올리면 ↺ 버튼이 표시되어 기본 위치로 초기화할 수 있습니다.
- **showCardTooltip**: 카드 이미지에 마우스를 올리면 카드 정보를 표시합니다.
- **rememberCustomPosition**: 게임 종료 시 드래그한 위치를 저장합니다. (`draggable: true` 필요)
- **cardColorMode**: 카드 이름 색상 모드 — `0` = 색상 없음 (모두 기본색), `1` = 강화/인챈트만 색상 표시, `2` = 희귀도 포함 전체 색상
- **scrollable**: 카드 목록이 패널 높이를 초과할 때 스크롤 가능하게 합니다.
- **scrollableAutoHeight**: 패널 Y 위치에 따라 스크롤 높이를 자동으로 설정합니다 (`height = 750 - Y`). `false`이면 `scrollableHeight`를 사용합니다.
- **scrollableHeight**: 고정 스크롤 높이 (픽셀 단위, `scrollableAutoHeight`가 `false`일 때 사용)

### 주의사항

- 설정 파일을 삭제하면 다음 실행 시 기본값으로 새 파일이 생성됩니다.
- 모드를 처음 활성화하면 세이브 파일이 두 개로 분할됩니다. 이를 방지하려면 이 모드를 사용하세요: https://www.nexusmods.com/slaythespire2/mods/6

## References

- https://github.com/jidon333/STS2_Superfast_Mod/tree/main/docs
  I referenced the information in this repository to get the basic information.

## How to Build

You can build this mod on dotnet or Godot 4.5.1

### Make DLL

1. Open the terminal and goto project directory
2. Enter `dotnet build`
3. DLL file will be in sts2 mod folder. Or copy the generated DLL in the `.godot/mono/temp/bin/Debug/sts2decktracker.dll`
