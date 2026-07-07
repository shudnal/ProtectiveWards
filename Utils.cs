using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtectiveWards
{
    public static class StringExtensions
    {
        public static string Localize(this string text) => Localization.instance != null ? Localization.instance.Localize(text) : text;

        public static string Localize(this string text, params string[] words) => Localization.instance != null ? Localization.instance.Localize(text, words) : text;
    }

}
