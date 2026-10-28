using System.Collections.Generic;
using UnityEngine;

namespace DotaStone
{
    // Правила игры. Ничего не знает про интерфейс — только состояние и действия.
    public class GameLogic
    {
        public const int MaxHand = 10;
        public const int MaxBoard = 7;
        public const int MaxManaCap = 10;
        public const int HeroPowerCost = 2;
        public const int StartHealth = 30;
        public const int DeckSize = 30;

        public readonly PlayerState Human;
        public readonly PlayerState Ai;
        public PlayerState Current { get; private set; }
        public bool IsOver { get; private set; }
        public PlayerState Winner { get; private set; }

        public event System.Action<string> OnLog;

        public GameLogic(HeroDef humanHero, HeroDef aiHero)
        {
            Human = CreatePlayer("Вы", humanHero, false);
            Ai = CreatePlayer("Противник", aiHero, true);
            Human.Opponent = Ai;
            Ai.Opponent = Human;
        }

        public void Start()
        {
            PlayerState first = Random.value < 0.5f ? Human : Ai;
            PlayerState second = first.Opponent;
            Say(first == Human ? "Вы ходите первым." : "Противник ходит первым.");
            Draw(first, 3);
            Draw(second, 4);
            second.Hand.Add(CardDB.Get("coin"));
            Current = first;
            StartTurn(first);
        }

        // ---------------- Ход ----------------

        void StartTurn(PlayerState p)
        {
            p.MaxMana = Mathf.Min(MaxManaCap, p.MaxMana + 1);
            p.Mana = p.MaxMana;
            p.PowerUsed = false;
            foreach (Minion m in p.Board)
            {
                m.Sleeping = false;
                m.AttacksDone = 0;
            }
            Draw(p, 1);
            ResolveDeaths();
        }

        public void EndTurn()
        {
            if (IsOver) return;
            Current = Current.Opponent;
            StartTurn(Current);
        }

        // ---------------- Карты ----------------

        public bool CanPlay(PlayerState p, CardDef card)
        {
            if (IsOver || p != Current || card.Cost > p.Mana) return false;
            if (card.Type == CardType.Minion) return p.Board.Count < MaxBoard;
            return card.Target == TargetType.None || HasValidTarget(p, card.Target);
        }

        // Нужно ли выбирать цель перед розыгрышем
        public bool NeedsTarget(PlayerState p, CardDef card)
        {
            if (card.Target == TargetType.None) return false;
            if (card.Type == CardType.Minion && card.Trigger != Trigger.Battlecry) return false;
            return HasValidTarget(p, card.Target);
        }

        public bool PlayCard(PlayerState p, int handIndex, Character target)
        {
            if (handIndex < 0 || handIndex >= p.Hand.Count) return false;
            CardDef card = p.Hand[handIndex];
            if (!CanPlay(p, card)) return false;
            bool needsTarget = NeedsTarget(p, card);
            if (needsTarget && !IsValidTarget(p, card.Target, target)) return false;
            if (!needsTarget) target = null;

            p.Mana -= card.Cost;
            p.Hand.RemoveAt(handIndex);
            Say(p.Name + ": разыграна карта «" + card.Name + "»");

            if (card.Type == CardType.Minion)
            {
                Summon(p, card, -1);
                if (card.Trigger == Trigger.Battlecry)
                    ApplyEffect(p, card.Effect, card.Value, card.Value2, card.TokenId, target, card.Name, -1);
            }
            else
            {
                ApplyEffect(p, card.Effect, card.Value, card.Value2, card.TokenId, target, card.Name, -1);
            }

            ResolveDeaths();
            return true;
        }

        // ---------------- Сила героя ----------------

        public bool CanUsePower(PlayerState p)
        {
            if (IsOver || p != Current || p.PowerUsed || p.Mana < HeroPowerCost) return false;
            HeroDef h = p.Hero.Def;
            if (h.Effect == EffectType.Summon && p.Board.Count >= MaxBoard) return false;
            return h.Target == TargetType.None || HasValidTarget(p, h.Target);
        }

        public bool UsePower(PlayerState p, Character target)
        {
            if (!CanUsePower(p)) return false;
            HeroDef h = p.Hero.Def;
            if (h.Target != TargetType.None && !IsValidTarget(p, h.Target, target)) return false;

            p.Mana -= HeroPowerCost;
            p.PowerUsed = true;
            Say(p.Name + ": сила героя «" + h.PowerName + "»");
            ApplyEffect(p, h.Effect, h.Value, 0, h.TokenId, target, h.PowerName, -1);
            ResolveDeaths();
            return true;
        }

