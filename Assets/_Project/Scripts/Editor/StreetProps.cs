using System.IO;
using SeoulFirstWeek.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static SeoulFirstWeek.EditorTools.BuildKit;

namespace SeoulFirstWeek.EditorTools
{
    /// <summary>
    /// Street furniture of a Seoul shopping street, built from primitives at real-world sizes. Every
    /// builder takes a parent and places the prop with its local origin on the ground, +Z facing the
    /// street. Sizes and colours follow CLAUDE.md "Visual standards".
    /// </summary>
    public static class StreetProps
    {
        static readonly Color Steel = C("#3A3D42");
        static readonly Color Reflector = C("#F2C230");

        /// <summary>Steel bollard (볼라드) with a reflective band, 0.85 m tall.</summary>
        public static void Bollard(Transform parent, Vector3 pos)
        {
            var g = Group("Bollard", parent);
            g.position = pos;
            Cylinder(g, "Post", new Vector3(0, 0.42f, 0), new Vector3(0.15f, 0.42f, 0.15f), Steel, true);
            Cylinder(g, "Band", new Vector3(0, 0.7f, 0), new Vector3(0.155f, 0.03f, 0.155f), Reflector);
            Sphere(g, "Cap", new Vector3(0, 0.84f, 0), new Vector3(0.15f, 0.06f, 0.15f), Steel);
        }

        /// <summary>A pair of street bins: general waste (grey) and recycling (green), each labelled.</summary>
        public static void Bins(Transform parent, Vector3 pos, Quaternion rot)
        {
            var g = Group("Bins", parent);
            g.SetPositionAndRotation(pos, rot);
            foreach (var (x, body, label) in new[] { (-0.26f, "#6E7B85", "일반쓰레기"), (0.26f, "#2E8B57", "재활용") })
            {
                Box(g, "Body", new Vector3(x, 0.5f, 0), new Vector3(0.48f, 1f, 0.42f), C(body));
                Box(g, "Lid", new Vector3(x, 1.02f, 0), new Vector3(0.5f, 0.05f, 0.44f), C("#3A4046"), false);
                Box(g, "Slot", new Vector3(x, 0.86f, 0.212f), new Vector3(0.3f, 0.07f, 0.01f), C("#1E2124"), false);
                var sign = Sign(g, "Label", new Vector3(x, 0.62f, 0.212f), new Vector2(0.4f, 0.16f), Color.white, label, C("#222222"), 0, 40, 0.005f);
                sign.GetChild(0).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static readonly (string text, string board, string ink)[] Boards =
        {
            ("OPEN", "#12141C", "#FFD23F"), ("SALE\n50%", "#E4405F", "#FFFFFF"), ("마스크팩\n1+1", "#FFFFFF", "#E4405F"),
            ("TAX FREE", "#3D7BFF", "#FFFFFF"), ("환전\nEXCHANGE", "#1B998B", "#FFFFFF"), ("오늘의\n특가", "#FFD23F", "#1E1E22"),
            ("떡볶이\n4,000원", "#D8452B", "#FFFFFF"), ("아메리카노\n2,500원", "#5B4535", "#FFFFFF"),
        };

        /// <summary>Folding A-frame board (입간판) in front of a shop, readable from both sides.</summary>
        public static void AFrame(Transform parent, Vector3 pos, Quaternion rot, System.Random rng)
        {
            var (text, board, ink) = Boards[rng.Next(Boards.Length)];
            var g = Group("AFrame", parent);
            g.SetPositionAndRotation(pos, rot);
            foreach (var side in new[] { 1f, -1f })
            {
                var leaf = Group("Leaf", g);
                leaf.localPosition = new Vector3(0, 0.95f, 0);
                // both leaves hang from the hinge and splay outwards, text tilted a little up
                leaf.localRotation = Quaternion.Euler(-12f, side > 0 ? 0 : 180, 0);
                var s = Sign(leaf, "Board", new Vector3(0, -0.47f, 0.02f), new Vector2(0.56f, 0.86f), C(board), text, C(ink), 0, 56, 0.025f);
                Box(leaf, "Frame", new Vector3(0, -0.47f, 0), new Vector3(0.6f, 0.92f, 0.02f), C("#2B2D31"), false);
                s.GetChild(0).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            }
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.47f, 0);
            col.size = new Vector3(0.6f, 0.95f, 0.45f);
        }

        /// <summary>Sidewalk sales table with baskets of small goods (socks, face masks) and a price card.</summary>
        public static void DisplayTable(Transform parent, Vector3 pos, Quaternion rot, System.Random rng)
        {
            var g = Group("DisplayTable", parent);
            g.SetPositionAndRotation(pos, rot);
            Box(g, "Top", new Vector3(0, 0.74f, 0), new Vector3(1.1f, 0.04f, 0.6f), C("#E8E4DC"));
            foreach (var x in new[] { -0.5f, 0.5f })
            foreach (var z in new[] { -0.25f, 0.25f })
                Box(g, "Leg", new Vector3(x, 0.36f, z), new Vector3(0.04f, 0.72f, 0.04f), C("#9AA0A6"), false);
            var goods = new[] { "#FF6FA8", "#6FD6C3", "#B89CFF", "#FFFFFF", "#FFD36F", "#7FB7FF", "#222428", "#FF8A65" };
            for (int b = 0; b < 3; b++)
            {
                float bx = -0.36f + b * 0.36f;
                Box(g, "Basket", new Vector3(bx, 0.81f, 0), new Vector3(0.32f, 0.1f, 0.5f), C("#C9A36B"), false);
                for (int i = 0; i < 8; i++)
                    Box(g, "Item", new Vector3(bx - 0.1f + (i % 3) * 0.1f, 0.87f + (float)rng.NextDouble() * 0.02f, -0.16f + (i / 3) * 0.14f),
                        new Vector3(0.08f, 0.03f, 0.11f), C(goods[rng.Next(goods.Length)]), false, (float)rng.NextDouble() * 30f - 15f);
            }
            var card = Sign(g, "Price", new Vector3(0.42f, 0.98f, 0.26f), new Vector2(0.24f, 0.16f), C("#FFD23F"), "3장\n1만원", C("#D7263D"), 0, 40, 0.004f);
            card.localRotation = Quaternion.Euler(-15, 0, 0);
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.45f, 0);
            col.size = new Vector3(1.1f, 0.9f, 0.6f);
        }

