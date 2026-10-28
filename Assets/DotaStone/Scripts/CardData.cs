using System.Collections.Generic;
using UnityEngine;

namespace DotaStone
{
    public enum CardType { Minion, Spell }

    // Когда срабатывает эффект существа
    public enum Trigger { None, Battlecry, Deathrattle }

    public enum EffectType
    {
        None,
        Damage,               // урон цели
        DamageEnemyMinions,   // урон всем вражеским существам
        DamageAllEnemies,     // урон всем врагам (существа + герой)
        Heal,                 // лечение цели
        Draw,                 // взять карты
        Buff,                 // +Value/+Value2 цели
        BuffFriendlyMinions,  // +Value/+Value2 всем своим существам
        Summon,               // призвать Value копий TokenId
        Armor,                // броня своему герою
        Destroy,              // уничтожить цель
        GainMana,             // временная мана
        GiveShield            // божественный щит цели
    }

    public enum TargetType { None, AnyCharacter, EnemyCharacter, EnemyMinion, FriendlyMinion }

    public class CardDef
    {
        public string Id;
        public string Name;
        public string Text = "";
        public int Cost;
        public CardType Type;
        public int Attack;
        public int Health;
        public bool Taunt;
        public bool Charge;
        public bool Shield;
        public Trigger Trigger;
        public EffectType Effect;
        public int Value;
        public int Value2;
        public TargetType Target;
        public string TokenId;
        public bool IsToken;
        public Color Color;

        public CardDef Keys(bool taunt = false, bool charge = false, bool shield = false)
        {
            Taunt = taunt;
            Charge = charge;
            Shield = shield;
            return this;
        }

        public CardDef Battlecry(EffectType effect, int value, TargetType target = TargetType.None, string tokenId = null)
        {
            Trigger = Trigger.Battlecry;
            Effect = effect;
            Value = value;
            Target = target;
            TokenId = tokenId;
            return this;
        }

        public CardDef Deathrattle(EffectType effect, int value, string tokenId = null)
        {
            Trigger = Trigger.Deathrattle;
            Effect = effect;
            Value = value;
            TokenId = tokenId;
            return this;
        }

        public CardDef Says(string text)
        {
            Text = text;
            return this;
        }

        public CardDef Token()
        {
            IsToken = true;
            return this;
        }
    }

    public class HeroDef
    {
        public string Id;
        public string Name;
        public string PowerName;
        public string PowerText;
        public EffectType Effect;
        public int Value;
        public TargetType Target;
        public string TokenId;
        public Color Color;
    }

    public static class CardDB
    {
        // Цвета по атрибутам героев Доты
        public static readonly Color Str = new Color(0.60f, 0.20f, 0.17f);
        public static readonly Color Agi = new Color(0.20f, 0.48f, 0.24f);
        public static readonly Color Int = new Color(0.20f, 0.34f, 0.64f);
        public static readonly Color Neutral = new Color(0.42f, 0.38f, 0.30f);
        public static readonly Color Item = new Color(0.55f, 0.42f, 0.14f);
        public static readonly Color Ability = new Color(0.40f, 0.22f, 0.55f);

        public static readonly List<CardDef> Cards = new List<CardDef>();
        public static readonly List<HeroDef> Heroes = new List<HeroDef>();
        static readonly Dictionary<string, CardDef> byId = new Dictionary<string, CardDef>();