        // ---------------- Атака ----------------

        public bool HasTaunt(PlayerState p)
        {
            foreach (Minion m in p.Board)
                if (m.Taunt && !m.IsDead) return true;
            return false;
        }

        public bool CanAttackTarget(Minion attacker, Character target)
        {
            if (IsOver || attacker == null || target == null) return false;
            if (attacker.Owner != Current || !attacker.CanAttack) return false;
            if (target.Owner == attacker.Owner || target.IsDead) return false;
            Minion tm = target as Minion;
            bool targetIsTaunt = tm != null && tm.Taunt;
            return targetIsTaunt || !HasTaunt(target.Owner);
        }

        public bool DoAttack(Minion attacker, Character target)
        {
            if (!CanAttackTarget(attacker, target)) return false;
            Say(attacker.Owner.Name + ": " + attacker.Name + " атакует " + target.Name);
            int attackerDamage = attacker.Attack;
            int targetDamage = target.Attack;
            Damage(target, attackerDamage);
            Damage(attacker, targetDamage);
            attacker.AttacksDone++;
            ResolveDeaths();
            return true;
        }

        // ---------------- Цели ----------------

        public List<Character> AllCharacters()
        {
            List<Character> list = new List<Character>();
            list.Add(Human.Hero);
            list.Add(Ai.Hero);
            list.AddRange(Human.Board.ConvertAll(m => (Character)m));
            list.AddRange(Ai.Board.ConvertAll(m => (Character)m));
            return list;
        }

        public bool IsValidTarget(PlayerState caster, TargetType type, Character c)
        {
            if (c == null || c.IsDead) return false;
            bool isMinion = c is Minion;
            bool isEnemy = c.Owner != caster;
            switch (type)
            {
                case TargetType.AnyCharacter: return true;
                case TargetType.EnemyCharacter: return isEnemy;
                case TargetType.EnemyMinion: return isMinion && isEnemy;
                case TargetType.FriendlyMinion: return isMinion && !isEnemy;
                default: return false;
            }
        }

        public bool HasValidTarget(PlayerState caster, TargetType type)
        {
            foreach (Character c in AllCharacters())
                if (IsValidTarget(caster, type, c)) return true;
            return false;
        }

        // ---------------- Эффекты ----------------

        void ApplyEffect(PlayerState caster, EffectType effect, int value, int value2, string tokenId,
            Character target, string source, int summonIndex)
        {
            PlayerState enemy = caster.Opponent;
            Minion targetMinion = target as Minion;

            switch (effect)
            {
                case EffectType.Damage:
                    if (target == null) return;
                    Say(source + ": " + value + " урона → " + target.Name);
                    Damage(target, value);
                    break;

                case EffectType.DamageEnemyMinions:
                    Say(source + ": " + value + " урона всем вражеским существам");
                    foreach (Minion m in enemy.Board.ToArray()) Damage(m, value);
                    break;

                case EffectType.DamageAllEnemies:
                    Say(source + ": " + value + " урона всем врагам");
                    foreach (Minion m in enemy.Board.ToArray()) Damage(m, value);
                    Damage(enemy.Hero, value);
                    break;

                case EffectType.Heal:
                    if (target == null) return;
                    Say(source + ": +" + value + " здоровья → " + target.Name);
                    Heal(target, value);
                    break;

                case EffectType.Draw:
                    Draw(caster, value);
                    break;

                case EffectType.Buff:
                    if (targetMinion == null) return;
                    Buff(targetMinion, value, value2);
                    Say(source + ": " + targetMinion.Name + " получает +" + value + "/+" + value2);
                    break;

                case EffectType.BuffFriendlyMinions:
                    foreach (Minion m in caster.Board) Buff(m, value, value2);
                    Say(source + ": ваши существа получают +" + value + "/+" + value2);
                    break;

                case EffectType.Summon:
                    CardDef token = CardDB.Get(tokenId);
                    if (token == null) return;
                    for (int i = 0; i < value; i++)
                    {
                        if (Summon(caster, token, summonIndex) == null) break;
                        if (summonIndex >= 0) summonIndex++;
                    }
                    break;

                case EffectType.Armor:
                    caster.Hero.Armor += value;
                    Say(caster.Hero.Name + " получает " + value + " брони");
                    break;

                case EffectType.Destroy:
                    if (target == null) return;
                    Say(source + ": уничтожено " + target.Name);
                    target.Health = 0;
                    break;

                case EffectType.GainMana:
                    caster.Mana = Mathf.Min(MaxManaCap, caster.Mana + value);
                    break;

                case EffectType.GiveShield:
                    if (targetMinion == null) return;
                    targetMinion.Shield = true;
                    Say(targetMinion.Name + " получает божественный щит");
                    break;
            }
        }

