using System;
using System.Collections.Generic;
using System.Linq;
using InElementum.Combat.Managers;
using InElementum.Combat.Model;
using InElementum.Core.UI;
using InElementum.Data.Progression;
using UnityEngine;
using UnityEngine.UIElements;

namespace InElementum.Combat.UI
{
    /// <summary>
    /// The victory screen: a large "Victory!" title over a rule across the screen, the battle's Gold and EXP on
    /// bars with pointed ends, a card per party member who fought with their level and EXP toward the next one,
    /// and an Items Obtained list on the right. A dark band runs along the bottom with the exit prompt. Numbers
    /// count up and bars fill as the screen appears; the field stays visible behind a soft dark wash. Built once per
    /// result; each piece registers an animator that places it for the time since the screen appeared.
    /// </summary>
    public partial class CombatHud
    {
        private const float VictoryLeft = 64f;
        private const float VictoryTitleY = 84f;
        private const float VictoryRuleY = 150f;
        private const float RewardBarTop = 170f;
        private const float RewardBarHeight = 40f;
        private const float RewardBarGap = 10f;
        private const float MemberCardWidth = 250f;
        private const float MemberColumnGap = 34f;
        private const float MemberCardHeight = 66f;
        private const float VictoryBandHeight = 64f;
        private const float ItemRowHeight = 40f;

        private static readonly Color RewardBarFill = new Color(0.62f, 0.7f, 0.78f, 0.42f);
        private static readonly Color RewardBarEdge = new Color(0.86f, 0.9f, 0.96f, 0.35f);
        private static readonly Color ExpBarColor = new Color(0.9f, 0.74f, 0.44f);
        private static readonly Color ExpBarTrack = new Color(0.04f, 0.05f, 0.08f, 0.75f);
        private static readonly Color LevelUpColor = new Color(0.86f, 0.92f, 0.45f);
        private static readonly Color LevelUpArrow = new Color(0.95f, 0.32f, 0.22f);
        private static readonly Color VictoryRule = new Color(0.86f, 0.88f, 0.92f, 0.45f);

        /// <summary>Top of the Items Obtained block: its header, then a rule, then the rows.</summary>
        private const float ItemsTop = RewardBarTop - 4f;
        private const float ItemsRuleY = ItemsTop + 50f;
        private const float ItemsFirstRowY = ItemsRuleY + 14f;

        private VisualElement _victory;
        private readonly List<Action<float>> _victoryAnimators = new List<Action<float>>();
        private BattleResult _victoryFor;
        private float _victoryTime;

        private float VictoryElapsed => Time.unscaledTime - _victoryTime;

        /// <summary>Where the party cards' columns end, in panel units.</summary>
        private static float VictoryColumnRight => VictoryLeft + MemberCardWidth * 2f + MemberColumnGap + 8f;

        private void BuildVictory(VisualElement root)
        {
            _victory = Layer(root);
            _victory.Show(false);
        }

        /// <summary>
        /// While the victory screen is up, the field to the right of the results is the stage: the HUD claims the
        /// card columns on the left, the Items Obtained list above, and the bottom band, and the camera frames the
        /// party still standing in what's left.
        /// </summary>
        private void FrameVictory(CombatCamera camera, float width)
        {
            var rows = Mathf.Max(1, ObtainedEntries().Count);
            var itemsBottom = ItemsFirstRowY + rows * ItemRowHeight + CameraClearance;
            var left = (VictoryColumnRight + 40f) / width;
            var top = Mathf.Min(itemsBottom, ReferenceHeight * 0.55f) / ReferenceHeight;
            var bottom = (VictoryBandHeight + 44f + CameraClearance) / ReferenceHeight;
            camera.HudMargins = new Vector4(left, bottom, 0.03f, top);

            // Reframed every frame, so a shot an action queued as the battle ended can't pull the camera away.
            camera.Frame(Model.Party
                .Where(member => member.IsAlive)
                .Select(member => Manager.GetView(member))
                .Where(view => view != null)
                .Select(view => view.transform));
        }

        private void RefreshVictory(bool shown)
        {
            var result = shown ? Model.Result : null;
            _victory.Show(result != null);
            if (result == null) return;

            // The clock starts the first frame this result is shown, so everything animates in from there.
            if (_victoryFor != result)
            {
                _victoryFor = result;
                _victoryTime = Time.unscaledTime;
                BuildVictoryScreen(result);
            }

            var elapsed = VictoryElapsed;
            foreach (var animate in _victoryAnimators) animate(elapsed);
        }

