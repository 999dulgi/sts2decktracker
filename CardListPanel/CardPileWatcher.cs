using Godot;
using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace sts2decktracker
{
    // Draw/Discard 파일 하나를 지켜보며 "지금 어떤 카드가 몇 장 있는지"를 계속 최신으로 유지하는
    // 감지 전담 클래스. Godot 노드 트리나 행(row) UI에는 관여하지 않는다. CardListPanel이 이 클래스를
    // 소유하고, 아래 콜백들을 구독해서 실제 UI(행 생성/갱신/삭제)를 처리한다.
    public sealed class CardPileWatcher
    {
        public PileType PileType { get; set; } = PileType.Draw;

        // 카드 표시 순서/카운트. 초기 정렬·셔플은 CardListPanel(UpdateCardList)이 담당하고,
        // 이후 증분 갱신(OnCardAdded/OnCardRemoved)은 이 클래스가 담당한다.
        public List<(CardModel card, int count)> ShuffledOrder { get; set; }
        public CardPile CurrentPile { get; private set; }

        // 파일 전체를 다시 그려야 할 때(파일 전환 또는 증분 갱신을 놓친 dirty 상태) 호출된다.
        public Action PileNeedsFullRebuild;
        // 기존 그룹의 카운트가 바뀌었을 때 (key, card, newCount). card는 그룹 대표 카드 인스턴스.
        public Action<string, CardModel, int> GroupCountChanged;
        // 새 그룹이 카운트 1로 생겼을 때 (key, card)
        public Action<string, CardModel> GroupCreated;
        // 기존 그룹의 카운트가 감소했을 때 (key, newCount)
        public Action<string, int> GroupCountDecreased;
        // 그룹이 완전히 사라졌을 때 (key)
        public Action<string> GroupRemoved;

        private readonly HashSet<CardModel> _pileCardSet = new();
        // GroupKey(card) 결과를 카드별로 캐싱한다. 카드 자체 상태 변화는 그 카드 캐시만 지우고,
        // 코스트/키워드 계산에 쓰이는 훅 자체가 바뀔 수 있는 이벤트는 캐시 전체를 지운다.
        private readonly Dictionary<CardModel, string> _groupKeyCache = new();
        private bool _pileDirty = false;
        private bool _combatStartLogged = false;
        private MegaCrit.Sts2.Core.Entities.Players.Player _currentPlayer = null;
        private MegaCrit.Sts2.Core.Entities.Creatures.Creature _subscribedCreature = null;
        private MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState _subscribedCombatState = null;

        private static readonly Dictionary<Type, bool> _powerAffectsGroupKeyCache = new();

        public void Start()
        {
            CardStateBridge.CardStateChanged += OnCardStateChanged;
        }

        public void Dispose()
        {
            CardStateBridge.CardStateChanged -= OnCardStateChanged;
            if (CurrentPile != null)
            {
                CurrentPile.CardAdded -= OnCardAdded;
                CurrentPile.CardRemoved -= OnCardRemoved;
                CurrentPile.ContentsChanged -= OnPileContentsChanged;
            }
            UnsubscribeCreaturePowerEvents();
            UnsubscribeEnergyEvents();
        }

        // 증분 경로(OnCardAdded/OnCardRemoved)가 놓친 게 있어도 여기서 파일 실제 내용과 다시 맞춘다.
        public void ResyncTrackedCards(CardPile pile)
        {
            _pileCardSet.Clear();
            if (pile == null) return;
            foreach (var c in pile.Cards)
                _pileCardSet.Add(c);
        }

        public string CachedGroupKey(CardModel card)
        {
            if (_groupKeyCache.TryGetValue(card, out var cached))
                return cached;
            string key = GroupKey(card);
            _groupKeyCache[card] = key;
            return key;
        }

        // CardListPanel._Process에서 매 프레임 호출. 전투 진행 여부/현재 플레이어/파일을 판별해
        // CurrentPile을 최신 상태로 유지하고, 파일 전환이나 증분 갱신 누락(dirty)이 있으면
        // PileNeedsFullRebuild를 호출해 CardListPanel이 다시 그리도록 알린다.
        // 반환값이 false면 이번 프레임에는 파일을 확정하지 못한 것이므로(전투 아님/플레이어 미해결 등)
        // 호출부는 그 이후의 파일 관련 로직(투명도 등)을 건너뛰어야 한다.
        public bool Tick()
        {
            if (CombatManager.Instance == null || !CombatManager.Instance.IsInProgress)
            {
                if (CurrentPile != null)
                {
                    CurrentPile.CardAdded -= OnCardAdded;
                    CurrentPile.CardRemoved -= OnCardRemoved;
                    CurrentPile.ContentsChanged -= OnPileContentsChanged;
                }
                UnsubscribeCreaturePowerEvents();
                UnsubscribeEnergyEvents();
                _pileCardSet.Clear();
                _groupKeyCache.Clear();
                CurrentPile = null;
                _currentPlayer = null;
                _combatStartLogged = false;
                return false;
            }

            if (_currentPlayer == null)
            {
                var combatState = CombatManager.Instance.DebugOnlyGetState();
                if (combatState == null)
                    return false;

                _currentPlayer = LocalContext.GetMe(combatState);
                if (_currentPlayer?.PlayerCombatState == null)
                    return false;

                SubscribeCreaturePowerEvents(_currentPlayer.Creature);
                SubscribeEnergyEvents(_currentPlayer.PlayerCombatState);
            }

            var drawPile = _currentPlayer.PlayerCombatState.DrawPile;
            if (drawPile == null)
            {
                if (!_combatStartLogged)
                {
                    GD.PrintErr("[CardPileWatcher] DrawPile is null!");
                    _combatStartLogged = true;
                }
                return false;
            }

            var pile = PileType == PileType.Draw ? drawPile : _currentPlayer.PlayerCombatState.DiscardPile;
            if (pile == null)
                return false;

            if (!_combatStartLogged)
                _combatStartLogged = true;

            if (CurrentPile != pile)
            {
                if (CurrentPile != null)
                {
                    CurrentPile.CardAdded -= OnCardAdded;
                    CurrentPile.CardRemoved -= OnCardRemoved;
                    CurrentPile.ContentsChanged -= OnPileContentsChanged;
                }

                CurrentPile = pile;
                CurrentPile.CardAdded += OnCardAdded;
                CurrentPile.CardRemoved += OnCardRemoved;
                CurrentPile.ContentsChanged += OnPileContentsChanged;

                PileNeedsFullRebuild?.Invoke();
            }
            else if (CurrentPile != null && _pileDirty)
            {
                _pileDirty = false;
                if (PileType == PileType.Draw)
                    TopCardTracker.PruneCards(CurrentPile);
                PileNeedsFullRebuild?.Invoke();
            }

            return true;
        }

        // CardStateBridge는 전역으로 발동하므로 _pileCardSet으로 걸러서, 이 파일에 속한 카드가
        // 바뀔 때만 dirty를 세운다.
        private void OnCardStateChanged(CardModel card)
        {
            _groupKeyCache.Remove(card);
            if (_pileCardSet.Contains(card))
                _pileDirty = true;
        }

        // 0.110.0부터 셔플처럼 배치로 카드를 옮길 때 파일에서 빠지는 쪽은 RemoveInternal(silent:true)로
        // 처리되어 CardRemoved가 발생하지 않고 ContentsChanged만 수동으로 발생한다. 그래서 CardAdded/
        // CardRemoved만으로는 이 케이스를 놓친다. 다만 ContentsChanged는 정상적인 개별 추가/제거에서도
        // CardAdded/CardRemoved와 항상 같이 발생하므로, 그런 경우엔 이미 _pileCardSet이 최신 상태라
        // 카운트가 일치해 아래 조건에 안 걸리고 기존 증분 갱신 경로가 그대로 유지된다.
        private void OnPileContentsChanged()
        {
            if (CurrentPile != null && CurrentPile.Cards.Count != _pileCardSet.Count)
                _pileDirty = true;
        }

        // 카드 한 장의 추가/제거를 그 카드가 속한 그룹의 카운트만 증감시키는 증분 방식으로 처리한다.
        // ShuffledOrder는 그룹 수만큼만 순회한다.
        private void OnCardAdded(CardModel card)
        {
            _pileCardSet.Add(card);

            if (CurrentPile == null || ShuffledOrder == null || ShuffledOrder.Count == 0)
            {
                _pileDirty = true;
                return;
            }

            string key = CachedGroupKey(card);
            int idx = FindShuffledOrderIndex(key);
            if (idx >= 0)
            {
                var (existingCard, count) = ShuffledOrder[idx];
                int newCount = count + 1;
                ShuffledOrder[idx] = (existingCard, newCount);
                GroupCountChanged?.Invoke(key, existingCard, newCount);
            }
            else
            {
                ShuffledOrder.Add((card, 1));
                GroupCreated?.Invoke(key, card);
            }
        }

        private void OnCardRemoved(CardModel card)
        {
            _pileCardSet.Remove(card);

            if (PileType == PileType.Draw)
                TopCardTracker.Untrack(card);

            if (CurrentPile == null || ShuffledOrder == null || ShuffledOrder.Count == 0)
            {
                _pileDirty = true;
                return;
            }

            string key = CachedGroupKey(card);
            int idx = FindShuffledOrderIndex(key);
            if (idx < 0)
            {
                // 증분 상태가 실제 파일과 어긋난 경우를 대비한 안전망 — 전체 재계산으로 복구.
                _pileDirty = true;
                return;
            }

            var (existingCard, count) = ShuffledOrder[idx];
            if (count <= 1)
            {
                // 자리를 완전히 지우지 않고 카운트 0인 "좀비"로 남긴다. UpdateCardList(전체 재구성)의
                // 병합 로직과 동일한 방식으로, 같은 카드가 상태 변화(클론 등)로 잠깐 빠졌다가 다시
                // 들어와도 원래 위치를 유지하게 하기 위함이다. 자리를 지워버리면 idx<0이 되어
                // OnCardAdded가 새 그룹으로 취급해 리스트 맨 뒤에 추가하므로, 카드를 넣을 때마다
                // 순서가 흔들려 보이는 원인이 된다.
                ShuffledOrder[idx] = (existingCard, 0);
                GroupRemoved?.Invoke(key);
            }
            else
            {
                int newCount = count - 1;
                ShuffledOrder[idx] = (existingCard, newCount);
                GroupCountDecreased?.Invoke(key, newCount);
            }
        }

        private int FindShuffledOrderIndex(string key)
        {
            for (int i = 0; i < ShuffledOrder.Count; i++)
                if (CachedGroupKey(ShuffledOrder[i].card) == key)
                    return i;
            return -1;
        }

        // key를 가진 그룹이 화면에 보이는 행들 중 몇 번째 위치에 있어야 하는지 계산한다(count==0인
        // 좀비 항목은 세지 않음). 좀비에서 부활한 행을 리스트 끝이 아니라 원래 자리에 다시 넣기 위해
        // CardListPanel이 사용한다.
        public int VisibleIndexOf(string key)
        {
            int index = 0;
            foreach (var group in ShuffledOrder)
            {
                if (CachedGroupKey(group.card) == key)
                    return index;
                if (group.count > 0)
                    index++;
            }
            return -1;
        }

        // 파워 감지
        private void SubscribeCreaturePowerEvents(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature)
        {
            if (creature == null || _subscribedCreature == creature) return;
            UnsubscribeCreaturePowerEvents();
            _subscribedCreature = creature;
            _subscribedCreature.PowerApplied += OnCreaturePowerApplied;
            _subscribedCreature.PowerIncreased += OnCreaturePowerIncreased;
            _subscribedCreature.PowerDecreased += OnCreaturePowerDecreased;
            _subscribedCreature.PowerRemoved += OnCreaturePowerRemoved;
        }

        private void UnsubscribeCreaturePowerEvents()
        {
            if (_subscribedCreature == null) return;
            _subscribedCreature.PowerApplied -= OnCreaturePowerApplied;
            _subscribedCreature.PowerIncreased -= OnCreaturePowerIncreased;
            _subscribedCreature.PowerDecreased -= OnCreaturePowerDecreased;
            _subscribedCreature.PowerRemoved -= OnCreaturePowerRemoved;
            _subscribedCreature = null;
        }

        // 코스트/키워드 계산 훅을 오버라이드하는 파워일 때만 캐시 전체를 지운다.
        private void OnCreaturePowerApplied(PowerModel power) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerIncreased(PowerModel power, int change, bool silent) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerDecreased(PowerModel power, bool silent) => MaybeInvalidateGroupKeyCache(power);
        private void OnCreaturePowerRemoved(PowerModel power) => MaybeInvalidateGroupKeyCache(power);

        private static bool PowerAffectsGroupKey(PowerModel power)
        {
            var type = power.GetType();
            if (_powerAffectsGroupKeyCache.TryGetValue(type, out var cached))
                return cached;

            bool affects = OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyEnergyCostInCombat))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyEnergyCostInCombatLate))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyStarCost))
                || OverridesAbstractModelMethod(type, nameof(AbstractModel.TryModifyKeywordsInCombat));

            _powerAffectsGroupKeyCache[type] = affects;
            return affects;
        }

        private static bool OverridesAbstractModelMethod(Type type, string methodName)
        {
            var method = type.GetMethod(methodName);
            return method != null && method.DeclaringType != typeof(AbstractModel);
        }

        private void MaybeInvalidateGroupKeyCache(PowerModel power)
        {
            bool affects = PowerAffectsGroupKey(power);
            if (!affects)
                return; // 바리케이드처럼 코스트/키워드와 무관한 파워는 캐시를 건드릴 필요가 없다.
            _groupKeyCache.Clear();
            _pileDirty = true;
        }

        // CardCostHelper.GetEnergyCostColor는 파일 종류와 무관하게 현재 Energy/Stars 자원량과 코스트를
        // 비교해 "자원 부족(빨강)" 색을 결정한다. 카드/파워 이벤트로는 안 잡히므로 자원 변화 자체를 구독한다.
        private void SubscribeEnergyEvents(MegaCrit.Sts2.Core.Entities.Players.PlayerCombatState combatState)
        {
            if (combatState == null || _subscribedCombatState == combatState) return;
            UnsubscribeEnergyEvents();
            _subscribedCombatState = combatState;
            _subscribedCombatState.EnergyChanged += OnResourceChanged;
            _subscribedCombatState.StarsChanged += OnResourceChanged;
        }

        private void UnsubscribeEnergyEvents()
        {
            if (_subscribedCombatState == null) return;
            _subscribedCombatState.EnergyChanged -= OnResourceChanged;
            _subscribedCombatState.StarsChanged -= OnResourceChanged;
            _subscribedCombatState = null;
        }

        private void OnResourceChanged(int newValue, int oldValue) => _pileDirty = true;

        private const string RetainIconPath = "res://images/powers/retain_hand_power.png";
        private const string SlyIconPath = "res://images/packed/combat_ui/discard_pile.png";
        private const string EtherealIconPath = "res://images/powers/slippery_power.png";
        private const string ReplayIconPath = "res://images/packed/modifiers/binary.png";

        // 전투 중 후천적으로 얻은 키워드(다른 모델이 임시로 부여했거나 단발성 Retain/Sly)가 있는지.
        // 인챈트/카드 자체 효과로 붙은 키워드는 여기 포함 안 됨 (그건 이미 인챈트 아이콘으로 표시됨).
        internal static bool HasTemporaryKeyword(CardModel card) => GetTemporaryKeywordIconPath(card) != null;

        // 어떤 키워드가 원인인지에 따라 다른 아이콘 경로를 반환. 여러 개가 겹치면 보존 > 교활 > 휘발성 > 재사용 순.
        internal static string GetTemporaryKeywordIconPath(CardModel card)
        {
            // card.Keywords는 호출마다 파워/유물 훅을 전부 순회하는 무거운 연산이라 한 번만 계산해서 재사용한다.
            var local = card.GetKeywordsWithSources(KeywordSources.Local);
            var all = card.Keywords;

            bool grantedByEffect(CardKeyword kw) =>
                (all.Contains(kw) && !local.Contains(kw))                       // Global (파워 등이 전투 중 임시 부여)
                || (local.Contains(kw) && AppliedKeywordTrackerPatch.WasAppliedByEffect(card, kw)); // CardCmd.ApplyKeyword로 부여

            if (card.ShouldRetainThisTurn && !all.Contains(CardKeyword.Retain))
                return RetainIconPath;
            if (grantedByEffect(CardKeyword.Retain))
                return RetainIconPath;

            if (card.IsSlyThisTurn && !all.Contains(CardKeyword.Sly))
                return SlyIconPath;
            if (grantedByEffect(CardKeyword.Sly))
                return SlyIconPath;

            if (grantedByEffect(CardKeyword.Ethereal))
                return EtherealIconPath;

            // Keyword가 아닌 별도 시스템(BaseReplayCount)이지만 같은 부류: Transfigure/SoldiersStew가
            // 전투 중에만 ++로 부여하고, 카드 자체 설계로 처음부터 갖고 있는 카드는 없음.
            if (card.BaseReplayCount > 0)
                return ReplayIconPath;

            return null;
        }

        private static string GroupKey(CardModel card)
        {
            // 이 함수가 예외를 던지면 OnCardAdded/OnCardRemoved가 행 정리 로직에 도달하기 전에 중단되고
            // _pileDirty도 세팅되지 않아, 파일에서 이미 빠진 카드의 행이 패널에 영구히 남는다 (예: 방금
            // 생성된 토큰 카드가 아직 일부 필드가 완전히 준비되지 않은 짧은 순간에 호출되는 경우).
            // 다른 모든 렌더링 헬퍼처럼 여기도 방어적으로 감싸고, 실패 시에도 최소한 카드별로 구분되는
            // 키를 반환해 행이 유령처럼 남지 않게 한다.
            try
            {
                string enchantmentKey = card.Enchantment != null ? card.Enchantment.GetType().Name : "none";
                string afflictionKey = card.Affliction != null ? card.Affliction.Id.Entry : "none";
                bool hasTemporaryKeyword = HasTemporaryKeyword(card);
                int energy = card.EnergyCost.CostsX ? int.MinValue : card.EnergyCost.GetWithModifiers(CostModifiers.All);
                int star = card.HasStarCostX ? int.MinValue : card.GetStarCostWithModifiers();
                return $"{card.Title}|{card.IsUpgraded}|{enchantmentKey}|{afflictionKey}|{hasTemporaryKeyword}|{energy}|{star}";
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[CardPileWatcher] GroupKey failed for '{card.Title}': {ex}");
                return $"{card.Title}|error|{card.GetHashCode()}";
            }
        }
    }
}
