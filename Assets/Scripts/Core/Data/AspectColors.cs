using UnityEngine;
using System.Collections.Generic;

namespace Banganka.Core.Data
{
    public enum Aspect
    {
        Contest,   // 曙 - red  (攻撃・突破・速攻)
        Whisper,   // 空 - blue (妨害・手数・攪乱)
        Weave,     // 穏 - green (展開・補助・継続力)
        Manifest,  // 遊 - yellow (柔軟・中速・盤面圧)
        Verse,     // 妖 - purple (術・連鎖・変則)
        Hush       // 玄 - dark (防御・封じ・耐久)
    }

    public static class AspectColors
    {
        static readonly Dictionary<Aspect, Color> _colors = new()
        {
            { Aspect.Contest,  HexToColor("FF5A36") }, // 曙 赤
            { Aspect.Whisper,  HexToColor("4DA3FF") }, // 空 青
            { Aspect.Weave,    HexToColor("59C36A") }, // 穏 緑
            { Aspect.Manifest, HexToColor("F4C542") }, // 遊 黄
            { Aspect.Verse,    HexToColor("9A5BFF") }, // 妖 紫
            { Aspect.Hush,     HexToColor("3A3A46") }, // 玄 暗
        };

        static readonly Dictionary<Aspect, string> _displayNames = new()
        {
            { Aspect.Contest,  "曙" },
            { Aspect.Whisper,  "空" },
            { Aspect.Weave,    "穏" },
            { Aspect.Manifest, "遊" },
            { Aspect.Verse,    "妖" },
            { Aspect.Hush,     "玄" },
        };

        public static Color GetColor(Aspect aspect) => _colors[aspect];
        public static string GetDisplayName(Aspect aspect) => _displayNames[aspect];

        static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