        private void BuildVictoryScreen(BattleResult result)
        {
            _victory.Clear();
            _victoryAnimators.Clear();

            BuildVictoryBackdrop();
            BuildVictoryTitle();
            BuildRewardBars(result.Rewards);
            BuildMemberCards(result, RewardBarTop + 2f * (RewardBarHeight + RewardBarGap) + 34f);
            BuildItemsObtained();
            BuildVictoryBand();
        }

        private T Add<T>(T element) where T : VisualElement
        {
            _victory.Add(element);
            return element;
        }

        /// <summary>A dark wash that fades in, heavier on the left where the text sits, and a faint lower rule.</summary>
        private void BuildVictoryBackdrop()
        {
            var wash = Add(Hud.Fill(Color.black));
            var shade = Add(Hud.Tinted(UiTextures.FadeRight, Color.clear));
            var low = Add(Hud.Tinted(UiTextures.FadeBoth, Color.clear));
            _victoryAnimators.Add(elapsed =>
            {
                var fade = Mathf.Clamp01(elapsed / 0.4f);
                wash.Place(new Rect(0f, 0f, _width, ReferenceHeight));
                wash.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f * fade);
                shade.Place(new Rect(0f, 0f, _width * 0.7f, ReferenceHeight));
                shade.style.unityBackgroundImageTintColor = new Color(0.01f, 0.02f, 0.05f, 0.55f * fade);
                low.Place(new Rect(0f, ReferenceHeight - VictoryBandHeight - 44f, _width, 1f));
                low.style.unityBackgroundImageTintColor = VictoryRule.Alpha(0.6f * fade);
            });
        }

        private void BuildVictoryTitle()
        {
            const float ruleStart = VictoryLeft - 20f;
            var rule = Add(Hud.Fill(VictoryRule));
            var lead = Add(Hud.Tinted(UiTextures.FadeLeft, VictoryRule));
            lead.Place(new Rect(0f, VictoryRuleY, ruleStart + 1f, 1f));
            var title = Add(Hud.Text("Victory!", "cb-victory-title").Absolute());
            title.style.unityTextAlign = TextAnchor.LowerLeft;

            _victoryAnimators.Add(elapsed =>
            {
                var t = EaseOutCubic(elapsed / 0.45f);
                // The rule sweeps out from under the title to the right edge.
                rule.Place(new Rect(ruleStart, VictoryRuleY, (_width - ruleStart) * t, 1f));
                title.Place(new Rect(VictoryLeft + (1f - t) * -40f, VictoryTitleY, 400f, 70f));
                title.style.opacity = t;
            });
        }

        /// <summary>Gold, then EXP: a bar per reward, its total counting up from zero.</summary>
        private void BuildRewardBars(BattleRewards rewards)
        {
            if (rewards == null) return;

            var rows = new[] { ("GOLD", rewards.Currency), ("EXP", rewards.Experience) };
            for (var i = 0; i < rows.Length; i++)
            {
                var (name, value) = rows[i];
                var index = i;
                var bar = new PointedBar(_victory);
                var label = Add(Hud.Text(name, "cb-caps").Absolute());
                label.style.color = Muted;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                var amount = Add(Hud.Text("0", "cb-victory-value").Absolute());
                amount.style.unityTextAlign = TextAnchor.MiddleLeft;

                _victoryAnimators.Add(elapsed =>
                {
                    var t = EaseOutCubic((elapsed - 0.15f - index * 0.1f) / 0.35f);
                    bar.Show(t > 0f);
                    label.Show(t > 0f);
                    amount.Show(t > 0f);
                    if (t <= 0f) return;

                    var y = RewardBarTop + index * (RewardBarHeight + RewardBarGap);
                    var rect = new Rect(VictoryLeft - 16f + (1f - t) * -60f, y, VictoryColumnRight - VictoryLeft + 16f, RewardBarHeight);
                    bar.Set(rect, RewardBarFill.Alpha(t), RewardBarEdge.Alpha(t));
                    label.Place(new Rect(rect.x + 30f, rect.y, 80f, rect.height));

                    var count = Mathf.Clamp01((elapsed - 0.35f - index * 0.1f) / 0.8f);
                    amount.text = Mathf.RoundToInt(value * EaseOutCubic(count)).ToString("N0");
                    amount.Place(new Rect(rect.x + 120f, rect.y, 200f, rect.height));
                });
            }
        }

