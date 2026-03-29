using System;
using System.Collections.Generic;

namespace Banganka.Core.Data
{
    /// <summary>
    /// 願主秘話（エージェント秘話相当）のデータモデル。
    /// 各願主の元いた世界、仲間、過去のエピソードを段階的に解放して読める。
    ///
    /// 解放条件:
    ///   - メインストーリーの該当章クリア → プロフィール + EP01 解放
    ///   - その願主のリーダーでバトル勝利N回 → 追加エピソード解放
    /// </summary>

    // ── エピソードの種類 ──
    public enum ArchiveEpisodeType
    {
        Profile,      // 基本プロフィール（常時表示）
        Origin,       // 元いた時空・時代の話
        Companion,    // 配下・仲間のエピソード
        Memory,       // 過去の記憶・回想
        CrossLeader,  // 他の願主との交流
        Secret        // 隠しエピソード（全解放後）
    }

    [Serializable]
    public class ArchiveEpisode
    {
        public string id;              // "ALD_EP01" etc.
        public string title;           // "花園の秘密"
        public ArchiveEpisodeType type;
        public string[] paragraphs;    // テキスト本文（段落ごと）
        public string unlockCondition; // 解放条件の説明文
        public int requiredWins;       // 必要勝利数（0 = ストーリー解放）
        public string requiredStoryId; // 必要ストーリーID（"CH_02" etc.）
        public string[] relatedCards;  // 関連カードID（そのエピソードに登場するキャラ）
        public string backgroundKey;   // 背景画像キー（Resources/Story/Backgrounds/）
    }

    [Serializable]
    public class LeaderArchive
    {
        public string leaderId;          // "LDR_CON_01" etc.
        public string leaderName;        // "アルドリック"
        public Aspect aspect;
        public string worldName;         // "次元侵食期・大陸征服王朝"
        public string era;               // "中世征服王朝時代"
        public string sfElement;         // "次元の亀裂を兵器化した世界"
        public string profileSummary;    // 短いプロフィール
        public List<ArchiveEpisode> episodes;
    }

    /// <summary>
    /// 願主秘話データベース。6人分のアーカイブを保持。
    /// </summary>
    public static class LeaderArchiveDatabase
    {
        static List<LeaderArchive> _archives;

        public static IReadOnlyList<LeaderArchive> Archives
        {
            get
            {
                if (_archives == null) Init();
                return _archives;
            }
        }

        public static LeaderArchive GetByLeaderId(string leaderId)
        {
            if (_archives == null) Init();
            foreach (var a in _archives)
                if (a.leaderId == leaderId) return a;
            return null;
        }

        public static LeaderArchive GetByAspect(Aspect aspect)
        {
            if (_archives == null) Init();
            foreach (var a in _archives)
                if (a.aspect == aspect) return a;
            return null;
        }

