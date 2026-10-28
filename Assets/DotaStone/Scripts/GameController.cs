using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DotaStone
{
    // Главный скрипт: строит интерфейс, обрабатывает клики, запускает ход бота.
    // Добавляется в сцену автоматически при нажатии Play — ничего настраивать не нужно.
    public class GameController : MonoBehaviour
    {
        enum Mode { Idle, CardTarget, PowerTarget, Attack }

        static readonly Color BgColor = new Color(0.08f, 0.09f, 0.12f);
        static readonly Color BoardColor = new Color(0.20f, 0.16f, 0.12f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color FrameIdle = new Color(0.06f, 0.06f, 0.06f);
        static readonly Color FrameTaunt = new Color(0.72f, 0.72f, 0.78f);
        static readonly Color FrameReady = new Color(0.25f, 0.90f, 0.35f);
        static readonly Color FrameSelected = new Color(0.20f, 0.80f, 1f);
        static readonly Color FrameTarget = new Color(1f, 0.45f, 0.10f);
        static readonly Color ManaBlue = new Color(0.15f, 0.40f, 0.90f);
        static readonly Color AttackYellow = new Color(0.90f, 0.65f, 0.10f);
        static readonly Color HealthRed = new Color(0.80f, 0.15f, 0.15f);

        static GameController instance;

        GameLogic game;
        Mode mode = Mode.Idle;
        int pendingCard = -1;
        Minion attacker;
        bool aiBusy;
        bool gameOverShown;
        readonly List<string> log = new List<string>();

        RectTransform root;
        RectTransform enemyHeroArea, enemyBoardArea, playerBoardArea, playerHeroArea, handArea, previewArea;
        HorizontalLayoutGroup handLayout;
        Text logText, statusText, bannerText;
        GameObject banner, cancelButton;
        Button endTurnButton;
        Text endTurnLabel;
        Coroutine bannerRoutine;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (instance != null) return;
            new GameObject("DotaStone").AddComponent<GameController>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            UIKit.Init();
            SetupCamera();
            SetupCanvas();
            ShowHeroSelect();
        }

        bool HumanCanAct
        {
            get { return game != null && !game.IsOver && game.Current == game.Human && !aiBusy; }
        }

        // ======================= Сцена =======================

        void SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgColor;
        }

        void SetupCanvas()
        {
            GameObject cgo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            cgo.transform.SetParent(transform, false);
            Canvas canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            root = (RectTransform)cgo.transform;

            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }
        }

        // ======================= Выбор героя =======================

        void ShowHeroSelect()
        {
            StopAllCoroutines();
            game = null;
            aiBusy = false;
            UIKit.Clear(root);
            UIKit.Panel(root, "Background", BgColor);

            Text title = UIKit.Label(root, "DOTASTONE", 110, Gold, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(title.rectTransform, 0, 0.78f, 1, 0.96f);
            Text sub = UIKit.Label(root, "Карточная битва героев Доты. Выберите героя:", 34, Color.white);
            UIKit.Anchor(sub.rectTransform, 0, 0.68f, 1, 0.78f);

            RectTransform row = UIKit.Rect(root, "Heroes");
            UIKit.Anchor(row, 0.03f, 0.2f, 0.97f, 0.66f);
            UIKit.Row(row, 40);

            foreach (HeroDef h in CardDB.Heroes)
            {
                HeroDef hero = h;
                RectTransform card = UIKit.Fixed(row, hero.Name, 330, 400);
                Image frame = card.gameObject.AddComponent<Image>();
                frame.color = FrameIdle;
                Button b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = frame;
                b.onClick.AddListener(() => StartGame(hero));

                Image inner = UIKit.Panel(card, "Inner", hero.Color);
                UIKit.Inset(inner.rectTransform, 6);
                UIKit.ArtBox(inner.transform, "hero_" + hero.Id, hero.Name, hero.Color, 0.15f, 0.42f, 0.85f, 0.82f);
                Text name = UIKit.Label(inner.transform, hero.Name, 42, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Anchor(name.rectTransform, 0.03f, 0.83f, 0.97f, 0.98f);
                Text power = UIKit.Label(inner.transform,
                    "<color=#ffd75a>Сила героя (2): " + hero.PowerName + "</color>\n" + hero.PowerText, 24, Color.white);
                UIKit.Anchor(power.rectTransform, 0.05f, 0.05f, 0.95f, 0.4f);
            }

            Text hint = UIKit.Label(root,
                "Правила как в Hearthstone: мана растёт каждый ход, существа атакуют со следующего хода, провокация закрывает героя.",
                22, new Color(1, 1, 1, 0.6f));
            UIKit.Anchor(hint.rectTransform, 0.05f, 0.04f, 0.95f, 0.14f);
        }

        void StartGame(HeroDef heroDef)
        {
            List<HeroDef> others = CardDB.Heroes.FindAll(x => x != heroDef);
            HeroDef aiHero = others[Random.Range(0, others.Count)];

            game = new GameLogic(heroDef, aiHero);
            game.OnLog += AddLog;
            log.Clear();
            gameOverShown = false;
            aiBusy = false;
            BuildGameScreen();
            game.Start();
            ResetMode();
            Refresh();

            if (game.Current == game.Ai) StartCoroutine(AiTurn());
            else ShowBanner("Ваш ход");
        }

        // ======================= Игровой экран =======================

        void BuildGameScreen()
        {
            UIKit.Clear(root);
            UIKit.Panel(root, "Background", BgColor);

            Image board = UIKit.Panel(root, "Board", BoardColor);
            UIKit.Anchor(board.rectTransform, 0.175f, 0.32f, 0.855f, 0.80f);
            Image divider = UIKit.Panel(root, "Divider", new Color(0.55f, 0.42f, 0.22f, 0.7f));
            UIKit.Anchor(divider.rectTransform, 0.19f, 0.5575f, 0.84f, 0.5605f);

            // Журнал событий
            Image logPanel = UIKit.Panel(root, "Log", new Color(0, 0, 0, 0.35f));
            UIKit.Anchor(logPanel.rectTransform, 0.006f, 0.22f, 0.168f, 0.80f);
            Text logTitle = UIKit.Label(logPanel.transform, "Журнал", 22, Gold, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(logTitle.rectTransform, 0, 0.93f, 1, 1);
            logText = UIKit.Label(logPanel.transform, "", 15, new Color(0.9f, 0.9f, 0.9f), TextAnchor.LowerLeft);
            UIKit.Anchor(logText.rectTransform, 0.05f, 0.01f, 0.95f, 0.92f);
            // Новые строки снизу, старые уезжают вверх и обрезаются маской
            logPanel.gameObject.AddComponent<RectMask2D>();

            enemyHeroArea = Area("EnemyHero", 0.2f, 0.81f, 0.83f, 0.995f, 20);
            enemyBoardArea = Area("EnemyBoard", 0.18f, 0.565f, 0.85f, 0.795f, 12);
            playerBoardArea = Area("PlayerBoard", 0.18f, 0.325f, 0.85f, 0.553f, 12);
            playerHeroArea = Area("PlayerHero", 0.2f, 0.197f, 0.83f, 0.318f, 20);
            handArea = Area("Hand", 0.17f, 0.0f, 0.86f, 0.19f, 8);
            handLayout = handArea.GetComponent<HorizontalLayoutGroup>();

            previewArea = UIKit.Rect(root, "Preview");
            UIKit.Anchor(previewArea, 0.865f, 0.58f, 0.995f, 0.99f);

            statusText = UIKit.Label(root, "", 21, Color.white);
            UIKit.Anchor(statusText.rectTransform, 0.862f, 0.42f, 0.998f, 0.57f);

            RectTransform endHolder = UIKit.Rect(root, "EndTurn");
            UIKit.Anchor(endHolder, 0.875f, 0.33f, 0.985f, 0.41f);
            endTurnButton = UIKit.MakeButton(endHolder, "Конец хода", new Color(0.75f, 0.55f, 0.1f), OnEndTurnClick, 28);
            endTurnLabel = endTurnButton.GetComponentInChildren<Text>();

            RectTransform cancelHolder = UIKit.Rect(root, "Cancel");
            UIKit.Anchor(cancelHolder, 0.885f, 0.245f, 0.975f, 0.305f);
            UIKit.MakeButton(cancelHolder, "Отмена", new Color(0.4f, 0.4f, 0.45f), OnCancelClick, 22);
            cancelButton = cancelHolder.gameObject;

            banner = UIKit.Panel(root, "Banner", new Color(0, 0, 0, 0.75f)).gameObject;
            UIKit.Anchor((RectTransform)banner.transform, 0.3f, 0.45f, 0.7f, 0.57f);
            bannerText = UIKit.Label(banner.transform, "", 60, Gold, TextAnchor.MiddleCenter, true);
            banner.SetActive(false);
        }

        RectTransform Area(string name, float x0, float y0, float x1, float y1, float spacing)
        {
            RectTransform rt = UIKit.Rect(root, name);
            UIKit.Anchor(rt, x0, y0, x1, y1);
            UIKit.Row(rt, spacing);
            return rt;
        }

        void Refresh()
        {
            if (game == null) return;
            UIKit.Clear(enemyHeroArea);
            UIKit.Clear(enemyBoardArea);
            UIKit.Clear(playerBoardArea);
            UIKit.Clear(playerHeroArea);
            UIKit.Clear(handArea);
            UIKit.Clear(previewArea);

            BuildHeroRow(enemyHeroArea, game.Ai);
            foreach (Minion m in game.Ai.Board) BuildMinion(enemyBoardArea, m);
            foreach (Minion m in game.Human.Board) BuildMinion(playerBoardArea, m);
            BuildHeroRow(playerHeroArea, game.Human);
            BuildHand();

            bool myTurn = HumanCanAct;
            endTurnButton.interactable = myTurn;
            endTurnLabel.text = myTurn ? "Конец хода" : "Ход врага";
            cancelButton.SetActive(mode != Mode.Idle);
            statusText.text = DefaultStatus();
        }

        string DefaultStatus()
        {
            if (game.IsOver) return "";
            if (!HumanCanAct) return "Противник думает...";
            switch (mode)
            {
                case Mode.CardTarget: return "Выберите цель для «" + game.Human.Hand[pendingCard].Name + "»";
                case Mode.PowerTarget: return "Выберите цель для силы героя";
                case Mode.Attack: return "Кого атакует " + attacker.Name + "?";
                default: return "Ваш ход.\nНажмите на карту, чтобы сыграть, или на своё существо, чтобы атаковать.";
            }
        }

        void Hint(string text)
        {
            statusText.text = "<color=#ff9a5a>" + text + "</color>";
        }

        // ---------- Герой: портрет, сила, мана ----------

        void BuildHeroRow(RectTransform area, PlayerState p)
        {
            Hero hero = p.Hero;

            // Портрет
            RectTransform fr = UIKit.Fixed(area, "Hero", 260, 118);
            Image frame = fr.gameObject.AddComponent<Image>();
            frame.color = FrameColor(hero);
            Button b = fr.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.onClick.AddListener(() => OnCharacterClick(hero));

            Image inner = UIKit.Panel(fr, "Inner", hero.Def.Color);
            UIKit.Inset(inner.rectTransform, 5);
            UIKit.ArtBox(inner.transform, "hero_" + hero.Def.Id, hero.Def.Name, hero.Def.Color, 0.03f, 0.07f, 0.38f, 0.93f);
            Text name = UIKit.Label(inner.transform, hero.Def.Name, 26, Color.white, TextAnchor.MiddleLeft, true);
            UIKit.Anchor(name.rectTransform, 0.42f, 0.5f, 1f, 0.95f);
            Text owner = UIKit.Label(inner.transform, p.Name, 17, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft);
            UIKit.Anchor(owner.rectTransform, 0.42f, 0.25f, 1f, 0.5f);
            UIKit.Badge(inner.transform, new Vector2(1, 0), new Vector2(-28, 28), 50, HealthRed, hero.Health.ToString(), 24);
            if (hero.Armor > 0)
                UIKit.Badge(inner.transform, new Vector2(1, 0), new Vector2(-80, 24), 40, new Color(0.45f, 0.48f, 0.52f), hero.Armor.ToString(), 20);

            // Сила героя
            bool isHuman = p == game.Human;
            bool usable = isHuman && HumanCanAct && game.CanUsePower(p);
            RectTransform pr = UIKit.Fixed(area, "Power", 160, 108);
            Image pframe = pr.gameObject.AddComponent<Image>();
            pframe.color = isHuman && mode == Mode.PowerTarget ? FrameSelected : (usable ? FrameReady : FrameIdle);
            if (isHuman)
            {
                Button pb = pr.gameObject.AddComponent<Button>();
                pb.targetGraphic = pframe;
                pb.onClick.AddListener(OnPowerClick);
            }
            Image pinner = UIKit.Panel(pr, "Inner", p.PowerUsed ? new Color(0.2f, 0.2f, 0.2f) : new Color(0.16f, 0.14f, 0.22f));
            UIKit.Inset(pinner.rectTransform, 4);
            Text ptext = UIKit.Label(pinner.transform,
                "<b>" + hero.Def.PowerName + "</b>\n<size=13>" + hero.Def.PowerText + "</size>",
                16, p.PowerUsed ? Color.gray : Color.white, TextAnchor.MiddleCenter, false, true);
            UIKit.Anchor(ptext.rectTransform, 0.08f, 0.05f, 0.95f, 0.8f);
            UIKit.Badge(pinner.transform, new Vector2(0.5f, 1), new Vector2(0, -14), 30, ManaBlue, GameLogic.HeroPowerCost.ToString(), 16);

            // Мана и карты
            RectTransform ir = UIKit.Fixed(area, "Info", 250, 108);
            string crystals = "<color=#4fa3ff>" + new string('●', p.Mana) + "</color>"
                + "<color=#555a66>" + new string('●', Mathf.Max(0, p.MaxMana - p.Mana)) + "</color>";
            UIKit.Label(ir, "Мана: " + p.Mana + "/" + p.MaxMana + "\n" + crystals
                + "\nРука: " + p.Hand.Count + "   Колода: " + p.Deck.Count, 20, Color.white, TextAnchor.MiddleLeft);
        }

        // ---------- Существо на столе ----------

        void BuildMinion(RectTransform area, Minion m)
        {
            RectTransform fr = UIKit.Fixed(area, m.Name, 130, 170);
            Image frame = fr.gameObject.AddComponent<Image>();
            frame.color = FrameColor(m);
            Button b = fr.gameObject.AddComponent<Button>();
            b.targetGraphic = frame;
            b.onClick.AddListener(() => OnCharacterClick(m));
            fr.gameObject.AddComponent<HoverHandler>().Enter = () => ShowPreview(m.Def);

            Image inner = UIKit.Panel(fr, "Inner", m.Def.Color);
            UIKit.Inset(inner.rectTransform, m.Taunt ? 9 : 5);
            UIKit.ArtBox(inner.transform, m.Def.Id, m.Name, m.Def.Color, 0.06f, 0.42f, 0.94f, 0.95f);

            if (m.Sleeping && !m.Charge && m.Owner == game.Current)
            {
                Text zzz = UIKit.Label(inner.transform, "Zzz", 22, new Color(0.8f, 0.9f, 1f), TextAnchor.UpperRight, true);
                UIKit.Anchor(zzz.rectTransform, 0.1f, 0.7f, 0.92f, 0.94f);
            }

            Text name = UIKit.Label(inner.transform, m.Name, 15, Color.white, TextAnchor.MiddleCenter, true, true);
            UIKit.Anchor(name.rectTransform, 0.03f, 0.25f, 0.97f, 0.42f);

            List<string> keys = new List<string>();
            if (m.Taunt) keys.Add("Провокация");
            if (m.Shield) keys.Add("Щит");
            if (m.Charge) keys.Add("Рывок");
            if (m.Def.Trigger == Trigger.Deathrattle) keys.Add("Хрип");
            Text kw = UIKit.Label(inner.transform, string.Join(" ", keys.ToArray()), 12, Gold, TextAnchor.MiddleCenter, false, true);
            UIKit.Anchor(kw.rectTransform, 0.25f, 0.02f, 0.75f, 0.25f);

            if (m.Shield)
            {
                Image glow = UIKit.Panel(inner.transform, "Shield", new Color(1f, 0.85f, 0.2f, 0.22f));
                UIKit.Stretch(glow.rectTransform);
            }

            UIKit.Badge(inner.transform, Vector2.zero, new Vector2(20, 20), 40, AttackYellow, m.Attack.ToString(), 20);
            bool damaged = m.Health < m.MaxHealth;
            Text hp = UIKit.Badge(inner.transform, new Vector2(1, 0), new Vector2(-20, 20), 40,
                damaged ? new Color(0.45f, 0.05f, 0.05f) : HealthRed, m.Health.ToString(), 20);
            if (damaged) hp.color = new Color(1f, 0.45f, 0.45f);
            else if (m.Health > m.Def.Health) hp.color = new Color(0.6f, 1f, 0.6f);
        }

        // ---------- Карты в руке ----------

        void BuildHand()
        {
            List<CardDef> hand = game.Human.Hand;
            handLayout.spacing = hand.Count > 8 ? -30 : 8;
            for (int i = 0; i < hand.Count; i++)
            {
                int index = i;
                CardDef c = hand[i];
                Color frameColor = FrameIdle;
                if (mode == Mode.CardTarget && pendingCard == i) frameColor = FrameSelected;
                else if (HumanCanAct && game.CanPlay(game.Human, c)) frameColor = FrameReady;

                RectTransform card = BuildCardFace(handArea, c, frameColor);
                Button b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card.GetComponent<Image>();
                b.onClick.AddListener(() => OnHandCardClick(index));
                card.gameObject.AddComponent<HoverHandler>().Enter = () => ShowPreview(c);
            }
        }

        RectTransform BuildCardFace(Transform parent, CardDef c, Color frameColor)
        {
            RectTransform fr = UIKit.Fixed(parent, c.Name, 145, 200);
            Image frame = fr.gameObject.AddComponent<Image>();
            frame.color = frameColor;

            Image inner = UIKit.Panel(fr, "Inner", c.Color);
            UIKit.Inset(inner.rectTransform, 4);
            UIKit.ArtBox(inner.transform, c.Id, c.Name, c.Color, 0.08f, 0.52f, 0.92f, 0.95f);

            Text name = UIKit.Label(inner.transform, c.Name, 15, Color.white, TextAnchor.MiddleCenter, true, true);
            UIKit.Anchor(name.rectTransform, 0.03f, 0.38f, 0.97f, 0.52f);
            Text text = UIKit.Label(inner.transform, c.Text, 12, new Color(1, 1, 1, 0.92f), TextAnchor.UpperCenter, false, true);
            UIKit.Anchor(text.rectTransform, 0.06f, 0.14f, 0.94f, 0.38f);

            UIKit.Badge(inner.transform, new Vector2(0, 1), new Vector2(17, -17), 38, ManaBlue, c.Cost.ToString(), 20);
            if (c.Type == CardType.Minion)
            {
                UIKit.Badge(inner.transform, Vector2.zero, new Vector2(17, 17), 36, AttackYellow, c.Attack.ToString(), 18);
                UIKit.Badge(inner.transform, new Vector2(1, 0), new Vector2(-17, 17), 36, HealthRed, c.Health.ToString(), 18);
            }
            else
            {
                Text type = UIKit.Label(inner.transform, c.Color == CardDB.Item ? "Предмет" : "Способность", 11,
                    new Color(1, 1, 1, 0.6f));
                UIKit.Anchor(type.rectTransform, 0, 0.02f, 1, 0.13f);
            }
            return fr;
        }

        void ShowPreview(CardDef c)
        {
            if (previewArea == null) return;
            UIKit.Clear(previewArea);
            RectTransform card = BuildCardFace(previewArea, c, FrameIdle);
            card.localScale = Vector3.one * 1.5f;
            CanvasGroup cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
        }

        Color FrameColor(Character c)
        {
            if (mode == Mode.Attack && c == attacker) return FrameSelected;
            if (IsHighlightedTarget(c)) return FrameTarget;
            Minion m = c as Minion;
            if (mode == Mode.Idle && HumanCanAct && m != null && m.Owner == game.Human && m.CanAttack) return FrameReady;
            if (m != null && m.Taunt) return FrameTaunt;
            return FrameIdle;
        }

        bool IsHighlightedTarget(Character c)
        {
            if (!HumanCanAct) return false;
            switch (mode)
            {
                case Mode.CardTarget: return game.IsValidTarget(game.Human, game.Human.Hand[pendingCard].Target, c);
                case Mode.PowerTarget: return game.IsValidTarget(game.Human, game.Human.Hero.Def.Target, c);
                case Mode.Attack: return game.CanAttackTarget(attacker, c);
                default: return false;
            }
        }

        // ======================= Клики игрока =======================

        void OnHandCardClick(int index)
        {
            if (!HumanCanAct) return;
            if (mode == Mode.CardTarget && pendingCard == index)
            {
                OnCancelClick();
                return;
            }

            PlayerState me = game.Human;
            CardDef c = me.Hand[index];
            ResetMode();
            if (!game.CanPlay(me, c))
            {
                Refresh();
                if (c.Cost > me.Mana) Hint("Не хватает маны");
                else if (c.Type == CardType.Minion) Hint("На столе нет места");
                else Hint("Нет подходящей цели");
                return;
            }

            if (game.NeedsTarget(me, c))
            {
                mode = Mode.CardTarget;
                pendingCard = index;
                Refresh();
                return;
            }

            game.PlayCard(me, index, null);
            AfterAction();
        }

        void OnPowerClick()
        {
            if (!HumanCanAct) return;
            if (mode == Mode.PowerTarget)
            {
                OnCancelClick();
                return;
            }

            PlayerState me = game.Human;
            ResetMode();
            if (!game.CanUsePower(me))
            {
                Refresh();
                Hint(me.PowerUsed ? "Сила героя уже использована в этом ходу" : "Нельзя использовать силу героя");
                return;
            }
            if (me.Hero.Def.Target != TargetType.None)
            {
                mode = Mode.PowerTarget;
                Refresh();
                return;
            }
            game.UsePower(me, null);
            AfterAction();
        }

        void OnCharacterClick(Character c)
        {
            if (!HumanCanAct) return;
            PlayerState me = game.Human;
            Minion m = c as Minion;

            switch (mode)
            {
                case Mode.CardTarget:
                    if (game.PlayCard(me, pendingCard, c))
                    {
                        ResetMode();
                        AfterAction();
                    }
                    else Hint("Сюда нельзя");
                    return;

                case Mode.PowerTarget:
                    if (game.UsePower(me, c))
                    {
                        ResetMode();
                        AfterAction();
                    }
                    else Hint("Сюда нельзя");
                    return;

                case Mode.Attack:
                    if (c == attacker)
                    {
                        OnCancelClick();
                        return;
                    }
                    if (m != null && m.Owner == me)
                    {
                        ResetMode();
                        SelectAttacker(m);
                        return;
                    }
                    if (game.DoAttack(attacker, c))
                    {
                        ResetMode();
                        AfterAction();
                    }
                    else if (game.HasTaunt(me.Opponent)) Hint("Сначала нужно атаковать существо с провокацией");
                    else Hint("Сюда нельзя");
                    return;

                default:
                    if (m != null && m.Owner == me) SelectAttacker(m);
                    else Hint("Сначала выберите своё существо");
                    return;
            }
        }

        void SelectAttacker(Minion m)
        {
            if (!m.CanAttack)
            {
                Refresh();
                if (m.Attack <= 0) Hint(m.Name + " не может атаковать");
                else if (m.AttacksDone > 0) Hint(m.Name + " уже атаковал в этом ходу");
                else Hint(m.Name + " только что призван и атакует со следующего хода");
                return;
            }
            mode = Mode.Attack;
            attacker = m;
            Refresh();
        }

        void OnCancelClick()
        {
            ResetMode();
            Refresh();
        }

        void OnEndTurnClick()
        {
            if (!HumanCanAct) return;
            ResetMode();
            game.EndTurn();
            if (game.IsOver)
            {
                AfterAction();
                return;
            }
            StartCoroutine(AiTurn());
        }

        void ResetMode()
        {
            mode = Mode.Idle;
            pendingCard = -1;
            attacker = null;
        }

        void AfterAction()
        {
            Refresh();
            if (game.IsOver) ShowGameOver();
        }

        // ======================= Ход бота =======================

        IEnumerator AiTurn()
        {
            aiBusy = true;
            ShowBanner("Ход противника");
            Refresh();
            yield return new WaitForSeconds(1.2f);

            for (int safety = 0; safety < 40 && !game.IsOver; safety++)
            {
                if (!AIBrain.DoOneAction(game, game.Ai)) break;
                Refresh();
                yield return new WaitForSeconds(0.9f);
            }

            if (!game.IsOver) game.EndTurn();
            aiBusy = false;
            AfterAction();
            if (!game.IsOver) ShowBanner("Ваш ход");
        }

        // ======================= Сообщения =======================

        void AddLog(string line)
        {
            log.Add("• " + line);
            if (log.Count > 30) log.RemoveAt(0);
            if (logText != null) logText.text = string.Join("\n", log.ToArray());
        }

        void ShowBanner(string text)
        {
            if (banner == null) return;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine(text));
        }

        IEnumerator BannerRoutine(string text)
        {
            bannerText.text = text;
            banner.SetActive(true);
            banner.transform.SetAsLastSibling();
            yield return new WaitForSeconds(1.1f);
            banner.SetActive(false);
        }

        void ShowGameOver()
        {
            if (gameOverShown) return;
            gameOverShown = true;
            StopAllCoroutines();
            aiBusy = false;

            Image overlay = UIKit.Panel(root, "GameOver", new Color(0, 0, 0, 0.82f));
            overlay.raycastTarget = true; // перекрывает клики по столу

            string title;
            Color color;
            if (game.Winner == game.Human) { title = "ПОБЕДА!"; color = Gold; }
            else if (game.Winner == game.Ai) { title = "ПОРАЖЕНИЕ"; color = new Color(1f, 0.35f, 0.3f); }
            else { title = "НИЧЬЯ"; color = Color.white; }

            Text t = UIKit.Label(overlay.transform, title, 120, color, TextAnchor.MiddleCenter, true);
            UIKit.Anchor(t.rectTransform, 0, 0.5f, 1, 0.75f);

            RectTransform holder = UIKit.Rect(overlay.transform, "Again");
            UIKit.Anchor(holder, 0.4f, 0.3f, 0.6f, 0.4f);
            UIKit.MakeButton(holder, "Играть снова", new Color(0.75f, 0.55f, 0.1f), ShowHeroSelect, 34);
        }
    }
}
