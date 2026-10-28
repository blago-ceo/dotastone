using System.Collections.Generic;

namespace DotaStone
{
    // Простой «жадный» бот: за один вызов делает одно действие.
    public static class AIBrain
    {
        // Возвращает false, когда делать больше нечего
        public static bool DoOneAction(GameLogic g, PlayerState me)
        {
            return TryPlayCoin(g, me) || TryPlayCard(g, me) || TryHeroPower(g, me) || TryAttack(g, me);
        }

        static bool TryPlayCoin(GameLogic g, PlayerState me)
        {
            int coin = me.Hand.FindIndex(c => c.Id == "coin");
            if (coin < 0) return false;
            // Монетка нужна, только если без неё ничего не сыграть, а с ней — можно
            bool somethingPlayable = false;
            bool unlocksCard = false;
            foreach (CardDef c in me.Hand)
            {
                if (c.Id == "coin") continue;
                if (g.CanPlay(me, c)) somethingPlayable = true;
                if (c.Cost == me.Mana + 1) unlocksCard = true;
            }
            if (somethingPlayable || !unlocksCard) return false;
            return g.PlayCard(me, coin, null);
        }

        static bool TryPlayCard(GameLogic g, PlayerState me)
        {
            // Сначала самые дорогие карты
            List<int> order = new List<int>();
            for (int i = 0; i < me.Hand.Count; i++) order.Add(i);
            order.Sort((a, b) => me.Hand[b].Cost.CompareTo(me.Hand[a].Cost));

            foreach (int i in order)
            {
                CardDef c = me.Hand[i];
                if (c.Id == "coin" || !g.CanPlay(me, c)) continue;

                Character target = null;
                if (g.NeedsTarget(me, c))
                {
                    target = ChooseTarget(g, me, c.Effect, c.Value, c.Target);
                    // Существо всё равно стоит выставить, даже если эффект пропадёт впустую
                    if (target == null && c.Type == CardType.Minion) target = AnyTarget(g, me, c.Target);
                    if (target == null) continue;
                }
                if (g.PlayCard(me, i, target)) return true;
            }
            return false;
        }

        static bool TryHeroPower(GameLogic g, PlayerState me)
        {
            if (!g.CanUsePower(me)) return false;
            HeroDef h = me.Hero.Def;
            Character target = null;
            if (h.Target != TargetType.None)
            {
                target = ChooseTarget(g, me, h.Effect, h.Value, h.Target);
                if (target == null) return false;
            }
            return g.UsePower(me, target);
        }

        static bool TryAttack(GameLogic g, PlayerState me)
        {
            foreach (Minion m in me.Board)
            {
                if (!m.CanAttack) continue;
                Character target = ChooseAttackTarget(g, me, m);
                if (target != null && g.DoAttack(m, target)) return true;
            }
            return false;
        }

        static Character ChooseAttackTarget(GameLogic g, PlayerState me, Minion attacker)
        {
            PlayerState enemy = me.Opponent;
            List<Minion> options = enemy.Board.FindAll(t => g.CanAttackTarget(attacker, t));
            bool canHitFace = g.CanAttackTarget(attacker, enemy.Hero);

            if (canHitFace)
            {
                // Хватает урона на летал — бьём в лицо
                int totalAttack = 0;
                foreach (Minion m in me.Board)
                    if (m.CanAttack) totalAttack += m.Attack;
                if (totalAttack >= enemy.Hero.Health + enemy.Hero.Armor) return enemy.Hero;
            }

            // Выгодный размен: убиваем и выживаем, либо меняемся на более сильное существо
            Minion best = null;
            int bestScore = int.MinValue;
            foreach (Minion t in options)
            {
                bool kills = !t.Shield && t.Health <= attacker.Attack;
                bool survives = t.Attack < attacker.Health;
                int score;
                if (kills && survives) score = 100 + t.Attack * 2 + t.Health;
                else if (kills && t.Attack + t.Health >= attacker.Attack + attacker.Health) score = 50 + t.Attack;
                else if (!canHitFace) score = -t.Health; // обязаны бить провокацию — выбираем самую хлипкую
                else continue;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
            if (best != null) return best;
            return canHitFace ? enemy.Hero : null;
        }

        static Character ChooseTarget(GameLogic g, PlayerState me, EffectType effect, int value, TargetType type)
        {
            List<Character> valid = g.AllCharacters().FindAll(c => g.IsValidTarget(me, type, c));
            Character best = null;
            int bestScore = int.MinValue;

            foreach (Character c in valid)
            {
                Minion m = c as Minion;
                bool mine = c.Owner == me;
                int score;
                switch (effect)
                {
                    case EffectType.Damage:
                        if (mine) continue;
                        if (m != null && !m.Shield && m.Health <= value) score = 100 + m.Attack * 2 + m.Health;
                        else if (m == null) score = 50;
                        else if (m.Shield && value == 1) score = 60 + m.Attack; // снять щит дешёвым уроном
                        else score = 10 + m.Attack;
                        break;
                    case EffectType.Heal:
                        if (!mine || c.Health >= c.MaxHealth) continue;
                        score = c.MaxHealth - c.Health + (m == null ? 1 : 0);
                        break;
                    case EffectType.Buff:
                        if (!mine || m == null) continue;
                        score = m.Attack + m.Health;
                        break;
                    case EffectType.GiveShield:
                        if (!mine || m == null || m.Shield) continue;
                        score = m.Attack + m.Health;
                        break;
                    case EffectType.Destroy:
                        if (mine || m == null) continue;
                        score = m.Attack + m.Health;
                        if (score < 6) continue; // не тратим на мелочь
                        break;
                    default:
                        continue;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        // Любая допустимая цель: сначала своя (для лечения/баффов), потом вражеская
        static Character AnyTarget(GameLogic g, PlayerState me, TargetType type)
        {
            Character fallback = null;
            foreach (Character c in g.AllCharacters())
            {
                if (!g.IsValidTarget(me, type, c)) continue;
                if (c.Owner == me) return c;
                if (fallback == null) fallback = c;
            }
            return fallback;
        }
    }
}
