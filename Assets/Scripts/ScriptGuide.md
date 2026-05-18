# RealHoldem Script Guide

이 문서는 `Assets/Scripts` 아래 주요 C# 스크립트가 어떤 책임을 갖고, 내부에 어떤 기능이 있는지 빠르게 파악하기 위한 안내서입니다.

## PokerDemo: 게임 규칙과 상태

### `Card.cs`
- `Suit`, `Rank`, `Card`를 정의합니다.
- 카드의 문양/랭크, 족보 계산용 랭크 값, 카드 스프라이트 리소스 경로, UI 표시용 짧은 이름을 제공합니다.

### `Deck.cs`
- 52장 표준 덱을 생성, 초기화, 셔플, 드로우합니다.
- `Draw(int count)`로 여러 장을 한 번에 뽑을 수 있습니다.

### `PlayerState.cs`
- 한 플레이어의 런타임 상태를 보관합니다.
- 닉네임, 좌석 번호, 사람/봇 여부, 칩, 현재 베팅액, 폴드/올인 상태, 마지막 액션, 홀카드를 관리합니다.

### `PokerSeat.cs`
- 테이블의 한 좌석을 표현합니다.
- 플레이어 착석/퇴장 시 `PlayerState.SeatIndex`를 함께 동기화합니다.

### `PokerTableState.cs`
- 전체 좌석 목록을 관리합니다.
- 빈 좌석 찾기, 착석 플레이어 순회, 마지막 봇 제거 같은 테이블 단위 기능을 담당합니다.

### `BettingManager.cs`
- 베팅 라운드의 칩 흐름을 관리합니다.
- 블라인드 지불, 콜/체크/폴드/베팅/레이즈 적용, 팟 지급/분배를 처리합니다.
- 플레이어가 칩을 냈을 때 `PlayerPaidChips` 이벤트를 발생시켜 칩 애니메이션과 연결됩니다.

### `PokerHandEvaluator.cs`
- 5~7장의 카드 중 가장 강한 5장 조합을 평가합니다.
- 족보 등급과 타이브레이커를 함께 비교해서 승패를 판단합니다.

### `SimplePokerAI.cs`
- 봇의 액션을 결정하는 간단한 AI입니다.
- 현재 패 강도, 콜 금액, 블러프 확률, 레이즈 제한을 참고해 fold/check/call/bet/raise를 고릅니다.

## PokerDemo: 게임 진행

### `PokerGameManager.cs`
- 포커 데모의 중앙 관리자입니다.
- 좌석 편집, 새 핸드 시작, 블라인드, 홀카드/커뮤니티 카드 딜, 각 스트리트 베팅, 타이머, 봇 턴, 쇼다운, 팟 지급, UI 갱신을 조율합니다.
- 핵심 흐름은 `StartHandRoutine -> DealHoleCardsRoutine -> BeginBetting -> ContinueBettingFrom -> AdvanceAfterBettingRound -> ShowdownRoutine`입니다.

## PokerDemo: UI와 시각화

### `PokerUIManager.cs`
- 런타임 UI 전체를 렌더링합니다.
- 좌석 패널, 팟/상태 텍스트, 결과 텍스트, 베팅 슬라이더, 액션 버튼, 봇 추가/삭제 버튼, 액션 이미지, 캐릭터 이미지, 2D/3D 카드 전환을 관리합니다.

### `CardView.cs`
- 2D UI 카드 한 장을 표시합니다.
- 앞면/뒷면/빈 슬롯 표현과 딜 애니메이션을 제공합니다.

### `Poker3DCardView.cs`
- 3D 카드 한 장의 메시, 재질, 카드 앞/뒷면, 딜 이동, 코너 플립 애니메이션을 처리합니다.
- 스프라이트가 없으면 런타임 생성 백 텍스처나 단색 재질로 대체합니다.

### `Poker3DCardTableView.cs`
- 좌석 카드 12장과 커뮤니티 카드 5장을 3D로 배치하고 렌더링합니다.
- UI 카드 앵커 위치를 월드 좌표로 변환해 2D UI 배치와 3D 카드 배치를 맞춥니다.

### `PokerBetChipAnimator.cs`
- 베팅 칩 UI 애니메이션을 관리합니다.
- 칩 지불 이벤트를 받아 좌석 앞 칩 스택을 만들고, 베팅 라운드 종료 시 팟으로 모으거나 승자에게 지급합니다.

### `ChipStackView.cs`
- 특정 금액을 칩 단위로 분해해 시각적인 칩 스택으로 만듭니다.
- UI `Image` 기반 칩과 월드 `SpriteRenderer` 기반 칩을 모두 생성할 수 있습니다.

## Card Demo

### `CardFlipDemo.cs`
- 리버 카드가 들렸다가 휘어지며 뒤집히는 3D 카드 플립 데모입니다.
- 세그먼트 메시를 직접 만들고 DOTween으로 피킹, 스냅, 정착 단계 애니메이션을 재생합니다.

### `CardOpenPeekingDemo.cs`
- 카드 한 장의 아래쪽을 드래그해 살짝 들춰보는 데모입니다.
- 세그먼트 메시를 곡면으로 변형해 카드가 종이처럼 말리는 느낌을 만듭니다.

### `CardOpenHoldCardPairDemo.cs`
- 홀카드 두 장을 한 번에 들어 올려 확인하는 데모 컨트롤러입니다.
- 두 `CardOpenPeekingDemo` 자식의 위치, 회전, 말림 정도를 같은 드래그 입력으로 조절합니다.

## Editor Installers

### `PokerDemoSceneInstaller.cs`
- `SampleScene`에 포커 데모 루트 오브젝트와 핵심 컴포넌트를 자동 설치합니다.
- `EventSystem` 생성과 누락 스크립트 정리도 수행합니다.

### `Poker3DCardSceneInstaller.cs`
- `SampleScene`에 3D 카드 루트, 좌석 카드, 커뮤니티 카드 오브젝트를 만들고 `PokerUIManager`와 연결합니다.
- 기존 레거시 3D 카드 오브젝트가 있으면 정리합니다.
