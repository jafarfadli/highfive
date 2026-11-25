using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SeoulFirstWeek.World
{
    /// <summary>
    /// Builds a person: the joint hierarchy <see cref="HumanRig"/> animates, and one skinned mesh for the body,
    /// clothes, face and hair (bones = those joints), plus small rigid parts for the mouth, the eyelids and a
    /// phone. Pedestrians use the light version (<c>detailed: false</c>): fewer polygons, no mouth or blinking.
    /// </summary>
    public static class HumanBuilder
    {
        /// <summary>Set by editor builders so meshes of people saved in scenes become assets instead of scene data.</summary>
        public static Func<Mesh, Mesh> Persist;

        static Material fallback;

        public static HumanRig Build(Transform parent, string name, HumanLook look, bool collider = false, bool detailed = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var c = new HumanContext { L = look, Mb = new MeshBuilder(), High = detailed };
            HumanBody.Prepare(c);

            Transform Pivot(string n, Transform p, Vector3 pos)
            {
                var tr = new GameObject(n).transform;
                tr.SetParent(p, false);
                tr.localPosition = pos;
                return tr;
            }

            var body = Pivot("Body", root, Vector3.zero);
            var legL = Pivot("LegL", body, new Vector3(-HumanContext.HipX, HumanRig.HipHeight, 0));
            var legR = Pivot("LegR", body, new Vector3(HumanContext.HipX, HumanRig.HipHeight, 0));
            var kneeL = Pivot("Knee", legL, new Vector3(0, -HumanRig.ThighLength, 0));
            var kneeR = Pivot("Knee", legR, new Vector3(0, -HumanRig.ThighLength, 0));
            var torso = Pivot("Torso", body, new Vector3(0, HumanRig.TorsoY, 0));
            var armL = Pivot("ArmL", torso, new Vector3(-c.ShoulderX, HumanRig.ShoulderY - HumanRig.TorsoY, 0));
            var armR = Pivot("ArmR", torso, new Vector3(c.ShoulderX, HumanRig.ShoulderY - HumanRig.TorsoY, 0));
            var elbowL = Pivot("Elbow", armL, new Vector3(0, HumanRig.ElbowY - HumanRig.ShoulderY, 0));
            var elbowR = Pivot("Elbow", armR, new Vector3(0, HumanRig.ElbowY - HumanRig.ShoulderY, 0));
            var head = Pivot("Head", torso, new Vector3(0, HumanRig.NeckY - HumanRig.TorsoY, 0));
            var bones = new[] { body, legL, kneeL, legR, kneeR, torso, armL, elbowL, armR, elbowR, head };

            HumanBody.Build(c);
            HumanHead.Build(c);

            var skin = new GameObject("Skin").transform;
            skin.SetParent(root, false);
            var bind = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * skin.localToWorldMatrix;
            var mesh = c.Mb.ToMesh(name, bind, out var slots);
            if (Persist != null) mesh = Persist(mesh);

            var style = HumanStyle.Current;
            var smr = skin.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = body;
            var mats = new Material[slots.Count];
            for (int i = 0; i < slots.Count; i++) mats[i] = MaterialFor(style, slots[i]);
            smr.sharedMaterials = mats;
            smr.localBounds = new Bounds(new Vector3(0, 0.95f, 0.05f), new Vector3(1.4f, 2.1f, 1.3f));
            smr.updateWhenOffscreen = false;
            smr.skinnedMotionVectors = false;
            smr.quality = SkinQuality.Bone4;

            Transform mouth = null, lidL = null, lidR = null;
            if (detailed)
            {
                mouth = Rigid("Mouth", head, root, HumanHead.MouthMesh(c, out var mouthAt), mouthAt, style);
                mouth.localScale = new Vector3(1, 0.02f, 1);
                lidL = Rigid("LidL", head, root, HumanHead.LidMesh(c, -1f, out var lidAtL), lidAtL, style);
                lidR = Rigid("LidR", head, root, HumanHead.LidMesh(c, 1f, out var lidAtR), lidAtR, style);
                lidL.localScale = lidR.localScale = new Vector3(1, 0.02f, 1);
            }
            var phone = Phone(c, elbowR, root, style);

            root.localScale = Vector3.one * (look.Height / HumanRig.RigHeight);

            if (collider)
            {
                var cap = root.gameObject.AddComponent<CapsuleCollider>();
                cap.center = new Vector3(0, HumanRig.RigHeight / 2f, 0);
                cap.height = HumanRig.RigHeight;
                cap.radius = 0.25f;
            }

            var rig = root.gameObject.AddComponent<HumanRig>();
            rig.body = body;
            rig.legL = legL;
            rig.legR = legR;
            rig.kneeL = kneeL;
            rig.kneeR = kneeR;
            rig.torso = torso;
            rig.armL = armL;
            rig.armR = armR;
            rig.elbowL = elbowL;
            rig.elbowR = elbowR;
            rig.head = head;
            rig.mouth = look.Mask ? null : mouth;
            if (look.Mask && mouth) mouth.gameObject.SetActive(false);
            rig.lidL = lidL;
            rig.lidR = lidR;
            rig.shoulderBag = look.Bag is BagKind.Tote or BagKind.Shoulder;
            rig.phoneProp = phone;
            return rig;
        }

        static Material MaterialFor(HumanStyle style, int slot)
        {
            if (style)
            {
                var m = slot == Paint.HairSlot ? style.hair : slot == Paint.LensSlot ? style.lens : style.character;
                if (m) return m;
            }
            if (!fallback)
            {
                Debug.LogError("[HumanBuilder] Resources/HumanStyle is missing: run Seoul First Week → Repaint Characters.");
                fallback = Mats.Lit(new Color(0.6f, 0.6f, 0.62f));
            }
            return fallback;
        }

        /// <summary>A small rigid mesh (pivot at <paramref name="at"/>, rest-pose root space) attached to a bone.</summary>
        static Transform Rigid(string name, Transform bone, Transform root, Mesh mesh, Vector3 at, HumanStyle style)
        {
            if (Persist != null) mesh = Persist(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(bone, false);
            go.transform.position = root.TransformPoint(at);
            go.transform.rotation = root.rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFor(style, Paint.CharacterSlot);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>A phone in the right hand, shown while the person looks at it.</summary>
        static GameObject Phone(HumanContext c, Transform elbow, Transform root, HumanStyle style)
        {
            // in the phone pose the forearm swings up, so the screen is set to face the eyes then
            var normal = new Vector3(0, 0.683f, 0.73f);
            var along = new Vector3(0, -0.73f, 0.683f);
            var rot = Quaternion.LookRotation(normal, along);
            var hand = new Vector3(c.ShoulderX - 0.006f, 0.86f, 0.03f);
            var mb = new MeshBuilder();
            HumanMesh.Box(mb, Vector3.zero, new Vector3(0.072f, 0.148f, 0.008f), rot, Paint.Cloth(Fabric.Satin, new Color(0.12f, 0.12f, 0.13f)), _ => default);
            HumanMesh.Box(mb, rot * new Vector3(0, 0, 0.0042f), new Vector3(0.064f, 0.136f, 0.001f), rot, Paint.Cloth(Fabric.Satin, new Color(0.42f, 0.55f, 0.68f)), _ => default);
            var t = Rigid("Phone", elbow, root, mb.ToRigidMesh("Phone"), hand + normal * 0.012f, style);
            t.gameObject.SetActive(false);
            return t.gameObject;
        }
    }
}