        static readonly string[] ScooterColors = { "#D7263D", "#FFFFFF", "#3D7BFF", "#2B2D31" };

        /// <summary>Delivery scooter (배달 오토바이) with a box on the back, parked along the kerb.</summary>
        public static void Scooter(Transform parent, Vector3 pos, Quaternion rot, System.Random rng)
        {
            var g = Group("Scooter", parent);
            g.SetPositionAndRotation(pos, rot);
            var body = C(ScooterColors[rng.Next(ScooterColors.Length)]);
            var dark = C("#1E2124");
            foreach (var z in new[] { -0.52f, 0.55f })
            {
                var w = Cylinder(g, "Wheel", new Vector3(0, 0.22f, z), new Vector3(0.44f, 0.05f, 0.44f), dark);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var hub = Cylinder(g, "Hub", new Vector3(0, 0.22f, z), new Vector3(0.18f, 0.06f, 0.18f), C("#9AA0A6"));
                hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Box(g, "Floor", new Vector3(0, 0.36f, 0.05f), new Vector3(0.3f, 0.08f, 0.7f), dark, false);
            Box(g, "Body", new Vector3(0, 0.52f, -0.3f), new Vector3(0.34f, 0.42f, 0.62f), body, false);
            Box(g, "Seat", new Vector3(0, 0.76f, -0.28f), new Vector3(0.3f, 0.08f, 0.58f), dark, false);
            var shield = Box(g, "Shield", new Vector3(0, 0.66f, 0.46f), new Vector3(0.36f, 0.62f, 0.08f), body, false);
            shield.transform.localRotation = Quaternion.Euler(-14, 0, 0);
            Box(g, "Bar", new Vector3(0, 1.0f, 0.42f), new Vector3(0.6f, 0.04f, 0.04f), dark, false);
            Box(g, "Lamp", new Vector3(0, 0.9f, 0.5f), new Vector3(0.14f, 0.08f, 0.04f), C("#F4F1EA"), false);
            // delivery box
            var boxCol = rng.NextDouble() < 0.5 ? C("#F4F1EA") : body;
            Box(g, "DeliveryBox", new Vector3(0, 1.02f, -0.5f), new Vector3(0.46f, 0.4f, 0.44f), boxCol, false);
            Box(g, "BoxStripe", new Vector3(0, 1.02f, -0.278f), new Vector3(0.46f, 0.08f, 0.005f), C("#FFB547"), false);
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.size = new Vector3(0.6f, 1.2f, 1.6f);
        }

        /// <summary>Grey street utility cabinet (전기 분전함) with vents and a warning sticker.</summary>
        public static void UtilityBox(Transform parent, Vector3 pos, Quaternion rot)
        {
            var g = Group("UtilityBox", parent);
            g.SetPositionAndRotation(pos, rot);
            Box(g, "Body", new Vector3(0, 0.65f, 0), new Vector3(1.0f, 1.3f, 0.45f), C("#8C9298"));
            Box(g, "Plinth", new Vector3(0, 0.04f, 0), new Vector3(1.06f, 0.08f, 0.5f), C("#5E6368"), false);
            Box(g, "Roof", new Vector3(0, 1.32f, 0), new Vector3(1.06f, 0.04f, 0.5f), C("#6F757B"), false);
            for (int i = 0; i < 4; i++) Box(g, "Vent", new Vector3(-0.3f, 1.05f - i * 0.05f, 0.226f), new Vector3(0.3f, 0.015f, 0.005f), C("#4A4F54"), false);
            Box(g, "DoorLine", new Vector3(0, 0.65f, 0.226f), new Vector3(0.01f, 1.2f, 0.005f), C("#5E6368"), false);
            Sign(g, "Warning", new Vector3(0.28f, 0.95f, 0.226f), new Vector2(0.22f, 0.16f), C("#FFD23F"), "위험\n고압", C("#1E1E22"), 0, 36, 0.003f);
        }

        /// <summary>Cast-iron manhole cover set into the paving.</summary>
        public static void Manhole(Transform parent, Vector3 pos)
        {
            var g = Group("Manhole", parent);
            g.position = pos;
            var lid = Prims.Make(PrimitiveType.Cylinder, g, "Lid", new Vector3(0, 0.006f, 0), new Vector3(0.7f, 0.006f, 0.7f), Mats.Lit(C("#3F3E3B"), 0.35f, 0.6f));
            lid.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var ring = Prims.Make(PrimitiveType.Cylinder, g, "Ring", new Vector3(0, 0.004f, 0), new Vector3(0.78f, 0.004f, 0.78f), Mats.Lit(C("#55534F"), 0.3f, 0.5f));
            ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            for (int i = -2; i <= 2; i++)
            {
                var bar = Box(g, "Rib", new Vector3(i * 0.1f, 0.013f, 0), new Vector3(0.02f, 0.004f, 0.52f - Mathf.Abs(i) * 0.08f), C("#2E2D2A"), false);
                bar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static readonly (string text, string color)[] Banners = { ("명동\nMYEONG\nDONG", "#E4405F"), ("환영\nWELCOME", "#3D7BFF"), ("명동\n쇼핑\n축제", "#1B998B"), ("K-BEAUTY\n명동", "#8E44AD") };

        /// <summary>A pair of vertical banners hung on a lamp post, one each side along the street.</summary>
        public static void LampBanners(Transform lamp, int index)
        {
            var (text, color) = Banners[index % Banners.Length];
            foreach (var side in new[] { -1f, 1f })
            {
                Box(lamp, "BannerArm", new Vector3(side * 0.28f, 3.72f, 0), new Vector3(0.5f, 0.03f, 0.03f), C("#2F3237"), false);
                Box(lamp, "BannerArm", new Vector3(side * 0.28f, 2.58f, 0), new Vector3(0.5f, 0.03f, 0.03f), C("#2F3237"), false);
                var b = Sign(lamp, "Banner", new Vector3(side * 0.32f, 3.15f, 0), new Vector2(0.42f, 1.08f), C(color), text, Color.white, 90, 44, 0.01f);
                var back = Object.Instantiate(b.Find("Text").gameObject, b);
                back.transform.localPosition = new Vector3(0, 0, -0.012f);
                back.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>Soft steam rising from a griddle or a pot.</summary>
        public static void Steam(Transform parent, Vector3 localPos, Vector3 area)
        {
            var go = new GameObject("Steam");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 4f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(1f, 1f, 1f, 0.22f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.gravityModifier = -0.015f;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            fade.color = grad;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.6f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SteamMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        static Material steam;

        static Material SteamMaterial()
        {
            if (steam) return steam;
            var texPath = $"{TextureKit.Folder}/Soft_Dot.png";
            if (!File.Exists(texPath))
            {
                EnsureFolder(TextureKit.Folder);
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = new Vector2(x - n / 2f + 0.5f, y - n / 2f + 0.5f).magnitude / (n / 2f);
                    float a = Mathf.Clamp01(1f - d);
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
                }
                tex.Apply();
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            var m = TextureKit.MaterialAsset("Particle_Steam", "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return steam = m;
        }
    }
}
