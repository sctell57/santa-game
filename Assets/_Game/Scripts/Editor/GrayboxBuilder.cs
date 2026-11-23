using System.Collections.Generic;
using System.IO;
using System.Linq;
using EvilSanta.Gameplay;
using EvilSanta.Player;
using EvilSanta.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EvilSanta.EditorTools
{
    /// <summary>
    /// One click builds everything Week 1 needs: palette materials, physics materials,
    /// the Elf and Present prefabs, the post-processing profile and the graybox village
    /// scene (ground, 3 cabins, sleigh, ramps, ice, trees, presents, camera, lighting).
    /// Safe to run again: assets are updated in place and the scene is rebuilt.
    /// Your ElfTuning and Volume profile are kept if they already exist.
    /// </summary>
    public static class GrayboxBuilder
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Level_Village_Graybox.unity";
        const string MatDir = Root + "/Art/Materials";
        const string PhysDir = Root + "/Settings/Physics";
        const string PrefabDir = Root + "/Prefabs";

        // Palette from the Art Style Guide.
        static readonly Color Snow = Hex("#F4F7FB");
        static readonly Color SnowShadow = Hex("#B9CCE8");
        static readonly Color Pine = Hex("#3F8F6B");
        static readonly Color Gingerbread = Hex("#C9814A");
        static readonly Color CandyRed = Hex("#E8434B");
        static readonly Color WarmLight = Hex("#FFC46B");
        static readonly Color ElfTeal = Hex("#18C7B8");
        static readonly Color Ink = Hex("#1E2235");
        static readonly Color Paper = Hex("#FFF6E6");
        static readonly Color Gold = Hex("#FFB51F");
        static readonly Color Ice = Hex("#BFE6F7");

        class Mats
        {
            public Material Snow, SnowShadow, Pine, Gingerbread, CandyRed, Window, ElfTeal, Ink, Paper, Gold, Ice;
        }

        class Phys
        {
            public PhysicsMaterial Elf, Ice, Prop;
        }

        [MenuItem("Evil Santa/2. Build Graybox Level", priority = 10)]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild graybox level?",
                    "This replaces Level_Village_Graybox and the Elf/Present prefabs. Your ElfTuning and Volume profile are kept.",
                    "Rebuild", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolders();
            StyleGuideRenderSettings.Apply();

            Mats mats = CreateMaterials();
            Phys phys = CreatePhysicsMaterials();
            ElfTuning tuning = LoadOrCreate<ElfTuning>(Root + "/Settings/ElfTuning.asset");
            VolumeProfile profile = LoadOrCreateVolumeProfile(Root + "/Settings/VP_Global_Day.asset");
            GameObject elfPrefab = CreateElfPrefab(mats, phys, tuning);
            GameObject[] presents =
            {
                CreatePresentPrefab("Present_Small", 0.45f, 2f, mats.CandyRed, mats.Gold, phys),
                CreatePresentPrefab("Present_Medium", 0.7f, 5f, mats.Gold, mats.CandyRed, phys),
                CreatePresentPrefab("Present_Big", 1.1f, 12f, mats.Paper, mats.CandyRed, phys),
            };

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLighting();
            BuildVillage(mats, phys);
            PlacePresents(presents);

            Vector3 spawn = new Vector3(0f, 0.05f, -4f);
            var elf = (GameObject)PrefabUtility.InstantiatePrefab(elfPrefab);
            elf.transform.SetPositionAndRotation(spawn, Quaternion.identity);

            SetupCamera(elf);

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            new GameObject("Controls Hint").AddComponent<ControlsHint>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Evil Santa] Graybox village built. Press Play. Tune feel in Assets/_Game/Settings/ElfTuning (Play Mode edits are kept).");
        }

        // ---------------------------------------------------------------- assets

        static void EnsureFolders()
        {
            string[] folders =
            {
                "Art", "Art/Materials", "Audio", "Prefabs", "Scenes", "Settings", "Settings/Physics",
                "Scripts/Network", "Scripts/AI",
            };
            foreach (string f in folders)
            {
                string path = $"{Root}/{f}";
                if (AssetDatabase.IsValidFolder(path)) continue;
                string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
            if (!AssetDatabase.IsValidFolder("Assets/ThirdParty")) AssetDatabase.CreateFolder("Assets", "ThirdParty");
        }

        static Mats CreateMaterials() => new Mats
        {
            Snow = Mat("M_Snow", Snow, 0.1f),
            SnowShadow = Mat("M_SnowShadow", SnowShadow, 0.1f),
            Pine = Mat("M_Pine", Pine, 0.1f),
            Gingerbread = Mat("M_Gingerbread", Gingerbread, 0.2f),
            CandyRed = Mat("M_CandyRed", CandyRed, 0.35f),
            Window = Mat("M_WarmLight_Emissive", WarmLight, 0.2f, emission: WarmLight * 2.5f),
            ElfTeal = Mat("M_ElfTeal", ElfTeal, 0.3f),
            Ink = Mat("M_Ink", Ink, 0.4f),
            Paper = Mat("M_Paper", Paper, 0.2f),
            Gold = Mat("M_Gold", Gold, 0.45f),
            Ice = Mat("M_Ice", Ice, 0.92f),
        };

        static Material Mat(string name, Color color, float smoothness, Color? emission = null)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Phys CreatePhysicsMaterials() => new Phys
        {
            Elf = PhysMat("PM_Elf_Frictionless", 0f, 0f, 0f, PhysicsMaterialCombine.Minimum),
            Ice = PhysMat("PM_Ice", 0.02f, 0.02f, 0f, PhysicsMaterialCombine.Minimum),
            Prop = PhysMat("PM_Present", 0.6f, 0.7f, 0.2f, PhysicsMaterialCombine.Average),
        };

        static PhysicsMaterial PhysMat(string name, float dynamicFriction, float staticFriction, float bounce, PhysicsMaterialCombine combine)
        {
            string path = $"{PhysDir}/{name}.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm == null)
            {
                pm = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(pm, path);
            }
            pm.dynamicFriction = dynamicFriction;
            pm.staticFriction = staticFriction;
            pm.bounciness = bounce;
            pm.frictionCombine = combine;
            pm.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        static VolumeProfile LoadOrCreateVolumeProfile(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;

            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);

            // Values from the Art Style Guide, "Global Volume (post-processing)".
            var tonemap = p.Add<Tonemapping>();
            tonemap.mode.Override(TonemappingMode.Neutral);

            var color = p.Add<ColorAdjustments>();
            color.postExposure.Override(0.2f);
            color.contrast.Override(10f);
            color.saturation.Override(15f);

            var wb = p.Add<WhiteBalance>();
            wb.temperature.Override(8f);

            var bloom = p.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.65f);

            var smh = p.Add<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.96f, 0.96f, 1.06f, 0f));    // slightly blue-violet
            smh.highlights.Override(new Vector4(1.04f, 1.01f, 0.96f, 0f)); // slightly warm

            var vignette = p.Add<Vignette>();
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.4f);

            foreach (VolumeComponent c in p.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, p);
            }
            EditorUtility.SetDirty(p);
            return p;
        }

        // ---------------------------------------------------------------- prefabs

        static GameObject CreateElfPrefab(Mats mats, Phys phys, ElfTuning tuning)
        {
            // 1.1 m tall, head about 40% of height, big mitts (Art Style Guide shape table).
            var root = new GameObject("Elf") { tag = "Player" };

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.1f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.55f, 0f);
            capsule.sharedMaterial = phys.Elf;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 20f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Part(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.36f, 0f), new Vector3(0.56f, 0.36f, 0.5f), mats.ElfTeal);
            Part(PrimitiveType.Sphere, "Head", visual, new Vector3(0f, 0.84f, 0f), Vector3.one * 0.46f, mats.Paper);
            Part(PrimitiveType.Cube, "Eyes", visual, new Vector3(0f, 0.88f, 0.21f), new Vector3(0.22f, 0.06f, 0.04f), mats.Ink);
            Part(PrimitiveType.Sphere, "Nose", visual, new Vector3(0f, 0.82f, 0.24f), Vector3.one * 0.08f, mats.CandyRed);
            var hat = Part(PrimitiveType.Cylinder, "Hat", visual, new Vector3(0f, 1.1f, -0.02f), new Vector3(0.36f, 0.14f, 0.36f), mats.ElfTeal);
            hat.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Part(PrimitiveType.Sphere, "HatPom", visual, new Vector3(0f, 1.27f, -0.08f), Vector3.one * 0.13f, mats.Paper);
            Part(PrimitiveType.Sphere, "MittL", visual, new Vector3(-0.34f, 0.42f, 0.06f), Vector3.one * 0.18f, mats.CandyRed);
            Part(PrimitiveType.Sphere, "MittR", visual, new Vector3(0.34f, 0.42f, 0.06f), Vector3.one * 0.18f, mats.CandyRed);

            var input = root.AddComponent<ElfInput>();
            var motor = root.AddComponent<ElfMotor>();
            root.AddComponent<ElfGrabber>();
            SetRef(motor, "tuning", tuning);
            SetRef(motor, "input", input);

            string path = $"{PrefabDir}/Elf.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreatePresentPrefab(string name, float size, float mass, Material wrap, Material ribbon, Phys phys)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.localScale = Vector3.one * size;
            root.GetComponent<Renderer>().sharedMaterial = wrap;
            root.GetComponent<Collider>().sharedMaterial = phys.Prop;

            // Ribbons are visual only (children of a scaled cube, so sizes are relative).
            Part(PrimitiveType.Cube, "RibbonX", root.transform, Vector3.zero, new Vector3(1.02f, 1.02f, 0.18f), ribbon);
            Part(PrimitiveType.Cube, "RibbonZ", root.transform, Vector3.zero, new Vector3(0.18f, 1.02f, 1.02f), ribbon);
            Part(PrimitiveType.Sphere, "Bow", root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 0.2f, 0.35f), ribbon);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<Grabbable>();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ---------------------------------------------------------------- scene

        static void SetupLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Hex("#FFE7C4");
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#9CC8F5");
            RenderSettings.ambientEquatorColor = Hex("#FFE1C7");
            RenderSettings.ambientGroundColor = Hex("#B9CCE8");

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#FFD9B8");
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                string path = $"{MatDir}/M_Sky_Day.mat";
                var sky = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (sky == null) { sky = new Material(skyShader); AssetDatabase.CreateAsset(sky, path); }
                sky.SetColor("_SkyTint", Hex("#7FB8F0"));
                sky.SetColor("_GroundColor", Hex("#FFD9B8"));
                sky.SetFloat("_AtmosphereThickness", 0.7f);
                sky.SetFloat("_Exposure", 1.15f);
                sky.SetFloat("_SunDisk", 0f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }
        }

        static void BuildVillage(Mats mats, Phys phys)
        {
            var level = new GameObject("Level").transform;

            // Ground: 90 x 90 m of snow with a gingerbread fence so nobody gets lost.
            Block("Ground", level, new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 90f), mats.Snow);
            var fence = new GameObject("Fence").transform;
            fence.SetParent(level, false);
            for (int side = 0; side < 4; side++)
            {
                Quaternion r = Quaternion.Euler(0f, side * 90f, 0f);
                var rail = Block("Fence", fence, r * new Vector3(0f, 0.6f, 44.5f), new Vector3(90f, 1.2f, 0.4f), mats.Gingerbread);
                rail.transform.localRotation = r;
            }

            // Three cabins with snowy gable roofs and glowing windows. Roofs are climbable.
            Cabin(level, mats, new Vector3(-14f, 0f, 10f), 20f);
            Cabin(level, mats, new Vector3(-3f, 0f, 19f), -8f);
            Cabin(level, mats, new Vector3(11f, 0f, 14f), -30f);

            // Sleigh with a drop-off trigger for Week 4.
            var sleigh = new GameObject("Sleigh").transform;
            sleigh.SetParent(level, false);
            sleigh.SetPositionAndRotation(new Vector3(7f, 0f, -7f), Quaternion.Euler(0f, -60f, 0f));
            Block("Body", sleigh, new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.9f, 3.2f), mats.CandyRed);
            Block("Seat", sleigh, new Vector3(0f, 1.35f, -1.1f), new Vector3(1.8f, 0.5f, 1f), mats.CandyRed);
            Block("RunnerL", sleigh, new Vector3(-0.75f, 0.15f, 0.2f), new Vector3(0.15f, 0.3f, 3.8f), mats.Gold);
            Block("RunnerR", sleigh, new Vector3(0.75f, 0.15f, 0.2f), new Vector3(0.15f, 0.3f, 3.8f), mats.Gold);
            var drop = new GameObject("DropZone").AddComponent<BoxCollider>();
            drop.transform.SetParent(sleigh, false);
            drop.transform.localPosition = new Vector3(0f, 1.6f, 0.4f);
            drop.size = new Vector3(1.8f, 1.4f, 2.2f);
            drop.isTrigger = true;

            // A raised platform with a ramp up, plus a standalone jump ramp.
            Block("Platform", level, new Vector3(-10f, 1f, -12f), new Vector3(7f, 2f, 7f), mats.SnowShadow);
            Ramp(level, mats.SnowShadow, new Vector3(-10f, 0f, -3f), 180f, 2f, 20f, 3f);
            Ramp(level, mats.SnowShadow, new Vector3(14f, 0f, 2f), 90f, 1.4f, 24f, 3f);
            Ramp(level, mats.SnowShadow, new Vector3(-22f, 0f, -6f), 0f, 0.8f, 14f, 4f);

            // Ice patches: low traction for elves, low friction for presents.
            IcePatch(level, mats, phys, new Vector3(2f, 0f, 6f), new Vector2(8f, 5f), 15f);
            IcePatch(level, mats, phys, new Vector3(-18f, 0f, -14f), new Vector2(6f, 9f), -10f);
            IcePatch(level, mats, phys, new Vector3(18f, 0f, -16f), new Vector2(10f, 6f), 40f);

            // Pines around the edges.
            Vector3[] trees =
            {
                new(-30f, 0f, 25f), new(-25f, 0f, 32f), new(24f, 0f, 30f), new(32f, 0f, 18f),
                new(-34f, 0f, -20f), new(30f, 0f, -28f), new(-6f, 0f, 34f), new(0f, 0f, -30f),
            };
            for (int i = 0; i < trees.Length; i++) Tree(level, mats, trees[i], 0.9f + (i % 3) * 0.2f);
        }

        static void PlacePresents(GameObject[] presents)
        {
            var parent = new GameObject("Presents").transform;
            var rng = new System.Random(1225);
            Vector3[] spots =
            {
                new(-2f, 0f, 2f), new(2f, 0f, 0f), new(-4f, 0f, -1f), new(-12f, 2f, -12f), new(-9f, 2f, -13f),
                new(-14f, 0f, 5f), new(10f, 0f, 9f), new(4f, 0f, 7f), new(16f, 0f, -14f), new(-1f, 0f, 14f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                GameObject prefab = presents[i % 5 == 4 ? 2 : (i % 2)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.transform.SetPositionAndRotation(spots[i] + Vector3.up * 1f,
                    Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));
            }
        }

        static void SetupCamera(GameObject elf)
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camGo.AddComponent<CinemachineBrain>();

            var pivot = new GameObject("Camera Pivot").AddComponent<ElfCameraRig>();
            pivot.transform.position = elf.transform.position + Vector3.up;
            SetRef(pivot, "target", elf.transform);
            SetRef(pivot, "input", elf.GetComponent<ElfInput>());

            var vcamGo = new GameObject("Elf Camera");
            var vcam = vcamGo.AddComponent<CinemachineCamera>();
            vcam.Follow = pivot.transform;
            vcam.Lens.FieldOfView = 55f;
            vcam.Lens.NearClipPlane = 0.1f;
            vcam.Lens.FarClipPlane = 300f;

            var follow = vcamGo.AddComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0.35f, 0.35f, 0f);
            follow.VerticalArmLength = 0.25f;
            follow.CameraSide = 0.75f;
            follow.CameraDistance = 5.5f;
            follow.Damping = new Vector3(0.1f, 0.3f, 0.3f);
            follow.AvoidObstacles.Enabled = true;
            follow.AvoidObstacles.CollisionFilter = Physics.DefaultRaycastLayers;
            follow.AvoidObstacles.IgnoreTag = "Player";
            follow.AvoidObstacles.CameraRadius = 0.25f;
            follow.AvoidObstacles.DampingIntoCollision = 0.05f;
            follow.AvoidObstacles.DampingFromCollision = 0.4f;

            SetRef(elf.GetComponent<ElfMotor>(), "cameraRig", pivot);
        }

        // ---------------------------------------------------------------- level pieces

        static void Cabin(Transform parent, Mats mats, Vector3 position, float yaw)
        {
            var cabin = new GameObject("Cabin").transform;
            cabin.SetParent(parent, false);
            cabin.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Block("Walls", cabin, new Vector3(0f, 1.5f, 0f), new Vector3(5f, 3f, 4f), mats.Gingerbread);
            var roof = Block("Roof", cabin, new Vector3(0f, 3f, 0f), new Vector3(5.6f, 2.6f, 2.6f), mats.Snow);
            roof.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            Block("Chimney", cabin, new Vector3(1.6f, 4.3f, 0.6f), new Vector3(0.7f, 1.6f, 0.7f), mats.CandyRed);
            Block("Door", cabin, new Vector3(0f, 0.8f, 2.02f), new Vector3(1f, 1.6f, 0.1f), mats.Ink, collider: false);
            Block("WindowL", cabin, new Vector3(-1.6f, 1.7f, 2.02f), new Vector3(0.8f, 0.8f, 0.1f), mats.Window, collider: false);
            Block("WindowR", cabin, new Vector3(1.6f, 1.7f, 2.02f), new Vector3(0.8f, 0.8f, 0.1f), mats.Window, collider: false);
        }

        /// <summary>A ramp whose low end starts at <paramref name="foot"/> and climbs toward <paramref name="yaw"/>.</summary>
        static void Ramp(Transform parent, Material mat, Vector3 foot, float yaw, float rise, float angle, float width)
        {
            float length = rise / Mathf.Sin(angle * Mathf.Deg2Rad);
            const float thickness = 0.4f;
            Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
            Quaternion tilt = heading * Quaternion.Euler(-angle, 0f, 0f);
            // Centre of the top surface sits halfway up the slope; push down by half thickness.
            Vector3 centre = foot + heading * Vector3.forward * (length * 0.5f * Mathf.Cos(angle * Mathf.Deg2Rad))
                             + Vector3.up * (rise * 0.5f) - tilt * Vector3.up * (thickness * 0.5f);
            var ramp = Block("Ramp", parent, centre, new Vector3(width, thickness, length), mat);
            ramp.transform.rotation = tilt;
        }

        static void IcePatch(Transform parent, Mats mats, Phys phys, Vector3 position, Vector2 size, float yaw)
        {
            var ice = Block("Ice", parent, position + Vector3.up * 0.03f, new Vector3(size.x, 0.06f, size.y), mats.Ice);
            ice.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ice.GetComponent<Collider>().sharedMaterial = phys.Ice;
            ice.AddComponent<SurfaceProperties>().Traction = 0.12f;
        }

        static void Tree(Transform parent, Mats mats, Vector3 position, float scale)
        {
            var tree = new GameObject("Pine").transform;
            tree.SetParent(parent, false);
            tree.position = position;
            tree.localScale = Vector3.one * scale;
            Part(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.6f, 0.5f), mats.Gingerbread, keepCollider: true);
            Part(PrimitiveType.Sphere, "Lower", tree, new Vector3(0f, 2.2f, 0f), new Vector3(3f, 2.6f, 3f), mats.Pine, keepCollider: true);
            Part(PrimitiveType.Sphere, "Upper", tree, new Vector3(0f, 3.8f, 0f), new Vector3(2f, 2.2f, 2f), mats.Pine, keepCollider: true);
            Part(PrimitiveType.Sphere, "Snowcap", tree, new Vector3(0f, 4.8f, 0f), new Vector3(1f, 0.6f, 1f), mats.Snow);
        }

        static GameObject Block(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.transform;
        }

        // ---------------------------------------------------------------- helpers

        static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogWarning($"[Evil Santa] {target.GetType().Name}.{field} not found"); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuild(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
