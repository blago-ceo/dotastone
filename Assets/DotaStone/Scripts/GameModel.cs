using System.Collections.Generic;

namespace DotaStone
{
    // Всё, что можно атаковать или лечить: герой или существо
    public abstract class Character
    {
        public PlayerState Owner;
        public int Attack;
        public int Health;
        public int MaxHealth;

        public bool IsDead { get { return Health <= 0; } }
        public abstract string Name { get; }
    }

    public class Minion : Character
    {
        public CardDef Def;
        public bool Taunt;
        public bool Charge;
        public bool Shield;
        public bool Sleeping = true; // только что призвано
        public int AttacksDone;

        public override string Name { get { return Def.Name; } }

        public bool CanAttack
        {
            get { return Attack > 0 && AttacksDone == 0 && (!Sleeping || Charge); }
        }
    }

    public class Hero : Character
    {
        public HeroDef Def;
        public int Armor;

        public override string Name { get { return Def.Name; } }
    }

    public class PlayerState
    {
        public string Name;
        public bool IsAI;
        public Hero Hero;
        public PlayerState Opponent;
        public List<CardDef> Deck = new List<CardDef>();
        public List<CardDef> Hand = new List<CardDef>();
        public List<Minion> Board = new List<Minion>();
        public int Mana;
        public int MaxMana;
        public int Fatigue;
        public bool PowerUsed;
    }
}
