#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

namespace Banganka.Editor
{
    public static class FontSetup
    {
        [MenuItem("万願果/Setup Japanese Font")]
        public static void SetupJapaneseFont()
        {
            // Try to find a Japanese system font
            string[] candidates = new[]
            {
                "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc",
                "/System/Library/Fonts/Hiragino Sans GB.ttc",
                "/System/Library/Fonts/ヒラギノ丸ゴ ProN W4.ttc",
                "/Library/Fonts/Arial Unicode.ttf",
                "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
            };

            string sourcePath = null;
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    sourcePath = c;
                    break;
                }
            }

            // Also try Noto fonts if installed
            if (sourcePath == null)
            {
                string[] notoCandidates = new[]
                {
                    "/System/Library/Fonts/NotoSansJP-Regular.otf",
                    "/System/Library/Fonts/Supplemental/NotoSansJP-Regular.otf",
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Library/Fonts/NotoSansJP-Regular.otf"),
                    Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Library/Fonts/NotoSansCJKjp-Regular.otf"),
                };
                foreach (var c in notoCandidates)
                {
                    if (File.Exists(c))
                    {
                        sourcePath = c;
                        break;
                    }
                }
            }

            if (sourcePath == null)
            {
                // List available system fonts for user
                Debug.LogError(
                    "日本語フォントが見つかりませんでした。\n" +
                    "以下のいずれかを試してください:\n" +
                    "1. Window > TextMeshPro > Font Asset Creator を開く\n" +
                    "2. Source Font に日本語対応フォントをドラッグ\n" +
                    "3. Character Set を 'Custom Characters' にして日本語文字を入力\n" +
                    "4. Generate Font Atlas → Save で Assets/Fonts/ に保存\n\n" +
                    "または NotoSansJP をダウンロードして Assets/Fonts/ に配置してください。"
                );

                // Try to use any available font
                var fonts = Font.GetOSInstalledFontNames();
                string jaFont = null;
                foreach (var f in fonts)
                {
                    if (f.Contains("Hiragino") || f.Contains("ヒラギノ") ||
                        f.Contains("Noto") || f.Contains("Yu Gothic") ||
                        f.Contains("Meiryo") || f.Contains("メイリオ"))
                    {
                        jaFont = f;
                        break;
                    }
                }

                if (jaFont != null)
                {
                    Debug.Log($"システムフォント '{jaFont}' を検出。Font.CreateDynamicFontFromOSFont で対応します。");
                    CreateFallbackFromOSFont(jaFont);
                    return;
                }

                Debug.LogWarning("利用可能な日本語システムフォント一覧:");
                foreach (var f in fonts)
                    Debug.Log($"  {f}");
                return;
            }

            // Copy font to project
            string destDir = "Assets/Fonts";
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            string ext = Path.GetExtension(sourcePath);
            string destPath = Path.Combine(destDir, "JapaneseFont" + ext);
            File.Copy(sourcePath, destPath, true);
            AssetDatabase.Refresh();

            Debug.Log($"日本語フォントをコピーしました: {destPath}");
            Debug.Log(
                "次の手順:\n" +
                "1. Window > TextMeshPro > Font Asset Creator を開く\n" +
                "2. Source Font File に Assets/Fonts/JapaneseFont を設定\n" +
                "3. Atlas Resolution: 4096x4096\n" +
                "4. Character Set: Custom Characters\n" +
                "5. 下の文字列をCustom Character Listにペースト\n" +
                "6. Generate Font Atlas → Save as... で Assets/Fonts/JapaneseFont SDF.asset\n" +
                "7. Edit > Project Settings > TextMeshPro > Settings > Default Font Asset に設定"
            );
        }

        static void CreateFallbackFromOSFont(string fontName)
        {
            // Create a Unity Font from OS font
            var font = Font.CreateDynamicFontFromOSFont(fontName, 32);
            if (font == null)
            {
                Debug.LogError("フォント作成に失敗しました。");
                return;
            }

            string destDir = "Assets/Fonts";
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            Debug.Log(
                $"'{fontName}' を検出しました。\n" +
                "TMP で日本語を表示するには Font Asset Creator でSDFアセットを生成する必要があります。\n\n" +
                "【簡易手順】\n" +
                "1. Window > TextMeshPro > Font Asset Creator\n" +
                "2. Source Font: ドロップダウンから '" + fontName + "' を選択（またはOSフォントファイルをドラッグ）\n" +
                "3. Sampling Point Size: Auto Sizing\n" +
                "4. Atlas Resolution: 4096 x 4096\n" +
                "5. Character Set: 'Custom Characters'\n" +
                "6. Custom Character List に以下をペースト（メニュー '万願果/Print Required Characters' で出力）\n" +
                "7. Generate Font Atlas → Save\n" +
                "8. Project Settings > TMP Settings > Default Font Asset に設定"
            );
        }

        [MenuItem("万願果/Print Required Characters")]
        public static void PrintRequiredCharacters()
        {
            // All Japanese characters used in the game
            string chars =
                // Hiragana
                "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん" +
                "がぎぐげござじずぜぞだぢづでどばびぶべぼぱぴぷぺぽ" +
                "ぁぃぅぇぉっゃゅょ" +
                // Katakana
                "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン" +
                "ガギグゲゴザジズゼゾダヂヅデドバビブベボパピプペポ" +
                "ァィゥェォッャュョヴ" +
                // Numbers & basic symbols
                "0123456789+-=/<>()[]{}:;,.!?・…「」『』【】" +
                // Latin
                "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
                // Game-specific kanji
                "万願果交界求者主札顕現詠術界律力成撃相空曙穏妖遊玄" +
                "戦闘攻防御退場待機消耗酔先攻後手番" +
                "断章剣士誓鎧衛迅刃追跡勝鬨槍手境語部夢見守継糸工匠" +
                "現世渡旅人灰都盾持祈歌舞静謐執行" +
                "罪号令文囁導眠結句補修閃旋律沈黙帳" +
                "増幅圏闘争域速境鎖" +
                "ホームバトルカードストーリーショップ" +
                "対準備完了開始降参終了勝利敗北引分" +
                "ターン願主レベルアップ" +
                "戦力値味方敵体除去" +
                "全設置効直" +
                "第一二三四五六七八九十" +
                "覚醒目集者達本来時代時空重領存在" +
                "古代王滅未兵名放浪失魔師異歴史運命背負奇跡地" +
                "叶滅国取戻蘇赦過去変書換衝突" +
                "放率書換願重武器激突" +
                "ローカル所コイン" +
                "選択中央傾危険端到達" +
                "数表示" +
                "枚固定同上限" +
                "内訳混在可能" +
                "残盤面" +
                "即発動墓送" +
                "共有永続上" +
                "回復" +
                "情火奇" +
                "果のを" +
                // More common kanji for UI
                "確認取消戻読込保存設定通知更新情報画面" +
                "入出作参加接続状態有無効" +
                "商品購買価格注通貨" +
                "進行解放章節点図鑑" +
                "使方指基本";

            // Deduplicate
            var set = new System.Collections.Generic.HashSet<char>();
            foreach (char c in chars)
                if (c != ' ' && c != '\n') set.Add(c);

            var sb = new System.Text.StringBuilder();
            foreach (char c in set) sb.Append(c);
            string result = sb.ToString();

            Debug.Log($"=== Required Characters ({result.Length} chars) ===\n{result}");
            GUIUtility.systemCopyBuffer = result;
            Debug.Log("クリップボードにコピーしました。Font Asset Creator の Custom Character List にペーストしてください。");
        }
    }
}
#endif
