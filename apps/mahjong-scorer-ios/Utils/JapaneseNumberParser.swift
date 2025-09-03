import Foundation

enum JapaneseNumberParser {
    // Simple parser for small integers (0-20) commonly spoken
    static func parse(_ text: String) -> Int? {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if let n = Int(trimmed) { return n }
        let map: [String: Int] = [
            "ゼロ":0, "れい":0, "零":0,
            "いち":1, "一":1, "ひとつ":1,
            "に":2, "二":2, "ふたつ":2,
            "さん":3, "三":3, "みっつ":3,
            "よん":4, "し":4, "四":4, "よっつ":4,
            "ご":5, "五":5, "いつつ":5,
            "ろく":6, "六":6, "むっつ":6,
            "なな":7, "しち":7, "七":7,
            "はち":8, "八":8, "やっつ":8,
            "きゅう":9, "く":9, "九":9,
            "じゅう":10, "十":10,
            "じゅういち":11, "十一":11,
            "じゅうに":12, "十二":12,
            "じゅうさん":13, "十三":13,
            "じゅうよん":14, "十四":14,
            "じゅうご":15, "十五":15,
            "じゅうろく":16, "十六":16,
            "じゅうなな":17, "十七":17,
            "じゅうはち":18, "十八":18,
            "じゅうきゅう":19, "十九":19,
            "にじゅう":20, "二十":20
        ]
        return map[trimmed]
    }
}

