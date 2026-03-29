#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Banganka.Tests.EditMode
{
    /// <summary>
    /// 用語監査 (CLAUDE.md 用語ルール)
    /// 禁止用語が日本語テキストに含まれていないか検証
    /// </summary>
    public static class TerminologyAudit
    {
        // 禁止用語 → 正式用語
        static readonly Dictionary<string, string> ForbiddenTerms = new()
        {
            { "ダメージ", "退場させる / 願力をN動かす" },
            { "破壊", "退場" },
            { "タップ", "消耗" },
            { "アンタップ", "待機" },
            { "マナ", "CP" },
            { "バーン", "願力をN動かす" },
            { "ヒーロー", "願主" },
            { "チャンピオン", "願主" },
            { "デッキ破壊", "（該当メカニクスなし）" },
        };

        // 許可される文脈 (コメント内、変数名内などは許可)
        static readonly string[] AllowedContexts =
        {
            "//", "///", "/*", "Debug.Log", "LogWarning", "LogError",
            "TUTORIAL_FLOW", "COMPANION_CHARACTER", "ACCESSIBILITY",
            "damageType", "wishDamage", "HpDamage", "DamageText",
            "battlePower", "DamageHalve", "ApplyHpDamage",
        };

        public static List<(string file, int line, string term, string replacement)> RunAudit()
        {
            var violations = new List<(string, int, string, string)>();
            string scriptsPath = Path.Combine(Application.dataPath, "Scripts");

            if (!Directory.Exists(scriptsPath)) return violations;

            var csFiles = Directory.GetFiles(scriptsPath, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var lines = File.ReadAllLines(file);
                string relativePath = file.Replace(Application.dataPath, "Assets");

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];

                    // Skip allowed contexts
                    bool inAllowedContext = AllowedContexts.Any(ctx => line.Contains(ctx));
                    if (inAllowedContext) continue;

                    foreach (var kv in ForbiddenTerms)
                    {
                        // Only check string literals (Japanese text in quotes)
                        var matches = Regex.Matches(line, $"\"{kv.Key}\"");
                        if (matches.Count > 0)
                        {
                            violations.Add((relativePath, i + 1, kv.Key, kv.Value));
                        }

                        // Also check UI text assignments
                        if (line.Contains(".text") && line.Contains(kv.Key))
                        {
                            violations.Add((relativePath, i + 1, kv.Key, kv.Value));
                        }
                    }
                }
            }

            return violations;
        }

        public static void LogAuditResults()
        {
            var results = RunAudit();
            if (results.Count == 0)
            {
                Debug.Log("[TerminologyAudit] 用語監査クリア — 禁止用語なし");
                return;
            }

            Debug.LogWarning($"[TerminologyAudit] {results.Count}件の禁止用語を検出:");
            foreach (var (file, line, term, replacement) in results)
            {
                Debug.LogWarning($"  {file}:{line} — 「{term}」→ 正式: 「{replacement}」");
            }
        }
    }
}
#endif
