namespace ProtectiveWards
{
    public static class StringExtensions
    {
        public static string Localize(this string text) => Localization.instance != null ? Localization.instance.Localize(text) : text;

        public static string Localize(this string text, params string[] words) => Localization.instance != null ? Localization.instance.Localize(text, words) : text;
    }

    internal static class ComponentExtensions
    {
        internal static ZNetView GetComponentZNetView(this UnityEngine.Component component)
        {
            if (component == null)
                return null;

            if (component is PrivateArea ward)
                return ward.m_nview;

            if (component is Ship ship)
                return ship.m_nview;

            if (component is ShipControlls shipControls)
                return shipControls.m_nview;

            if (component is Vagon vagon)
                return vagon.m_nview;

            if (component is Catapult catapult)
                return catapult.m_nview;

            if (component is MapTable mapTable)
                return mapTable.m_nview;

            if (component is Turret turret)
                return turret.m_nview;

            if (component is Fireplace fireplace)
                return fireplace.m_nview;

            if (component is Tameable tameable)
                return tameable.m_nview;

            if (component is Sadle sadle)
                return sadle.m_nview;

            if (component is TeleportWorld teleportWorld)
                return teleportWorld.m_nview;

            return component.GetComponent<ZNetView>() ?? component.GetComponentInParent<ZNetView>();
        }

        internal static ZDO GetWardZDO(this UnityEngine.Component component)
        {
            ZNetView nview = component.GetComponentZNetView();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }
    }

    internal static class PrivateAreaExtensions
    {
        internal static ZDO GetWardZDO(this PrivateArea ward)
        {
            ZNetView nview = ward.GetComponentZNetView();
            return nview != null && nview.IsValid() ? nview.GetZDO() : null;
        }

        internal static bool HasDirectWardAccess(this PrivateArea ward, long playerID) => ProtectiveWards.HasDirectAccessToWard(ward, playerID);

        internal static bool HasConnectedWardAccess(this PrivateArea ward, long playerID, ProtectiveWards.WardConnectedAccessMode mode) => ProtectiveWards.HasAccessToWardOrConnectedWard(ward, playerID, mode);
    }

    internal static class WardZdoExtensions
    {
        internal static bool IsWard(this ZDO zdo) => WardZdoUtils.IsWard(zdo);

        internal static long GetCreatorId(this ZDO zdo) => zdo != null ? zdo.GetLong(ZDOVars.s_creator, 0L) : 0L;

        internal static bool IsCreator(this ZDO zdo, long creatorID) => zdo.GetCreatorId() == creatorID;

        internal static bool HasDirectWardAccess(this ZDO zdo, long playerID) => WardZdoUtils.HasDirectAccessToWardZdo(zdo, playerID);

        internal static bool HasConnectedWardAccess(this ZDO zdo, long playerID, ProtectiveWards.WardConnectedAccessMode mode, System.Func<ZDO, bool> isActiveCandidate) => WardZdoUtils.HasAccessToWardOrConnectedWardZdo(zdo, playerID, mode, isActiveCandidate);

        internal static float GetWardRadius(this ZDO zdo) => WardZdoUtils.GetWardRadius(zdo);

        internal static bool IsWardPermitted(this ZDO zdo, long playerID) => WardZdoUtils.IsPermitted(zdo, playerID);
    }
}