        static CardDB()
        {
            // ---------- Существа ----------
            Minion("melee_creep", "Мили-крип", 1, 1, 2, Neutral);
            Minion("ranged_creep", "Рендж-крип", 1, 2, 1, Neutral);
            Minion("courier", "Курьер", 1, 1, 1, Neutral)
                .Battlecry(EffectType.Draw, 1).Says("Боевой клич: возьмите карту.");
            Minion("centaur", "Кентавр", 2, 2, 3, Str)
                .Keys(taunt: true).Says("Провокация.");
            Minion("ogre", "Огр Маги", 2, 3, 2, Int);
            Minion("phantom", "Фантом Ассасин", 3, 3, 2, Agi)
                .Keys(charge: true).Says("Рывок.");
            Minion("abaddon", "Абаддон", 3, 2, 3, Str)
                .Keys(shield: true).Says("Божественный щит.");
            Minion("sniper", "Снайпер", 3, 2, 2, Agi)
                .Battlecry(EffectType.Damage, 2, TargetType.EnemyCharacter).Says("Боевой клич: нанесите 2 урона врагу.");
            Minion("dazzle", "Даззл", 3, 2, 3, Int)
                .Battlecry(EffectType.Heal, 3, TargetType.AnyCharacter).Says("Боевой клич: восстановите 3 здоровья.");
            Minion("undying", "Андаинг", 3, 2, 2, Str)
                .Deathrattle(EffectType.Summon, 1, "zombie").Says("Предсмертный хрип: призовите Зомби 2/2.");
            Minion("techies", "Течис", 3, 1, 3, Int)
                .Deathrattle(EffectType.DamageAllEnemies, 2).Says("Предсмертный хрип: 2 урона всем врагам.");
            Minion("tide", "Тайдхантер", 4, 3, 5, Str)
                .Keys(taunt: true).Says("Провокация.");
            Minion("jugg", "Джаггернаут", 4, 3, 3, Agi)
                .Battlecry(EffectType.DamageEnemyMinions, 1).Says("Боевой клич: 1 урон всем вражеским существам.");
            Minion("brood", "Бруда", 4, 2, 3, Agi)
                .Battlecry(EffectType.Summon, 2, TargetType.None, "spider").Says("Боевой клич: призовите двух Паучков 1/1.");
            Minion("axe", "Акс", 5, 4, 6, Str)
                .Keys(taunt: true).Says("Провокация.");
            Minion("sven", "Свен", 6, 5, 5, Str)
                .Keys(charge: true).Says("Рывок.");
            Minion("warlock", "Варлок", 6, 3, 3, Int)
                .Battlecry(EffectType.Summon, 1, TargetType.None, "golem").Says("Боевой клич: призовите Голема 4/4 с провокацией.");
            Minion("roshan", "Рошан", 9, 8, 8, Neutral)
                .Keys(taunt: true, shield: true).Says("Провокация. Божественный щит.");

            // ---------- Токены (не попадают в колоду) ----------
            Minion("zombie", "Зомби", 1, 2, 2, Neutral).Token();
            Minion("spider", "Паучок", 1, 1, 1, Agi).Token();
            Minion("golem", "Голем", 4, 4, 4, Int).Keys(taunt: true).Says("Провокация.").Token();
            Minion("robot", "Робот", 1, 1, 1, Int).Token();

            // ---------- Заклинания и предметы ----------
            Spell("coin", "Монетка", 0, Item, EffectType.GainMana, 1).Says("Получите 1 кристалл маны до конца хода.").Token();
            Spell("tango", "Танго", 1, Item, EffectType.Heal, 4, TargetType.AnyCharacter).Says("Восстановите 4 здоровья.");
            Spell("mom", "Маска безумия", 1, Item, EffectType.Buff, 2, TargetType.FriendlyMinion).Says("Дайте своему существу +2 к атаке.");
            Spell("bkb", "Блэк Кинг Бар", 1, Item, EffectType.GiveShield, 0, TargetType.FriendlyMinion).Says("Дайте своему существу божественный щит.");
            Spell("dagon", "Дагон", 2, Item, EffectType.Damage, 3, TargetType.EnemyCharacter).Says("Нанесите 3 урона врагу.");
            Spell("drum", "Барабан выносливости", 2, Item, EffectType.BuffFriendlyMinions, 1, TargetType.None, 1).Says("Ваши существа получают +1/+1.");
            Spell("tome", "Том знаний", 3, Item, EffectType.Draw, 2).Says("Возьмите 2 карты.");
            Spell("heart", "Сердце Тарраска", 3, Item, EffectType.Buff, 1, TargetType.FriendlyMinion, 4).Says("Дайте своему существу +1/+4.");
            Spell("freezing", "Ледяное поле", 4, Ability, EffectType.DamageAllEnemies, 2).Says("Нанесите 2 урона всем врагам.");
            Spell("laguna", "Лагуна Блейд", 5, Ability, EffectType.Damage, 6, TargetType.EnemyCharacter).Says("Нанесите 6 урона врагу.");
            Spell("finger", "Палец смерти", 5, Ability, EffectType.Destroy, 0, TargetType.EnemyMinion).Says("Уничтожьте вражеское существо.");
            Spell("wrath", "Гнев богов", 6, Ability, EffectType.DamageEnemyMinions, 3).Says("Нанесите 3 урона всем вражеским существам.");

            // ---------- Герои ----------
            Hero("sf", "Шадоу Финд", new Color(0.35f, 0.09f, 0.06f), "Шадоурейз", "Нанесите 1 урона любой цели.", EffectType.Damage, 1, TargetType.AnyCharacter);
            Hero("invoker", "Инвокер", new Color(0.48f, 0.29f, 0.07f), "Инвок", "Возьмите карту.", EffectType.Draw, 1, TargetType.None);
            Hero("pudge", "Пудж", new Color(0.30f, 0.35f, 0.15f), "Груда мяса", "Получите 2 брони.", EffectType.Armor, 2, TargetType.None);
            Hero("tinker", "Тинкер", new Color(0.12f, 0.31f, 0.45f), "Марш машин", "Призовите Робота 1/1.", EffectType.Summon, 1, TargetType.None, "robot");
        }

        public static CardDef Get(string id)
        {
            CardDef def;
            if (!byId.TryGetValue(id, out def))
                Debug.LogError("Нет карты с id " + id);
            return def;
        }

        static CardDef Minion(string id, string name, int cost, int attack, int health, Color color)
        {
            CardDef c = new CardDef
            {
                Id = id, Name = name, Cost = cost, Type = CardType.Minion,
                Attack = attack, Health = health, Color = color
            };
            Add(c);
            return c;
        }

        static CardDef Spell(string id, string name, int cost, Color color, EffectType effect, int value,
            TargetType target = TargetType.None, int value2 = 0)
        {
            CardDef c = new CardDef
            {
                Id = id, Name = name, Cost = cost, Type = CardType.Spell, Color = color,
                Effect = effect, Value = value, Value2 = value2, Target = target
            };
            Add(c);
            return c;
        }

        static void Hero(string id, string name, Color color, string powerName, string powerText,
            EffectType effect, int value, TargetType target, string tokenId = null)
        {
            Heroes.Add(new HeroDef
            {
                Id = id, Name = name, Color = color, PowerName = powerName, PowerText = powerText,
                Effect = effect, Value = value, Target = target, TokenId = tokenId
            });
        }

        static void Add(CardDef c)
        {
            Cards.Add(c);
            byId[c.Id] = c;
        }
    }
}
