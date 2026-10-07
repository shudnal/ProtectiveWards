from support import *

text = read('BackgroundProtection.cs')
text = method(text, 'internal static bool IsBackgroundProtectionActiveAt(Vector3 point, out ZDO ward)', '''internal static bool IsBackgroundProtectionActiveAt(Vector3 point, out ZDO ward)
{
    foreach (ZDO candidate in GetBackgroundWards(point, point))
    {
        if (!IsQualifiedProtectedBase(candidate) || HasEffectiveAccessPresence(candidate, point))
            continue;
        ward = candidate;
        return true;
    }
    ward = null;
    return false;
}''')
text = method(text, 'internal static bool TryFindBackgroundWard(Vector3 sourcePoint, Vector3 targetPoint, out ZDO ward)', '''private static IEnumerable<ZDO> GetBackgroundWards(Vector3 sourcePoint, Vector3 targetPoint)
{
    bool samePoint = sourcePoint.x == targetPoint.x && sourcePoint.y == targetPoint.y && sourcePoint.z == targetPoint.z;
    if (!TryResolveWardCheckPoint(sourcePoint, out sourcePoint))
        yield break;
    if (samePoint)
        targetPoint = sourcePoint;
    else if (!TryResolveWardCheckPoint(targetPoint, out targetPoint))
        yield break;

    WardConnectedAccessMode mode = wardBackgroundConnectedAccessMode?.Value ?? WardConnectedAccessMode.Off;
    foreach (ZDO area in WardZdoUtils.GetAllWards())
    {
        if (!IsActiveBackgroundWard(area) || !IsInsideWardXZ(area, sourcePoint))
            continue;

        if (samePoint || IsInsideWardXZ(area, targetPoint))
        {
            yield return area;
            continue;
        }
        if (mode == WardConnectedAccessMode.Off)
            continue;

        foreach (ZDO candidate in WardZdoUtils.ConnectedAccessWardZdos(area, mode, activeWardPredicate))
        {
            if (!IsInsideWardXZ(candidate, targetPoint))
                continue;
            yield return area;
            break;
        }
    }
}''')
text = method(text, 'internal static bool ShouldSuppressWearNTearDamage(WearNTear wearNTear, HitData hit, bool isShip, bool isCart)', '''internal static bool ShouldSuppressWearNTearDamage(WearNTear wearNTear, HitData hit, bool isShip, bool isCart)
{
    if (wearNTear == null || hit == null)
        return false;

    Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
    bool isPlayerBuiltPiece = piece != null && piece.IsPlacedByPlayer();
    if (!isPlayerBuiltPiece && !isShip && !isCart)
        return false;

    Player attacker = hit.GetAttacker() as Player;
    Vector3 point = wearNTear.transform.position;
    // Each covering root contributes independently. A protecting root wins; load order never grants access.
    foreach (ZDO ward in GetBackgroundWards(point, point))
    {
        if (!IsQualifiedProtectedBase(ward))
            continue;

        if (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockNonPermittedPlayerDamage
            && isPlayerBuiltPiece && attacker != null
            && !ward.HasConnectedWardAccess(attacker.GetPlayerID(), wardBackgroundConnectedAccessMode.Value, activeWardPredicate))
            return true;

        if (HasEffectiveAccessPresence(ward, point))
            continue;

        if ((wardBackgroundProtectBoats.Value && isShip) || (wardBackgroundProtectCarts.Value && isCart))
            return true;

        if (isPlayerBuiltPiece
            && (wardBackgroundStructureProtection.Value == WardBackgroundStructureProtectionMode.BlockAllDamageWhenNoPermittedNearby
                || (wardBackgroundProtectFire.Value && IsFireDamage(hit))))
            return true;
    }
    return false;
}''')
text = method(text, 'internal static bool ShouldSuppressTameDamageToStructure(WearNTear wearNTear, HitData hit)', '''internal static bool ShouldSuppressTameDamageToStructure(WearNTear wearNTear, HitData hit)
{
    if (!wardBackgroundTamesPreventDamageToStructures.Value || wearNTear == null || hit == null || !hit.HaveAttacker())
        return false;

    Piece piece = wearNTear.m_piece ?? wearNTear.GetComponent<Piece>();
    if (piece == null || !piece.IsPlacedByPlayer())
        return false;

    Character attacker = hit.GetAttacker();
    if (attacker == null || attacker.IsPlayer() || !attacker.IsTamed())
        return false;

    foreach (ZDO ward in GetBackgroundWards(attacker.transform.position, wearNTear.transform.position))
        if (IsQualifiedProtectedBase(ward)
            && !HasEffectiveAccessPresence(ward, attacker.transform.position)
            && !HasEffectiveAccessPresence(ward, wearNTear.transform.position))
            return true;
    return false;
}''')
text = method(text, 'internal static bool IsBuildingRestricted(Player player, Vector3 point)', '''internal static bool IsBuildingRestricted(Player player, Vector3 point)
{
    if (!wardBackgroundPreventBuildingAndDemolishing.Value || player == null)
        return false;

    long playerID = player.GetPlayerID();
    if (HasWardAdminAccess(playerID))
        return false;

    foreach (ZDO ward in GetBackgroundWards(point, point))
        if (IsQualifiedProtectedBase(ward)
            && !ward.HasConnectedWardAccess(playerID, wardBackgroundConnectedAccessMode.Value, activeWardPredicate)
            && !HasEffectiveAccessPresence(ward, point))
            return true;
    return false;
}''')
assert 'TryFindBackgroundWard' not in text
write('BackgroundProtection.cs', text)
commit('fix: aggregate all overlapping background protection roots', 'BackgroundProtection.cs')

