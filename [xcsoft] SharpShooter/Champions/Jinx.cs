﻿using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Collision = LeagueSharp.Common.Collision;

namespace Sharpshooter.Champions
{
    public static class Jinx
    {
        static Obj_AI_Hero Player { get { return ObjectManager.Player; } }
        static Orbwalking.Orbwalker Orbwalker { get { return SharpShooter.Orbwalker; } }

        static Spell Q, W, E, R;

        static bool QisActive { get { return Player.HasBuff("JinxQ", true); } }

        static readonly int DefaultRange = 590;
        static float GetQActiveRange { get { return DefaultRange + ((25 * Q.Level) + 50); } }

        static float WLastCastedTime;
        public static void Load()
        {
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W, 1450f);
            E = new Spell(SpellSlot.E, 900f);
            R = new Spell(SpellSlot.R, 2500f);

            W.SetSkillshot(0.6f, 60f, 3300f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(1.1f, 120f, 1750f, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.6f, 140f, 1700f, false, SkillshotType.SkillshotLine);

            var drawDamageMenu = new MenuItem("Draw_RDamage", "Draw R Damage").SetValue(true);
            var drawFill = new MenuItem("Draw_Fill", "Draw R Damage Fill").SetValue(new Circle(true, Color.FromArgb(90, 255, 169, 4)));

            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseE", "Use E", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Combo").AddItem(new MenuItem("comboUseR", "Use R", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Harass").AddItem(new MenuItem("harassMana", "if Mana % >", true).SetValue(new Slider(50, 0, 100)));

            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Laneclear").AddItem(new MenuItem("laneclearMana", "if Mana % >", true).SetValue(new Slider(60, 0, 100)));

            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseQ", "Use Q", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearUseW", "Use W", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Jungleclear").AddItem(new MenuItem("jungleclearMana", "if Mana % >", true).SetValue(new Slider(20, 0, 100)));

            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("antigapcloser", "Use Anti-Gapcloser", true).SetValue(true));
            SharpShooter.Menu.SubMenu("Misc").AddItem(new MenuItem("AutoE", "Autocast E On Immobile Targets", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingAA", "Real AA Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingW", "W Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingE", "E Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingR", "R Range", true).SetValue(new Circle(true, Color.HotPink)));
            SharpShooter.Menu.SubMenu("Drawings").AddItem(new MenuItem("drawingPTimer", "Passive Timer", true).SetValue(true));

            SharpShooter.Menu.SubMenu("Drawings").AddItem(drawDamageMenu);
            SharpShooter.Menu.SubMenu("Drawings").AddItem(drawFill);

            DamageIndicator.DamageToUnit = GetComboDamage;
            DamageIndicator.Enabled = drawDamageMenu.GetValue<bool>();
            DamageIndicator.Fill = drawFill.GetValue<Circle>().Active;
            DamageIndicator.FillColor = drawFill.GetValue<Circle>().Color;

            drawDamageMenu.ValueChanged +=
            delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                DamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            drawFill.ValueChanged +=
            delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                DamageIndicator.Fill = eventArgs.GetNewValue<Circle>().Active;
                DamageIndicator.FillColor = eventArgs.GetNewValue<Circle>().Color;
            };

            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead)
                return;

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                Combo();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
                Harass();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                Laneclear();
                Jungleclear();
            }

            AutoE();
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;

            var drawingAA = SharpShooter.Menu.Item("drawingAA", true).GetValue<Circle>();
            var drawingW = SharpShooter.Menu.Item("drawingW", true).GetValue<Circle>();
            var drawingE = SharpShooter.Menu.Item("drawingE", true).GetValue<Circle>();
            var drawingR = SharpShooter.Menu.Item("drawingR", true).GetValue<Circle>();

            if (drawingAA.Active)
                Render.Circle.DrawCircle(Player.Position, Orbwalking.GetRealAutoAttackRange(Player), drawingAA.Color);

            if (drawingW.Active && W.IsReady())
                Render.Circle.DrawCircle(Player.Position, W.Range, drawingW.Color);

            if (drawingE.Active && E.IsReady())
                Render.Circle.DrawCircle(Player.Position, E.Range, drawingE.Color);

            if (drawingR.Active && R.IsReady())
                Render.Circle.DrawCircle(Player.Position, R.Range, drawingR.Color);

            if (SharpShooter.Menu.Item("drawingPTimer", true).GetValue<Boolean>())
            {
                foreach (var buff in Player.Buffs)
                {
                    if (buff.Name == "jinxpassivekill")
                    {
                        var targetpos = Drawing.WorldToScreen(Player.Position);
                        Drawing.DrawText(targetpos[0] - 10, targetpos[1], Color.Gold, "" + (buff.EndTime - Game.ClockTime));
                        break;
                    }
                }
            }

