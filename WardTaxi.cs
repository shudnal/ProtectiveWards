using HarmonyLib;
using SoftReferenceableAssets;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ProtectiveWards.ProtectiveWards;

namespace ProtectiveWards
{
    internal static class WardTaxi
    {
        private const string WardTaxiStatusEffectName = "PW_WardTaxiStatus";
        internal static readonly int wardTaxiStatusHash = WardTaxiStatusEffectName.GetStableHashCode();
        private static readonly int slowFallHash = "SlowFall".GetStableHashCode();

        private const float MinTaxiDistance = 300f;
        private const float MaxTaxiOfferingSourceDistance = 50f;
        private const float TaxiStartAltitude = 30f;
        private const float TaxiDescentAltitude = 150f;
        private const float TaxiProgressDistance = 3f;
        private const float TaxiStuckSeconds = 10f;
        private const int TaxiOutboundWaitSeconds = 10;

        private const string EikthyrAltarLocation = "Eikthyrnir";
        private const string ElderAltarLocation = "GDKing";
        private const string BonemassAltarLocation = "Bonemass";
        private const string ModerAltarLocation = "Dragonqueen";
        private const string YagluthAltarLocation = "GoblinKing";
        private const string QueenAltarLocation = "Mistlands_DvergrBossEntrance1";
        private const string FaderAltarLocation = "FaderLocation";

        private enum TaxiState
        {
            Idle,
            WaitingOutbound,
            OutboundFlight,
            FallingBeforeReturn,
            WaitingReturn,
            WaitingRetry,
            ReturnFlight,
            Falling
        }

        internal struct TaxiOffer
        {
            public string LocationName;
            public string ItemName;
            public int Stack;
            public bool ConsumeItem;
        }

        private static TaxiState s_state = TaxiState.Idle;
        private static Player s_player;
        private static Vector3 s_targetPosition;
        private static Vector3 s_returnPosition;
        private static TaxiOffer s_offer;
        private static bool s_returnBack;
        private static bool s_waitingForValkyrieSpawn;
        private static bool s_statusExpected;
        private static bool s_controlledStatusRemoval;
        private static ZDOID s_activeValkyrieId = ZDOID.None;
        private static Vector3 s_lastProgressPosition;
        private static float s_lastProgressTime;

        internal sealed class SE_WardTaxi : SE_Stats
        {
            public override void UpdateStatusEffect(float dt)
            {
                base.UpdateStatusEffect(dt);
                WardTaxi.UpdateStatusEffect(this, dt);
            }

            public override void Stop()
            {
                WardTaxi.OnStatusStopped(this);
                base.Stop();
            }
        }

        internal static bool IsTaxiOfferingItem(string itemName)
        {
            return TryGetTaxiOffer(itemName, out _);
        }

        internal static void TryStartTaxiFromOffering(ItemDrop.ItemData item, Player initiator, Vector3 offeringPosition)
        {
            if (item == null || item.m_shared == null || initiator == null)
                return;

            if (!TryGetTaxiOffer(item.m_shared.m_name, out TaxiOffer offer))
                return;

            if (initiator.IsEncumbered())
            {
                initiator.Message(MessageHud.MessageType.Center, "$se_encumbered_start".Localize());
                return;
            }

            if (!WardOfferings.IsTeleportable(initiator))
            {
                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_notravel".Localize());
                return;
            }

            Vector3 location = Vector3.zero;
            if (TryGetFoundLocation(offer.LocationName, offeringPosition, ref location))
            {
                TryStartPassage(initiator, offeringPosition, location, offer);
            }
            else if (ZNet.instance != null && !ZNet.instance.IsServer())
            {
                ClosestLocationRequest(offer, offeringPosition);
            }
            else
            {
                LogInfo($"Location {offer.LocationName} is not found");
            }
        }

        internal static void RegisterRPCs()
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null)
                return;