        /// <summary>エピソードの解放判定</summary>
        public static bool IsEpisodeUnlocked(ArchiveEpisode ep, PlayerData player)
        {
            // ストーリー条件
            if (!string.IsNullOrEmpty(ep.requiredStoryId))
            {
                var chapters = StoryDatabase.Chapters;
                bool found = false;
                foreach (var ch in chapters)
                {
                    if (ch.id == ep.requiredStoryId && ch.completed)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            // 勝利数条件
            if (ep.requiredWins > 0 && player.wins < ep.requiredWins)
                return false;

            return true;
        }

        // ============================================================
        // 秘話データ初期化
        // ============================================================
        static void Init()
        {
            _archives = new List<LeaderArchive>
            {
                BuildAldric(),
                BuildVael(),
                BuildTouna(),
                BuildAmara(),
                BuildRahim(),
                BuildTsueifeng(),
            };
        }

        // ────────────────────────────────────────
        // アルドリック（曙 / Contest）
        // ────────────────────────────────────────
        static LeaderArchive BuildAldric()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_CON_01",
                leaderName = "アルドリック",
                aspect = Aspect.Contest,
                worldName = "次元侵食期・大陸征服王朝",
                era = "中世征服王朝時代",
                sfElement = "次元の亀裂を兵器化した世界。使うたびに世界が壊れていく",
                profileSummary = "大陸を征服した暴君にして、焦土に花を植える男。\n" +
                                 "暗い目の下に隠した贖罪の心を、誰にも見せない。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "ALD_EP01", title = "征服王の素顔",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_02", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第2章クリア",
                        paragraphs = new[]
                        {
                            "アルドリック。大陸征服王。暴君。人はそう呼ぶ。",
                            "だが誰も知らない。この男が毎夜、踏みにじった民の顔を夢に見ていることを。",
                            "目の下の隈は、その夢から逃れられない証。",
                            "鎧の内側に描かれた花の絵――孤児院の子どもがクレヨンで描いたもの――だけが、彼の唯一の宝物だった。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "ALD_EP02", title = "次元裂開の兵器",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_02", requiredWins = 3,
                        relatedCards = new[] { "MAN_CON_11" }, // イグナーツ
                        unlockCondition = "アルドリックで3勝",
                        paragraphs = new[]
                        {
                            "アルドリックの軍が無敵だったのは、兵の強さだけではない。",
                            "次元の亀裂を兵器として利用する技術。空間を裂き、敵の背後に部隊を送り込む。",
                            "だがその代償は大きかった。亀裂を開くたびに、世界そのものが少しずつ壊れていった。",
                            "総帥イグナーツはこの技術を躊躇なく使った。アルドリックだけが、その先に何があるか理解していた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "ALD_EP03", title = "副将ガーネット",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_02", requiredWins = 5,
                        relatedCards = new[] { "MAN_CON_10" }, // ガーネット
                        unlockCondition = "アルドリックで5勝",
                        paragraphs = new[]
                        {
                            "ガーネット。血誓の副将。アルドリックに最も長く仕えた女戦士。",
                            "彼女だけが知っている。征服王が、占領した街の焼け跡に花の種を撒いていることを。",
                            "「王よ、また園芸ですか」「……黙っていろ」",
                            "ガーネットは黙った。だが翌朝、彼女の剣の鞘に小さな花が挿してあった。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "ALD_EP04", title = "焦土の花園",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_02", requiredWins = 10,
                        relatedCards = new[] { "MAN_CON_01", "MAN_CON_02" }, // テオ, フレデリク
                        unlockCondition = "アルドリックで10勝",
                        paragraphs = new[]
                        {
                            "征服した街には必ず孤児が残った。",
                            "テオもフレデリクも、そうした孤児だった。彼らは王を恨むべきだった。",
                            "だがアルドリックは黙って彼らの傍にいた。庭の水やりを教え、木の植え方を見せた。",
                            "ある日、テオがアルドリックの鎧の内側にクレヨンで花を描いた。",
                            "アルドリックは何も言わなかった。ただ、その絵を決して消さないと鎧匠に命じた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "ALD_EP05", title = "崔鋒との酒",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_03", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "第3章クリア + アルドリックで15勝",
                        paragraphs = new[]
                        {
                            "焚き火を挟んで、二人の男が黙って座っている。",
                            "崔鋒が酒を注ぐ。アルドリックが受け取る。二人とも何も言わない。",
                            "「お前の背中は重そうだな」崔鋒がぼそりと言った。",
                            "「……お前もな」",
                            "それだけだった。それだけで十分だった。",
                            "二人は同じものを背負っている。死者の名前を。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "ALD_EP06", title = "鎧の内側の絵",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + アルドリックで25勝",
                        paragraphs = new[]
                        {
                            "全てが終わった後、アルドリックは鎧を脱いだ。",
                            "内側に描かれた花の絵。子どものクレヨン画。色あせて、かすれて、でもまだそこにある。",
                            "「……贖罪か、自己満足か。そんなことはどうでもよかった」",
                            "彼は花の種を地面に撒いた。焦土ではない、ただの土に。",
                            "初めて、破壊する前の大地に花を植えた。",
                            "それが彼の願いの答えだった。",
                        },
                    },
                },
            };
        }

        // ────────────────────────────────────────
        // ヴァエル（空 / Whisper）
        // ────────────────────────────────────────
        static LeaderArchive BuildVael()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_WHI_01",
                leaderName = "ヴァエル",
                aspect = Aspect.Whisper,
                worldName = "不明 ── 交界そのもの",
                era = "不明（記録なし）",
                sfElement = "交界に最初から存在した、あるいは界主の試作存在。記録が一切ない",
                profileSummary = "名前の由来すらない。影がない。足跡が残らない。\n" +
                                 "3秒黙ると消えてしまいそうで、ずっと喋り続けている。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "VAE_EP01", title = "名前のない存在",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_06", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第6章クリア",
                        paragraphs = new[]
                        {
                            "ヴァエル。その名前には語源がない。どの言語にも属さない。",
                            "当然だ。名付けた人間がいないのだから。",
                            "影がない。足跡が残らない。鏡に映らないことすらある。",
                            "それでも彼は笑い続ける。喋り続ける。止まったら消えてしまうから。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "VAE_EP02", title = "借り物の服",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_06", requiredWins = 3,
                        relatedCards = new string[0],
                        unlockCondition = "ヴァエルで3勝",
                        paragraphs = new[]
                        {
                            "ヴァエルの服は、いつも誰かのものだ。",
                            "アルドリックのシャツ。灯凪の帯。ラヒムのマフラー。",
                            "「匂いがするだろ？人の匂い。それを着てると、自分がここにいる気がするんだ」",
                            "アルドリックは黙って着せた。灯凪は聞こえないふりをした。",
                            "ラヒムだけが「明日返せよ」と笑った。ヴァエルはその言葉に一番救われた。",
                            "「明日」があるということは、自分が明日もここにいると信じてもらえたということだから。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "VAE_EP03", title = "ラヒムと走る理由",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_06", requiredWins = 5,
                        relatedCards = new string[0],
                        unlockCondition = "ヴァエルで5勝",
                        paragraphs = new[]
                        {
                            "ヴァエルとラヒム。二人はよく走る。理由もなく、ただ走る。",
                            "影のある少年と、影のない存在。どちらも何かを追いかけている。",
                            "ラヒムは止まると姉のことを考えてしまうから走る。",
                            "ヴァエルは止まると消えてしまいそうだから走る。",
                            "二人は同じ速度で走る。決して追いつけないものに向かって。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "VAE_EP04", title = "灯凪に見抜かれた日",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_06", requiredWins = 10,
                        relatedCards = new string[0],
                        unlockCondition = "ヴァエルで10勝",
                        paragraphs = new[]
                        {
                            "灯凪は盲目だ。だが、「存在の気配」を感じ取る。",
                            "ある日、灯凪がヴァエルに言った。",
                            "「あんた、影がないね」",
                            "ヴァエルの笑顔が、一瞬だけ凍った。",
                            "影がないことを指摘されたのは初めてだった。見える人間には分からないことを、見えない彼女が感じ取った。",
                            "「……バレた？」「最初から。でも声はある。声があるなら、あんたはここにいるよ」",
                            "ヴァエルはその日、初めて3秒以上黙ることができた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "VAE_EP05", title = "ナルの沈黙",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_06", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "ヴァエルで15勝",
                        paragraphs = new[]
                        {
                            "ナルはいつも喋っている。果求者に、願主たちに、誰にでも。",
                            "だがヴァエルの前では、ナルは黙る。",
                            "最終章の焚き火で、ナルはヴァエルを見つめたまま一言も発さなかった。",
                            "他の願主は気づかなかった。だが果求者は気づいた。",
                            "ナルが何かを感じ取っている。ヴァエルの中に、何かを見ている。",
                            "界主の欠片か。交界そのものの残滓か。",
                            "ナルは答えなかった。ただ、少しだけ震えていた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "VAE_EP06", title = "影が映った日",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + ヴァエルで25勝",
                        paragraphs = new[]
                        {
                            "果求者がヴァエルの名前を呼んだ。",
                            "何度も呼ばれた名前。でもこの時だけは、違った。",
                            "焚き火の光の中で、ヴァエルの足元に、薄い影が映った。",
                            "ほんの一瞬。だが確かに。",
                            "ヴァエルは下を見た。そして笑った。いつもの空っぽの笑みではなく。",
                            "「……ありがとう。覚えていてくれて」",
                            "影はすぐに消えた。でもヴァエルは、初めて3秒以上黙ったまま笑い続けた。",
                        },
                    },
                },
            };
        }

        // ────────────────────────────────────────
        // 灯凪（穏 / Weave）
        // ────────────────────────────────────────
        static LeaderArchive BuildTouna()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_WEA_01",
                leaderName = "灯凪",
                aspect = Aspect.Weave,
                worldName = "第一世代星間植民都市国家",
                era = "口伝記録時代（文字以前）",
                sfElement = "恒星間入植者が建てた植民都市。母星との通信が途絶し孤立した",
                profileSummary = "生まれつき盲目の語り部。滅んだ都市国家の最後の生き残り。\n" +
                                 "死者の声を聞き、物語として語り継ぐ少女。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "TOU_EP01", title = "声の巫女",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_01", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第1章クリア",
                        paragraphs = new[]
                        {
                            "灯凪。盲目の語り部。声の巫女。",
                            "左耳の金の耳飾りは語り部の証。もう片方は、滅んだ街に置いてきた。",
                            "彼女は嘘を許さない。声に嘘が混じると、すぐに分かるから。",
                            "「優しさ」という言葉を灯凪に使うと怒る。彼女の優しさは、真実を突きつけること。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TOU_EP02", title = "石畳の広場",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_01", requiredWins = 3,
                        relatedCards = new[] { "MAN_WEA_07", "MAN_WEA_08" }, // 織江, 花守
                        unlockCondition = "灯凪で3勝",
                        paragraphs = new[]
                        {
                            "都市国家の中心には石畳の広場があった。",
                            "毎朝、子どもたちが灯凪の髪を結いに来た。彼女は自分では結えないから。",
                            "織江が糸で飾り、花守が花を挿した。",
                            "夕方になると灯凪はその広場で物語を語った。",
                            "文字のない世界で、語り部の声だけが歴史を伝える唯一の方法だった。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TOU_EP03", title = "天文観測者たち",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_01", requiredWins = 5,
                        relatedCards = new[] { "MAN_WEA_05", "MAN_WEA_04" }, // 幹太, 穂波
                        unlockCondition = "灯凪で5勝",
                        paragraphs = new[]
                        {
                            "都市国家は星間入植者の末裔が建てた。",
                            "だから天文観測の伝統があった。幹太は大樹の上から星を読み、穂波は星の動きから種まきの時期を計った。",
                            "灯凪は星を見たことがない。だが星の物語を語ることはできた。",
                            "「見えなくても知っている。星は遠くで燃えていて、いつか消える。私たちと同じ」",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TOU_EP04", title = "都市国家の滅亡",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_01", requiredWins = 10,
                        relatedCards = new string[0],
                        unlockCondition = "灯凪で10勝",
                        paragraphs = new[]
                        {
                            "滅亡の日、灯凪は広場にいた。いつもと同じように。",
                            "母星との通信が途絶えてから、都市国家はゆっくりと衰えていた。",
                            "外敵ではなかった。宇宙的な孤立と、内部の崩壊。",
                            "最後の日、灯凪は全ての声を聞いた。死んでいく人々の声を。",
                            "彼女はその声を一つも忘れなかった。語り部だから。全て覚えて、語り継ぐ。",
                            "たとえ聞く者が誰もいなくても。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TOU_EP05", title = "崔鋒への敬意",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_03", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "第3章クリア + 灯凪で15勝",
                        paragraphs = new[]
                        {
                            "灯凪が崔鋒を「クソジジイ」と呼ぶのは、敬意の表現だ。",
                            "本当に嫌いな相手には、灯凪は何も言わない。声を使う価値がないから。",
                            "崔鋒の声には「記録者の重み」がある。灯凪はそれを聞き取っている。",
                            "二人とも、消えたものを残そうとしている。方法が違うだけ。",
                            "灯凪は声で。崔鋒は身体で。",
                            "「クソジジイ」「口の減らないガキが」――その掛け合いの奥に、互いへの唯一の敬語がある。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TOU_EP06", title = "最後の物語",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + 灯凪で25勝",
                        paragraphs = new[]
                        {
                            "全てが終わった後、灯凪は最後の物語を語った。",
                            "6人の願主の物語。果求者の物語。そしてナルの物語。",
                            "誰にでもなく、ただ空に向かって。",
                            "「この声が届くかは分からない。でも語ることに意味がある」",
                            "左耳の金の耳飾りが、かすかに震えた。",
                            "遠い滅んだ街から、返事が聞こえた気がした。",
                        },
                    },
                },
            };
        }

        // ────────────────────────────────────────
        // アマラ（妖 / Verse）
        // ────────────────────────────────────────
        static LeaderArchive BuildAmara()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_VER_01",
                leaderName = "アマラ",
                aspect = Aspect.Verse,
                worldName = "多星系AI管理文明",
                era = "ポスト文明崩壊期",
                sfElement = "AIが都市を設計・管理する完璧な文明。完璧すぎて人々の自由を奪った",
                profileSummary = "完璧な都市を設計した建築家。自分の作品が人々を苦しめていると知り、\n" +
                                 "「永遠」の名を持つ女が、永遠を消そうとしている。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "AMA_EP01", title = "永遠の設計者",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_05", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第5章クリア",
                        paragraphs = new[]
                        {
                            "アマラ。「永遠」を意味する名前。皮肉な名だ。",
                            "彼女は完璧な都市を設計した。AIが全てを管理する理想の文明。",
                            "だが完璧な設計は、人間から夢を奪った。逸脱する余地がない世界。",
                            "指先のインク染みは洗っても取れない。「もう創ることを止められない証拠」だと彼女は言う。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "AMA_EP02", title = "設計都市の真実",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_05", requiredWins = 3,
                        relatedCards = new[] { "MAN_VER_10" }, // イマーラ（守護AI）
                        unlockCondition = "アマラで3勝",
                        paragraphs = new[]
                        {
                            "設計都市は美しかった。犯罪率ゼロ。飢餓ゼロ。病気ゼロ。",
                            "守護AIイマーラは、アマラの設計思想を完璧に実行した。",
                            "だがある日、アマラは気づいた。市民の目が死んでいることに。",
                            "完璧すぎる世界には、何も望む必要がない。望まない人間は、生きていても死んでいる。",
                            "「私が作ったのは楽園じゃない。美しい牢獄だ」",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "AMA_EP03", title = "砂の模型",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_05", requiredWins = 5,
                        relatedCards = new[] { "MAN_VER_06" }, // ンジェリ
                        unlockCondition = "アマラで5勝",
                        paragraphs = new[]
                        {
                            "アマラは毎日、砂で都市の模型を作る。そして毎日、自分で壊す。",
                            "ンジェリだけがその理由を理解していた。",
                            "「設計したものを壊す練習をしているの？」",
                            "「練習ではない。確認よ。私にまだ壊せるか。壊す勇気があるか」",
                            "砂の模型を壊す手は震えていなかった。だが、壊した後に作り直す手は、いつも震えていた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "AMA_EP04", title = "ラヒムの一言",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_05", requiredWins = 10,
                        relatedCards = new string[0],
                        unlockCondition = "アマラで10勝",
                        paragraphs = new[]
                        {
                            "アマラは全員の食事の時間を管理していた。効率的な栄養摂取のために。",
                            "ある日、ラヒムが言った。",
                            "「アマラさんって、姉ちゃんに似てる」",
                            "二人とも黙った。長い沈黙。",
                            "ラヒムの姉は工場事故で死んだ。アマラの都市はAIの暴走で市民を苦しめた。",
                            "「守りたかった」人間を「仕組み」が壊した。二人は同じ傷を持っていた。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "AMA_EP05", title = "ヴァエルという誤差",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_06", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "第6章クリア + アマラで15勝",
                        paragraphs = new[]
                        {
                            "アマラの設計図に、ヴァエルは載っていない。",
                            "これは彼女にとって前代未聞のことだった。全てを設計できる女が、一人だけ設計できない。",
                            "「あなたは誤差？ それとも奇跡？」",
                            "ヴァエルは笑った。「どっちでもいいよ。俺がここにいるのは事実だろ？」",
                            "アマラは無意識にヴァエルの服の襟を直していた。設計できないものを、手で整えようとして。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "AMA_EP06", title = "美しいと言われた日",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + アマラで25勝",
                        paragraphs = new[]
                        {
                            "果求者がアマラの砂の模型を見て、言った。",
                            "「美しい」",
                            "たった一言。だがアマラの表情が、一瞬だけ崩れた。",
                            "丁寧語が途切れた。声が震えた。",
                            "「……そう。美しいのよ。美しいから、壊さなければならないの」",
                            "そしてすぐに丁寧語に戻った。だが指先のインク染みに、一滴だけ涙が落ちた。",
                        },
                    },
                },
            };
        }

        // ────────────────────────────────────────
        // ラヒム（遊 / Manifest）
        // ────────────────────────────────────────
        static LeaderArchive BuildRahim()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_MAN_01",
                leaderName = "ラヒム",
                aspect = Aspect.Manifest,
                worldName = "次元共鳴型重工業文明",
                era = "産業崩壊期",
                sfElement = "次元共鳴を利用した重工業。効率的だが構造崩壊を起こしやすい",
                profileSummary = "姉を工場事故で亡くした少年。\n" +
                                 "姉の工具箱を背負い、「誰かを助けられる人間になれ」という遺言を守り続ける。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "RAH_EP01", title = "工場街の少年",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_04", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第4章クリア",
                        paragraphs = new[]
                        {
                            "ラヒム。14歳。煤けた工場街の少年。",
                            "髪は自分で切る。不揃い。姉がいた頃は姉が切ってくれた。",
                            "姉の工具箱だけは、いつもピカピカに磨いてある。",
                            "笑顔の裏で、工具箱を握る手が白くなっていることに、誰も気づかない。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "RAH_EP02", title = "次元共鳴炉",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_04", requiredWins = 3,
                        relatedCards = new[] { "MAN_MAN_06" }, // ユーセフ
                        unlockCondition = "ラヒムで3勝",
                        paragraphs = new[]
                        {
                            "ラヒムの街は次元共鳴炉で動いていた。",
                            "異次元のエネルギーを利用する技術。効率的で安価。だが不安定。",
                            "技師ユーセフは「この炉は制御しきれない」と警告していた。",
                            "誰も聞かなかった。炉は動き続けた。街は栄えた。",
                            "そして、ある日崩壊した。ラヒムの姉が働いていた工場ごと。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "RAH_EP03", title = "路地裏の仲間たち",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_04", requiredWins = 5,
                        relatedCards = new[] { "MAN_MAN_01", "MAN_MAN_02", "MAN_MAN_03" }, // アリ, ダリア, ハッサン
                        unlockCondition = "ラヒムで5勝",
                        paragraphs = new[]
                        {
                            "アリ。幻灯機を操る小僧。いつも壊れかけの機械を動かしている。",
                            "ダリア。電気糸を紡ぐ少女。器用な指先で何でも直す。",
                            "ハッサン。鏡細工の少年。壊れた鏡を集めて万華鏡を作る。",
                            "みんな孤児だ。みんなラヒムの姉に世話になった。",
                            "姉が死んでから、ラヒムが彼らの面倒を見ている。「姉ちゃんがそうしてたから」。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "RAH_EP04", title = "姉の最後の日",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_04", requiredWins = 10,
                        relatedCards = new[] { "MAN_MAN_11" }, // ライラ
                        unlockCondition = "ラヒムで10勝",
                        paragraphs = new[]
                        {
                            "あの日の朝、姉は笑っていた。いつもと同じように。",
                            "「今日は早く帰るから、ラヒムの髪を切ってあげる」",
                            "帰ってこなかった。次元共鳴炉の制御不能で、工場ごと崩壊した。",
                            "ラヒムが見つけたのは、工具箱だけだった。",
                            "それ以来、ラヒムは髪を自分で切る。不揃いのまま。",
                            "姉が最後に切ってくれた長さを、なんとなく維持しようとして。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "RAH_EP05", title = "崔鋒の夜食",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_03", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "第3章クリア + ラヒムで15勝",
                        paragraphs = new[]
                        {
                            "ラヒムは時々、夜眠れない。姉の夢を見るから。",
                            "そういう夜、崔鋒の焚き火の近くに座る。何も言わずに。",
                            "崔鋒も何も言わない。ただ、少し多めに飯を炊く。",
                            "「食え」「……ありがとう、じいちゃん」",
                            "崔鋒の声が、ほんの少しだけ柔らかくなる。周りの全員が気づいている。本人だけが気づいていない。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "RAH_EP06", title = "届かない祈り",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + ラヒムで25勝",
                        paragraphs = new[]
                        {
                            "万願果は願いを叶える。だがラヒムは知っている。",
                            "死者を蘇らせることは、誰かの犠牲なしにはできないかもしれない。",
                            "「姉ちゃんは言ってた。誰かを助けられる人間になれって」",
                            "「姉ちゃんを蘇らせるために、誰かを犠牲にしたら、俺はその言葉を裏切ることになる」",
                            "工具箱を抱きしめた。初めて泣いた。",
                            "「ごめん、姉ちゃん。俺、姉ちゃんの言いつけを守るよ」",
                            "届かない祈り。でもそれが、ラヒムの答えだった。",
                        },
                    },
                },
            };
        }

        // ────────────────────────────────────────
        // 崔鋒（玄 / Hush）
        // ────────────────────────────────────────
        static LeaderArchive BuildTsueifeng()
        {
            return new LeaderArchive
            {
                leaderId = "LDR_HUS_01",
                leaderName = "崔鋒",
                aspect = Aspect.Hush,
                worldName = "記憶結晶文明・革命動乱期",
                era = "近代革命期",
                sfElement = "人間の記憶を結晶化して保存する技術。革命軍はこれで歴史を改竄した",
                profileSummary = "革命に敗れた老将。歴史から消された男。\n" +
                                 "三千の部下の名前を全て覚えている。一人目は李明。",
                episodes = new List<ArchiveEpisode>
                {
                    new ArchiveEpisode
                    {
                        id = "TSU_EP01", title = "最後の門将",
                        type = ArchiveEpisodeType.Profile,
                        requiredStoryId = "CH_03", requiredWins = 0,
                        relatedCards = new string[0],
                        unlockCondition = "メインストーリー第3章クリア",
                        paragraphs = new[]
                        {
                            "崔鋒。60を超えた老将。暴言家。料理上手。髭の手入れだけは怠らない。",
                            "革命軍に敗れ、歴史から消された。盾の紋章は革命軍に削り取られた。",
                            "だが彼は消されない。三千の部下の名前を、一人残らず覚えているから。",
                            "「勝者が歴史を書く。だが俺は、敗者の真実を身体に刻む」",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TSU_EP02", title = "記憶結晶の世界",
                        type = ArchiveEpisodeType.Origin,
                        requiredStoryId = "CH_03", requiredWins = 3,
                        relatedCards = new[] { "MAN_HUS_10" }, // 周鎖
                        unlockCondition = "崔鋒で3勝",
                        paragraphs = new[]
                        {
                            "崔鋒の世界では、人間の記憶を結晶化して保存できた。",
                            "歴史は結晶に刻まれ、改竄不可能……のはずだった。",
                            "革命軍は結晶の書き換え技術を開発した。歴史そのものを上書きした。",
                            "周鎖の仕事は、書き換えられていない原本の結晶を守ること。",
                            "だが守るべき結晶は、一つまた一つと奪われていった。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TSU_EP03", title = "李明という男",
                        type = ArchiveEpisodeType.Companion,
                        requiredStoryId = "CH_03", requiredWins = 5,
                        relatedCards = new[] { "MAN_HUS_08" }, // 李明
                        unlockCondition = "崔鋒で5勝",
                        paragraphs = new[]
                        {
                            "李明。崔鋒の部下の第一号。門番。真面目。無口。",
                            "崔鋒が「三千の部下の名前を覚えている」と言うとき、最初に出る名前が李明だ。",
                            "革命軍に殺された。歴史から消された。記憶結晶からも名前を消された。",
                            "だが崔鋒の記憶からは消せない。人間の脳は、まだ結晶化されていないから。",
                            "「李明。門番。真面目な奴だった。飯の食い方だけは雑だった」",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TSU_EP04", title = "髭の手入れ",
                        type = ArchiveEpisodeType.Memory,
                        requiredStoryId = "CH_03", requiredWins = 10,
                        relatedCards = new string[0],
                        unlockCondition = "崔鋒で10勝",
                        paragraphs = new[]
                        {
                            "全てを失った男が、なぜ髭の手入れだけは怠らないのか。",
                            "崔鋒は答えない。だが理由は単純だ。",
                            "部下たちの前に立つ将軍は、いつも整っていなければならない。",
                            "たとえ部下がもういなくても。たとえ将軍の地位を剥奪されても。",
                            "朝、鏡の前で髭を整えるとき、崔鋒は三千人の前に立っている。",
                            "それが彼の最後の矜持だ。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TSU_EP05", title = "灯凪との罵り合い",
                        type = ArchiveEpisodeType.CrossLeader,
                        requiredStoryId = "CH_01", requiredWins = 15,
                        relatedCards = new string[0],
                        unlockCondition = "第1章クリア + 崔鋒で15勝",
                        paragraphs = new[]
                        {
                            "「クソジジイ、今日の飯はまずい」「口の減らないガキめ」",
                            "周りは毎日の光景に慣れてしまった。",
                            "だが果求者は気づいている。二人の罵り合いには、一つの規則がある。",
                            "灯凪は崔鋒の料理を絶対に残さない。崔鋒は灯凪の分だけ少し多めに盛る。",
                            "二人とも消えたものを残そうとしている。声で。身体で。",
                            "その共通点を、言葉にしたことは一度もない。",
                        },
                    },
                    new ArchiveEpisode
                    {
                        id = "TSU_EP06", title = "三千の名前",
                        type = ArchiveEpisodeType.Secret,
                        requiredStoryId = "CH_06", requiredWins = 25,
                        relatedCards = new string[0],
                        unlockCondition = "全章クリア + 崔鋒で25勝",
                        paragraphs = new[]
                        {
                            "全てが終わった後の焚き火。全員が眠っている。",
                            "崔鋒だけが起きている。いつものように。",
                            "彼は小さな声で、名前を呟き始めた。",
                            "「李明。張維。陳靜。王鉄。趙固。孫義……」",
                            "三千の名前。一人ずつ。欠かさず。",
                            "最後の名前を呟き終えたとき、東の空が白み始めていた。",
                            "崔鋒は立ち上がり、髭を整え、朝飯の支度を始めた。",
                            "「起きろ、ガキども。飯だ」",
                        },
                    },
                },
            };
        }
    }
}
