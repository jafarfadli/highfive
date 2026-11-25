using System;
using UnityEngine;

namespace SeoulFirstWeek.World
{
    public enum Age { Teen, Young, Adult, Middle, Senior }

    public enum HairStyle
    {
        // men
        TwoBlock, Comma, SidePart, MiddlePart, Buzz, Receding,
        // women
        LongStraight, Bob, Ponytail, LowBun, Layered, ShortPerm,
    }

    public enum TopKind { Tee, LongTee, Shirt, Blouse, Hoodie, Sweatshirt, Knit, Turtleneck, Polo, Scrubs }

    public enum OuterKind
    {
        None, Cardigan, Blazer, Trench, WoolCoat, Puffer, DenimJacket, LeatherJacket, Varsity, Windbreaker,
        PaddedVest, Fleece, LabCoat, UniformVest, Apron, GuardJacket,
    }

    public enum BottomKind { Jeans, WideJeans, Slacks, WideSlacks, Joggers, LongSkirt, PleatedSkirt, ScrubPants, HikingPants }

    public enum ShoeKind { Sneakers, ChunkySneakers, Loafers, Boots, Flats, Heels, WalkingShoes }

    public enum HatKind { None, Cap, BucketHat, Beanie, Visor, GuardCap }

    public enum BagKind { None, Crossbody, Tote, Shoulder, Backpack }

    public enum GlassesKind { None, Round, Square }

    /// <summary>Who the random people around a place are.</summary>
    public enum Crowd { Street, Clinic, Immigration, Bank }

    /// <summary>
    /// Everything that makes one person look like themselves: body, face, hair and a full outfit. Random
    /// people come from Korean everyday archetypes (students, office workers, ajumma, ajeossi, grandparents,
    /// schoolkids in uniform, people from abroad); staff come from the role presets.
    /// </summary>
    [Serializable]
    public class HumanLook
    {
        public bool Female;
        public Age Age = Age.Young;
        public float Height = 1.7f;
        [Range(0, 1)] public float Build = 0.4f;
        public Color Skin = Hex("#EBCDB4");
        public int Face;

        public HairStyle Hair = HairStyle.TwoBlock;
        public Color HairColor = Hex("#161210");
        public bool Bangs;

        public TopKind Top = TopKind.Tee;
        public Color TopColor = Hex("#EDEDEB");
        public Fabric TopFabric = Fabric.Jersey;

        public OuterKind Outer;
        public Color OuterColor = Hex("#22304A");
        public Color OuterAccent = Hex("#EDEDEB");
        public Fabric OuterFabric = Fabric.Twill;

        public BottomKind Bottom = BottomKind.Jeans;
        public Color BottomColor = Hex("#3C5878");
        public Fabric BottomFabric = Fabric.Denim;
        public Color LegColor = Color.clear; // tights or stockings; clear = bare legs

        public ShoeKind Shoes = ShoeKind.Sneakers;
        public Color ShoeColor = Hex("#EDEDEB");
        public Color SoleColor = Hex("#F2F2F0");

        public HatKind Hat;
        public Color HatColor = Hex("#1C1C1E");
        public BagKind Bag;
        public Color BagColor = Hex("#1C1C1E");
        public GlassesKind Glasses;
        public Color FrameColor = Hex("#1C1C1E");

        public bool Mask;
        public Color MaskColor = Hex("#F4F4F6");
        public bool Tie;
        public Color TieColor = Hex("#22304A");
        public bool Scarf;
        public Color ScarfColor = Hex("#3E8E2A");
        public bool Lanyard;
        public Color LanyardColor = Hex("#2F5FD0");
        public bool Belt;
        public bool Stethoscope;
        public bool Earphones;
        public bool Watch;
        public bool NameTag;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ------------------------------------------------------------------ palettes

        static readonly Color[] KoreanSkin = { Hex("#F4DDCD"), Hex("#F0D7C5"), Hex("#EBD0BB"), Hex("#E5C7AF"), Hex("#DCBBA0"), Hex("#D0AD91") };
        static readonly Color[] KoreanHair = { Hex("#141110"), Hex("#141110"), Hex("#1E1612"), Hex("#2A1D14"), Hex("#3A2618"), Hex("#4F3A2C") };
        static readonly Color[] Neutrals =
        {
            Hex("#1C1C1E"), Hex("#36383C"), Hex("#8A8D91"), Hex("#C9CACC"), Hex("#EDEDEB"), Hex("#E9E1D0"),
            Hex("#CDB99A"), Hex("#A87D52"), Hex("#8E8467"), Hex("#5E6142"), Hex("#22304A"), Hex("#5B4232"),
        };
        static readonly Color[] Soft =
        {
            Hex("#A9C3DE"), Hex("#EEDC9A"), Hex("#B9AACB"), Hex("#E7B9C2"), Hex("#B8D8C8"), Hex("#F1E6D2"),
            Hex("#C9D6E3"), Hex("#D9C3B0"),
        };
        static readonly Color[] Deep = { Hex("#6A2633"), Hex("#2F4A3C"), Hex("#22304A"), Hex("#4A3A5C"), Hex("#7A4B2A") };
        static readonly Color[] Outdoor = { Hex("#C0392B"), Hex("#1F8A8A"), Hex("#D9772B"), Hex("#6B3F8C"), Hex("#C2357A"), Hex("#2D5BB8"), Hex("#2E6B4F"), Hex("#8CB43C") };
        static readonly Color[] DenimBlue = { Hex("#2F4766"), Hex("#3C5878"), Hex("#56708F"), Hex("#7A93AE"), Hex("#2A2D33"), Hex("#E9E4D8") };
        static readonly Color[] Shirt = { Hex("#F5F5F2"), Hex("#F5F5F2"), Hex("#C9D9EC"), Hex("#E3E6EA"), Hex("#EFE6DA") };
        static readonly Color[] School = { Hex("#22304A"), Hex("#5C1E2B"), Hex("#36383C"), Hex("#2F4A3C") };