            if (SharpShooter.Menu.Item("drawingTarget").GetValue<Boolean>())
            {
                var target = Orbwalker.GetTarget();

                if(target != null)
                {
                    if(QisActive)
                        Render.Circle.DrawCircle(target.Position, 160, Color.Red);
                }
            }

        }

        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!SharpShooter.Menu.Item("antigapcloser", true).GetValue<Boolean>() || Player.IsDead)
                return;

            if (gapcloser.Sender.IsValidTarget(E.Range))
            {
                Render.Circle.DrawCircle(gapcloser.Sender.Position, gapcloser.Sender.BoundingRadius, Color.Gold, 5);
                var targetpos = Drawing.WorldToScreen(gapcloser.Sender.Position);
                Drawing.DrawText(targetpos[0] - 40, targetpos[1] + 20, Color.Gold, "Gapcloser");
            }

            if (E.CanCast(gapcloser.Sender))
                E.Cast(Player);
        }

        static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
            if (unit.IsMe && args.SData.Name == "JinxW")
                WLastCastedTime = Game.ClockTime;
        }

        static void QSwitchForUnit(AttackableUnit Unit)
        {
            if (Unit == null)
            {
                QSwitch(false);
                return;
            }

            if (Utility.CountEnemiesInRange(Unit.Position, 160) >= 2)
            {
                QSwitch(true);
                return;
            }

            if (Unit.IsValidTarget(DefaultRange))
                QSwitch(false);
            else
                QSwitch(true);

        }

        static void QSwitch(Boolean activate)
        {
            if (!Q.IsReady())
                return;

            if (QisActive && Player.IsWindingUp)
                return;

            if (activate && !QisActive)
                Q.Cast();
            else if (!activate && QisActive)
                Q.Cast();
        }

        static void AutoE()
        {
            if (!SharpShooter.Menu.Item("AutoE", true).GetValue<Boolean>())
                return;

            foreach (Obj_AI_Hero target in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(E.Range)))
            {
                if (E.CanCast(target) && UnitIsImmobileUntil(target) >= E.Delay)
                    E.Cast(target);
            }
        }

        static double UnitIsImmobileUntil(Obj_AI_Base unit)
        {
            var result =
                unit.Buffs.Where(
                    buff =>
                        buff.IsActive && Game.Time <= buff.EndTime &&
                        (buff.Type == BuffType.Charm || buff.Type == BuffType.Knockup || buff.Type == BuffType.Stun ||
                         buff.Type == BuffType.Suppression || buff.Type == BuffType.Snare))
                    .Aggregate(0d, (current, buff) => Math.Max(current, buff.EndTime));
            return (result - Game.Time);
        }

        static bool CollisionCheck(Obj_AI_Hero source, Vector3 targetpos, float width)
        {
            var input = new PredictionInput
            {
                Radius = width,
                Unit = source,
            };

            input.CollisionObjects[0] = CollisionableObjects.Heroes;
            input.CollisionObjects[1] = CollisionableObjects.YasuoWall;

            return Collision.GetCollision(new List<Vector3> { targetpos }, input).Count() <= 1;
        }

        static int CountEnemyMinionsInRange(this Vector3 point, float range)
        {
            return ObjectManager.Get<Obj_AI_Minion>().Count(h => h.IsValidTarget(range, true, point));
        }

        static bool aaKillunableCheck(Obj_AI_Base aatarget)
        {
            return !(aatarget.IsValidTarget(Orbwalking.GetRealAutoAttackRange(Player)) && aatarget.Health > Player.GetAutoAttackDamage(aatarget));
        }

        static float GetComboDamage(Obj_AI_Base enemy)
        {
            return R.IsReady() ? R.GetDamage(enemy) : 0;
        }

        static void Combo()
        {
            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("comboUseQ", true).GetValue<Boolean>())
                QSwitchForUnit(TargetSelector.GetTarget(GetQActiveRange + 30, TargetSelector.DamageType.Physical, true));

            if (SharpShooter.Menu.Item("comboUseW", true).GetValue<Boolean>())
            {
                var Wtarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical, true);

                if (W.CanCast(Wtarget) && !Wtarget.IsValidTarget(DefaultRange / 3) && W.GetPrediction(Wtarget).Hitchance >= HitChance.VeryHigh && aaKillunableCheck(Wtarget))
                    W.Cast(Wtarget);
            }

            if (SharpShooter.Menu.Item("comboUseE", true).GetValue<Boolean>())
            {
                var Etarget = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical, false);

                if (E.CanCast(Etarget) && !Etarget.HasBuffOfType(BuffType.SpellImmunity) && E.GetPrediction(Etarget).Hitchance >= HitChance.VeryHigh && aaKillunableCheck(Etarget))
                    E.Cast(Etarget);
            }

            if (SharpShooter.Menu.Item("comboUseR", true).GetValue<Boolean>() && R.IsReady() && WLastCastedTime + W.Delay + 0.2 < Game.ClockTime)
            {
                foreach (Obj_AI_Hero Rtarget in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(R.Range) && !Player.HasBuffOfType(BuffType.SpellShield) && !Player.HasBuffOfType(BuffType.Invulnerability)))
                {
                    var Rpred = R.GetPrediction(Rtarget);

                    if (R.CanCast(Rtarget) && Rpred.Hitchance >= HitChance.High && !Player.IsWindingUp && aaKillunableCheck(Rtarget))
                    {
                        var dis = Player.Distance(Rpred.UnitPosition);
                        double predhealth = HealthPrediction.GetHealthPrediction(Rtarget, (int)(R.Delay + dis / R.Speed) * 1000) + Rtarget.HPRegenRate;

                        if(Rtarget.IsValidTarget(DefaultRange))
                            predhealth -= Player.GetAutoAttackDamage(Rtarget, true) * 2;
                        else
                        if (Rtarget.IsValidTarget(GetQActiveRange - 50))
                            predhealth -= Player.GetAutoAttackDamage(Rtarget, true);

                        if (CollisionCheck(Player, Rpred.UnitPosition, R.Width))
                        {
                            if (predhealth <= R.GetDamage(Rtarget))
                            {
                                R.Cast(Rtarget);
                                break;
                            }
                        }
                        else
                            if (predhealth <= R.GetDamage(Rtarget) * 0.8)
                            {
                                foreach (Obj_AI_Hero ExplosionTarget in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget(R.Range)))
                                {
                                    var pred = R.GetPrediction(ExplosionTarget);

                                    if (pred.Hitchance >= HitChance.High && CollisionCheck(Player, pred.UnitPosition, R.Width) && Rtarget.IsValidTarget(235, true, ExplosionTarget.ServerPosition))
                                    {
                                        R.Cast(ExplosionTarget);
                                        break;
                                    }
                                }
                            }
                    }
                }
            }
        }

        static void Harass()
        {
            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("harassMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>())
                    QSwitch(false);

                return;
            }

            if (!Orbwalking.CanMove(1))
                return;

            if (SharpShooter.Menu.Item("harassUseQ", true).GetValue<Boolean>() && Q.IsReady())
                QSwitchForUnit(TargetSelector.GetTarget(GetQActiveRange + 30, TargetSelector.DamageType.Physical, true));

            if (SharpShooter.Menu.Item("harassUseW", true).GetValue<Boolean>() && W.IsReady())
            {
                var Wtarget = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical, true);

                if (W.CanCast(Wtarget) && W.GetPrediction(Wtarget).Hitchance >= HitChance.VeryHigh)
                    W.Cast(Wtarget);
            }
        }

        static void Laneclear()
        {
            var Minions = MinionManager.GetMinions(Player.ServerPosition, GetQActiveRange, MinionTypes.All, MinionTeam.Enemy);

            if (Minions.Count <= 0)
            {
                QSwitch(false);
                return;
            }

            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("laneclearMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>()) 
                    QSwitch(false);

                return;
            }

            if (SharpShooter.Menu.Item("laneclearUseQ", true).GetValue<Boolean>())
            {
                var target = Orbwalker.GetTarget();

                if(target != null)
                    QSwitch((CountEnemyMinionsInRange(target.Position, 160) >= 2));
            }
                
        }

        static void Jungleclear()
        {
            var Mobs = MinionManager.GetMinions(Player.ServerPosition, GetQActiveRange, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (Mobs.Count <= 0)
            {
                QSwitch(false);
                return;
            }

            if (!(Player.ManaPercentage() > SharpShooter.Menu.Item("jungleclearMana", true).GetValue<Slider>().Value))
            {
                if (SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>())
                    QSwitch(false);

                return;
            }

            if (SharpShooter.Menu.Item("jungleclearUseQ", true).GetValue<Boolean>() && Q.IsReady())
            {
                var target = Orbwalker.GetTarget();

                if (target != null)
                    QSwitch((CountEnemyMinionsInRange(target.Position, 160) >= 2));
            }

            if (!Orbwalking.CanMove(1))
                return;

            if (W.CanCast(Mobs[0]) && SharpShooter.Menu.Item("jungleclearUseW", true).GetValue<Boolean>())
                W.Cast(Mobs[0]);
        }

    }
}