text = read('FullProtection.cs')
text = replace(text, '        private static int s_privateAreaCheckBypassDepth;', '''        private static readonly Stack<PrivateAreaBypassContext> s_privateAreaBypasses = new();

        private readonly struct PrivateAreaBypassContext
        {
            internal readonly Component Target;
            internal readonly Player Actor;
            internal readonly int Frame;

            internal PrivateAreaBypassContext(Component target, Player actor)
            {
                Target = target;
                Actor = actor;
                Frame = Time.frameCount;
            }
        }''')
text = replace(text, '            s_privateAreaCheckBypassDepth++;', '            s_privateAreaBypasses.Push(new PrivateAreaBypassContext(component, human as Player));')
text = replace(text, '            s_privateAreaCheckBypassDepth = Math.Max(0, s_privateAreaCheckBypassDepth - 1);', '''            if (s_privateAreaBypasses.Count > 0)
                s_privateAreaBypasses.Pop();''')
marker = '        private static void RegisterTeleportAccessRPC()'
new = '''        private static bool HasMatchingObjectExemption(Player player, Vector3 point, float radius, bool wardCheck)
        {
            if (player == null || wardCheck || radius != 0f || s_privateAreaBypasses.Count == 0)
                return false;

            PrivateAreaBypassContext context = s_privateAreaBypasses.Peek();
            return context.Actor == player && context.Target != null && context.Frame == Time.frameCount
                && (context.Target.transform.position - point).sqrMagnitude <= 0.0001f
                && ShouldBypassVanillaPrivateAreaCheck(context.Target, player);
        }

'''
text = replace(text, marker, new + marker)
text = replace(text, '            s_teleportAccessRpcRegistered = false;', '            s_teleportAccessRpcRegistered = false;\n            s_privateAreaBypasses.Clear();')
text = replace(text, '''                if (s_privateAreaCheckBypassDepth > 0)
                {
                    __result = true;
                    return false;
                }

''', '')
text = replace(text, '''                bool hasGrantedArea = false;
                bool hasDeniedArea = false;''', '''                bool skipManagedContribution = HasMatchingObjectExemption(player, point, radius, wardCheck);
                bool hasGrantedArea = false;
                bool hasDeniedArea = false;''')
text = replace(text, '''                    if (!IsPrivateAreaInsideAccessRange(area, point, radius))
                        continue;

                    if (HasEffectiveLocalPrivateAreaAccess(area, player))''', '''                    if (!IsPrivateAreaInsideAccessRange(area, point, radius)
                        || (skipManagedContribution && IsPlayerWardPrefab(area)))
                        continue;

                    if (HasEffectiveLocalPrivateAreaAccess(area, player))''')
text = replace(text, '''                        if (!IsPrivateAreaInsideAccessRange(area, point, radius) || HasEffectiveLocalPrivateAreaAccess(area, player))''', '''                        if (!IsPrivateAreaInsideAccessRange(area, point, radius)
                            || (skipManagedContribution && IsPlayerWardPrefab(area))
                            || HasEffectiveLocalPrivateAreaAccess(area, player))''')
assert 's_privateAreaCheckBypassDepth' not in text
write('FullProtection.cs', text)
commit('fix: scope ownership exceptions to the exact interaction target', 'FullProtection.cs')

text = read('WardPasswordProtection.cs')
text = method(text, 'private static bool VerifyPassword(ZDO zdo, string password)', '''private static bool VerifyPassword(ZDO zdo, string password)
{
    if (zdo == null || string.IsNullOrEmpty(password) || password.Length > PasswordCharacterLimit)
        return false;

    string encodedSalt = zdo.GetString(s_passwordSalt, "");
    string encodedHash = zdo.GetString(s_passwordHash, "");
    if (!string.IsNullOrEmpty(encodedSalt) || !string.IsNullOrEmpty(encodedHash))
    {
        // An incomplete or malformed hash must never fall back to a readable/empty value.
        if (encodedSalt.Length != 24 || encodedHash.Length != 44)
            return false;
        try
        {
            byte[] salt = Convert.FromBase64String(encodedSalt);
            byte[] expected = Convert.FromBase64String(encodedHash);
            if (salt.Length != 16 || expected.Length != 32)
                return false;
            using Rfc2898DeriveBytes derive = new(password, salt, PasswordHashIterations);
            return FixedTimeEquals(expected, derive.GetBytes(32));
        }
        catch (FormatException) { return false; }
        catch (ArgumentException) { return false; }
        catch (CryptographicException) { return false; }
    }

    string legacy = zdo.GetString(s_passwordPlaintext, "");
    return !string.IsNullOrEmpty(legacy) && legacy.Length <= PasswordCharacterLimit
        && string.Equals(legacy, password, StringComparison.Ordinal);
}''')
write('WardPasswordProtection.cs', text)
commit('fix: reject malformed ward password state without plaintext fallback', 'WardPasswordProtection.cs')