        /// <summary>
        /// One card per member who fought, two to a row: a pointed name tab with their level, then EXP toward the
        /// next level over a gold bar. Members who levelled up get a Level Up tag above the card.
        /// </summary>
        private void BuildMemberCards(BattleResult result, float top)
        {
            var members = result.Participants
                .Where(unit => unit.Member != null)
                .GroupBy(unit => unit.Member)
                .Select(group => group.First())
                .ToList();
            if (members.Count == 0) return;

            var rows = (members.Count + 1) / 2;
            var bottom = ReferenceHeight - VictoryBandHeight - 52f;
            var pitch = Mathf.Min(MemberCardHeight + 44f, (bottom - top) / rows);
            var experience = result.Rewards != null ? result.Rewards.Experience : 0;
            var levelUps = Manager.LastVictory != null ? Manager.LastVictory.LevelUps : new List<LevelUp>();

            for (var i = 0; i < members.Count; i++)
            {
                var unit = members[i];
                var index = i;
                var progress = Manager.Party != null ? Manager.Party.GetProgress(unit.Member) : null;
                var levelUp = levelUps.FirstOrDefault(up => up.Member == unit.Member);
                var card = BuildMemberCard(unit, progress, levelUp.Member != null ? levelUp.LevelsGained : 0, experience);

                _victoryAnimators.Add(elapsed =>
                {
                    var t = EaseOutCubic((elapsed - 0.4f - index * 0.08f) / 0.35f);
                    card.Show(t > 0f);
                    if (t <= 0f) return;
                    var x = VictoryLeft + (index % 2) * (MemberCardWidth + MemberColumnGap) + (1f - t) * -40f;
                    card.Move(new Vector2(x, top + (index / 2) * pitch + 20f));
                });
            }
        }

        private VisualElement BuildMemberCard(Combatant unit, CharacterProgress progress, int levelsGained, int experience)
        {
            var card = Add(Hud.Box().Absolute());
            card.style.width = MemberCardWidth;
            card.style.height = MemberCardHeight;

            // Name tab, pointed at both ends.
            var tab = new Rect(0f, 0f, MemberCardWidth, 26f);
            PointedTab(card, tab);
            var mark = new Diamond(10f).Absolute();
            mark.Fill = ElementColor(unit);
            mark.CenterOn(new Vector2(16f, tab.center.y));
            card.Add(mark);
            var name = Text(card, "cb-victory-item", unit.Name);
            name.Place(new Rect(26f, 0f, tab.width - 100f, tab.height));
            name.style.unityTextAlign = TextAnchor.MiddleLeft;

            var levelRow = Hud.Box("cb-row", "cb-level").Absolute();
            levelRow.Place(new Rect(tab.width - 120f, 0f, 106f, tab.height));
            levelRow.Add(Hud.Text("Lv.", "cb-small", "cb-level__tag"));
            levelRow.Add(Hud.Text((progress != null ? progress.Level : 1).ToString(), "cb-stat"));
            card.Add(levelRow);

            if (levelsGained > 0)
            {
                var tag = Hud.Box("cb-row", "cb-level-up").Absolute();
                tag.Place(new Rect(tab.width - 208f, -26f, 200f, 24f));
                var arrow = Hud.Text("▲", "cb-victory-item", "cb-level-up__arrow");
                arrow.style.color = LevelUpArrow;
                var text = Hud.Text(levelsGained > 1 ? $"Level Up ×{levelsGained}" : "Level Up", "cb-victory-item");
                tag.Add(arrow);
                tag.Add(text);
                card.Add(tag);
                _victoryAnimators.Add(_ =>
                {
                    var pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 4f);
                    text.style.color = LevelUpColor * new Color(pulse, pulse, pulse, 1f);
                });
            }

            // EXP toward the next level: the bar fills from where it stood before the battle, or from empty after a level up.
            var maxed = progress == null || progress.IsMaxLevel;
            var current = maxed ? 0 : progress.Xp;
            var next = maxed ? 0 : progress.XpToNextLevel;
            var before = levelsGained > 0 ? 0 : Mathf.Max(0, current - experience);

