using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProtectiveWards
{
    internal static class ItemNameTokens
    {
        private static readonly Dictionary<string, string> s_itemNames = new(StringComparer.OrdinalIgnoreCase);


        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        private static class ObjectDB_Awake_UpdateItemNameTokens
        {
            private static void Postfix() => UpdateRegisters();
        }


        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static class ObjectDB_CopyOtherDB_UpdateItemNameTokens
        {
            private static void Postfix() => UpdateRegisters();
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static class Player_Load_UpdateItemNameTokens
        {
            private static void Prefix() => UpdateRegisters();
        }

        internal static void UpdateRegisters()
        {
            if (!ObjectDB.instance)
                return;

            s_itemNames.Clear();

            foreach (GameObject item in ObjectDB.instance.m_items)
            {
                if (item == null || item.GetComponent<ItemDrop>() is not ItemDrop itemDrop)
                    continue;

                ItemDrop.ItemData.SharedData shared = itemDrop.m_itemData?.m_shared;
                if (shared == null || string.IsNullOrWhiteSpace(shared.m_name) || !shared.m_name.StartsWith("$"))
                    continue;

                Register(item.name, shared.m_name);
                Register(shared.m_name, shared.m_name);
                Register(Localization.instance?.Localize(shared.m_name), shared.m_name);
            }
        }

        internal static string GetItemName(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            string key = input.Trim();
            return s_itemNames.TryGetValue(key, out string token) ? token : key;
        }

        private static void Register(string key, string token)
        {
            if (!string.IsNullOrWhiteSpace(key))
                s_itemNames[key.Trim()] = token;
        }
    }
}