        // ------------------------------------------------------------------ random people

        public static HumanLook Random(System.Random rng) => Random(rng, Crowd.Street);

        public static HumanLook Random(System.Random rng, Crowd crowd)
        {
            // weights: student, office, trendy, ajumma, ajeossi, senior, school, abroad
            int[] w = crowd switch
            {
                Crowd.Clinic => new[] { 22, 22, 6, 12, 10, 14, 4, 10 },
                Crowd.Immigration => new[] { 12, 6, 4, 0, 0, 0, 0, 78 },
                Crowd.Bank => new[] { 14, 28, 6, 14, 14, 14, 0, 10 },
                _ => new[] { 18, 16, 18, 7, 7, 5, 7, 22 },
            };
            int pick = rng.Next(Sum(w));
            int kind = 0;
            for (; kind < w.Length; kind++)
            {
                if (pick < w[kind]) break;
                pick -= w[kind];
            }
            bool female = rng.NextDouble() < 0.52;
            var look = kind switch
            {
                0 => Student(rng, female),
                1 => Office(rng, female),
                2 => Trendy(rng, female),
                3 => Ajumma(rng),
                4 => Ajeossi(rng),
                5 => SeniorPerson(rng, female),
                6 => Schoolkid(rng, female),
                _ => Abroad(rng, female),
            };
            // indoors people wait seated, so only a small crossbody bag stays on (a backpack would sink into
            // the backrest, a tote into the next seat)
            if (crowd != Crowd.Street && look.Bag is BagKind.Tote or BagKind.Shoulder or BagKind.Backpack)
                look.Bag = Chance(rng, 0.5) ? BagKind.Crossbody : BagKind.None;
            return look;
        }

        static int Sum(int[] a)
        {
            int s = 0;
            foreach (var x in a) s += x;
            return s;
        }

        static T Pick<T>(System.Random rng, params T[] a) => a[rng.Next(a.Length)];
        static bool Chance(System.Random rng, double p) => rng.NextDouble() < p;
        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static HumanLook Base(System.Random rng, bool female, Age age)
        {
            var l = new HumanLook
            {
                Female = female,
                Age = age,
                Skin = Pick(rng, KoreanSkin),
                HairColor = Pick(rng, KoreanHair),
                Height = female ? Range(rng, 1.56f, 1.7f) : Range(rng, 1.68f, 1.84f),
                Build = Range(rng, 0.2f, 0.6f),
            };
            l.Face = FaceFor(rng, female, age, false);
            return l;
        }

        /// <summary>Picks a face from the painted set: young, adult, middle-aged, senior and teen faces per gender.</summary>
        public static int FaceFor(System.Random rng, bool female, Age age, bool abroad)
        {
            if (abroad) return female ? 4 : 15;
            if (female)
                return age switch
                {
                    Age.Teen => 7,
                    Age.Young => Pick(rng, 0, 1, 2),
                    Age.Adult => Pick(rng, 2, 3),
                    Age.Middle => 5,
                    _ => 6,
                };
            return age switch
            {
                Age.Teen => 14,
                Age.Young => Pick(rng, 8, 9),
                Age.Adult => Pick(rng, 10, 11),
                Age.Middle => 12,
                _ => 13,
            };
        }

        static void SetBottom(System.Random rng, HumanLook l, BottomKind kind, Color color)
        {
            l.Bottom = kind;
            l.BottomColor = color;
            l.BottomFabric = kind switch
            {
                BottomKind.Jeans or BottomKind.WideJeans => Fabric.Denim,
                BottomKind.Joggers => Fabric.Jersey,
                BottomKind.HikingPants => Fabric.Nylon,
                BottomKind.ScrubPants => Fabric.Poplin,
                BottomKind.PleatedSkirt => Fabric.Check,
                BottomKind.LongSkirt => Pick(rng, Fabric.Twill, Fabric.Satin, Fabric.Corduroy, Fabric.Wool),
                _ => Chance(rng, 0.2) ? Fabric.Corduroy : Fabric.Twill,
            };
        }

        static void SetTop(HumanLook l, TopKind kind, Color color)
        {
            l.Top = kind;
            l.TopColor = color;
            l.TopFabric = kind switch
            {
                TopKind.Shirt or TopKind.Blouse or TopKind.Scrubs => Fabric.Poplin,
                TopKind.Knit or TopKind.Turtleneck => Fabric.Knit,
                TopKind.Polo => Fabric.Jersey,
                _ => Fabric.Jersey,
            };
        }

        static void SetOuter(HumanLook l, OuterKind kind, Color color, Color? accent = null)
        {
            l.Outer = kind;
            l.OuterColor = color;
            if (accent.HasValue) l.OuterAccent = accent.Value;
            l.OuterFabric = kind switch
            {
                OuterKind.Cardigan => Fabric.Knit,
                OuterKind.Trench => Fabric.Twill,
                OuterKind.WoolCoat => Fabric.Wool,
                OuterKind.Puffer or OuterKind.PaddedVest => Fabric.Puffer,
                OuterKind.DenimJacket => Fabric.Denim,
                OuterKind.LeatherJacket => Fabric.Leather,
                OuterKind.Varsity => Fabric.Wool,
                OuterKind.Windbreaker => Fabric.Nylon,
                OuterKind.Fleece => Fabric.Fleece,
                OuterKind.LabCoat => Fabric.Poplin,
                OuterKind.Apron => Fabric.Canvas,
                _ => Fabric.Twill,
            };
        }

