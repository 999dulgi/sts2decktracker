# Slay the Spire 2 Deck Tracker

A mod that displays decktracker your draw pile and discard pile during combat.

## ScreenShot

![Deck Tracker in Action](deck_tracker_example.webp)

## Installation

1. Download the latest release from the [releases page](https://github.com/999dulgi/sts2decktracker/releases)
2. Extract the zip file
3. Copy the `sts2decktracker.dll`and `sts2decktracker.pck` file to your STS2 mods folder (usually `C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods\`) if the mods folder doesn't exist, create mods folder
4. Launch Slay the Spire 2
5. When you first install the mod and enter the game, a mod activation message will appear; if you activate it, the mod will be enabled

## Configuration

You can configure the mod in-game or manually edit the JSON file:

**Location:** `[Game Install Directory]\mods\DeckTracker.config.json`

The config file will be created automatically on first run with default values.

### Available Settings

```json
{
  "drawPileX": 0,
  "drawPileY": 140,
  "discardPileX": -250,
  "discardPileY": 140,
  "cardSize": 24,
  "idleOpacity": 0.3,
  "activeOpacity": 1.0,
  "idleDelaySeconds": 5.0
}
```

### Settings Explanation

- **drawPileX/drawPileY**: Position of the draw pile panel (X=0 is left edge, Y=140 is below top)
- **discardPileX/discardPileY**: Position of the discard pile panel (negative X positions from right edge, position is based on basic card size)
- **cardSize**: Size of each card in the deck tracker panels and all text sizes
- **idleOpacity/activeOpacity**: Opacity of cards when they haven't changed and when they have changed (0.0 = fully transparent, 1.0 = fully opaque)
- **idleDelaySeconds**: Delay in seconds before opacity changes after a card has been modified

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

**위치:** `[게임 설치 디렉토리]\mods\DeckTracker.config.json`

설정 파일은 첫 실행 시 자동으로 생성되며 기본값으로 생성됩니다.

### 사용 가능한 설정

```json
{
  "drawPileX": 0,
  "drawPileY": 140,
  "discardPileX": -250,
  "discardPileY": 140,
  "cardSize": 24,
  "idleOpacity": 0.3,
  "activeOpacity": 1.0,
  "idleDelaySeconds": 5.0
}
```

### 설정 설명

- **drawPileX/drawPileY**: 드로우 피일 패널의 위치 (X=0은 왼쪽 가장자리, Y=140은 상단 아래)
- **discardPileX/discardPileY**: 디스카드 피일 패널의 위치 (음수 X 위치는 오른쪽 가장자리에서부터, 위치는 기본 카드 크기를 기준으로 합니다)
- **cardSize**: 덱 트래커 패널의 각 카드 크기와 모든 텍스트 크기
- **idleOpacity/activeOpacity**: 카드가 변경되지 않았을 때와 변경되었을 때의 투명도 (0.0 = 완전 투명, 1.0 = 완전 불투명)
- **idleDelaySeconds**: 카드가 변경된 후 투명도가 변경되기까지의 대기 시간 (초)

### 주의사항

- 설정 파일을 삭제하면 다음 실행 시 기본값으로 새 파일이 생성됩니다.
- 모드를 처음 활성화하면 세이브 파일이 두 개로 분할됩니다. 이를 방지하려면 이 모드를 사용하세요: https://www.nexusmods.com/slaythespire2/mods/6

## References

- https://github.com/jidon333/STS2_Superfast_Mod/tree/main/docs
  I referenced the information in this repository to get the basic information.

## How to Build

You can build this mod on Godot 4.5.1

### Make DLL

1. Open the project in Godot 4.5.1
2. Go to Project -> Export
3. Export the project
4. Copy the generated DLL in the .godot/mono/temp/bin/Debug/sts2decktracker.dll

### Make PCK

in sts2 beta version, you don't need to make pck file. you can just copy the sts2decktracker_manifest.json and the dll file to the mods folder.

1. Open the project in Godot 4.5.1
2. Go to Project -> Export
3. Select Resource - Export selected resources (and dependencies) and select mod_manifest.json and Export PCK/ZIP
4. Copy the generated PCK in the project folder