            if (ZNet.instance.IsServer())
                ZRoutedRpc.instance.Register<ZPackage>("ClosestLocationRequest", RPC_ClosestLocationRequest);
            else
                ZRoutedRpc.instance.Register<ZPackage>("StartTaxi", RPC_StartTaxi);
        }

        private static void ClosestLocationRequest(TaxiOffer offer, Vector3 offeringPosition)
        {
            LogInfo($"{offer.LocationName} closest location request");

            ZPackage zPackage = new();
            zPackage.Write(offer.LocationName);
            zPackage.Write(offer.ItemName);
            zPackage.Write(offer.Stack);
            zPackage.Write(offeringPosition);

            ZRoutedRpc.instance.InvokeRoutedRPC("ClosestLocationRequest", zPackage);
        }

        private static void RPC_ClosestLocationRequest(long sender, ZPackage pkg)
        {
            string name = pkg.ReadString();
            string itemName = pkg.ReadString().GetItemName();
            int stack = pkg.ReadInt();
            Vector3 offeringPosition = pkg.ReadVector3();

            if (!TryGetTaxiRequester(sender, out RoutedPlayerContext requester))
                return;

            if (!IsValidServerTaxiRequest(name, itemName, stack))
                return;

            Vector3 target = Vector3.zero;
            Vector3 searchPosition = GetValidatedOfferingSourcePosition(requester, offeringPosition);
            if (!TryGetFoundLocation(name, searchPosition, ref target))
            {
                LogInfo($"Location {name} is not found");
                return;
            }

            ZPackage zPackage = new();
            zPackage.Write(target);
            zPackage.Write(name);
            zPackage.Write(itemName);
            zPackage.Write(stack);
            zPackage.Write(searchPosition);
            zPackage.Write(ShouldConsumeTaxiItem(name, itemName));

            ZRoutedRpc.instance.InvokeRoutedRPC(sender, "StartTaxi", zPackage);
        }

        private static void RPC_StartTaxi(long sender, ZPackage pkg)
        {
            Vector3 location = pkg.ReadVector3();
            string locationName = pkg.ReadString();
            string itemName = pkg.ReadString().GetItemName();
            int stack = pkg.ReadInt();
            Vector3 offeringPosition = pkg.ReadVector3();
            bool consumeItem = pkg.ReadBool();

            LogInfo("Server responded with closest location");
            TryStartPassage(Player.m_localPlayer, offeringPosition, location, new TaxiOffer { LocationName = locationName, ItemName = itemName, Stack = stack, ConsumeItem = consumeItem });
        }

        private static bool TryGetTaxiRequester(long sender, out RoutedPlayerContext requester)
        {
            requester = default;
            return sender != 0L && TryGetRoutedPlayer(sender, out requester);
        }

        private static bool IsValidServerTaxiRequest(string name, string itemName, int stack)
        {
            itemName = itemName.GetItemName();
            return name switch
            {
                "StartTemple" => offeringTaxiStartTempleEnabled.Value && WardOfferings.IsBossTrophy(itemName) && stack == GetSacrificialStonesTaxiStack(),
                "Vendor_BlackForest" => offeringTaxiHaldorEnabled.Value && IsItemForHaldorTravel(itemName) && stack == GetExpectedHaldorTaxiPrice(),
                "Hildir_camp" => offeringTaxiHildirEnabled.Value && ((offeringTaxiHildirChestsEnabled.Value && WardOfferings.IsHildirChestItem(itemName) && stack == 1) || (IsItemForHildirTravel(itemName) && stack == GetHildirTaxiPrice())),
                "BogWitch_Camp" => offeringTaxiBogWitchEnabled.Value && IsItemForBogWitchTravel(itemName) && stack == offeringTaxiPriceBogWitchAmount.Value,
                _ => IsValidBossAltarTaxiRequest(name, itemName, stack),
            };
        }

        private static void TryStartPassage(Player initiator, Vector3 offeringPosition, Vector3 position, TaxiOffer offer)
        {
            if (initiator == null)
                return;

            if (IsTaxiDestinationTooClose(offeringPosition, position))
            {
                LogInfo($"Valkyrie passage destination is too close. Source: {offeringPosition}, destination: {position}, distance: {Vector3.Distance(offeringPosition, position):0.0}");
                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_tooclose".Localize());
                return;
            }

            if (HasTaxiStatusEffect(initiator) || s_statusExpected || s_state != TaxiState.Idle)
            {
                if (offeringTaxiExistingFlightMode.Value == WardTaxiExistingFlightMode.StopActivePassage)
                {
                    StopActivePassage(initiator, showMessage: true);
                    return;
                }

                initiator.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_already_active".Localize());
                return;
            }

            if (!HasTaxiPayment(initiator, offer))
            {
                initiator.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering".Localize());
                return;
            }

            s_player = initiator;
            s_targetPosition = position;
            s_returnPosition = initiator.transform.position;
            s_offer = offer;
            s_returnBack = offeringTaxiSecondsToFlyBack.Value > 0;
            s_activeValkyrieId = ZDOID.None;
            s_waitingForValkyrieSpawn = false;

            SetState(TaxiState.WaitingOutbound, TaxiOutboundWaitSeconds);
            initiator.Message(MessageHud.MessageType.Center, "$pw_msg_valkyrie_coming".Localize());
        }

        private static void StopActivePassage(Player player, bool showMessage)
        {
            if (showMessage)
                player?.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_stopped".Localize());

            CleanupActiveValkyrie();
            RemoveTaxiStatusEffect(player ?? s_player, controlled: true);
            ResetState();
        }

        private static void UpdateStatusEffect(SE_WardTaxi status, float dt)
        {
            Player player = s_player;
            if (player == null || Player.m_localPlayer != player)
                return;

            switch (s_state)
            {
                case TaxiState.WaitingOutbound:
                    if (status.m_time >= Math.Max(TaxiOutboundWaitSeconds, 1))
                        TryStartFlight(isReturnFlight: false);
                    break;

                case TaxiState.OutboundFlight:
                case TaxiState.ReturnFlight:
                    UpdateActiveFlight();
                    break;

                case TaxiState.FallingBeforeReturn:
                    ApplyTaxiSlowFall(status);
                    if (PlayerIsGrounded(player))
                        StartWaitingReturn(player);
                    break;

                case TaxiState.WaitingReturn:
                    if (status.m_time >= Math.Max(offeringTaxiSecondsToFlyBack.Value, 1))
                        TryStartFlight(isReturnFlight: true);
                    break;

                case TaxiState.WaitingRetry:
                    if (CanStartValkyrieFlight(player, silent: true))
                        TryStartFlight(isReturnFlight: true);
                    break;

                case TaxiState.Falling:
                    ApplyTaxiSlowFall(status);
                    if (PlayerIsGrounded(player))
                    {
                        player.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_complete".Localize());
                        FinishTaxiStatus(player);
                    }
                    break;
            }
        }

        private static void TryStartFlight(bool isReturnFlight)
        {
            Player player = s_player;
            if (player == null)
                return;

            if (!CanStartValkyrieFlight(player, silent: false))
            {
                if (isReturnFlight)
                {
                    SetState(TaxiState.WaitingRetry, 0);
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_waiting".Localize());
                    return;
                }

                player.Message(MessageHud.MessageType.Center, "$pw_msg_canttravel".Localize());
                FinishTaxiStatus(player);
                return;
            }

            Vector3 destination = isReturnFlight ? s_returnPosition : s_targetPosition;
            if (IsTaxiDestinationTooClose(player.transform.position, destination))
            {
                if (isReturnFlight)
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_complete".Localize());
                else
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_tooclose".Localize());

                FinishTaxiStatus(player);
                return;
            }

            if (!isReturnFlight && !TryConsumeTaxiPayment(player, s_offer))
            {
                player.Message(MessageHud.MessageType.Center, "$msg_incompleteoffering".Localize());
                FinishTaxiStatus(player);
                return;
            }

            if (!TrySpawnValkyrie(player, destination, isReturnFlight))
            {
                player.Message(MessageHud.MessageType.Center, "$pw_msg_canttravel".Localize());
                FinishTaxiStatus(player);
            }
        }

        private static Vector3 GetValidatedOfferingSourcePosition(RoutedPlayerContext requester, Vector3 offeringPosition)
        {
            if (!requester.HasPosition)
                return offeringPosition;

            return Vector3.Distance(requester.Position, offeringPosition) <= MaxTaxiOfferingSourceDistance
                ? offeringPosition
                : requester.Position;
        }

        private static bool IsTaxiDestinationTooClose(Vector3 sourcePosition, Vector3 destinationPosition)
        {
            return Vector3.Distance(sourcePosition, destinationPosition) < MinTaxiDistance;
        }

        private static bool CanStartValkyrieFlight(Player player, bool silent)
        {
            if (player == null || player.IsDead())
                return false;

            if (Valkyrie.m_instance != null && Valkyrie.m_instance.enabled && Valkyrie.m_instance.m_nview != null && Valkyrie.m_instance.m_nview.IsOwner())
            {
                if (!silent)
                    player.Message(MessageHud.MessageType.Center, "$menu_pleasewait".Localize());
                return false;
            }

            bool playerShouldExit = player.IsAttachedToShip() || player.IsAttached() || player.IsRiding() || player.IsSleeping() || player.IsTeleporting()
                                    || player.InPlaceMode() || player.InBed() || player.InCutscene() || player.InInterior();
            if (playerShouldExit)
            {
                if (!silent)
                    player.Message(MessageHud.MessageType.TopLeft, "$pw_msg_travel_inside".Localize(""));
                return false;
            }

            if (player.IsEncumbered())
            {
                if (!silent)
                    player.Message(MessageHud.MessageType.Center, "$se_encumbered_start".Localize());
                return false;
            }

            if (!WardOfferings.IsTeleportable(player))
            {
                if (!silent)
                    player.Message(MessageHud.MessageType.Center, "$pw_msg_notravel".Localize());
                return false;
            }

            return true;
        }

        private static bool TrySpawnValkyrie(Player player, Vector3 targetPosition, bool isReturnFlight)
        {
            bool assetLoaded = false;
            try
            {
                Player.m_localPlayer.m_valkyrie.Load();
                assetLoaded = true;

                GameObject valkyriePrefab = Player.m_localPlayer.m_valkyrie.Asset;
                if (valkyriePrefab == null || !valkyriePrefab.GetComponent<ZNetView>())
                    throw new InvalidOperationException("Failed to load Valkyrie passage prefab.");

                s_targetPosition = targetPosition;
                s_activeValkyrieId = ZDOID.None;
                s_waitingForValkyrieSpawn = true;
                SetState(isReturnFlight ? TaxiState.ReturnFlight : TaxiState.OutboundFlight, 0);
                ResetTaxiProgressWatch(player);

                player.Message(MessageHud.MessageType.Center, "$pw_msg_travel_start".Localize());

                GameObject valkyrie = UnityEngine.Object.Instantiate(valkyriePrefab, player.transform.position, Quaternion.identity);
                s_waitingForValkyrieSpawn = false;
                if (valkyrie == null || !valkyrie.TryGetComponent(out ZNetView zNetView))
                    throw new InvalidOperationException("Failed to create Valkyrie passage instance.");

                zNetView.HoldReferenceTo((IReferenceCounted)(object)Player.m_localPlayer.m_valkyrie);
                return true;
            }
            catch (Exception e)
            {
                s_waitingForValkyrieSpawn = false;
                LogInfo($"Valkyrie passage start failed: {e}");
                return false;
            }
            finally
            {
                if (assetLoaded)
                    Player.m_localPlayer.m_valkyrie.Release();
            }
        }

        private static void UpdateActiveFlight()
        {
            Valkyrie valkyrie = GetActiveValkyrie();
            if (valkyrie == null)
                return;

            UpdateTaxiProgressWatch(valkyrie);

            if (ZInput.GetButton("Use") && ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyUse") && ZInput.GetButton("JoyAltPlace"))
                valkyrie.DropPlayer(true);
        }

        private static void HandleFlightEnded(Player player, bool dropped)
        {
            if (player == null)
                return;

            TaxiState previousState = s_state;
            s_activeValkyrieId = ZDOID.None;
            s_waitingForValkyrieSpawn = false;

            if (dropped)
            {
                s_returnBack = false;
                SetState(TaxiState.Falling, 0);
                return;
            }

            player.Message(MessageHud.MessageType.Center, "$pw_msg_taxi_complete".Localize());

            if (previousState == TaxiState.OutboundFlight && s_returnBack && offeringTaxiSecondsToFlyBack.Value > 0)
                SetState(TaxiState.FallingBeforeReturn, 0);
            else
                SetState(TaxiState.Falling, 0);
        }

        private static void StartWaitingReturn(Player player)
        {
            if (player == null)
                return;

            int seconds = Math.Max(offeringTaxiSecondsToFlyBack.Value, 0);
            if (seconds <= 0)
            {
                FinishTaxiStatus(player);
                return;
            }

            s_returnBack = false;
            SetState(TaxiState.WaitingReturn, seconds);
        }

        private static void SetState(TaxiState state, int durationSeconds)
        {
            Player player = s_player;
            s_state = state;
            RegisterTaxiStatusEffect();

            SE_WardTaxi status = GetTaxiStatusEffect(player);
            if (status == null && player != null && player.m_seman != null && state != TaxiState.Idle)
            {
                s_statusExpected = true;
                player.m_seman.AddStatusEffect(wardTaxiStatusHash, resetTime: true);
                status = GetTaxiStatusEffect(player);
            }

            if (status == null)
            {
                if (state != TaxiState.Idle)
                {
                    LogInfo("Failed to add Valkyrie passage status effect. Cleaning up taxi state.");
                    ResetState();
                }

                return;
            }

            status.m_time = 0f;
            status.m_ttl = Math.Max(durationSeconds, 0);
            status.m_name = "$pw_se_ward_taxi_status";
            status.m_icon = GetTaxiStatusEffectIcon();
            status.m_startMessage = "$pw_msg_valkyrie_coming";
            status.m_startMessageType = MessageHud.MessageType.Center;
            status.m_stopMessage = "$pw_msg_taxi_stopped";
            status.m_stopMessageType = MessageHud.MessageType.Center;

            ClearTaxiSlowFall(status);

            status.m_tooltip = state switch
            {
                TaxiState.WaitingOutbound => "$pw_se_ward_taxi_waiting_outbound_tooltip".Localize(),
                TaxiState.OutboundFlight or TaxiState.ReturnFlight => "$pw_se_ward_taxi_flight_tooltip".Localize(GetDropHotkeyText()),
                TaxiState.WaitingReturn => "$pw_se_ward_taxi_return_tooltip".Localize(),
                TaxiState.WaitingRetry => "$pw_se_ward_taxi_waiting_retry_tooltip".Localize(),
                TaxiState.FallingBeforeReturn or TaxiState.Falling => "$pw_se_ward_taxi_falling_tooltip".Localize(),
                _ => "",
            };

            if (state == TaxiState.FallingBeforeReturn || state == TaxiState.Falling)
                ApplyTaxiSlowFall(status);
        }

        private static void FinishTaxiStatus(Player player)
        {
            RemoveTaxiStatusEffect(player, controlled: true);
            ResetState();
        }

        private static void OnStatusStopped(SE_WardTaxi status)
        {
            if (s_controlledStatusRemoval)
                return;

            if (!s_statusExpected)
                return;

            LogInfo("Valkyrie passage status effect stopped unexpectedly. Cleaning up taxi state.");
            CleanupActiveValkyrie();
            ResetState();
        }

        private static void RegisterTaxiStatusEffect()
        {
            if (ObjectDB.instance == null)
                return;

            StatusEffect statusEffect = null;
            for (int i = ObjectDB.instance.m_StatusEffects.Count - 1; i >= 0; i--)
            {
                StatusEffect registeredStatusEffect = ObjectDB.instance.m_StatusEffects[i];
                if (registeredStatusEffect == null || registeredStatusEffect.name != WardTaxiStatusEffectName)
                    continue;

                if (statusEffect == null && registeredStatusEffect is SE_WardTaxi)
                {
                    statusEffect = registeredStatusEffect;
                    continue;
                }

                ObjectDB.instance.m_StatusEffects.RemoveAt(i);
            }

            if (statusEffect == null)
            {
                statusEffect = ScriptableObject.CreateInstance<SE_WardTaxi>();
                statusEffect.name = WardTaxiStatusEffectName;
                statusEffect.m_name = "$pw_se_ward_taxi_status";
                statusEffect.m_icon = GetTaxiStatusEffectIcon();
                statusEffect.m_startMessage = "$pw_msg_valkyrie_coming";
                statusEffect.m_startMessageType = MessageHud.MessageType.Center;
                statusEffect.m_stopMessage = "$pw_msg_taxi_stopped";
                statusEffect.m_stopMessageType = MessageHud.MessageType.Center;
                ObjectDB.instance.m_StatusEffects.Add(statusEffect);
            }
        }

        private static SE_WardTaxi GetTaxiStatusEffect(Player player)
        {
            return player?.m_seman?.GetStatusEffect(wardTaxiStatusHash) as SE_WardTaxi;
        }

        private static bool HasTaxiStatusEffect(Player player)
        {
            return player != null && player.m_seman != null && player.m_seman.HaveStatusEffect(wardTaxiStatusHash);
        }

        private static void RemoveTaxiStatusEffect(Player player, bool controlled)
        {
            if (!HasTaxiStatusEffect(player))
                return;

            bool previous = s_controlledStatusRemoval;
            s_controlledStatusRemoval = controlled;
            try
            {
                player.m_seman.RemoveStatusEffect(wardTaxiStatusHash, true);
            }
            finally
            {
                s_controlledStatusRemoval = previous;
            }
        }

        private static void ResetState()
        {
            s_state = TaxiState.Idle;
            s_player = null;
            s_targetPosition = Vector3.zero;
            s_returnPosition = Vector3.zero;
            s_offer = default;
            s_returnBack = false;
            s_waitingForValkyrieSpawn = false;
            s_statusExpected = false;
            s_controlledStatusRemoval = false;
            s_activeValkyrieId = ZDOID.None;
            s_lastProgressPosition = Vector3.zero;
            s_lastProgressTime = 0f;
        }

        private static void CleanupActiveValkyrie()
        {
            GameObject active = GetActiveValkyrieObject();
            s_activeValkyrieId = ZDOID.None;
            s_waitingForValkyrieSpawn = false;
            if (active == null)
                return;

            Valkyrie valkyrie = active.GetComponent<Valkyrie>();
            if (valkyrie != null && s_player != null && s_player.InIntro())
            {
                valkyrie.DropPlayer(true);
                return;
            }

            if (ZNetScene.instance != null)
                ZNetScene.instance.Destroy(active);
        }

        private static GameObject GetActiveValkyrieObject()
        {
            if (s_activeValkyrieId.Equals(ZDOID.None) || ZNetScene.instance == null)
                return null;

            return ZNetScene.instance.FindInstance(s_activeValkyrieId);
        }

        private static Valkyrie GetActiveValkyrie()
        {
            return GetActiveValkyrieObject()?.GetComponent<Valkyrie>();
        }

        private static bool IsActiveTaxiValkyrie(Valkyrie valkyrie)
        {
            if (valkyrie == null || valkyrie.m_nview == null || !valkyrie.m_nview.IsValid())
                return false;

            ZDO zdo = valkyrie.m_nview.GetZDO();
            return zdo != null && !s_activeValkyrieId.Equals(ZDOID.None) && zdo.m_uid.Equals(s_activeValkyrieId);
        }

        private static void ResetTaxiProgressWatch(Player player)
        {
            if (player == null)
                return;

            s_lastProgressPosition = player.transform.position;
            s_lastProgressTime = Time.time;
        }

        private static void UpdateTaxiProgressWatch(Valkyrie valkyrie)
        {
            if (s_player == null || !IsActiveTaxiValkyrie(valkyrie))
                return;

            if (Vector3.Distance(s_player.transform.position, s_lastProgressPosition) >= TaxiProgressDistance)
            {
                ResetTaxiProgressWatch(s_player);
                return;
            }

            if (Time.time - s_lastProgressTime < TaxiStuckSeconds)
                return;

            LogInfo("Valkyrie passage appears stuck. Dropping player.");
            valkyrie.DropPlayer(true);
            ResetTaxiProgressWatch(s_player);
        }

        private static bool PlayerIsGrounded(Player player)
        {
            return player == null || player.IsOnGround() || player.IsSwimming();
        }

        private static void ApplyTaxiSlowFall(SE_WardTaxi status)
        {
            if (status == null)
                return;

            SE_Stats slowFall = ObjectDB.instance?.GetStatusEffect(slowFallHash) as SE_Stats;
            if (slowFall != null)
            {
                status.m_maxMaxFallSpeed = slowFall.m_maxMaxFallSpeed;
                status.m_fallDamageModifier = slowFall.m_fallDamageModifier;
            }
            else
            {
                status.m_maxMaxFallSpeed = 5f;
                status.m_fallDamageModifier = -1f;
            }
        }

        private static void ClearTaxiSlowFall(SE_WardTaxi status)
        {
            if (status == null)
                return;

            status.m_maxMaxFallSpeed = 0f;
            status.m_fallDamageModifier = 0f;
        }

        private static Sprite GetTaxiStatusEffectIcon()
        {
            GameObject prefab = ObjectDB.instance?.GetItemPrefab("CelestialFeather");
            ItemDrop itemDrop = prefab?.GetComponent<ItemDrop>();
            return itemDrop?.m_itemData.GetIcon();
        }

        private static string GetDropHotkeyText()
        {
            string alt = "$KEY_AltPlace".Localize();
            string use = "$KEY_Use".Localize();
            if (alt == "$KEY_AltPlace")
                alt = "AltPlace";
            if (use == "$KEY_Use")
                use = "Use";
            return $"{alt} + {use}";
        }

        internal static bool TryGetFoundLocation(string name, Vector3 position, ref Vector3 target)
        {
            if (ZoneSystem.instance == null)
                return false;

            ZoneSystem.instance.tempIconList.Clear();
            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            bool foundIcon = false;
            float closestIconDistance = float.MaxValue;
            foreach (KeyValuePair<Vector3, string> loc in ZoneSystem.instance.tempIconList)
            {
                if (loc.Value != name)
                    continue;

                float distance = Vector3.Distance(position, loc.Key);
                if (distance >= closestIconDistance)
                    continue;

                closestIconDistance = distance;
                target = loc.Key;
                foundIcon = true;
            }

            if (foundIcon)
            {
                LogInfo($"Found closest {name} in icon list");
                return true;
            }

            if (ZoneSystem.instance.FindClosestLocation(name, position, out ZoneSystem.LocationInstance location))
            {
                target = location.m_position;
                LogInfo($"Found closest {name} in location list");
                return true;
            }

            return false;
        }

        private static bool HasTaxiPayment(Player player, TaxiOffer offer)
        {
            return offer.Stack <= 0 || player.GetInventory().CountItems(offer.ItemName) >= offer.Stack;
        }

        private static bool TryConsumeTaxiPayment(Player player, TaxiOffer offer)
        {
            if (!HasTaxiPayment(player, offer))
                return false;

            if (offer.ConsumeItem && offer.Stack > 0)
                player.GetInventory().RemoveItem(offer.ItemName, offer.Stack);

            return true;
        }

        private static bool TryGetTaxiOffer(string itemName, out TaxiOffer offer)
        {
            offer = default;
            itemName = itemName.GetItemName();

            if (offeringTaxiStartTempleEnabled.Value && WardOfferings.IsBossTrophy(itemName))
            {
                offer = new TaxiOffer { LocationName = "StartTemple", ItemName = itemName, Stack = GetSacrificialStonesTaxiStack(), ConsumeItem = offeringTaxiStartTempleConsumeItem.Value };
                return true;
            }

            if (offeringTaxiHaldorEnabled.Value && IsItemForHaldorTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "Vendor_BlackForest", ItemName = itemName, Stack = GetExpectedHaldorTaxiPrice(), ConsumeItem = offeringTaxiHaldorConsumeItem.Value };
                return true;
            }

            if (offeringTaxiHildirEnabled.Value && offeringTaxiHildirChestsEnabled.Value && WardOfferings.IsHildirChestItem(itemName))
            {
                offer = new TaxiOffer { LocationName = "Hildir_camp", ItemName = itemName, Stack = 1, ConsumeItem = false };
                return true;
            }

            if (offeringTaxiHildirEnabled.Value && IsItemForHildirTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "Hildir_camp", ItemName = itemName, Stack = GetHildirTaxiPrice(), ConsumeItem = offeringTaxiHildirConsumeItem.Value };
                return true;
            }

            if (offeringTaxiBogWitchEnabled.Value && IsItemForBogWitchTravel(itemName))
            {
                offer = new TaxiOffer { LocationName = "BogWitch_Camp", ItemName = itemName, Stack = offeringTaxiPriceBogWitchAmount.Value, ConsumeItem = offeringTaxiBogWitchConsumeItem.Value };
                return true;
            }

            return TryGetBossAltarTaxiOffer(itemName, out offer);
        }

        private static bool IsItemForHaldorTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceHaldorItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceHaldorItem);

        private static bool IsItemForHildirTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceHildirItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceHildirItem);

        private static bool IsItemForBogWitchTravel(string itemName) => ConfiguredItemName(offeringTaxiPriceBogWitchItem) != "" && itemName.GetItemName() == ConfiguredItemName(offeringTaxiPriceBogWitchItem);

        private static string ConfiguredItemName(BepInEx.Configuration.ConfigEntry<string> entry) => entry.Value.GetItemName();

        private static int GetExpectedHaldorTaxiPrice()
        {
            return HasLocationIcon("Vendor_BlackForest")
                ? offeringTaxiPriceHaldorDiscovered.Value
                : offeringTaxiPriceHaldorUndiscovered.Value;
        }

        private static int GetHildirTaxiPrice() => Math.Max(offeringTaxiPriceHildirAmount.Value, 0);

        private static int GetSacrificialStonesTaxiStack() => offeringTaxiStartTempleConsumeItem.Value ? 1 : 0;

        private static bool HasLocationIcon(string name)
        {
            if (ZoneSystem.instance == null)
                return false;

            ZoneSystem.instance.tempIconList.Clear();
            ZoneSystem.instance.GetLocationIcons(ZoneSystem.instance.tempIconList);
            return ZoneSystem.instance.tempIconList.Any(icon => icon.Value == name);
        }

        private static bool ShouldConsumeTaxiItem(string locationName, string itemName)
        {
            itemName = itemName.GetItemName();
            return locationName switch
            {
                "StartTemple" => offeringTaxiStartTempleConsumeItem.Value,
                "Vendor_BlackForest" => offeringTaxiHaldorConsumeItem.Value,
                "Hildir_camp" => !WardOfferings.IsHildirChestItem(itemName) && offeringTaxiHildirConsumeItem.Value,
                "BogWitch_Camp" => offeringTaxiBogWitchConsumeItem.Value,
                _ => TryGetBossAltarConsumeItem(locationName, itemName, out bool consumeItem) && consumeItem,
            };
        }

        private static bool TryGetBossAltarTaxiOffer(string itemName, out TaxiOffer offer)
        {
            itemName = itemName.GetItemName();
            return TryGetBossAltarTaxiOffer(offeringTaxiEikthyrAltarEnabled, EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarAmount, offeringTaxiEikthyrAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiElderAltarEnabled, ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarAmount, offeringTaxiElderAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiBonemassAltarEnabled, BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarAmount, offeringTaxiBonemassAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiModerAltarEnabled, ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarAmount, offeringTaxiModerAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiYagluthAltarEnabled, YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarAmount, offeringTaxiYagluthAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiQueenAltarEnabled, QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarAmount, offeringTaxiQueenAltarConsumeItem, itemName, out offer) ||
                   TryGetBossAltarTaxiOffer(offeringTaxiFaderAltarEnabled, FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarAmount, offeringTaxiFaderAltarConsumeItem, itemName, out offer);
        }

        private static bool TryGetBossAltarTaxiOffer(BepInEx.Configuration.ConfigEntry<bool> enabled, string locationName, BepInEx.Configuration.ConfigEntry<string> item, BepInEx.Configuration.ConfigEntry<int> amount, BepInEx.Configuration.ConfigEntry<bool> consume, string itemName, out TaxiOffer offer)
        {
            offer = default;
            string configuredItem = ConfiguredItemName(item);
            if (!enabled.Value || configuredItem == "" || itemName != configuredItem)
                return false;

            offer = new TaxiOffer { LocationName = locationName, ItemName = configuredItem, Stack = Math.Max(amount.Value, 0), ConsumeItem = consume.Value };
            return true;
        }

        private static bool IsValidBossAltarTaxiRequest(string locationName, string itemName, int stack)
        {
            itemName = itemName.GetItemName();
            return IsValidBossAltarTaxiRequest(offeringTaxiEikthyrAltarEnabled, EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiElderAltarEnabled, ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiBonemassAltarEnabled, BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiModerAltarEnabled, ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiYagluthAltarEnabled, YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiQueenAltarEnabled, QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarAmount, locationName, itemName, stack) ||
                   IsValidBossAltarTaxiRequest(offeringTaxiFaderAltarEnabled, FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarAmount, locationName, itemName, stack);
        }

        private static bool IsValidBossAltarTaxiRequest(BepInEx.Configuration.ConfigEntry<bool> enabled, string expectedLocationName, BepInEx.Configuration.ConfigEntry<string> item, BepInEx.Configuration.ConfigEntry<int> amount, string locationName, string itemName, int stack)
        {
            return enabled.Value && locationName == expectedLocationName && itemName == ConfiguredItemName(item) && stack == Math.Max(amount.Value, 0);
        }

        private static bool TryGetBossAltarConsumeItem(string locationName, string itemName, out bool consumeItem)
        {
            itemName = itemName.GetItemName();
            return TryGetBossAltarConsumeItem(EikthyrAltarLocation, offeringTaxiEikthyrAltarItem, offeringTaxiEikthyrAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(ElderAltarLocation, offeringTaxiElderAltarItem, offeringTaxiElderAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(BonemassAltarLocation, offeringTaxiBonemassAltarItem, offeringTaxiBonemassAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(ModerAltarLocation, offeringTaxiModerAltarItem, offeringTaxiModerAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(YagluthAltarLocation, offeringTaxiYagluthAltarItem, offeringTaxiYagluthAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(QueenAltarLocation, offeringTaxiQueenAltarItem, offeringTaxiQueenAltarConsumeItem, locationName, itemName, out consumeItem) ||
                   TryGetBossAltarConsumeItem(FaderAltarLocation, offeringTaxiFaderAltarItem, offeringTaxiFaderAltarConsumeItem, locationName, itemName, out consumeItem);
        }

        private static bool TryGetBossAltarConsumeItem(string expectedLocationName, BepInEx.Configuration.ConfigEntry<string> item, BepInEx.Configuration.ConfigEntry<bool> consume, string locationName, string itemName, out bool consumeItem)
        {
            consumeItem = false;
            if (locationName != expectedLocationName || itemName != ConfiguredItemName(item))
                return false;

            consumeItem = consume.Value;
            return true;
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.Awake))]
        private static class Valkyrie_Awake_Taxi
        {
            private static bool Prefix(Valkyrie __instance)
            {
                if (!offeringTaxi.Value)
                    return true;

                if ((s_state != TaxiState.OutboundFlight && s_state != TaxiState.ReturnFlight) || s_player == null || !s_waitingForValkyrieSpawn)
                    return true;

                __instance.m_nview = __instance.GetComponent<ZNetView>();
                __instance.m_animator = __instance.GetComponentInChildren<Animator>();
                if (__instance.m_nview == null || !__instance.m_nview.IsOwner() || Valkyrie.m_instance != null && Valkyrie.m_instance != __instance)
                {
                    __instance.enabled = false;
                    return false;
                }

                ZDO zdo = __instance.m_nview.GetZDO();
                if (zdo == null)
                {
                    __instance.enabled = false;
                    return false;
                }

                Valkyrie.m_instance = __instance;
                s_activeValkyrieId = zdo.m_uid;

                __instance.m_startAltitude = TaxiStartAltitude;
                __instance.m_textDuration = 0f;
                __instance.m_descentAltitude = TaxiDescentAltitude;
                __instance.m_attachOffset = new Vector3(-0.1f, 1.5f, 0.1f);

                __instance.m_targetPoint = s_targetPosition + new Vector3(0f, __instance.m_dropHeight, 0f);

                Vector3 position = s_player.transform.position;
                position.y += __instance.m_startAltitude;

                float flyDistance = Vector3.Distance(__instance.m_targetPoint, position);
                __instance.m_startDistance = flyDistance;
                __instance.m_startDescentDistance = Math.Min(200f, flyDistance / 5);
                __instance.m_speed = Math.Max(Math.Min(flyDistance / 90f, Math.Min(30f, maxTaxiSpeed.Value)), 10f);

                if (__instance.m_speed <= 15)
                    EnvMan.instance.m_introEnvironment = EnvMan.instance.m_currentEnv.m_name;
                else
                    EnvMan.instance.m_introEnvironment = "ThunderStorm";

                s_player.m_intro = true;
                __instance.transform.position = position;

                float landDistance = Vector3.Distance(__instance.m_targetPoint, __instance.transform.position);
                float descentPathPart = Mathf.Clamp(__instance.m_descentAltitude / Math.Max(landDistance, 1f), 0.2f, 0.8f);
                __instance.m_descentStart = Vector3.Lerp(__instance.m_targetPoint, __instance.transform.position, descentPathPart);
                __instance.m_descentStart.y = __instance.m_descentAltitude;
                Vector3 a2 = __instance.m_targetPoint - __instance.m_descentStart;
                a2.y = 0f;
                if (a2.sqrMagnitude < 1f)
                    a2 = s_player.transform.forward;
                else
                    a2.Normalize();
                __instance.m_flyAwayPoint = __instance.m_targetPoint + a2 * __instance.m_startDescentDistance;
                __instance.m_flyAwayPoint.y += 100f;
                __instance.SyncPlayer(doNetworkSync: true);
                ResetTaxiProgressWatch(s_player);

                LogInfo("Setting up Valkyrie passage from " + __instance.transform.position + " to " + __instance.m_targetPoint + "   " + ZNet.instance.GetReferencePosition());
                return false;
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.OnDestroy))]
        private static class Valkyrie_OnDestroy_Taxi
        {
            private static void Prefix(Valkyrie __instance)
            {
                if (!IsActiveTaxiValkyrie(__instance))
                    return;

                s_activeValkyrieId = ZDOID.None;
                if ((s_state == TaxiState.OutboundFlight || s_state == TaxiState.ReturnFlight) && s_player != null && !s_player.InIntro())
                    HandleFlightEnded(s_player, dropped: false);
            }
        }

        [HarmonyPatch(typeof(Valkyrie), nameof(Valkyrie.DropPlayer))]
        private static class Valkyrie_DropPlayer_Taxi
        {
            private static void Postfix(Valkyrie __instance)
            {
                if (!offeringTaxi.Value)
                    return;

                if (!IsActiveTaxiValkyrie(__instance) || s_player == null || Player.m_localPlayer != s_player)
                    return;

                HandleFlightEnded(s_player, dropped: true);
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.SetIntro))]
        private static class Player_SetIntro_Taxi
        {
            private static void Postfix(Player __instance)
            {
                if (__instance.InIntro() || __instance != s_player)
                    return;

                if (s_state == TaxiState.OutboundFlight || s_state == TaxiState.ReturnFlight)
                    HandleFlightEnded(__instance, dropped: false);
            }
        }

        [HarmonyPatch(typeof(SEMan), nameof(SEMan.Update))]
        private static class SEMan_Update_TaxiStatusConsistency
        {
            private static void Postfix(SEMan __instance)
            {
                Player player = Player.m_localPlayer;
                if (player == null || __instance != player.m_seman)
                    return;

                bool hasStatus = HasTaxiStatusEffect(player);
                if (s_statusExpected && !hasStatus)
                {
                    LogInfo("Valkyrie passage status effect disappeared. Cleaning up taxi state.");
                    CleanupActiveValkyrie();
                    ResetState();
                    return;
                }

                if (!s_statusExpected && hasStatus)
                    RemoveTaxiStatusEffect(player, controlled: true);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class ObjectDB_Awake_RegisterTaxiStatusEffect
        {
            private static void Postfix() => RegisterTaxiStatusEffect();
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class ObjectDB_CopyOtherDB_RegisterTaxiStatusEffect
        {
            private static void Postfix() => RegisterTaxiStatusEffect();
        }

        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static class ZoneSystem_Start_Taxi
        {
            private static void Postfix()
            {
                CleanupActiveValkyrie();
                ResetState();
                RegisterRPCs();
            }
        }
    }
}