            var row = new Rect(8f, tab.yMax + 4f, MemberCardWidth - 16f, 20f);
            var expTag = Text(card, "cb-tag", "EXP");
            expTag.Place(row);
            expTag.style.color = Muted;
            expTag.style.unityTextAlign = TextAnchor.MiddleLeft;

            var values = Hud.Box("cb-row", "cb-exp").Absolute();
            values.Place(row);
            var shown = Hud.Text(maxed ? "MAX" : "0", "cb-stat");
            if (maxed) shown.style.color = Accent;
            values.Add(shown);
            if (!maxed)
            {
                var nextText = Hud.Text($"/{next:N0}", "cb-small", "cb-exp__next");
                nextText.style.color = Muted;
                values.Add(nextText);
            }

            card.Add(values);

            var track = Hud.Fill(ExpBarTrack);
            track.Place(new Rect(row.x, row.yMax + 3f, row.width, 7f));
            card.Add(track);
            var fill = Hud.Fill(ExpBarColor);
            card.Add(fill);
            var rule = Hud.Tinted(UiTextures.FadeLeft, VictoryRule);
            rule.Place(new Rect(row.x, row.yMax + 15f, row.width, 1f));
            card.Add(rule);

            _victoryAnimators.Add(elapsed =>
            {
                var t = EaseOutCubic((elapsed - 0.7f) / 0.9f);
                var value = Mathf.RoundToInt(Mathf.Lerp(before, current, t));
                if (!maxed) shown.text = value.ToString("N0");
                var ratio = maxed ? 1f : next > 0 ? Mathf.Clamp01((float)value / next) : 0f;
                fill.Place(new Rect(row.x, row.yMax + 3f, row.width * ratio, 7f));
            });

