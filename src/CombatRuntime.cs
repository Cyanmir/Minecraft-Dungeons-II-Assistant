// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 组织战斗目标、摄像机和玩家就绪状态。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.IO;
using System.Text;

sealed class CombatTarget
{
    public string Id, Name, ActorName, Type;
    public ThreatVector Position;
    public double Distance;
    public bool TargetsPlayer;
}

sealed class CombatCamera
{
    public ThreatVector Position, Rotation;
    public double Fov, Aspect;
    public int Axis;
    public bool Constrain;
    // 把世界坐标投影到当前视口；用于旧瞄准路径，原生路径不移动鼠标。
    public bool Project(ThreatVector position, Rectangle client, out Point screen)
    {
        screen = Point.Empty;
        if (!position.Valid || !Position.Valid || !Rotation.Valid || client.Width < 300 || client.Height < 300 || !CombatNumber(Fov) || Fov < 20 || Fov > 130 || !CombatNumber(Aspect) || Aspect < .5 || Aspect > 4 || (Axis != 0 && Axis != 1))
            return false;
        double pitch = Rotation.X * Math.PI / 180, yaw = Rotation.Y * Math.PI / 180, roll = Rotation.Z * Math.PI / 180;
        var forward = new ThreatVector(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
        var right0 = new ThreatVector(-Math.Sin(yaw), Math.Cos(yaw), 0);
        var up0 = new ThreatVector(-Math.Sin(pitch) * Math.Cos(yaw), -Math.Sin(pitch) * Math.Sin(yaw), Math.Cos(pitch));
        var right = right0 * Math.Cos(roll) - up0 * Math.Sin(roll);
        var up = up0 * Math.Cos(roll) + right0 * Math.Sin(roll);
        var relative = position - Position;
        double depth = Dot(relative, forward);
        if (depth < 10)
            return false;
        double width = client.Width, height = client.Height;
        if (Constrain)
        {
            if (width / height > Aspect)
                width = height * Aspect;
            else
                height = width / Aspect;
        }

        double tan = Math.Tan(Fov * Math.PI / 360), tanX = Axis == 0 ? tan / Aspect * width / height : tan, tanY = Axis == 0 ? tan / Aspect : tan * height / width;
        double x = client.Left + client.Width * .5 + Dot(relative, right) / depth / tanX * width * .5;
        double y = client.Top + client.Height * .5 - Dot(relative, up) / depth / tanY * height * .5;
        // 排除屏幕边缘和快捷栏；这里是瞄准点，不能当成移动指令。
        if (!CombatNumber(x) || !CombatNumber(y) || x < client.Left + (client.Width - width) * .5 + 30 || x > client.Right - (client.Width - width) * .5 - 30 || y < client.Top + (client.Height - height) * .5 + 30 || y > client.Top + (client.Height + height) * .5 - 140)
            return false;
        screen = new Point((int)Math.Round(x), (int)Math.Round(y));
        return true;
    }

    static double Dot(ThreatVector a, ThreatVector b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    // 核对空间/摄像机数值是否为有限合法值。
    static bool CombatNumber(double n)
    {
        return !Double.IsNaN(n) && !Double.IsInfinity(n);
    }
}

sealed class CombatState
{
    // 只读原生属性；未知时显示等待，不把工具筛选上限当成武器攻击距离。
    public double? MeleeRange;
    public bool Known;
    public string Reason, Session, CameraReason, PawnName;
    public ThreatVector Player, PlayerVelocity;
    public double Radius, HalfHeight;
    public int DodgeKey;
    public RollEligibility Roll;
    public CombatCamera Camera;
    public CombatReadiness Readiness;
    public List<CombatTarget> Targets = new List<CombatTarget>();
    public int RootKey;
    public bool LeftMousePrimary;
    // 玩家状态、场景和原生前提都满足时允许尝试动作。
    public bool CanAttempt
    {
        get
        {
            return Known && Readiness != null && Readiness.Known && Readiness.Alive && Readiness.GameplayUiOnly;
        }
    }

    // 旧瞄准路径还需摄像机与投影可用；原生路径不依赖鼠标。
    public bool CanAim
    {
        get
        {
            return CanAttempt && Camera != null;
        }
    }
}

static class CombatActionRule
{
    // 按旧输入路径的投影、范围与目标状态选择近战对象。
    public static CombatTarget AttackTarget(CombatState state, int range)
    {
        return state != null && state.CanAim && state.LeftMousePrimary && state.RootKey != 0 && state.RootKey != state.DodgeKey ? state.Targets.Where(t => t.Distance <= range && Math.Abs(t.Position.Z - state.Player.Z) <= 100).OrderBy(t => t.Distance).FirstOrDefault() : null;
    }

    // 选择旧输入法器路径的可用目标。
    public static CombatTarget ArtifactTarget(CombatState state)
    {
        return state != null && state.CanAttempt ? state.Targets.Where(t => t.TargetsPlayer && t.Distance <= 800 && Math.Abs(t.Position.Z - state.Player.Z) <= 250).OrderBy(t => t.Distance).FirstOrDefault() : null;
    }

    // 按勾选槽位、冷却和灵魂预算生成可发送键集合。
    public static int[] ArtifactKeys(ToolboxSettings settings, CooldownInfo[] cooldowns, float souls, float[] costs)
    {
        // 战斗法器逻辑不使用药水备用；仅因附近有敌人不能授权用药水。
        if (float.IsNaN(souls) || float.IsInfinity(souls) || souls < 0 || costs == null || costs.Length != 3)
            return new int[0];
        var keys = new List<int>();
        float available = souls;
        foreach (int i in Enumerable.Range(0, 3).OrderBy(i => i == settings.PotionSlot ? 0 : 1))
        {
            if (settings.AutoSlots[i] && cooldowns[i].Ready && !float.IsNaN(costs[i]) && !float.IsInfinity(costs[i]) && costs[i] >= 0 && costs[i] <= available)
            {
                keys.Add(settings.Slots[i]);
                available -= costs[i];
            }
        }

        return keys.Distinct().ToArray();
    }

}

sealed partial class HealthReader
{
    CombatCamera ReadCombatCamera(Identity player)
    {
        Identity controller = Token(M.Q(local.Address + Offset(local, "PlayerController", 8)));
        if (M.Q(controller.Address + Offset(controller, "Pawn", 8)) != player.Address)
            throw new Exception("Combat controller changed");
        Identity camera = Token(M.Q(controller.Address + Offset(controller, "PlayerCameraManager", 8)));
        if (!IsA(camera.Class, "PlayerCameraManager") || M.Q(camera.Address + Offset(camera, "PCOwner", 8)) != controller.Address)
            throw new Exception("Combat camera owner invalid");
        long viewTarget = camera.Address + Offset(camera, "ViewTarget", 2272) + Prop(Struct("TViewTarget", 2272), "Target", 8).Offset;
        long targetAddress = M.Q(viewTarget);
        string targetClass = "none";
        try
        {
            targetClass = ClassName(Token(targetAddress).Class);
        }
        catch
        {
        }

        AdaptationRecord.Set("camera.view.target", targetClass + "; local pawn=" + (targetAddress == player.Address) + "; local controller=" + (targetAddress == controller.Address));
        if (targetAddress != player.Address && targetAddress != controller.Address)
            throw new Exception("Combat camera is not viewing the local player (target=" + targetClass + ")");
        long cacheType = Struct("CameraCacheEntry", 2256), view = Struct("MinimalViewInfo", 2240);
        long cache = camera.Address + Offset(camera, "CameraCachePrivate", 2256), pov = cache + Prop(cacheType, "POV", 2240).Offset;
        int projection = Prop(view, "ProjectionMode", 1).Offset;
        long off = pov + Prop(view, "OffCenterProjectionOffset", 16).Offset;
        byte[] offset = M.Read(off, 16);
        if (M.Read(pov + projection, 1)[0] != 0 || Math.Abs(BitConverter.ToDouble(offset, 0)) > .00001 || Math.Abs(BitConverter.ToDouble(offset, 8)) > .00001)
            throw new Exception("Combat camera projection unsupported");
        var result = new CombatCamera
        {
            Position = Vector(pov + Prop(view, "Location", 24).Offset),
            Rotation = Vector(pov + Prop(view, "Rotation", 24).Offset),
            Fov = M.F(pov + Prop(view, "FOV", 4).Offset),
            Aspect = M.F(pov + Prop(view, "AspectRatio", 4).Offset),
            Axis = M.Read(local.Address + Offset(local, "AspectRatioAxisConstraint", 1), 1)[0]
        };
        Property flag = Prop(view, "bConstrainAspectRatio", 1);
        byte[] meta = M.Read(flag.Field + 120, 4);
        if (meta[0] != 1 || meta[1] > 8 || meta[3] == 0)
            throw new Exception("Camera boolean schema invalid");
        result.Constrain = (M.Read(pov + flag.Offset + meta[1], 1)[0] & meta[3]) != 0;
        if (!Valid(camera) || !Valid(controller) || M.Q(viewTarget) != targetAddress || M.Q(controller.Address + Offset(controller, "Pawn", 8)) != player.Address)
            throw new Exception("Combat view changed");
        return result;
    }

    // 汇总玩家、敌人、摄像机和攻击/菜单状态，供调度层做一次判断。
    public CombatState ReadCombatState()
    {
        var state = new CombatState();
        try
        {
            Identity player = Pawn();
            state.PawnName = LootObjectName(player);
            RefreshThreatActors(0);
            OwnedTagsLayout(player);
            state.Player = Position(player);
            state.Readiness = ReadCombatReadiness(player);
            state.MeleeRange = ReadMeleeRange(player);
            state.Roll = ReadRollEligibility(player, state.Readiness);
            state.Session = InventorySession();
            Identity capsule = Token(M.Q(player.Address + Offset(player, "CapsuleComponent", 8)));
            state.Radius = M.F(capsule.Address + Offset(capsule, "CapsuleRadius", 4));
            state.HalfHeight = M.F(capsule.Address + Offset(capsule, "CapsuleHalfHeight", 4));
            if (state.Radius < 5 || state.Radius > 200 || state.HalfHeight < state.Radius || state.HalfHeight > 400)
                throw new Exception("Combat capsule invalid");
            Identity movement = Token(M.Q(player.Address + Offset(player, "CharacterMovement", 8)));
            state.PlayerVelocity = Vector(movement.Address + Offset(movement, "Velocity", 24));
            Identity level = Token(M.Q(player.Address + 32)), world = Token(M.Q(level.Address + Offset(level, "OwningWorld", 8)));
            if (!IsA(level.Class, "Level") || !IsA(world.Class, "World"))
                throw new Exception("Combat world invalid");
            foreach (Identity actor in threatActors.Values.ToArray())
                try
                {
                    if (ThreatClass(actor.Class) != 1 || !Valid(actor) || ReadBool(actor, "bHidden") || !ReadBool(actor, "bActorEnableCollision") || ReadBool(actor, "bActorIsBeingDestroyed"))
                        continue;
                    Identity actorLevel = Token(M.Q(actor.Address + 32));
                    if (!IsA(actorLevel.Class, "Level") || M.Q(actorLevel.Address + Offset(actorLevel, "OwningWorld", 8)) != world.Address)
                        continue;
                    ThreatVector pos = Position(actor);
                    double distance = (pos - state.Player).Length;
                    if (distance > 800)
                        continue;
                    var tags = OwnedTags(actor);
                    if (!ThreatRule.HostileTeam(tags) || !tags.Contains("SW.State.Life.Alive"))
                        continue;
                    Identity target = Weak(actor.Address + Offset(actor, "ReplicatedCurrentTargetActor", 8));
                    if (!Valid(actor))
                        continue;
                    state.Targets.Add(new CombatTarget { Id = actor.Index + ":" + actor.Serial, Name = ClassName(actor.Class), ActorName = LootObjectName(actor), Type = Name(M.I(actor.Address + Offset(actor, "TypeInfo", 16) + Prop(Struct("SWTypeInfo", 16), "TypeTag", 8).Offset)), Position = pos, Distance = distance, TargetsPlayer = target != null && Valid(target) && target.Address == player.Address });
                }
                catch
                {
                }

            var bindings = AllKeyboardBindings();
            string primary, root, dodge;
            if (bindings.TryGetValue("DirectionalDodge", out dodge))
            {
                state.DodgeKey = GameActionBindings.KeyboardCode(dodge);
                if (!ToolboxInput.Allowed(state.DodgeKey) || bindings.Any(b => b.Key != "DirectionalDodge" && b.Value == dodge))
                    state.DodgeKey = 0;
            }

            state.LeftMousePrimary = bindings.TryGetValue("PrimaryAction", out primary) && primary == "LeftMouseButton";
            if (bindings.TryGetValue("Root", out root))
            {
                state.RootKey = root == "LeftShift" ? 160 : root == "RightShift" ? 161 : GameActionBindings.KeyboardCode(root);
                if (state.RootKey != 160 && state.RootKey != 161 && !ToolboxInput.Allowed(state.RootKey))
                    state.RootKey = 0;
            }

            try
            {
                state.Camera = ReadCombatCamera(player);
                AdaptationRecord.Set("camera.read.reason", "verified local view");
            }
            catch (Exception e)
            {
                state.CameraReason = e.Message;
                AdaptationRecord.Set("camera.read.reason", e.Message);
            }

            if (!Valid(player) || !Valid(world) || M.Q(player.Address + 32) != level.Address || M.Q(level.Address + Offset(level, "OwningWorld", 8)) != world.Address)
                throw new Exception("Combat world changed");
            state.Known = true;
            AdaptationRecord.Set("combat.execution.source", "Hostility: native BaseCharacter.IsHostileTowards tag layout; targets: reflected MobCharacter fields; UI/charges: reflected widgets and attributes; camera: reflected PlayerCameraManager.CameraCachePrivate/ViewTarget; Read-only telemetry; no ProcessEvent or memory writes in this reader. Actions use the selected tool mode: native bridge or legacy keyboard profile.");
        }
        catch (Exception e)
        {
            state.Reason = e.Message;
        }

        return state;
    }

}
