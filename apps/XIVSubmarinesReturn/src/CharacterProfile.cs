using System;
using System.Collections.Generic;

namespace XIVSubmarinesReturn
{
    // 単一キャラクターのプロファイル（最大10件想定）
    public sealed class CharacterProfile
    {
        public ulong ContentId { get; set; } // IClientState.LocalContentId
        public string CharacterName { get; set; } = string.Empty;
        public string WorldName { get; set; } = string.Empty;
        public string FreeCompanyName { get; set; } = string.Empty;
        public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.MinValue;

        // キャラ依存の保存項目（従来はグローバル）
        public string[] SlotAliases { get; set; } = new string[4];
        public Dictionary<int, string> LastRouteBySlot { get; set; } = new();
        public Dictionary<byte, string> RouteNames { get; set; } = new();

        // 直近のスナップショット（プロファイル毎に永続化）
        public SubmarineSnapshot? LastSnapshot { get; set; }

        // 降格防止のための「最後に良かったルート」永続キャッシュ（Character|World|Slot）
        public Dictionary<int, LastGoodRoute> LastGoodRouteBySlot { get; set; } = new();
    }
}