            return card;
        }

        /// <summary>
        /// The Items Obtained list: material drops with their counts, new pictos, and Luminas learned. Rows that
        /// don't fit are summed up in a last line.
        /// </summary>
        private void BuildItemsObtained()
        {
            var block = Add(Hud.Box().Absolute());
            var header = Text(block, "cb-victory-header", "Items Obtained");
            header.style.unityTextAlign = TextAnchor.MiddleLeft;
            var rule = Hud.Tinted(UiTextures.FadeBoth, VictoryRule);
            block.Add(rule);
            var leftJewel = new Diamond(7f).Absolute();
            leftJewel.SetRim(1f, VictoryRule);
            var rightJewel = new Diamond(7f).Absolute();
            rightJewel.SetRim(1f, VictoryRule);
            block.Add(leftJewel);
            block.Add(rightJewel);

            var entries = ObtainedEntries();
            var rows = new List<VisualElement>();
            Label more = null;
            if (entries.Count == 0)
            {
                var none = Text(block, "cb-victory-item", "Nothing found.");
                none.style.color = Muted;
                none.style.unityTextAlign = TextAnchor.MiddleLeft;
                rows.Add(none);
            }
            else
            {
                foreach (var (name, kind, count, color) in entries)
                {
                    var row = Hud.Box().Absolute();
                    var disc = Ring(26f, 1f, Rail);
                    disc.style.backgroundColor = PanelColor;
                    SizeRing(disc, new Vector2(14f, ItemRowHeight * 0.5f), 26f);
                    var gem = new Diamond(12f).Absolute();
                    gem.Fill = color;
                    gem.CenterOn(new Vector2(14f, ItemRowHeight * 0.5f));
                    row.Add(disc);
                    row.Add(gem);

                    var line = Hud.Box("cb-row", "cb-item-row").Absolute();
                    line.Place(new Rect(36f, 0f, 400f, ItemRowHeight));
                    line.Add(Hud.Text(name, "cb-victory-item"));
                    if (!string.IsNullOrEmpty(kind))
                    {
                        var kindLabel = Hud.Text(kind, "cb-tag", "cb-item-row__kind");
                        kindLabel.style.color = color;
                        line.Add(kindLabel);
                    }

                    row.Add(line);
                    var amount = Text(row, "cb-victory-value", count);
                    amount.style.unityTextAlign = TextAnchor.MiddleRight;
                    amount.name = "amount";
                    block.Add(row);
                    rows.Add(row);
                }

                more = Text(block, "cb-victory-item");
                more.style.color = Muted;
                more.style.unityTextAlign = TextAnchor.MiddleLeft;
            }

            _victoryAnimators.Add(elapsed =>
            {
                var t = EaseOutCubic((elapsed - 0.25f) / 0.4f);
                block.Show(t > 0f);
                if (t <= 0f) return;

                var x = Mathf.Max(VictoryColumnRight + 60f, _width * 0.56f);
                var area = new Rect(x + (1f - t) * 40f, ItemsTop, _width - x - 56f, ReferenceHeight - VictoryBandHeight - 44f - ItemsTop);
                header.Place(new Rect(area.x, area.y, area.width, 40f));
                header.style.opacity = t;
                rule.Place(new Rect(area.x - 30f, ItemsRuleY, area.width + 60f, 1f));
                leftJewel.CenterOn(new Vector2(area.x - 24f, ItemsRuleY + 0.5f));
                rightJewel.CenterOn(new Vector2(area.xMax + 24f, ItemsRuleY + 0.5f));

                var fits = Mathf.Max(1, Mathf.FloorToInt((area.yMax - ItemsFirstRowY) / ItemRowHeight));
                var shownCount = rows.Count > fits && more != null ? fits - 1 : rows.Count;
                for (var i = 0; i < rows.Count; i++)
                {
                    var visible = i < shownCount && (more == null || elapsed >= 0.45f + i * 0.07f);
                    rows[i].Show(visible);
                    if (!visible) continue;
                    var rect = new Rect(area.x + (more == null ? 8f : 0f), ItemsFirstRowY + i * ItemRowHeight, area.width, ItemRowHeight);
                    rows[i].Place(rect);
                    var amount = rows[i].Q<Label>("amount");
                    amount?.Place(new Rect(rect.width - 100f, 0f, 92f, ItemRowHeight));
                }

                if (more == null) return;
                var overflow = shownCount < rows.Count && elapsed >= 0.45f + shownCount * 0.07f;
                more.Show(overflow);
                more.text = $"and {rows.Count - shownCount} more";
                more.Place(new Rect(area.x + 36f, ItemsFirstRowY + shownCount * ItemRowHeight, area.width, ItemRowHeight));
            });
        }

        /// <summary>Everything the battle handed out, in display order: materials summed by type, then new pictos, then Luminas learned.</summary>
        private List<(string name, string kind, string count, Color color)> ObtainedEntries()
        {
            var entries = new List<(string, string, string, Color)>();
            var rewards = Model.Result?.Rewards;
            if (rewards != null)
            {
                foreach (var group in rewards.Materials.Where(drop => drop.material != null).GroupBy(drop => drop.material))
                {
                    var name = string.IsNullOrEmpty(group.Key.DisplayName) ? group.Key.name : group.Key.DisplayName;
                    entries.Add((name, null, group.Sum(drop => drop.amount).ToString("N0"), Rail));
                }
            }

            var victory = Manager.LastVictory;
            if (victory != null)
            {
                entries.AddRange(victory.NewPictos.Select(p => (p.DisplayName, "NEW PICTO", "1", Accent)));
                entries.AddRange(victory.LearnedLuminas.Select(p => (p.DisplayName, "LUMINA LEARNED", string.Empty, SurgeColor)));
            }

            return entries;
        }

        /// <summary>The dark band along the bottom, with the exit prompt at its right. The prompt can be clicked.</summary>
        private void BuildVictoryBand()
        {
            var band = Add(Hud.Fill(Color.black));
            var rule = Add(Hud.Tinted(UiTextures.FadeBoth, VictoryRule));
            var prompt = Add(Hud.Box("cb-row", "cb-victory-prompt").Absolute());
            var key = new KeyPrompt(26f);
            var label = Hud.Text(Manager.ReturnsToExploration ? "Continue" : "Fight again", "cb-victory-item", "cb-victory-prompt__label");
            prompt.Add(key);
            prompt.Add(label);
            Clickable(prompt, () => Manager.ContinueAfterVictory());

            _victoryAnimators.Add(elapsed =>
            {
                var fade = Mathf.Clamp01(elapsed / 0.4f);
                var top = ReferenceHeight - VictoryBandHeight;
                band.Place(new Rect(0f, top, _width, VictoryBandHeight));
                band.style.backgroundColor = new Color(0f, 0f, 0f, 0.92f * fade);
                rule.Place(new Rect(0f, top, _width, 1f));
                rule.style.unityBackgroundImageTintColor = VictoryRule.Alpha(fade);
                key.Set(ConfirmKey, Input.UsingGamepad);
                prompt.style.right = 48f;
                prompt.style.top = top + VictoryBandHeight * 0.5f - 13f;
                prompt.style.height = 26f;
            });
        }

        /// <summary>A name tab: a dark fill pointed at both ends, outlined in a thin light rule.</summary>
        private static void PointedTab(VisualElement parent, Rect tab)
        {
            var point = tab.height * 0.5f;
            var body = new Rect(tab.x + point, tab.y, tab.width - point * 2f, tab.height);
            var fill = new Color(0.05f, 0.06f, 0.1f, 0.78f);

            var middle = Hud.Fill(fill);
            middle.Place(body);
            parent.Add(middle);
            HalfDiamond(parent, new Vector2(body.x, tab.center.y), tab.height, fill, RewardBarEdge, true);
            HalfDiamond(parent, new Vector2(body.xMax, tab.center.y), tab.height, fill, RewardBarEdge, false);

            var topEdge = Hud.Fill(RewardBarEdge);
            topEdge.Place(new Rect(body.x, tab.y, body.width, 1f));
            var bottomEdge = Hud.Fill(RewardBarEdge);
            bottomEdge.Place(new Rect(body.x, tab.yMax - 1f, body.width, 1f));
            parent.Add(topEdge);
            parent.Add(bottomEdge);
        }

        /// <summary>
        /// Half of a diamond <paramref name="size"/> tall whose flat side sits on <paramref name="joint"/>: the left
        /// half when <paramref name="left"/>, pointing left, otherwise the right half. Used as the pointed ends of bars.
        /// </summary>
        private static VisualElement HalfDiamond(VisualElement parent, Vector2 joint, float size, Color fill, Color rim, bool left)
        {
            var half = size * 0.5f;
            var clip = Hud.Box("cb-clip").Absolute();
            clip.Place(new Rect(left ? joint.x - half : joint.x, joint.y - half, half, size));
            var diamond = new Diamond(size).Absolute();
            diamond.Fill = fill;
            diamond.SetRim(1f, rim);
            diamond.Move(new Vector2(left ? 0f : -half, 0f));
            clip.Add(diamond);
            parent.Add(clip);
            return clip;
        }

        /// <summary>A bar with a pointed left end and a fill that fades out to the right, edged along top and bottom.</summary>
        private sealed class PointedBar
        {
            private readonly VisualElement _clip;
            private readonly Diamond _point;
            private readonly VisualElement _body;
            private readonly VisualElement _top;
            private readonly VisualElement _bottom;

            public PointedBar(VisualElement parent)
            {
                _clip = Hud.Box("cb-clip").Absolute();
                _point = new Diamond().Absolute();
                _clip.Add(_point);
                _body = Hud.Tinted(UiTextures.FadeRight, Color.clear);
                _top = Hud.Tinted(UiTextures.FadeRight, Color.clear);
                _bottom = Hud.Tinted(UiTextures.FadeRight, Color.clear);
                parent.Add(_clip);
                parent.Add(_body);
                parent.Add(_top);
                parent.Add(_bottom);
            }

            public void Show(bool shown)
            {
                _clip.Show(shown);
                _body.Show(shown);
                _top.Show(shown);
                _bottom.Show(shown);
            }

            public void Set(Rect bar, Color fill, Color edge)
            {
                var point = bar.height * 0.5f;
                var body = new Rect(bar.x + point, bar.y, bar.width - point, bar.height);
                _clip.Place(new Rect(bar.x, bar.y, point, bar.height));
                _point.Size = bar.height;
                _point.Move(Vector2.zero);
                _point.Fill = fill;
                _point.SetRim(1f, edge);
                _body.Place(body);
                _body.style.unityBackgroundImageTintColor = fill;
                _top.Place(new Rect(body.x, bar.y, body.width, 1f));
                _top.style.unityBackgroundImageTintColor = edge;
                _bottom.Place(new Rect(body.x, bar.yMax - 1f, body.width, 1f));
                _bottom.style.unityBackgroundImageTintColor = edge;
            }
        }
    }
}