        static void SetShoes(HumanLook l, ShoeKind kind, Color color, Color? sole = null)
        {
            l.Shoes = kind;
            l.ShoeColor = color;
            l.SoleColor = sole ?? (kind is ShoeKind.Loafers or ShoeKind.Boots or ShoeKind.Heels or ShoeKind.Flats ? Hex("#1A1A1A") : Hex("#F2F2F0"));
        }

        static Color Bright(System.Random rng) => Pick(rng, Outdoor);

        public static HumanLook Student(System.Random rng, bool female)
        {
            var l = Base(rng, female, Age.Young);
            if (female)
            {
                l.Hair = Pick(rng, HairStyle.LongStraight, HairStyle.LongStraight, HairStyle.Bob, HairStyle.Ponytail, HairStyle.Layered);
                l.Bangs = Chance(rng, 0.6);
                SetTop(l, Pick(rng, TopKind.Knit, TopKind.Sweatshirt, TopKind.LongTee, TopKind.Blouse, TopKind.Hoodie), Chance(rng, 0.5) ? Pick(rng, Soft) : Pick(rng, Neutrals));
                switch (rng.Next(6))
                {
                    case 0: SetOuter(l, OuterKind.Cardigan, Pick(rng, Soft)); break;
                    case 1: SetOuter(l, OuterKind.Trench, Pick(rng, Hex("#CDB99A"), Hex("#B9A27E"))); break;
                    case 2: SetOuter(l, OuterKind.Puffer, Pick(rng, Hex("#EDEDEB"), Hex("#1C1C1E"), Hex("#E9E1D0"))); break;
                    case 3: SetOuter(l, OuterKind.Fleece, Pick(rng, Hex("#E9E1D0"), Hex("#C9CACC"))); break;
                }
                if (Chance(rng, 0.45)) SetBottom(rng, l, BottomKind.LongSkirt, Pick(rng, Neutrals));
                else SetBottom(rng, l, Pick(rng, BottomKind.WideJeans, BottomKind.WideSlacks), Pick(rng, DenimBlue));
                SetShoes(l, Pick(rng, ShoeKind.Sneakers, ShoeKind.Loafers, ShoeKind.ChunkySneakers), Pick(rng, Hex("#EDEDEB"), Hex("#1C1C1E"), Hex("#5B4232")));
                l.Bag = Pick(rng, BagKind.Tote, BagKind.Shoulder, BagKind.Backpack, BagKind.Crossbody);
            }
            else
            {
                l.Hair = Pick(rng, HairStyle.TwoBlock, HairStyle.Comma, HairStyle.Comma, HairStyle.MiddlePart);
                SetTop(l, Pick(rng, TopKind.Hoodie, TopKind.Sweatshirt, TopKind.LongTee, TopKind.Knit, TopKind.Tee), Chance(rng, 0.7) ? Pick(rng, Neutrals) : Pick(rng, Soft));
                switch (rng.Next(7))
                {
                    case 0: SetOuter(l, OuterKind.Varsity, Pick(rng, Deep), Hex("#E9E1D0")); break;
                    case 1: SetOuter(l, OuterKind.DenimJacket, Pick(rng, DenimBlue)); break;
                    case 2: SetOuter(l, OuterKind.Windbreaker, Pick(rng, Neutrals), Pick(rng, Soft)); break;
                    case 3: SetOuter(l, OuterKind.Fleece, Pick(rng, Hex("#E9E1D0"), Hex("#36383C"))); break;
                    case 4: SetOuter(l, OuterKind.Puffer, Pick(rng, Hex("#1C1C1E"), Hex("#5E6142"))); break;
                }
                SetBottom(rng, l, Pick(rng, BottomKind.WideJeans, BottomKind.Joggers, BottomKind.WideSlacks), Chance(rng, 0.5) ? Pick(rng, DenimBlue) : Pick(rng, Neutrals));
                SetShoes(l, Pick(rng, ShoeKind.Sneakers, ShoeKind.ChunkySneakers), Pick(rng, Hex("#EDEDEB"), Hex("#EDEDEB"), Hex("#1C1C1E"), Hex("#8A8D91")));
                l.Bag = Pick(rng, BagKind.Backpack, BagKind.Backpack, BagKind.Crossbody, BagKind.Tote);
                if (Chance(rng, 0.2)) l.Hat = Pick(rng, HatKind.Cap, HatKind.Beanie, HatKind.BucketHat);
            }
            l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#E9E1D0"), Hex("#36383C"), Hex("#5E6142"));
            l.HatColor = Pick(rng, Neutrals);
            l.Earphones = Chance(rng, 0.3);
            Accessories(rng, l, 0.18, 0.06);
            return l;
        }

