using System.Collections.Generic;

namespace Banganka.Core.Data
{
    public class StoryChapter
    {
        public string id;
        public int number;
        public string title;
        public string description;
        public bool unlocked;
        public bool completed;
        public string keyCharacter;
        public Aspect themeAspect;
    }

    public static class StoryDatabase
    {
        static List<StoryChapter> _chapters;

        public static IReadOnlyList<StoryChapter> Chapters
        {
            get
            {
                if (_chapters == null) Init();
                return _chapters;
            }
        }

        static void Init()
        {
            _chapters = new List<StoryChapter>
            {
                new StoryChapter
                {
                    id = "CH_01", number = 1,
                    title = "灯凪の章 ── 届けたい声",
                    description = "盲目の語り部・灯凪。戦火で滅んだ都市国家の生き残り。\n" +
                                  "死者の声を聞き、言葉にして生きてきた少女。\n" +
                                  "「私が語った物語を、誰かひとりに届けたい」",
                    unlocked = true, completed = false,
                    keyCharacter = "灯凪",
                    themeAspect = Aspect.Weave // 穏（緑）
                },
                new StoryChapter
                {
                    id = "CH_02", number = 2,
                    title = "Aldricの章 ── 贖罪の剣",
                    description = "大陸を征服した王・Aldric。強さのために民を犠牲にした男。\n" +
                                  "万願果で滅ぼした民を蘇らせたい。\n" +
                                  "「贖罪か自己満足か──そんなことはどうでもいい。俺は戦う」",
                    unlocked = false, completed = false,
                    keyCharacter = "Aldric",
                    themeAspect = Aspect.Contest // 曙（赤）
                },
                new StoryChapter
                {
                    id = "CH_03", number = 3,
                    title = "崔鋒の章 ── 敗者の真実",
                    description = "革命に敗れた老将・崔鋒。歴史から抹消される側の人間。\n" +
                                  "力ではなく、記録を残すための戦い。\n" +
                                  "「勝者が歴史を書く。だが俺は、敗者の真実を残す」",
                    unlocked = false, completed = false,
                    keyCharacter = "崔鋒",
                    themeAspect = Aspect.Hush // 玄（暗）
                },
                new StoryChapter
                {
                    id = "CH_04", number = 4,
                    title = "Rahimの章 ── 姉への祈り",
                    description = "産業崩壊期の少年・Rahim。姉を事故で失い、蘇らせたいと願う。\n" +
                                  "純粋な動機──だが果求者は知っている。死者を蘇らせることの意味を。\n" +
                                  "「姉ちゃんは俺に、誰かを助けられる人間になれって言ってた」",
                    unlocked = false, completed = false,
                    keyCharacter = "Rahim",
                    themeAspect = Aspect.Manifest // 遊（黄）
                },
                new StoryChapter
                {
                    id = "CH_05", number = 5,
                    title = "Amaraの章 ── 永遠の破壊者",
                    description = "文明崩壊後の建築家・Amara。自分が作った都市が人々を苦しめ続ける。\n" +
                                  "「永遠」の名を持つ女が、永遠を消そうとしている。\n" +
                                  "「私が設計したこの世界を、根こそぎ消したい」",
                    unlocked = false, completed = false,
                    keyCharacter = "Amara",
                    themeAspect = Aspect.Verse // 妖（紫）
                },
                new StoryChapter
                {
                    id = "CH_06", number = 6,
                    title = "Vaelの章 ── 存在の証明",
                    description = "記憶なき交界の住人・Vael。由来のない名を持つ存在。\n" +
                                  "果求者が「なかったことにしたかった自分」の姿に重なる最終章。\n" +
                                  "「お前は俺を覚えてくれるか。俺がここにいたことを」",
                    unlocked = false, completed = false,
                    keyCharacter = "Vael",
                    themeAspect = Aspect.Whisper // 空（青）
                },
            };
        }

        public static void UnlockChapter(string id)
        {
            if (_chapters == null) Init();
            foreach (var ch in _chapters)
                if (ch.id == id) { ch.unlocked = true; return; }
        }

        public static void CompleteChapter(string id)
        {
            if (_chapters == null) Init();
            for (int i = 0; i < _chapters.Count; i++)
            {
                if (_chapters[i].id == id)
                {
                    _chapters[i].completed = true;
                    if (i + 1 < _chapters.Count)
                        _chapters[i + 1].unlocked = true;
                    return;
                }
            }
        }
    }
}
