using System.Collections.Generic;

namespace Banganka.Core.Data
{
    public class ShopProduct
    {
        public string id;
        public string productName;
        public string description;
        public int price;
        public string currency;
        public bool featured;
        public Aspect themeAspect;
        public ProductCategory category;
    }

    public enum ProductCategory
    {
        CardPack,
        Currency,
        Special
    }

    public static class ShopDatabase
    {
        static List<ShopProduct> _products;

        public static IReadOnlyList<ShopProduct> Products
        {
            get
            {
                if (_products == null) Init();
                return _products;
            }
        }

        static void Init()
        {
            _products = new List<ShopProduct>
            {
                new ShopProduct
                {
                    id = "PACK_01", productName = "万願果パック",
                    description = "願いの力を手に入れよ\nカード5枚入り",
                    price = 500, currency = "コイン", featured = true,
                    themeAspect = Aspect.Verse, category = ProductCategory.CardPack
                },
                new ShopProduct
                {
                    id = "PACK_02", productName = "曙の闘志パック",
                    description = "曙の願相カード強化パック\nレア1枚確定",
                    price = 800, currency = "コイン", featured = false,
                    themeAspect = Aspect.Contest, category = ProductCategory.CardPack
                },
                new ShopProduct
                {
                    id = "PACK_03", productName = "空の囁きパック",
                    description = "空の願相カード強化パック\nレア1枚確定",
                    price = 800, currency = "コイン", featured = false,
                    themeAspect = Aspect.Whisper, category = ProductCategory.CardPack
                },
                new ShopProduct
                {
                    id = "PACK_04", productName = "織界の緑パック",
                    description = "穏の願相カード強化パック\nレア1枚確定",
                    price = 800, currency = "コイン", featured = false,
                    themeAspect = Aspect.Weave, category = ProductCategory.CardPack
                },
                new ShopProduct
                {
                    id = "COIN_01", productName = "交界コイン 500",
                    description = "ゲーム内通貨 500コイン",
                    price = 120, currency = "円", featured = true,
                    themeAspect = Aspect.Verse, category = ProductCategory.Currency
                },
                new ShopProduct
                {
                    id = "COIN_02", productName = "交界コイン 2500",
                    description = "ゲーム内通貨 2500コイン（+500ボーナス）",
                    price = 480, currency = "円", featured = true,
                    themeAspect = Aspect.Verse, category = ProductCategory.Currency
                },
                new ShopProduct
                {
                    id = "SPECIAL_01", productName = "初心者応援セット",
                    description = "スターターデッキ + 1000コイン\n果求者の第一歩を踏み出せ",
                    price = 300, currency = "円", featured = true,
                    themeAspect = Aspect.Contest, category = ProductCategory.Special
                },
            };
        }
    }
}