        public static HumanLook Office(System.Random rng, bool female)
        {
            var l = Base(rng, female, Chance(rng, 0.5) ? Age.Young : Age.Adult);
            if (female)
            {
                l.Hair = Pick(rng, HairStyle.LongStraight, HairStyle.LowBun, HairStyle.Bob, HairStyle.Layered, HairStyle.Ponytail);
                l.Bangs = Chance(rng, 0.25);
                SetTop(l, Pick(rng, TopKind.Blouse, TopKind.Knit, TopKind.Shirt), Pick(rng, Hex("#F5F5F2"), Hex("#EFE6DA"), Hex("#C9D6E3"), Hex("#E7B9C2"), Hex("#1C1C1E")));
                switch (rng.Next(5))
                {
                    case 0: SetOuter(l, OuterKind.Cardigan, Pick(rng, Hex("#E9E1D0"), Hex("#22304A"), Hex("#8A8D91"))); break;
                    case 1: SetOuter(l, OuterKind.Blazer, Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#CDB99A"), Hex("#22304A"))); break;
                    case 2: SetOuter(l, OuterKind.Trench, Pick(rng, Hex("#CDB99A"), Hex("#B9A27E"))); break;
                    case 3: SetOuter(l, OuterKind.WoolCoat, Pick(rng, Hex("#A87D52"), Hex("#1C1C1E"), Hex("#8A8D91"), Hex("#E9E1D0"))); break;
                }
                if (Chance(rng, 0.35)) SetBottom(rng, l, BottomKind.LongSkirt, Pick(rng, Neutrals));
                else SetBottom(rng, l, Pick(rng, BottomKind.Slacks, BottomKind.WideSlacks), Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#22304A"), Hex("#CDB99A")));
                SetShoes(l, Pick(rng, ShoeKind.Flats, ShoeKind.Loafers, ShoeKind.Heels), Pick(rng, Hex("#1C1C1E"), Hex("#5B4232"), Hex("#E9E1D0")));
                l.Bag = Pick(rng, BagKind.Shoulder, BagKind.Tote, BagKind.Shoulder);
                l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#5B4232"), Hex("#A87D52"), Hex("#E9E1D0"));
            }
            else
            {
                l.Hair = Pick(rng, HairStyle.SidePart, HairStyle.SidePart, HairStyle.TwoBlock, HairStyle.Comma);
                SetTop(l, TopKind.Shirt, Pick(rng, Shirt));
                l.Tie = Chance(rng, 0.35);
                l.TieColor = Pick(rng, Hex("#22304A"), Hex("#6A2633"), Hex("#36383C"), Hex("#2F4A3C"));
                switch (rng.Next(6))
                {
                    case 0:
                    case 1: SetOuter(l, OuterKind.Blazer, Pick(rng, Hex("#22304A"), Hex("#36383C"), Hex("#8A8D91"), Hex("#1C1C1E"))); break;
                    case 2: SetOuter(l, OuterKind.Trench, Pick(rng, Hex("#CDB99A"), Hex("#8E8467"))); break;
                    case 3: SetOuter(l, OuterKind.WoolCoat, Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#A87D52"))); break;
                }
                SetBottom(rng, l, BottomKind.Slacks, l.Outer == OuterKind.Blazer ? l.OuterColor : Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#22304A"), Hex("#8E8467")));
                SetShoes(l, ShoeKind.Loafers, Pick(rng, Hex("#1A1A1A"), Hex("#4A3426")));
                l.Belt = true;
                l.Bag = Pick(rng, BagKind.None, BagKind.Shoulder, BagKind.Backpack);
                l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#4A3426"));
                l.Watch = Chance(rng, 0.6);
            }
            l.Lanyard = Chance(rng, 0.4);
            l.LanyardColor = Pick(rng, Hex("#2F5FD0"), Hex("#1C1C1E"), Hex("#C0392B"), Hex("#1F8A8A"));
            Accessories(rng, l, 0.3, 0.06);
            return l;
        }

        public static HumanLook Trendy(System.Random rng, bool female)
        {
            var l = Base(rng, female, Age.Young);
            if (female)
            {
                l.Hair = Pick(rng, HairStyle.LongStraight, HairStyle.Layered, HairStyle.Bob, HairStyle.LongStraight);
                l.Bangs = Chance(rng, 0.55);
                if (Chance(rng, 0.25)) l.HairColor = Pick(rng, Hex("#4F3A2C"), Hex("#5A4A40"), Hex("#3E1A1C"));
                SetTop(l, Pick(rng, TopKind.Knit, TopKind.Blouse, TopKind.LongTee, TopKind.Turtleneck), Pick(rng, Soft));
                switch (rng.Next(5))
                {
                    case 0: SetOuter(l, OuterKind.Cardigan, Pick(rng, Soft)); break;
                    case 1: SetOuter(l, OuterKind.LeatherJacket, Pick(rng, Hex("#1C1C1E"), Hex("#5B4232"))); break;
                    case 2: SetOuter(l, OuterKind.Blazer, Pick(rng, Hex("#E9E1D0"), Hex("#1C1C1E"), Hex("#CDB99A"))); break;
                    case 3: SetOuter(l, OuterKind.WoolCoat, Pick(rng, Hex("#E9E1D0"), Hex("#A87D52"), Hex("#C9CACC"))); break;
                }
                if (Chance(rng, 0.5)) SetBottom(rng, l, BottomKind.LongSkirt, Pick(rng, Neutrals));
                else if (Chance(rng, 0.25))
                {
                    SetBottom(rng, l, BottomKind.PleatedSkirt, Pick(rng, Hex("#8A8D91"), Hex("#5B4232"), Hex("#1C1C1E")));
                    l.LegColor = Hex("#1C1C1E");
                }
                else SetBottom(rng, l, BottomKind.WideJeans, Pick(rng, DenimBlue));
                SetShoes(l, Pick(rng, ShoeKind.Loafers, ShoeKind.Boots, ShoeKind.ChunkySneakers), Pick(rng, Hex("#1C1C1E"), Hex("#5B4232"), Hex("#EDEDEB")));
                l.Bag = Pick(rng, BagKind.Shoulder, BagKind.Crossbody, BagKind.Tote);
                l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#E9E1D0"), Hex("#A87D52"), Hex("#6A2633"));
            }
            else
            {
                l.Hair = Pick(rng, HairStyle.Comma, HairStyle.MiddlePart, HairStyle.TwoBlock);
                if (Chance(rng, 0.25)) l.HairColor = Pick(rng, Hex("#4F3A2C"), Hex("#5A4A40"), Hex("#8A7A6A"));
                SetTop(l, Pick(rng, TopKind.LongTee, TopKind.Knit, TopKind.Shirt, TopKind.Turtleneck), Chance(rng, 0.6) ? Pick(rng, Neutrals) : Pick(rng, Soft));
                switch (rng.Next(5))
                {
                    case 0: SetOuter(l, OuterKind.Blazer, Pick(rng, Hex("#1C1C1E"), Hex("#8E8467"), Hex("#36383C"))); break;
                    case 1: SetOuter(l, OuterKind.Varsity, Pick(rng, Deep), Hex("#E9E1D0")); break;
                    case 2: SetOuter(l, OuterKind.LeatherJacket, Hex("#1C1C1E")); break;
                    case 3: SetOuter(l, OuterKind.WoolCoat, Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#A87D52"))); break;
                    default: SetOuter(l, OuterKind.DenimJacket, Pick(rng, DenimBlue)); break;
                }
                SetBottom(rng, l, Pick(rng, BottomKind.WideSlacks, BottomKind.WideJeans), Chance(rng, 0.5) ? Pick(rng, Neutrals) : Pick(rng, DenimBlue));
                SetShoes(l, Pick(rng, ShoeKind.ChunkySneakers, ShoeKind.Loafers, ShoeKind.Boots, ShoeKind.Sneakers), Pick(rng, Hex("#1C1C1E"), Hex("#EDEDEB"), Hex("#5B4232")));
                l.Bag = Pick(rng, BagKind.Crossbody, BagKind.None, BagKind.Tote);
                l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#5E6142"));
            }
            l.Earphones = Chance(rng, 0.35);
            Accessories(rng, l, 0.15, 0.04);
            return l;
        }

        public static HumanLook Ajumma(System.Random rng)
        {
            var l = Base(rng, true, Age.Middle);
            l.Height = Range(rng, 1.52f, 1.63f);
            l.Build = Range(rng, 0.45f, 0.8f);
            l.Hair = HairStyle.ShortPerm;
            l.HairColor = Pick(rng, Hex("#141110"), Hex("#2A1D14"), Hex("#3A2618"), Hex("#4A2A22"));
            SetTop(l, Pick(rng, TopKind.Blouse, TopKind.Knit, TopKind.Turtleneck), Pick(rng, Hex("#C2357A"), Hex("#E7B9C2"), Hex("#B9AACB"), Hex("#F1E6D2"), Hex("#6B3F8C")));
            switch (rng.Next(4))
            {
                case 0:
                case 1: SetOuter(l, OuterKind.Windbreaker, Bright(rng), Pick(rng, Hex("#36383C"), Hex("#EDEDEB"))); break;
                case 2: SetOuter(l, OuterKind.PaddedVest, Pick(rng, Hex("#6B3F8C"), Hex("#36383C"), Hex("#C0392B"), Hex("#22304A"))); break;
                default: SetOuter(l, OuterKind.Cardigan, Pick(rng, Hex("#A87D52"), Hex("#8A8D91"), Hex("#6A2633"))); break;
            }
            SetBottom(rng, l, Pick(rng, BottomKind.Slacks, BottomKind.HikingPants), Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#5B4232")));
            SetShoes(l, ShoeKind.WalkingShoes, Pick(rng, Hex("#36383C"), Hex("#8A8D91"), Hex("#EDEDEB")), Hex("#36383C"));
            if (Chance(rng, 0.45)) l.Hat = Pick(rng, HatKind.Visor, HatKind.Visor, HatKind.BucketHat);
            l.HatColor = Pick(rng, Hex("#EDEDEB"), Hex("#1C1C1E"), Hex("#C2357A"), Hex("#CDB99A"));
            l.Bag = Pick(rng, BagKind.Tote, BagKind.Shoulder, BagKind.Crossbody);
            l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#5B4232"), Hex("#6A2633"));
            l.Mask = Chance(rng, 0.25);
            if (Chance(rng, 0.2)) l.Glasses = GlassesKind.Square;
            return l;
        }

        public static HumanLook Ajeossi(System.Random rng)
        {
            var l = Base(rng, false, Age.Middle);
            l.Height = Range(rng, 1.64f, 1.76f);
            l.Build = Range(rng, 0.55f, 0.95f);
            l.Hair = Pick(rng, HairStyle.Receding, HairStyle.SidePart, HairStyle.Buzz);
            l.HairColor = Pick(rng, Hex("#141110"), Hex("#2A2724"), Hex("#4E4A46"));
            SetTop(l, Pick(rng, TopKind.Polo, TopKind.Shirt, TopKind.Knit), Pick(rng, Hex("#F5F5F2"), Hex("#C9D9EC"), Hex("#8A8D91"), Hex("#5E6142"), Hex("#EFE6DA")));
            switch (rng.Next(5))
            {
                case 0:
                case 1: SetOuter(l, OuterKind.Windbreaker, Bright(rng), Pick(rng, Hex("#36383C"), Hex("#1C1C1E"))); break;
                case 2: SetOuter(l, OuterKind.PaddedVest, Pick(rng, Hex("#1C1C1E"), Hex("#22304A"), Hex("#5E6142"))); break;
                case 3: SetOuter(l, OuterKind.Blazer, Pick(rng, Hex("#5B4232"), Hex("#36383C"), Hex("#8E8467"))); break;
                default: SetOuter(l, OuterKind.Fleece, Pick(rng, Hex("#36383C"), Hex("#8E8467"))); break;
            }
            SetBottom(rng, l, Pick(rng, BottomKind.HikingPants, BottomKind.Slacks), Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#22304A"), Hex("#8E8467")));
            SetShoes(l, Pick(rng, ShoeKind.WalkingShoes, ShoeKind.Loafers), Pick(rng, Hex("#1A1A1A"), Hex("#36383C"), Hex("#4A3426")));
            l.Belt = true;
            if (Chance(rng, 0.35)) l.Hat = HatKind.Cap;
            l.HatColor = Pick(rng, Hex("#1C1C1E"), Hex("#22304A"), Hex("#CDB99A"), Hex("#EDEDEB"));
            l.Glasses = Chance(rng, 0.35) ? GlassesKind.Square : GlassesKind.None;
            l.FrameColor = Pick(rng, Hex("#1C1C1E"), Hex("#6B5A3A"), Hex("#9A8C6A"));
            l.Bag = Chance(rng, 0.25) ? BagKind.Crossbody : BagKind.None;
            l.Watch = Chance(rng, 0.5);
            l.Mask = Chance(rng, 0.15);
            return l;
        }

        public static HumanLook SeniorPerson(System.Random rng, bool female)
        {
            var l = Base(rng, female, Age.Senior);
            l.Height = female ? Range(rng, 1.48f, 1.58f) : Range(rng, 1.6f, 1.72f);
            l.Build = Range(rng, 0.4f, 0.75f);
            l.HairColor = Pick(rng, Hex("#8E8A86"), Hex("#A8A49F"), Hex("#BDBAB5"), Hex("#5A5652"));
            if (female)
            {
                l.Hair = HairStyle.ShortPerm;
                SetTop(l, Pick(rng, TopKind.Blouse, TopKind.Knit), Pick(rng, Hex("#B9AACB"), Hex("#E7B9C2"), Hex("#F1E6D2"), Hex("#6B3F8C")));
                SetOuter(l, Pick(rng, OuterKind.PaddedVest, OuterKind.Cardigan, OuterKind.Windbreaker), Pick(rng, Hex("#6B3F8C"), Hex("#36383C"), Hex("#A87D52"), Hex("#C2357A")));
                SetBottom(rng, l, BottomKind.Slacks, Pick(rng, Hex("#1C1C1E"), Hex("#36383C"), Hex("#5B4232")));
                if (Chance(rng, 0.4)) l.Hat = Pick(rng, HatKind.Visor, HatKind.BucketHat);
                l.Bag = BagKind.Tote;
            }
            else
            {
                l.Hair = Pick(rng, HairStyle.Receding, HairStyle.Buzz, HairStyle.SidePart);
                SetTop(l, Pick(rng, TopKind.Shirt, TopKind.Polo, TopKind.Knit), Pick(rng, Hex("#F5F5F2"), Hex("#C9D9EC"), Hex("#8A8D91"), Hex("#EFE6DA")));
                SetOuter(l, Pick(rng, OuterKind.Blazer, OuterKind.Windbreaker, OuterKind.PaddedVest), Pick(rng, Hex("#5B4232"), Hex("#36383C"), Hex("#8E8467"), Hex("#22304A")));
                SetBottom(rng, l, BottomKind.Slacks, Pick(rng, Hex("#36383C"), Hex("#5B4232"), Hex("#8E8467")));
                if (Chance(rng, 0.45)) l.Hat = HatKind.Cap;
                l.Glasses = Chance(rng, 0.5) ? GlassesKind.Square : GlassesKind.None;
                l.FrameColor = Pick(rng, Hex("#9A8C6A"), Hex("#1C1C1E"));
                l.Belt = true;
            }
            l.HatColor = Pick(rng, Hex("#CDB99A"), Hex("#36383C"), Hex("#EDEDEB"));
            l.BagColor = Pick(rng, Hex("#5B4232"), Hex("#1C1C1E"));
            SetShoes(l, ShoeKind.WalkingShoes, Pick(rng, Hex("#36383C"), Hex("#5B4232"), Hex("#8A8D91")), Hex("#36383C"));
            l.Mask = Chance(rng, 0.35);
            return l;
        }

        public static HumanLook Schoolkid(System.Random rng, bool female)
        {
            var l = Base(rng, female, Age.Teen);
            l.Height = female ? Range(rng, 1.55f, 1.66f) : Range(rng, 1.65f, 1.8f);
            l.Build = Range(rng, 0.15f, 0.4f);
            var school = Pick(rng, School);
            SetTop(l, TopKind.Shirt, Hex("#F5F5F2"));
            SetOuter(l, OuterKind.Blazer, school);
            l.Tie = true;
            l.TieColor = Pick(rng, Hex("#6A2633"), Hex("#22304A"), Hex("#2F4A3C"));
            if (female)
            {
                l.Hair = Pick(rng, HairStyle.LongStraight, HairStyle.Ponytail, HairStyle.Bob);
                l.Bangs = Chance(rng, 0.7);
                SetBottom(rng, l, BottomKind.PleatedSkirt, Pick(rng, Hex("#4A4F5A"), Hex("#22304A"), Hex("#5C1E2B")));
                l.LegColor = Chance(rng, 0.5) ? Hex("#1C1C1E") : Color.clear;
            }
            else
            {
                l.Hair = Pick(rng, HairStyle.TwoBlock, HairStyle.Comma);
                SetBottom(rng, l, BottomKind.Slacks, Pick(rng, Hex("#4A4F5A"), Hex("#22304A"), Hex("#36383C")));
            }
            SetShoes(l, ShoeKind.Sneakers, Pick(rng, Hex("#EDEDEB"), Hex("#1C1C1E")));
            l.Bag = BagKind.Backpack;
            l.BagColor = Pick(rng, Hex("#1C1C1E"), Hex("#22304A"), Hex("#E9E1D0"), Hex("#A9C3DE"));
            l.Earphones = Chance(rng, 0.3);
            return l;
        }

        /// <summary>Tourists and foreign residents: Southeast, East, South and Central Asian, Western and African.</summary>
        public static HumanLook Abroad(System.Random rng, bool female)
        {
            var l = Chance(rng, 0.5) ? Student(rng, female) : Chance(rng, 0.5) ? Office(rng, female) : Trendy(rng, female);
            int origin = rng.Next(100);
            if (origin < 40)
            {
                // Vietnam, the Philippines, Indonesia, Thailand
                l.Skin = Pick(rng, Hex("#D9AE88"), Hex("#C99B74"), Hex("#B88560"));
                l.HairColor = Pick(rng, Hex("#141110"), Hex("#1E1612"));
                l.Face = FaceFor(rng, female, l.Age, rng.Next(3) == 0);
            }
            else if (origin < 60)
            {
                // China, Mongolia, Japan
                l.Skin = Pick(rng, KoreanSkin);
                l.Face = FaceFor(rng, female, l.Age, false);
            }
            else if (origin < 78)
            {
                // South and Central Asia
                l.Skin = Pick(rng, Hex("#B07A55"), Hex("#9A6445"), Hex("#D8B394"));
                l.HairColor = Hex("#141110");
                l.Face = FaceFor(rng, female, l.Age, true);
            }
            else if (origin < 92)
            {
                // Europe, the Americas, Oceania
                l.Skin = Pick(rng, Hex("#F2D3C6"), Hex("#EAC4AE"), Hex("#E0B798"));
                l.HairColor = Pick(rng, Hex("#B48C55"), Hex("#8A6340"), Hex("#4A3220"), Hex("#C9A876"), Hex("#2A1D14"));
                l.Face = FaceFor(rng, female, l.Age, true);
                l.Height += female ? 0.03f : 0.04f;
            }
            else
            {
                // Africa
                l.Skin = Pick(rng, Hex("#7A4E35"), Hex("#5B3826"), Hex("#8D5F43"));
                l.HairColor = Hex("#141110");
                l.Face = FaceFor(rng, female, l.Age, true);
                if (!female) l.Hair = HairStyle.Buzz;
            }
            if (!female && Chance(rng, 0.3)) l.Hat = HatKind.Cap;
            if (Chance(rng, 0.5)) l.Bag = Pick(rng, BagKind.Backpack, BagKind.Crossbody);
            return l;
        }

        static void Accessories(System.Random rng, HumanLook l, double glasses, double mask)
        {
            if (Chance(rng, glasses))
            {
                l.Glasses = Pick(rng, GlassesKind.Round, GlassesKind.Square);
                l.FrameColor = Pick(rng, Hex("#1C1C1E"), Hex("#1C1C1E"), Hex("#6B4A32"), Hex("#B8A26A"));
            }
            if (Chance(rng, mask))
            {
                l.Mask = true;
                l.MaskColor = Pick(rng, Hex("#F4F4F6"), Hex("#F4F4F6"), Hex("#1C1C1E"), Hex("#DCE6F0"));
            }
        }

        // ------------------------------------------------------------------ staff

        /// <summary>Clinic nurse: pastel pink scrubs, white nurse shoes, hair tied back, name tag.</summary>
        public static HumanLook Nurse(int variant)
        {
            var rng = new System.Random(500 + variant);
            var l = Base(rng, true, variant == 0 ? Age.Adult : Age.Young);
            l.Height = variant == 0 ? 1.62f : 1.6f;
            l.Face = variant == 0 ? 3 : 0;
            l.Hair = variant == 0 ? HairStyle.LowBun : HairStyle.Ponytail;
            l.HairColor = Hex("#141110");
            SetTop(l, TopKind.Scrubs, Hex("#F2C3CF"));
            if (variant == 1) SetOuter(l, OuterKind.Cardigan, Hex("#F7F4EE"));
            SetBottom(rng, l, BottomKind.ScrubPants, Hex("#F2C3CF"));
            SetShoes(l, ShoeKind.Sneakers, Hex("#F7F7F5"));
            l.NameTag = true;
            return l;
        }

        /// <summary>The clinic's head doctor (원장): white coat over a shirt and tie, glasses, stethoscope.</summary>
        public static HumanLook Doctor()
        {
            var rng = new System.Random(510);
            var l = Base(rng, false, Age.Middle);
            l.Height = 1.74f;
            l.Build = 0.5f;
            l.Face = 12;
            l.Hair = HairStyle.SidePart;
            l.HairColor = Hex("#2A2724");
            SetTop(l, TopKind.Shirt, Hex("#C9D9EC"));
            l.Tie = true;
            l.TieColor = Hex("#22304A");
            SetOuter(l, OuterKind.LabCoat, Hex("#F7F7F5"));
            SetBottom(rng, l, BottomKind.Slacks, Hex("#36383C"));
            SetShoes(l, ShoeKind.Loafers, Hex("#1A1A1A"));
            l.Glasses = GlassesKind.Square;
            l.FrameColor = Hex("#36383C");
            l.Stethoscope = true;
            l.NameTag = true;
            return l;
        }

        /// <summary>Immigration office civil servant (주무관): navy cardigan or blazer, ID card on a lanyard.</summary>
        public static HumanLook Officer(bool female, int variant)
        {
            var rng = new System.Random(520 + variant);
            var l = Base(rng, female, variant % 2 == 0 ? Age.Adult : Age.Young);
            var navy = Hex("#1F2F55");
            if (female)
            {
                l.Height = 1.63f;
                l.Hair = variant == 1 ? HairStyle.Bob : HairStyle.LowBun;
                if (variant == 2)
                {
                    l.Glasses = GlassesKind.Square;
                    l.FrameColor = Hex("#36383C");
                }
                SetTop(l, TopKind.Blouse, Hex("#F5F5F2"));
                SetOuter(l, OuterKind.Cardigan, navy);
                SetBottom(rng, l, BottomKind.Slacks, navy);
                SetShoes(l, ShoeKind.Flats, Hex("#1A1A1A"));
            }
            else
            {
                l.Height = 1.74f;
                l.Hair = HairStyle.SidePart;
                SetTop(l, TopKind.Shirt, variant == 0 ? Hex("#F5F5F2") : Hex("#C9D9EC"));
                l.Tie = true;
                l.TieColor = navy;
                if (variant >= 2) SetOuter(l, OuterKind.Blazer, navy);
                SetBottom(rng, l, BottomKind.Slacks, navy);
                SetShoes(l, ShoeKind.Loafers, Hex("#1A1A1A"));
                l.Belt = true;
            }
            l.HairColor = Hex("#141110");
            l.Lanyard = true;
            l.LanyardColor = Hex("#2F5FD0");
            return l;
        }

        /// <summary>
        /// Bank teller in office clothes, as Korean banks dropped teller uniforms in 2019–21: a blouse and
        /// cardigan (women) or a shirt, sometimes with a blazer and tie (men), and the bank's green lanyard.
        /// </summary>
        public static HumanLook Teller(bool female, int variant)
        {
            var rng = new System.Random(530 + variant);
            var l = Base(rng, female, variant == 2 ? Age.Adult : Age.Young);
            var green = Hex("#3E8E2A");
            var charcoal = Hex("#3A3D44");
            if (female)
            {
                l.Height = 1.62f;
                l.Hair = variant == 3 ? HairStyle.Bob : HairStyle.LowBun;
                SetTop(l, TopKind.Blouse, variant == 3 ? Hex("#E9E1D0") : Hex("#F7F7F5"));
                SetOuter(l, OuterKind.Cardigan, variant == 3 ? Hex("#22304A") : Hex("#CDB99A"));
                SetBottom(rng, l, BottomKind.Slacks, variant == 3 ? Hex("#36383C") : charcoal);
                SetShoes(l, ShoeKind.Flats, Hex("#1A1A1A"));
            }
            else
            {
                l.Height = variant == 2 ? 1.7f : 1.76f;
                l.Hair = variant == 2 ? HairStyle.SidePart : HairStyle.TwoBlock;
                SetTop(l, TopKind.Shirt, variant == 1 ? Hex("#C9D9EC") : Hex("#F7F7F5"));
                if (variant == 2)
                {
                    SetOuter(l, OuterKind.Blazer, charcoal);
                    l.Tie = true;
                    l.TieColor = Hex("#22304A");
                    l.Glasses = GlassesKind.Square;
                    l.FrameColor = Hex("#36383C");
                }
                SetBottom(rng, l, BottomKind.Slacks, charcoal);
                SetShoes(l, ShoeKind.Loafers, Hex("#1A1A1A"));
                l.Belt = true;
            }
            l.HairColor = Hex("#141110");
            l.Lanyard = true;
            l.LanyardColor = green;
            return l;
        }

        /// <summary>Bank security guard (청원경찰): navy uniform jacket and peaked cap.</summary>
        public static HumanLook Guard()
        {
            var rng = new System.Random(540);
            var l = Base(rng, false, Age.Adult);
            l.Height = 1.78f;
            l.Build = 0.55f;
            l.Hair = HairStyle.Buzz;
            l.HairColor = Hex("#141110");
            var navy = Hex("#1F2A44");
            SetTop(l, TopKind.Shirt, Hex("#C9D9EC"));
            l.Tie = true;
            l.TieColor = navy;
            SetOuter(l, OuterKind.GuardJacket, navy, Hex("#C9A44A"));
            SetBottom(rng, l, BottomKind.Slacks, navy);
            SetShoes(l, ShoeKind.Loafers, Hex("#111111"));
            l.Hat = HatKind.GuardCap;
            l.HatColor = navy;
            l.Belt = true;
            return l;
        }

        /// <summary>Street food vendor: an apron over warm layers, a visor or cap.</summary>
        public static HumanLook Vendor(System.Random rng)
        {
            var l = Chance(rng, 0.6) ? Ajumma(rng) : Ajeossi(rng);
            SetOuter(l, OuterKind.Apron, Pick(rng, Hex("#C0392B"), Hex("#22304A"), Hex("#EDEDEB"), Hex("#2E6B4F")));
            l.Hat = l.Female ? HatKind.Visor : HatKind.Cap;
            l.HatColor = Pick(rng, Hex("#EDEDEB"), Hex("#1C1C1E"), Hex("#C0392B"));
            l.Bag = BagKind.None;
            l.Mask = false;
            l.Glasses = GlassesKind.None;
            return l;
        }
    }
}