        void Damage(Character c, int amount)
        {
            if (amount <= 0 || c.IsDead) return;

            Minion m = c as Minion;
            if (m != null && m.Shield)
            {
                m.Shield = false;
                Say(m.Name + " теряет божественный щит");
                return;
            }

            Hero h = c as Hero;
            if (h != null && h.Armor > 0)
            {
                int absorbed = Mathf.Min(h.Armor, amount);
                h.Armor -= absorbed;
                amount -= absorbed;
            }
            c.Health -= amount;
        }

        void Heal(Character c, int amount)
        {
            c.Health = Mathf.Min(c.MaxHealth, c.Health + amount);
        }

        static void Buff(Minion m, int attack, int health)
        {
            m.Attack += attack;
            m.Health += health;
            m.MaxHealth += health;
        }

        Minion Summon(PlayerState p, CardDef def, int index)
        {
            if (p.Board.Count >= MaxBoard) return null;
            Minion m = new Minion
            {
                Def = def, Owner = p,
                Attack = def.Attack, Health = def.Health, MaxHealth = def.Health,
                Taunt = def.Taunt, Charge = def.Charge, Shield = def.Shield
            };
            if (index < 0 || index > p.Board.Count) p.Board.Add(m);
            else p.Board.Insert(index, m);
            return m;
        }

        void Draw(PlayerState p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (p.Deck.Count == 0)
                {
                    p.Fatigue++;
                    Say(p.Name + ": колода пуста! Усталость " + p.Fatigue);
                    Damage(p.Hero, p.Fatigue);
                    continue;
                }
                CardDef card = p.Deck[0];
                p.Deck.RemoveAt(0);
                if (p.Hand.Count >= MaxHand)
                    Say(p.Name + ": рука полна, карта «" + card.Name + "» сгорает");
                else
                    p.Hand.Add(card);
            }
        }

        // Убираем погибших, срабатывают предсмертные хрипы (они могут убить ещё кого-то)
        void ResolveDeaths()
        {
            bool anyDied = true;
            while (anyDied)
            {
                anyDied = false;
                foreach (PlayerState p in new[] { Human, Ai })
                {
                    for (int i = p.Board.Count - 1; i >= 0; i--)
                    {
                        Minion m = p.Board[i];
                        if (!m.IsDead) continue;
                        p.Board.RemoveAt(i);
                        anyDied = true;
                        Say(m.Name + " погибает");
                        if (m.Def.Trigger == Trigger.Deathrattle)
                            ApplyEffect(p, m.Def.Effect, m.Def.Value, m.Def.Value2, m.Def.TokenId, null, m.Name, i);
                    }
                }
            }
            CheckGameOver();
        }

        void CheckGameOver()
        {
            if (IsOver) return;
            bool humanDead = Human.Hero.IsDead;
            bool aiDead = Ai.Hero.IsDead;
            if (!humanDead && !aiDead) return;
            IsOver = true;
            Winner = humanDead && aiDead ? null : (humanDead ? Ai : Human);
        }

        // ---------------- Создание ----------------

        static PlayerState CreatePlayer(string name, HeroDef hero, bool isAi)
        {
            PlayerState p = new PlayerState { Name = name, IsAI = isAi };
            p.Hero = new Hero { Def = hero, Owner = p, Health = StartHealth, MaxHealth = StartHealth };
            p.Deck = BuildDeck();
            return p;
        }

        // По 2 копии каждой карты, перемешать, взять 30
        static List<CardDef> BuildDeck()
        {
            List<CardDef> pool = new List<CardDef>();
            foreach (CardDef c in CardDB.Cards)
            {
                if (c.IsToken) continue;
                pool.Add(c);
                pool.Add(c);
            }
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardDef tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }
            if (pool.Count > DeckSize) pool.RemoveRange(DeckSize, pool.Count - DeckSize);
            return pool;
        }

        void Say(string text)
        {
            if (OnLog != null) OnLog(text);
        }
    }
}
