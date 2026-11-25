using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VRThrowing.EditorTools
{
    /// <summary>
    /// Generator sceny z dwoma stanowiskami. Wszystko z prymitywów Unity + OVRCameraRig z Meta XR Core SDK.
    /// Ponowne uruchomienie tworzy od nowa obiekty Environment, Basketball, Bowling, Player, UI i światło;
    /// rig i Twoje własne obiekty zostają.
    ///
    ///   KOSZ (x = 0):   boisko 10 x 13 m, kosz na z = 7,6 (obręcz 3,05 m), stojak z 5 piłkami obok gracza.
    ///   KRĘGLE (x = 12): rozbieg z = -5..0, linia spalonego z = 0, tor 18,29 m, 10 kręgli, dół i zasłona,
    ///                    stojak z 3 kulami, tablica 10 ramek nad torem.
    /// </summary>
    public static class VRThrowingSceneBuilder
    {
        static readonly string[] GeneratedRoots = { "Directional Light", "Environment", "Basketball", "Bowling", "Player", "UI" };
        static readonly Color PanelBg = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        static readonly Color SkyColor = new Color(0.62f, 0.74f, 0.88f);
        const string RigPrefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRCameraRig.prefab";
        const string PhysicsFolder = VRThrowingSetup.Root + "/Physics";
        const int IgnoreRaycastLayer = 2;

        // Kosz (wymiary FIBA)
        const float HoopHeight = 3.05f;
        const float BoardZ = 7.6f;           // przednia płaszczyzna tablicy
        const float RimRadius = 0.2286f;     // obręcz 45,7 cm
        static float RimZ => BoardZ - 0.15f - RimRadius;
        const float BallRadius = 0.12f;

        // Kręgle (wymiary regulaminowe)
        const float LaneX = 12f;
        const float LaneLength = 18.29f;     // linia spalonego → pierwszy kręgiel
        const float LaneHalfWidth = 0.525f;  // tor 1,05 m
        const float GutterWidth = 0.24f;
        const float LaneTop = 0.15f;
        const float BowlingBallRadius = 0.109f;
        const float HallHalfWidth = 1.75f;

        static Font font;

        class Mats
        {
            public Material ground, court, courtLine, paint, wall, board, boardLine, rim, net, pole, ball, ballSeam;
            public Material laneWood, deck, gutter, kickback, bumper, foul, arrow, pin, pinBand, pit, cushion, hallFloor;
            public Material bball1, bball2, bball3, hole, rack, peg, standMark;
            public Material pointer, preview, overlay, ctrlBody, ctrlLeft, ctrlRight, ctrlTip;
        }

        class PhysMats
        {
            public PhysicsMaterial basketball, bowlingBall, pin, lane, rim, board, floor, cushion;
        }

        // =====================================================================

        public static bool GenerateScene(bool interactive, out string info)
        {
            info = "";
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            bool exists = File.Exists(VRThrowingSetup.ScenePath);
            if (exists && interactive && !EditorUtility.DisplayDialog("VR Rzuty",
                    "Scena już istnieje. Wygenerować ją od nowa?\n\n" +
                    "• Environment, Basketball, Bowling, Player, UI i światło zostaną utworzone od nowa (zmiany w nich przepadną).\n" +
                    "• OVRCameraRig i Twoje własne obiekty zostaną.",
                    "Generuj", "Anuluj"))
                return false;

            VRThrowingSetup.EnsureUrp();
            Mats m = CreateMaterials();
            PhysMats pm = CreatePhysicsMaterials();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Scene scene;
            if (exists)
            {
                scene = EditorSceneManager.OpenScene(VRThrowingSetup.ScenePath, OpenSceneMode.Single);
                foreach (var go in scene.GetRootGameObjects())
                    if (GeneratedRoots.Contains(go.name)) Object.DestroyImmediate(go);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            var sb = new StringBuilder();
            OVRCameraRig rig = EnsureRig(sb);
            RemoveStrayCameras(rig, sb);
            var mgr = Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include);
            if (mgr != null && VRThrowingSetup.SetEnum(mgr, "FloorLevel", "_trackingOriginType", "m_TrackingOriginType", "trackingOriginType"))
                sb.AppendLine("• OVRManager: Tracking Origin = Floor Level");
            SetupRig(rig, sb);

            BuildLighting();
            BuildEnvironment(m);
            var basket = BuildBasketball(m, pm);
            var bowling = BuildBowling(m, pm);
            var ui = BuildUI();
            BuildPlayer(m, basket, bowling, ui);

            if (rig != null) rig.transform.SetPositionAndRotation(basket.standPoint.position, Quaternion.identity);

            VRThrowingSetup.EnsureFolder(VRThrowingSetup.Root + "/Scenes");
            EditorSceneManager.SaveScene(scene, VRThrowingSetup.ScenePath);
            VRThrowingSetup.AddSceneToBuild();
            sb.AppendLine("• Zapisano " + VRThrowingSetup.ScenePath + " (dodana do buildu)");
            info = sb.ToString();

            if (interactive)
                EditorUtility.DisplayDialog("VR Rzuty", "Scena gotowa.\n\n" + info + "\n" + VRThrowingSetup.ChecklistSummary(), "OK");
            return true;
        }

        /// <summary>Rig: warstwa Ignore Raycast, tło w kolorze nieba, bliska płaszczyzna 5 cm. Kolizje gracza nie są potrzebne.</summary>
        static void SetupRig(OVRCameraRig rig, StringBuilder sb)
        {
            if (rig == null) return;
            rig.gameObject.layer = IgnoreRaycastLayer;
            foreach (var cam in rig.GetComponentsInChildren<Camera>(true))
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = SkyColor;
                if (cam.nearClipPlane > 0.05f) cam.nearClipPlane = 0.05f;
                EditorUtility.SetDirty(cam);
            }
            EditorUtility.SetDirty(rig.gameObject);
            sb.AppendLine("• Rig: warstwa Ignore Raycast, tło w kolorze nieba");
        }

        // =====================================================================
        // MATERIAŁY
        // =====================================================================

        static Mats CreateMaterials()
        {
            VRThrowingSetup.EnsureFolder(VRThrowingSetup.MaterialsFolder);
            return new Mats
            {
                ground = Mat("Ground", new Color(0.33f, 0.37f, 0.33f)),
                court = Mat("CourtWood", new Color(0.78f, 0.58f, 0.36f), 0.45f),
                courtLine = Mat("CourtLine", new Color(0.97f, 0.97f, 0.95f), unlit: true),
                paint = Mat("CourtPaint", new Color(0.62f, 0.25f, 0.2f), 0.4f),
                wall = Mat("Wall", new Color(0.8f, 0.82f, 0.86f)),
                board = Mat("Backboard", new Color(0.92f, 0.95f, 1f), 0.8f),
                boardLine = Mat("BackboardLine", new Color(0.9f, 0.2f, 0.15f), unlit: true),
                rim = Mat("Rim", new Color(1f, 0.42f, 0.08f), 0.6f),
                net = Mat("Net", new Color(1f, 1f, 1f), unlit: true),
                pole = Mat("Pole", new Color(0.2f, 0.22f, 0.26f), 0.5f),
                ball = Mat("Basketball", new Color(0.92f, 0.42f, 0.12f), 0.25f),
                ballSeam = Mat("BallSeam", new Color(0.08f, 0.05f, 0.04f)),
                laneWood = Mat("LaneWood", new Color(0.86f, 0.7f, 0.45f), 0.85f),
                deck = Mat("PinDeck", new Color(0.92f, 0.8f, 0.58f), 0.85f),
                gutter = Mat("Gutter", new Color(0.35f, 0.36f, 0.4f), 0.6f),
                kickback = Mat("Kickback", new Color(0.2f, 0.25f, 0.45f)),
                bumper = Mat("Bumper", new Color(1f, 0.85f, 0.2f)),
                foul = Mat("FoulLine", new Color(0.95f, 0.1f, 0.1f), unlit: true),
                arrow = Mat("LaneArrow", new Color(0.35f, 0.2f, 0.1f)),
                pin = Mat("Pin", new Color(0.97f, 0.97f, 0.95f), 0.7f),
                pinBand = Mat("PinBand", new Color(0.85f, 0.1f, 0.1f), 0.5f),
                pit = Mat("Pit", new Color(0.08f, 0.08f, 0.1f)),
                cushion = Mat("Cushion", new Color(0.12f, 0.12f, 0.15f)),
                hallFloor = Mat("HallFloor", new Color(0.45f, 0.36f, 0.3f), 0.4f),
                bball1 = Mat("BowlingBall_Blue", new Color(0.12f, 0.3f, 0.85f), 0.9f),
                bball2 = Mat("BowlingBall_Purple", new Color(0.5f, 0.15f, 0.65f), 0.9f),
                bball3 = Mat("BowlingBall_Green", new Color(0.1f, 0.6f, 0.3f), 0.9f),
                hole = Mat("FingerHole", new Color(0.02f, 0.02f, 0.02f)),
                rack = Mat("Rack", new Color(0.25f, 0.27f, 0.3f), 0.5f),
                peg = Mat("Peg", new Color(0.6f, 0.62f, 0.66f), 0.6f),
                standMark = Mat("StandMark", new Color(0.2f, 0.6f, 1f), unlit: true),
                pointer = Mat("Pointer", Color.white, unlit: true),
                preview = Mat("TrajectoryPreview", new Color(0.3f, 0.95f, 1f), unlit: true),
                overlay = OverlayMat("ScreenOverlay"),
                ctrlBody = Mat("ControllerBody", new Color(0.12f, 0.12f, 0.14f), 0.6f),
                ctrlLeft = Mat("ControllerLeft", new Color(0.15f, 0.8f, 0.95f), unlit: true),
                ctrlRight = Mat("ControllerRight", new Color(1f, 0.55f, 0.15f), unlit: true),
                ctrlTip = Mat("ControllerTip", Color.white, unlit: true),
            };
        }

        static PhysMats CreatePhysicsMaterials()
        {
            VRThrowingSetup.EnsureFolder(PhysicsFolder);
            return new PhysMats
            {
                // Odbicie piłki: Maximum - podłoga i obręcz same nie muszą być „sprężyste”.
                basketball = PMat("Basketball", 0.6f, 0.6f, 0.82f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Maximum),
                // Kula: tarcie Minimum - o poślizgu decyduje naoliwiony tor.
                bowlingBall = PMat("BowlingBall", 0.2f, 0.2f, 0.05f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Minimum),
                pin = PMat("Pin", 0.35f, 0.4f, 0.45f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
                lane = PMat("Lane", 0.04f, 0.06f, 0.05f, PhysicsMaterialCombine.Minimum, PhysicsMaterialCombine.Average),
                rim = PMat("Rim", 0.5f, 0.5f, 0.55f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
                board = PMat("Backboard", 0.4f, 0.4f, 0.6f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
                floor = PMat("Floor", 0.6f, 0.6f, 0.3f, PhysicsMaterialCombine.Average, PhysicsMaterialCombine.Average),
                cushion = PMat("Cushion", 0.8f, 0.8f, 0f, PhysicsMaterialCombine.Maximum, PhysicsMaterialCombine.Minimum),
            };
        }

        static PhysicsMaterial PMat(string name, float dynamicFriction, float staticFriction, float bounciness,
            PhysicsMaterialCombine friction, PhysicsMaterialCombine bounce)
        {
            string path = PhysicsFolder + "/" + name + ".physicMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = dynamicFriction;
            mat.staticFriction = staticFriction;
            mat.bounciness = bounciness;
            mat.frictionCombine = friction;
            mat.bounceCombine = bounce;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =====================================================================
        // ŚWIATŁO, OTOCZENIE
        // =====================================================================

        static void BuildLighting()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.9f);
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.76f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.23f, 0.22f);
        }

        static void BuildEnvironment(Mats m)
        {
            var env = new GameObject("Environment").transform;
            // Teren pod spodem (0,5 m niżej) - widać go tylko między halami.
            Prim(PrimitiveType.Plane, "Ground", env, new Vector3(6f, -0.5f, 8f), new Vector3(6f, 1f, 6f), m.ground);
            Static(env);
        }

        // =====================================================================
        // KOSZ
        // =====================================================================

        class BasketRefs
        {
            public BasketballGame game;
            public Transform standPoint;
            public Throwable[] balls;
        }

        static BasketRefs BuildBasketball(Mats m, PhysMats pm)
        {
            var root = new GameObject("Basketball").transform;
            var refs = new BasketRefs();

            // ---- boisko i ściany (ściany zatrzymują piłki)
            var court = Prim(PrimitiveType.Cube, "Court", root, new Vector3(0f, -0.05f, 3.25f), new Vector3(10f, 0.1f, 13f), m.court, true);
            court.GetComponent<Collider>().sharedMaterial = pm.floor;
            WallBox(root, "Wall_L", new Vector3(-5.1f, 2f, 3.25f), new Vector3(0.2f, 4f, 13.2f), m.wall, pm.board);
            WallBox(root, "Wall_R", new Vector3(5.1f, 2f, 3.25f), new Vector3(0.2f, 4f, 13.2f), m.wall, pm.board);
            WallBox(root, "Wall_Back", new Vector3(0f, 2f, -3.35f), new Vector3(10.4f, 4f, 0.2f), m.wall, pm.board);
            WallBox(root, "Wall_Front", new Vector3(0f, 2f, 9.85f), new Vector3(10.4f, 4f, 0.2f), m.wall, pm.board);

            // ---- linie: końcowa, rzutów wolnych, „trumna”, łuk za 3 punkty
            float baseline = BoardZ + 1.2f;
            float freeThrow = BoardZ - 4.57f;
            FloorLine(root, new Vector3(-4.9f, 0f, baseline), new Vector3(4.9f, 0f, baseline), m.courtLine);
            Prim(PrimitiveType.Cube, "Paint", root, new Vector3(0f, 0.002f, (freeThrow + baseline) * 0.5f), new Vector3(4.9f, 0.004f, baseline - freeThrow), m.paint);
            FloorLine(root, new Vector3(-2.45f, 0f, freeThrow), new Vector3(2.45f, 0f, freeThrow), m.courtLine);
            FloorLine(root, new Vector3(-2.45f, 0f, freeThrow), new Vector3(-2.45f, 0f, baseline), m.courtLine);
            FloorLine(root, new Vector3(2.45f, 0f, freeThrow), new Vector3(2.45f, 0f, baseline), m.courtLine);
            const float arcR = 6.75f;
            const float arcMax = 41f;
            Vector3 rimFloor = new Vector3(0f, 0f, RimZ);
            Vector3 prev = rimFloor + Quaternion.Euler(0f, -arcMax, 0f) * Vector3.back * arcR;
            FloorLine(root, prev, new Vector3(prev.x, 0f, baseline), m.courtLine);
            for (int i = 1; i <= 20; i++)
            {
                float a = -arcMax + i * (2f * arcMax / 20f);
                Vector3 p = rimFloor + Quaternion.Euler(0f, a, 0f) * Vector3.back * arcR;
                FloorLine(root, prev, p, m.courtLine);
                prev = p;
            }
            FloorLine(root, prev, new Vector3(prev.x, 0f, baseline), m.courtLine);

            // ---- znaczniki odległości (2,0 / 4,2 / 6,75 m od środka obręczy)
            for (int i = 0; i < ThrowSettings.ShotDistances.Length; i++)
            {
                float z = RimZ - ThrowSettings.ShotDistances[i];
                Prim(PrimitiveType.Cylinder, "DistanceMark_" + i, root, new Vector3(1.2f, 0.004f, z), new Vector3(0.25f, 0.002f, 0.25f), m.standMark);
                FloorLabel(root, new Vector3(1.75f, 0.01f, z), 0f, ThrowSettings.ShotDistances[i].ToString("0.##").Replace('.', ',') + " m", 0.22f);
            }

            // ---- kosz (przesuwany w pionie przy zmianie wysokości)
            var hoop = new GameObject("Hoop").transform;
            hoop.SetParent(root, false);
            float rimY = HoopHeight;
            Prim(PrimitiveType.Cube, "Pole", hoop, new Vector3(0f, 1.8f, BoardZ + 1.25f), new Vector3(0.16f, 3.6f + 1.2f, 0.16f), m.pole, true);
            Prim(PrimitiveType.Cube, "Arm", hoop, new Vector3(0f, rimY + 0.35f, BoardZ + 0.65f), new Vector3(0.1f, 0.1f, 1.2f), m.pole, true);
            var board = Prim(PrimitiveType.Cube, "Backboard", hoop, new Vector3(0f, rimY - 0.15f + 0.525f, BoardZ + 0.025f), new Vector3(1.8f, 1.05f, 0.05f), m.board, true);
            board.GetComponent<Collider>().sharedMaterial = pm.board;
            board.AddComponent<Backboard>();
            // prostokąt na tablicy 59 x 45 cm, dolna krawędź na wysokości obręczy
            float bz = BoardZ - 0.002f;
            BoxLine(hoop, new Vector3(-0.295f, rimY, bz), new Vector3(0.295f, rimY, bz), m.boardLine);
            BoxLine(hoop, new Vector3(-0.295f, rimY + 0.45f, bz), new Vector3(0.295f, rimY + 0.45f, bz), m.boardLine);
            BoxLine(hoop, new Vector3(-0.295f, rimY, bz), new Vector3(-0.295f, rimY + 0.45f, bz), m.boardLine);
            BoxLine(hoop, new Vector3(0.295f, rimY, bz), new Vector3(0.295f, rimY + 0.45f, bz), m.boardLine);
            Prim(PrimitiveType.Cube, "Bracket", hoop, new Vector3(0f, rimY - 0.02f, BoardZ - 0.075f), new Vector3(0.14f, 0.03f, 0.15f), m.rim, true)
                .AddComponent<RimPart>();

            // obręcz: 16 cienkich walców (CapsuleCollider) ułożonych w okrąg
            var rimCenter = new GameObject("RimCenter").transform;
            rimCenter.SetParent(hoop, false);
            rimCenter.localPosition = new Vector3(0f, rimY, RimZ);
            const int seg = 16;
            float segLen = 2f * Mathf.PI * RimRadius / seg * 1.15f;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var pos = new Vector3(Mathf.Cos(a) * RimRadius, rimY, RimZ + Mathf.Sin(a) * RimRadius);
                var tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                var part = Prim(PrimitiveType.Cylinder, "Rim_" + i, hoop, pos, new Vector3(0.02f, segLen * 0.5f, 0.02f), m.rim, true,
                    Quaternion.FromToRotation(Vector3.up, tangent));
                part.GetComponent<Collider>().sharedMaterial = pm.rim;
                part.AddComponent<RimPart>();
            }
            // siatka (tylko wygląd)
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var top = new Vector3(Mathf.Cos(a) * RimRadius, rimY, RimZ + Mathf.Sin(a) * RimRadius);
                var bottom = new Vector3(Mathf.Cos(a + 0.35f) * RimRadius * 0.6f, rimY - 0.42f, RimZ + Mathf.Sin(a + 0.35f) * RimRadius * 0.6f);
                Strand(hoop, top, bottom, 0.006f, m.net);
            }

            // ---- tablica wyników nad koszem
            Vector3 eye = new Vector3(0f, 1.6f, RimZ - 4.2f);
            Vector3 sbPos = new Vector3(0f, 4.75f, BoardZ + 0.1f);
            var scorePanel = Panel("Scoreboard", root, sbPos, Face(sbPos, eye), new Vector2(1500f, 480f), 0.0017f, PanelBg);
            var scoreText = Txt(scorePanel, "Text", Vector2.zero, Vector2.one, 54, TextAnchor.MiddleCenter, Color.white, "KOSZ", 24f);

            // ---- tablice informacyjne na ścianach bocznych
            // tablice równoległe do ścian (czytelna strona do środka boiska)
            var bcPos = new Vector3(-4.97f, 1.75f, 2.4f);
            var bpPos = new Vector3(4.97f, 1.75f, 2.4f);
            Board(root, "Board_Controls", bcPos, bcPos + Vector3.right, new Vector2(900f, 1000f), 0.0012f, ControlsText);
            Board(root, "Board_Physics", bpPos, bpPos + Vector3.left, new Vector2(900f, 1000f), 0.0012f, BasketballText);

            // ---- stanowisko gracza: znacznik + stojak z 5 piłkami (przesuwane przy zmianie odległości)
            var stand = new GameObject("Stand").transform;
            stand.SetParent(root, false);
            stand.position = new Vector3(0f, 0f, RimZ - 4.2f);
            var standPoint = new GameObject("StandPoint").transform;
            standPoint.SetParent(stand, false);
            Prim(PrimitiveType.Cylinder, "StandMark", stand, new Vector3(0f, 0.004f, 0f), new Vector3(0.5f, 0.002f, 0.5f), m.standMark);

            var homes = BallRack(stand, "BallRack", new Vector3(-0.7f, 0f, 0f), 5, 0.27f, 1.0f, BallRadius, m);
            var ballsRoot = new GameObject("Balls").transform;
            ballsRoot.SetParent(root, false);
            var balls = new List<Throwable>();
            for (int i = 0; i < homes.Count; i++)
            {
                var b = Ball(ballsRoot, "Basketball_" + (i + 1), homes[i], BallRadius, 0.62f, m.ball, pm.basketball, BallKind.Basketball);
                // „szwy” piłki - dwa ciemne pierścienie (tylko wygląd)
                Prim(PrimitiveType.Cylinder, "Seam1", b.transform, Vector3.zero, new Vector3(1.01f, 0.01f, 1.01f), m.ballSeam);
                Prim(PrimitiveType.Cylinder, "Seam2", b.transform, Vector3.zero, new Vector3(1.01f, 0.01f, 1.01f), m.ballSeam, false, Quaternion.Euler(90f, 0f, 0f));
                b.allowedArea = new Rect(-5.4f, -3.7f, 10.8f, 13.9f);
                b.returnWhenRestingFor = 1.5f;
                b.returnAfter = 10f;
                b.gameObject.AddComponent<ImpactSound>().kind = ImpactKind.Basketball;
                balls.Add(b);
            }

            var game = root.gameObject.AddComponent<BasketballGame>();
            game.hoopRoot = hoop;
            game.rimCenter = rimCenter;
            game.rimRadius = RimRadius;
            game.builtHeight = HoopHeight;
            game.standRoot = stand;
            game.standPoint = standPoint;
            game.balls = balls.ToArray();
            game.scoreText = scoreText;

            refs.game = game;
            refs.standPoint = standPoint;
            refs.balls = game.balls;
            NoShadows(root);
            return refs;
        }

        // =====================================================================
        // KRĘGLE
        // =====================================================================

        class BowlingRefs
        {
            public BowlingLane lane;
            public Transform standPoint;
        }

        static BowlingRefs BuildBowling(Mats m, PhysMats pm)
        {
            var root = new GameObject("Bowling").transform;
            root.position = new Vector3(LaneX, 0f, 0f);
            var refs = new BowlingRefs();
            var stretch = new List<LaneStretch>();
            float outer = LaneHalfWidth + GutterWidth;          // zewnętrzna krawędź rynny
            float sideW = HallHalfWidth - outer - 0.1f;         // pas między bandą a ścianą hali

            // ---- rozbieg (z -5..0) i hala
            Prim(PrimitiveType.Cube, "Approach", root, new Vector3(0f, LaneTop * 0.5f, -2.5f), new Vector3(HallHalfWidth * 2f, LaneTop, 5f), m.hallFloor, true)
                .GetComponent<Collider>().sharedMaterial = pm.floor;
            Prim(PrimitiveType.Cube, "FoulLine", root, new Vector3(0f, LaneTop + 0.001f, -0.02f), new Vector3(HallHalfWidth * 2f, 0.002f, 0.04f), m.foul);
            WallBox(root.transform, "HallWall_L", new Vector3(-HallHalfWidth - 0.1f, 1.5f, 8f), new Vector3(0.2f, 3f, 26.4f), m.wall, pm.floor);
            WallBox(root.transform, "HallWall_R", new Vector3(HallHalfWidth + 0.1f, 1.5f, 8f), new Vector3(0.2f, 3f, 26.4f), m.wall, pm.floor);
            WallBox(root.transform, "HallWall_Back", new Vector3(0f, 1.5f, -5.1f), new Vector3(HallHalfWidth * 2f + 0.4f, 3f, 0.2f), m.wall, pm.floor);

            // ---- tor (rozciągany przy zmianie długości): podstawa = dno rynien, deska, bandy boczne, pasy przy ścianach
            stretch.Add(Stretch(Prim(PrimitiveType.Cube, "GutterBase", root, new Vector3(0f, 0.025f, 0f), new Vector3(outer * 2f, 0.05f, 1f), m.gutter, true), 0f, 1.0f));
            var lane = Prim(PrimitiveType.Cube, "Lane", root, new Vector3(0f, 0.1f, 0f), new Vector3(LaneHalfWidth * 2f, 0.1f, 1f), m.laneWood, true);
            lane.GetComponent<Collider>().sharedMaterial = pm.lane;
            stretch.Add(Stretch(lane, 0f, 1.0f));
            for (int s = -1; s <= 1; s += 2)
            {
                stretch.Add(Stretch(Prim(PrimitiveType.Cube, s < 0 ? "Kickback_L" : "Kickback_R", root,
                    new Vector3(s * (outer + 0.05f), 0.3f, 0f), new Vector3(0.1f, 0.5f, 1f), m.kickback, true), 0f, 2.6f));
                stretch.Add(Stretch(Prim(PrimitiveType.Cube, s < 0 ? "Side_L" : "Side_R", root,
                    new Vector3(s * (outer + 0.1f + sideW * 0.5f), 0.275f, 0f), new Vector3(sideW, 0.55f, 1f), m.hallFloor, true), 0f, 2.6f));
            }

            // ---- bandy (opcja w menu)
            var bumpers = new GameObject("Bumpers").transform;
            bumpers.SetParent(root, false);
            for (int s = -1; s <= 1; s += 2)
            {
                var bmp = Prim(PrimitiveType.Cube, s < 0 ? "Bumper_L" : "Bumper_R", bumpers,
                    new Vector3(s * (LaneHalfWidth + GutterWidth * 0.5f), 0.175f, 0f), new Vector3(GutterWidth, 0.25f, 1f), m.bumper, true);
                bmp.GetComponent<Collider>().sharedMaterial = pm.board;
                stretch.Add(Stretch(bmp, 0.3f, -0.2f));
            }

            // ---- strzałki celownicze (4,6 m za linią) i kropki (2,1 m)
            for (int i = -3; i <= 3; i++)
            {
                float x = i * 0.127f;
                Prim(PrimitiveType.Cube, "Arrow_" + i, root, new Vector3(x, LaneTop + 0.001f, 4.6f - Mathf.Abs(i) * 0.15f), new Vector3(0.03f, 0.002f, 0.12f), m.arrow);
                Prim(PrimitiveType.Cylinder, "Dot_" + i, root, new Vector3(x, LaneTop + 0.001f, 2.1f), new Vector3(0.025f, 0.001f, 0.025f), m.arrow);
            }

            // ---- koniec toru (przesuwany): płyta kręgli, kręgle, dół, zasłona
            var far = new GameObject("FarEnd").transform;
            far.SetParent(root, false);
            far.localPosition = new Vector3(0f, 0f, LaneLength);
            Prim(PrimitiveType.Cube, "PinDeck", far, new Vector3(0f, LaneTop + 0.0008f, 0.35f), new Vector3(LaneHalfWidth * 2f, 0.0015f, 1.3f), m.deck);
            Prim(PrimitiveType.Cube, "PitBottom", far, new Vector3(0f, -0.3f, 1.75f), new Vector3(HallHalfWidth * 2f, 0.1f, 1.5f), m.pit, true)
                .GetComponent<Collider>().sharedMaterial = pm.cushion;
            Prim(PrimitiveType.Cube, "Cushion", far, new Vector3(0f, 0.45f, 2.55f), new Vector3(HallHalfWidth * 2f, 1.5f, 0.1f), m.cushion, true)
                .GetComponent<Collider>().sharedMaterial = pm.cushion;
            Prim(PrimitiveType.Cube, "Masking", far, new Vector3(0f, 2f, 2.7f), new Vector3(HallHalfWidth * 2f, 2f, 0.1f), m.kickback, true);
            Prim(PrimitiveType.Cube, "MaskingTop", far, new Vector3(0f, 1.45f, 1.2f), new Vector3(HallHalfWidth * 2f, 0.5f, 0.1f), m.kickback);
            var aim = new GameObject("AimTarget").transform;
            aim.SetParent(far, false);
            aim.localPosition = new Vector3(0f, LaneTop + BowlingBallRadius, 0f);

            var pins = new List<BowlingPin>();
            int n = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int k = 0; k <= row; k++)
                {
                    n++;
                    float x = (k - row * 0.5f) * 0.3048f;
                    float z = row * 0.2638f;
                    var pin = Prim(PrimitiveType.Capsule, "Pin_" + n, far, new Vector3(x, LaneTop + 0.19f, z), new Vector3(0.12f, 0.19f, 0.12f), m.pin, true);
                    pin.GetComponent<Collider>().sharedMaterial = pm.pin;
                    Prim(PrimitiveType.Cylinder, "Band", pin.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.05f, 0.08f, 1.05f), m.pinBand);
                    var rb = pin.AddComponent<Rigidbody>();
                    rb.mass = 1.5f;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                    rb.solverIterations = 12;
                    rb.linearDamping = 0.05f;
                    rb.angularDamping = 0.15f;
                    pins.Add(pin.AddComponent<BowlingPin>());
                    pin.AddComponent<ImpactSound>().kind = ImpactKind.Pin;
                }
            }

            // ---- stojak z 3 kulami (po prawej od gracza) i stanowisko
            var standPoint = new GameObject("StandPoint").transform;
            standPoint.SetParent(root, false);
            standPoint.localPosition = new Vector3(0.25f, LaneTop, -3f);
            Prim(PrimitiveType.Cylinder, "StandMark", root, new Vector3(0.25f, LaneTop + 0.002f, -3f), new Vector3(0.5f, 0.002f, 0.5f), m.standMark);
            var homes = BallRack(root, "BallReturn", new Vector3(0.95f, LaneTop, -3f), 3, 0.3f, 0.85f, BowlingBallRadius, m);
            var ballsRoot = new GameObject("Balls").transform;
            ballsRoot.SetParent(root, false);
            var balls = new List<Throwable>();
            Material[] colors = { m.bball1, m.bball2, m.bball3 };
            float[] masses = { 5.4f, 6.4f, 7.2f };
            for (int i = 0; i < homes.Count; i++)
            {
                var b = Ball(ballsRoot, "BowlingBall_" + Mathf.RoundToInt(masses[i] * 10f) / 10f + "kg", homes[i], BowlingBallRadius, masses[i], colors[i], pm.bowlingBall, BallKind.Bowling);
                for (int h = 0; h < 3; h++)
                {
                    var hp = Quaternion.Euler(-20f + (h == 2 ? 25f : 0f), (h - 0.5f) * 16f * (h < 2 ? 1f : 0f), 0f) * Vector3.up * 0.49f;
                    Prim(PrimitiveType.Sphere, "Hole_" + h, b.transform, hp, Vector3.one * 0.12f, m.hole);
                }
                b.allowedArea = new Rect(LaneX - HallHalfWidth - 0.4f, -5.4f, HallHalfWidth * 2f + 0.8f, 30f);
                b.returnWhenRestingFor = 2.5f;
                b.returnAfter = 20f;
                b.minY = -1f;
                b.gameObject.AddComponent<ImpactSound>().kind = ImpactKind.BowlingBall;
                balls.Add(b);
            }

            // ---- tablica 10 ramek nad torem
            Vector3 eye = root.TransformPoint(new Vector3(0.25f, LaneTop + 1.6f, -3f));
            Vector3 sbPos = root.TransformPoint(new Vector3(0f, 2.55f, 2.5f));
            var board = Panel("Scoreboard", root, sbPos, Face(sbPos, eye), new Vector2(2000f, 440f), 0.00135f, PanelBg);
            var marks = new Text[BowlingScore.Frames];
            var totals = new Text[BowlingScore.Frames];
            const float cell = 172f;
            float x0 = -1000f + 20f + cell * 0.5f;
            for (int f = 0; f < BowlingScore.Frames; f++)
            {
                float cx = x0 + f * cell;
                var bg = Rect(board, "Frame_" + (f + 1), new Vector2(cx, 205f), new Vector2(cell - 8f, 290f));
                var img = bg.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.07f);
                img.raycastTarget = false;
                Label(board, "FrameNo_" + (f + 1), new Vector2(cx, 85f), new Vector2(cell, 50f), 32, TextAnchor.MiddleCenter, new Color(0.6f, 0.68f, 0.8f), (f + 1).ToString());
                marks[f] = Label(board, "Marks_" + (f + 1), new Vector2(cx, 165f), new Vector2(cell - 10f, 90f), 50, TextAnchor.MiddleCenter, Color.white, "");
                totals[f] = Label(board, "Total_" + (f + 1), new Vector2(cx, 275f), new Vector2(cell - 10f, 90f), 54, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f), "");
            }
            float tx = x0 + BowlingScore.Frames * cell + 30f;
            Label(board, "TotalTitle", new Vector2(tx, 85f), new Vector2(240f, 50f), 32, TextAnchor.MiddleCenter, new Color(0.6f, 0.68f, 0.8f), "SUMA");
            var totalText = Label(board, "TotalValue", new Vector2(tx, 220f), new Vector2(240f, 150f), 96, TextAnchor.MiddleCenter, Color.white, "0");
            var infoText = Label(board, "Info", new Vector2(0f, 395f), new Vector2(1960f, 60f), 34, TextAnchor.MiddleCenter, new Color(0.8f, 0.86f, 0.95f), "");

            var bbPos = root.TransformPoint(new Vector3(-HallHalfWidth + 0.02f, LaneTop + 1.55f, -2.4f));
            Board(root, "Board_Bowling", bbPos, bbPos + Vector3.right, new Vector2(900f, 1000f), 0.0011f, BowlingText);

            var bl = root.gameObject.AddComponent<BowlingLane>();
            bl.farEnd = far;
            bl.stretch = stretch.ToArray();
            bl.pins = pins.ToArray();
            bl.balls = balls.ToArray();
            bl.aimTarget = aim;
            bl.bumpers = bumpers.gameObject;
            bl.laneTop = LaneTop;
            bl.frameMarks = marks;
            bl.frameTotals = totals;
            bl.totalText = totalText;
            bl.infoText = infoText;
            foreach (var st in stretch) st.Apply(LaneLength);

            refs.lane = bl;
            refs.standPoint = standPoint;
            NoShadows(root);
            return refs;
        }

        static LaneStretch Stretch(GameObject go, float z0, float endOffset)
        {
            var s = go.AddComponent<LaneStretch>();
            s.z0 = z0;
            s.endOffset = endOffset;
            return s;
        }

        /// <summary>Stojak: podstawa, słupek i krótkie kołki pod piłkami. Zwraca punkty, na których leżą piłki.</summary>
        static List<Transform> BallRack(Transform parent, string name, Vector3 localPos, int count, float spacing, float height, float radius, Mats m)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;
            float len = (count - 1) * spacing + radius * 2f + 0.1f;
            Prim(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.03f, 0f), new Vector3(0.35f, 0.06f, len), m.rack, true);
            Prim(PrimitiveType.Cube, "Column", root, new Vector3(0f, height * 0.5f - radius * 0.5f, 0f), new Vector3(0.06f, height - radius, 0.06f), m.rack, true);
            Prim(PrimitiveType.Cube, "Shelf", root, new Vector3(0f, height - radius - 0.015f, 0f), new Vector3(0.2f, 0.03f, len), m.rack, true);
            var homes = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                float z = (i - (count - 1) * 0.5f) * spacing;
                Prim(PrimitiveType.Cylinder, "Peg_" + i, root, new Vector3(0f, height - radius + 0.01f, z), new Vector3(0.08f, 0.01f, 0.08f), m.peg);
                var h = new GameObject("Home_" + (i + 1)).transform;
                h.SetParent(root, false);
                h.localPosition = new Vector3(0f, height + 0.005f, z);
                homes.Add(h);
            }
            return homes;
        }

        static Throwable Ball(Transform parent, string name, Transform home, float radius, float mass, Material mat, PhysicsMaterial phys, BallKind kind)
        {
            var go = Prim(PrimitiveType.Sphere, name, parent, parent.InverseTransformPoint(home.position), Vector3.one * radius * 2f, mat, true);
            go.GetComponent<Collider>().sharedMaterial = phys;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = kind == BallKind.Basketball ? 0.05f : 0.02f;
            rb.angularDamping = kind == BallKind.Basketball ? 0.15f : 0.05f;
            rb.maxAngularVelocity = 60f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = true;
            var t = go.AddComponent<Throwable>();
            t.kind = kind;
            t.home = home;
            t.radius = radius;
            return t;
        }

        static void WallBox(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, PhysicsMaterial phys)
        {
            var w = Prim(PrimitiveType.Cube, name, parent, pos, size, mat, true);
            w.GetComponent<Collider>().sharedMaterial = phys;
        }

        /// <summary>Linia na podłodze od a do b (cienki prostopadłościan, bez kolidera).</summary>
        static void FloorLine(Transform parent, Vector3 a, Vector3 b, Material mat)
        {
            Vector3 d = b - a;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-6f) return;
            Prim(PrimitiveType.Cube, "Line", parent, (a + b) * 0.5f + Vector3.up * 0.005f, new Vector3(0.05f, 0.003f, d.magnitude + 0.05f), mat,
                false, Quaternion.LookRotation(d.normalized, Vector3.up));
        }

        /// <summary>Linia w płaszczyźnie pionowej (na tablicy kosza).</summary>
        static void BoxLine(Transform parent, Vector3 a, Vector3 b, Material mat)
        {
            Vector3 d = b - a;
            Vector3 mid = (a + b) * 0.5f;
            bool horizontal = Mathf.Abs(d.x) > Mathf.Abs(d.y);
            Prim(PrimitiveType.Cube, "BoardLine", parent, mid,
                horizontal ? new Vector3(Mathf.Abs(d.x) + 0.05f, 0.05f, 0.004f) : new Vector3(0.05f, Mathf.Abs(d.y) + 0.05f, 0.004f), mat);
        }

        /// <summary>Cienki walec od a do b (siatka kosza).</summary>
        static void Strand(Transform parent, Vector3 a, Vector3 b, float thickness, Material mat)
        {
            Vector3 d = b - a;
            Prim(PrimitiveType.Cylinder, "Net", parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), mat, false,
                Quaternion.FromToRotation(Vector3.up, d.normalized));
        }

        static void Static(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = true;
        }

        static void NoShadows(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------- teksty tablic

        const string ControlsText =
            "<size=46><b>STEROWANIE</b></size>\n" +
            "<color=#9AB>kontrolery Touch (OVRInput)</color>\n\n" +
            "<b>Grip</b> – złap piłkę, puść grip = rzut\n" +
            "     (w menu: tryb „przełącz”)\n" +
            "<b>Lewa gałka</b> – krok po stanowisku\n" +
            "<b>Prawa gałka ← →</b> – obrót o 45°\n\n" +
            "<b>Menu</b> (lewy) – ustawienia rzutu\n" +
            "<b>A</b> – piłki wracają na stojak\n" +
            "<b>B</b> – kosz: wyzwanie 60 s\n" +
            "     kręgle: nowa gra\n" +
            "<b>X</b> – podgląd toru lotu\n" +
            "<b>Y</b> – drugie stanowisko\n\n" +
            "<color=#9AB>Unieś lewą rękę – wyświetlacz pokazuje\nprędkość i kąt ostatniego rzutu.</color>";

        const string BasketballText =
            "<size=46><b>FIZYKA RZUTU</b></size>\n\n" +
            "Piłka dostaje prędkość ręki z chwili\n" +
            "puszczenia gripa. Potem leci po\n" +
            "<b>paraboli</b> – działa tylko grawitacja.\n\n" +
            "Obręcz: 3,05 m, średnica 45,7 cm,\n" +
            "piłka: 24 cm. Wyższy łuk (50–55°)\n" +
            "daje większe „okno” wejścia.\n\n" +
            "Porównaj w menu <b>źródło prędkości</b>:\n" +
            "OVRInput / historia / ostatnia klatka.\n\n" +
            "<color=#FFD54A>Za 3 pkt: rzut z 6,6 m i dalej.</color>";

        const string BowlingText =
            "<size=44><b>KRĘGLE</b></size>\n\n" +
            "Złap kulę ze stojaka (grip), podejdź\n" +
            "lewą gałką i puść ją nisko nad torem.\n\n" +
            "<color=#FF6B6B>Czerwona linia</color> = linia spalonego.\n" +
            "Głowa za linią w chwili puszczenia\n" +
            "= rzut za 0.\n\n" +
            "Strzałki na torze pomagają celować.\n" +
            "Obrót nadgarstka = rotacja kuli.\n\n" +
            "<b>B</b> = nowa gra (10 ramek)";

        // =====================================================================
        // UI: menu ustawień, komunikaty
        // =====================================================================

        class UiRefs
        {
            public GameObject menuPanel;
            public Text[] labels;
            public Text[] values;
            public Text hint;
        }

        static UiRefs BuildUI()
        {
            var ui = new GameObject("UI").transform;
            var refs = new UiRefs();
            int n = ThrowSettings.OptionCount;
            float rowsEnd = 150f + (n - 1) * 74f + 34f;
            float hintY = rowsEnd + 100f;
            float presetsTitleY = hintY + 115f;
            float presetsY = presetsTitleY + 65f;
            var size = new Vector2(1000f, presetsY + 70f);

            var menu = Panel("ThrowMenu", ui, new Vector3(0f, 1.35f, 0.75f), Quaternion.identity, size, 0.0006f, new Color(0.04f, 0.05f, 0.08f, 0.94f));
            AddPanelCollider(menu, size);
            Label(menu, "Title", new Vector2(-60f, 55f), new Vector2(840f, 90f), 52, TextAnchor.MiddleLeft, Color.white, "<b>USTAWIENIA RZUTU</b>");
            var close = Btn(menu, "Close", new Vector2(420f, 55f), new Vector2(120f, 76f), "X", 44, false);
            close.closesMenu = true;

            refs.labels = new Text[n];
            refs.values = new Text[n];
            for (int i = 0; i < n; i++)
            {
                float y = 150f + i * 74f;
                var rowBg = Rect(menu, "Row_" + i, new Vector2(0f, y), new Vector2(970f, 68f));
                var img = rowBg.gameObject.AddComponent<Image>();
                img.color = i % 2 == 0 ? new Color(1f, 1f, 1f, 0.04f) : new Color(1f, 1f, 1f, 0f);
                img.raycastTarget = false;

                refs.labels[i] = Label(menu, "Label_" + i, new Vector2(-250f, y), new Vector2(460f, 68f), 34, TextAnchor.MiddleLeft, Color.white,
                    ThrowSettings.OptionName(i));
                var prev = Btn(menu, "Prev_" + i, new Vector2(65f, y), new Vector2(84f, 60f), "<", 44, false);
                prev.option = i;
                prev.delta = -1;
                refs.values[i] = Label(menu, "Value_" + i, new Vector2(250f, y), new Vector2(270f, 68f), 32, TextAnchor.MiddleCenter, Color.white, "…");
                var next = Btn(menu, "Next_" + i, new Vector2(435f, y), new Vector2(84f, 60f), ">", 44, false);
                next.option = i;
                next.delta = 1;
            }

            var hintBg = Rect(menu, "HintBackground", new Vector2(0f, hintY), new Vector2(970f, 180f));
            var hintImg = hintBg.gameObject.AddComponent<Image>();
            hintImg.color = new Color(0.15f, 0.3f, 0.5f, 0.35f);
            hintImg.raycastTarget = false;
            refs.hint = Label(menu, "Hint", new Vector2(0f, hintY), new Vector2(940f, 170f), 27, TextAnchor.UpperLeft, new Color(0.85f, 0.92f, 1f), "");

            Label(menu, "PresetsTitle", new Vector2(0f, presetsTitleY), new Vector2(960f, 40f), 28, TextAnchor.MiddleCenter, new Color(0.7f, 0.75f, 0.85f), "Presety:");
            for (int p = 0; p < ThrowSettings.PresetNames.Length; p++)
            {
                var b = Btn(menu, "Preset_" + p, new Vector2(-325f + p * 325f, presetsY), new Vector2(310f, 80f), ThrowSettings.PresetNames[p], 32);
                b.preset = p;
            }

            menu.gameObject.SetActive(false);
            refs.menuPanel = menu.gameObject;

            var toast = Panel("Toast", ui, new Vector3(0f, 1.25f, 1.4f), Quaternion.identity, new Vector2(1150f, 230f), 0.0011f, PanelBg);
            var toastText = Txt(toast, "Text", Vector2.zero, Vector2.one, 44, TextAnchor.MiddleCenter, Color.white, "", 16f);
            var group = toast.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var t = toast.gameObject.AddComponent<Toast>();
            t.text = toastText;
            t.group = group;
            return refs;
        }

        // =====================================================================
        // GRACZ
        // =====================================================================

        static void BuildPlayer(Mats m, BasketRefs basket, BowlingRefs bowling, UiRefs ui)
        {
            var player = new GameObject("Player");
            player.AddComponent<ThrowSettings>();
            player.AddComponent<ThrowPhysics>();
            player.AddComponent<Haptics>();

            var fader = player.AddComponent<ScreenFader>();
            fader.overlayMaterial = m.overlay;

            var mover = player.AddComponent<PlayerMover>();
            mover.basketballStand = basket.standPoint;
            mover.bowlingStand = bowling.standPoint;
            mover.basketballArea = new Rect(-4.6f, -3f, 9.2f, 9.6f);
            mover.bowlingArea = new Rect(LaneX - HallHalfWidth + 0.15f, -4.9f, HallHalfWidth * 2f - 0.3f, 5.6f);
            mover.fader = fader;

            var grabber = player.AddComponent<Grabber>();
            grabber.previewLeft = Line(player.transform, "TrajectoryLeft", m.preview, 0.01f);
            grabber.previewRight = Line(player.transform, "TrajectoryRight", m.preview, 0.01f);

            var pointer = player.AddComponent<UiPointer>();
            pointer.line = Line(player.transform, "MenuPointer", m.pointer, 0.004f);
            pointer.dot = Prim(PrimitiveType.Sphere, "PointerDot", player.transform, new Vector3(0f, 1.2f, 1f), Vector3.one * 0.015f, m.pointer).transform;
            pointer.dot.gameObject.SetActive(false);

            var menu = player.AddComponent<ThrowMenu>();
            menu.panel = ui.menuPanel;
            menu.labelTexts = ui.labels;
            menu.valueTexts = ui.values;
            menu.hintText = ui.hint;

            Vector3 sp = basket.standPoint.position;
            var left = Controller(player.transform, HandSide.Left, sp + new Vector3(-0.25f, 1.0f, 0.35f), m.ctrlLeft, m);
            Controller(player.transform, HandSide.Right, sp + new Vector3(0.25f, 1.0f, 0.35f), m.ctrlRight, m);

            var hud = Panel("WristHud", left, left.position + new Vector3(0f, 0.065f, -0.02f), Quaternion.Euler(45f, 0f, 0f),
                new Vector2(820f, 300f), 0.00026f, new Color(0.03f, 0.04f, 0.07f, 0.85f));
            var hudText = Txt(hud, "Text", Vector2.zero, Vector2.one, 30, TextAnchor.MiddleLeft, Color.white, "HUD", 16f);
            hud.gameObject.AddComponent<WristHud>().text = hudText;

            NoShadows(player.transform);
        }

        // =====================================================================
        // RIG
        // =====================================================================

        static OVRCameraRig EnsureRig(StringBuilder sb)
        {
            var rigs = Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rigs.Length > 0)
            {
                sb.AppendLine("• OVRCameraRig: zachowany istniejący (" + rigs[0].name + ")" +
                              (rigs.Length > 1 ? " – UWAGA: w scenie jest " + rigs.Length + " rigów, zostaw jeden!" : ""));
                return rigs[0];
            }

            GameObject prefab = FindRigPrefab(out string path);
            GameObject go;
            if (prefab != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                sb.AppendLine("• OVRCameraRig: dodany z Meta XR Core SDK (" + path + ")");
            }
            else
            {
                go = new GameObject("OVRCameraRig");
                go.AddComponent<OVRCameraRig>();
                sb.AppendLine("• [!] Nie znaleziono prefabu OVRCameraRig – utworzono pusty rig. " +
                              "Lepiej: usuń go i dodaj Building Block „Camera Rig” (Meta › Tools › Building Blocks).");
            }
            if (Object.FindAnyObjectByType<OVRManager>(FindObjectsInactive.Include) == null) go.AddComponent<OVRManager>();
            return go.GetComponent<OVRCameraRig>();
        }

        static GameObject FindRigPrefab(out string path)
        {
            path = RigPrefabPath;
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (p != null && p.GetComponent<OVRCameraRig>() != null) return p;

            GameObject fallback = null;
            foreach (var guid in AssetDatabase.FindAssets("OVRCameraRig t:Prefab", new[] { "Packages/com.meta.xr.sdk.core" }))
            {
                string candidatePath = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(candidatePath);
                if (go == null || go.GetComponent<OVRCameraRig>() == null) continue;
                if (go.name == "OVRCameraRig") { path = candidatePath; return go; }
                if (fallback == null) { fallback = go; path = candidatePath; }
            }
            return fallback;
        }

        static void RemoveStrayCameras(OVRCameraRig rig, StringBuilder sb)
        {
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (rig != null && cam.transform.IsChildOf(rig.transform)) continue;
                sb.AppendLine("• Usunięto kamerę spoza rigu: " + cam.name);
                Object.DestroyImmediate(cam.gameObject);
            }
        }


        // =====================================================================
        // POMOCNICZE: materiały, tablice, kontrolery, prymitywy, UI
        // =====================================================================

        static Material Mat(string name, Color color, float smoothness = 0.2f, bool unlit = false)
        {
            string path = VRThrowingSetup.MaterialsFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Standard")) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (!unlit && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Przezroczysty, nieoświetlony materiał nakładek komfortu (winieta, mrugnięcie). Tworzony jako asset,
        /// żeby wariant shadera z przezroczystością trafił do buildu. Rysowany na końcu (kolejka Overlay), obie strony.
        /// </summary>
        static Material OverlayMat(string name)
        {
            Material mat = Mat(name, Color.black, unlit: true);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)CompareFunction.Always);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Overlay;
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static void Board(Transform parent, string name, Vector3 pos, Vector3 eye, Vector2 sizePx, float pixelSize, string content)
        {
            var panel = Panel(name, parent, pos, Face(pos, eye), sizePx, pixelSize, PanelBg);
            Txt(panel, "Text", Vector2.zero, Vector2.one, 34, TextAnchor.UpperLeft, Color.white, content, 30f);
            PostUnder(parent, pos, sizePx.y * pixelSize, null, 0f);
        }

        static void Sign(Transform parent, string name, Vector3 pos, Vector3 eye, string content)
        {
            var size = new Vector2(820f, 560f);
            const float px = 0.0016f;
            var panel = Panel(name, parent, pos, Face(pos, eye), size, px, PanelBg);
            Txt(panel, "Text", Vector2.zero, Vector2.one, 34, TextAnchor.UpperLeft, Color.white, content, 26f);
            PostUnder(parent, pos, size.y * px, null, 0f);
        }

        /// <summary>Słupek od ziemi do dolnej krawędzi tablicy (bez kolidera).</summary>
        static void PostUnder(Transform parent, Vector3 panelCenter, float panelHeight, Material mat, float minHeight)
        {
            float bottom = panelCenter.y - panelHeight * 0.5f;
            if (bottom < 0.05f) return;
            Vector3 local = parent.InverseTransformPoint(new Vector3(panelCenter.x, bottom * 0.5f, panelCenter.z));
            Prim(PrimitiveType.Cylinder, "Post", parent, local,
                new Vector3(0.06f, bottom * 0.5f, 0.06f), mat != null ? mat : AssetDatabase.LoadAssetAtPath<Material>(VRThrowingSetup.MaterialsFolder + "/Pole.mat"));
        }

        /// <summary>Schematyczny model kontrolera Touch z prymitywów (rękojeść + pierścień w kolorze ręki + końcówka).</summary>
        static Transform Controller(Transform parent, HandSide side, Vector3 editorPos, Material accent, Mats m)
        {
            var root = new GameObject(side == HandSide.Left ? "LeftController" : "RightController");
            root.transform.SetParent(parent, false);
            root.transform.position = editorPos;

            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            float s = side == HandSide.Left ? -1f : 1f;
            Prim(PrimitiveType.Cube, "Handle", model, new Vector3(0f, -0.035f, -0.025f), new Vector3(0.032f, 0.09f, 0.04f), m.ctrlBody, false, Quaternion.Euler(-25f, 0f, 0f));
            Prim(PrimitiveType.Cube, "Head", model, new Vector3(0f, 0.008f, 0f), new Vector3(0.05f, 0.022f, 0.06f), m.ctrlBody);
            Prim(PrimitiveType.Cylinder, "Ring", model, new Vector3(s * 0.006f, 0.022f, 0.012f), new Vector3(0.075f, 0.004f, 0.075f), accent, false, Quaternion.Euler(-35f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, "Stick", model, new Vector3(0f, 0.024f, -0.008f), new Vector3(0.016f, 0.006f, 0.016f), m.ctrlTip);
            Prim(PrimitiveType.Sphere, "Tip", model, new Vector3(0f, 0.005f, 0.035f), Vector3.one * 0.012f, m.ctrlTip);

            var f = root.AddComponent<ControllerFollower>();
            f.side = side;
            f.visual = model.gameObject;
            return root.transform;
        }

        static LineRenderer Line(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, Vector3.forward);
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        // =====================================================================
        // FABRYKA PRYMITYWÓW I UI
        // =====================================================================

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 localScale,
            Material mat, bool keepCollider = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            // Pozycja i obrót w układzie rodzica (rodzice tworzeni przez generator stoją w zerze bez obrotu
            // albo, jak bramki i kontrolery, mają własny układ - wtedy pozycja jest lokalna).
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            var rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            return go;
        }

        /// <summary>Napis leżący na podłodze, czytelny dla kogoś stojącego w środku startu (góra napisu wskazuje kierunek yaw).</summary>
        static void FloorLabel(Transform parent, Vector3 pos, float yaw, string content, float height)
        {
            float px = 0.002f;
            var panel = Panel("FloorLabel_" + content, parent, pos, Quaternion.Euler(90f, yaw, 0f), new Vector2(height * 2.2f / px, height / px), px, Color.clear);
            Object.DestroyImmediate(panel.Find("Background").gameObject);
            Txt(panel, "Text", Vector2.zero, Vector2.one, Mathf.RoundToInt(height / px * 0.8f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.9f), "<b>" + content + "</b>", 0f);
        }

        /// <summary>Obrót panelu tak, by czytelna strona była zwrócona do oczu w punkcie eye.</summary>
        static Quaternion Face(Vector3 position, Vector3 eye)
        {
            Vector3 dir = position - eye;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>World-space Canvas z tłem. Rozmiar w pikselach, pixelSize = metry na piksel.</summary>
        static RectTransform Panel(string name, Transform parent, Vector3 position, Quaternion rotation, Vector2 sizePx,
            float pixelSize, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * pixelSize;
            rt.SetPositionAndRotation(position, rotation);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRt = (RectTransform)bg.transform;
            bgRt.SetParent(rt, false);
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;
            var img = bg.GetComponent<Image>();
            img.color = background;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Kolider-trigger całego panelu - promień wskaźnika zatrzymuje się na nim.</summary>
        static void AddPanelCollider(RectTransform panel, Vector2 sizePx)
        {
            var bc = panel.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(sizePx.x, sizePx.y, 4f);
            bc.center = Vector3.zero;
        }

        static Text Txt(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize,
            TextAnchor alignment, Color color, string content, float margin = 10f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
            SetupText(go.GetComponent<Text>(), fontSize, alignment, color, content);
            return go.GetComponent<Text>();
        }

        /// <summary>Prostokąt o środku (x, y) liczonym od środka górnej krawędzi panelu (y w dół).</summary>
        static RectTransform Rect(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(centerFromTop.x, -centerFromTop.y);
            return rt;
        }

        static Text Label(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size, int fontSize, TextAnchor alignment,
            Color color, string content)
        {
            var rt = Rect(parent, name, centerFromTop, size);
            var t = rt.gameObject.AddComponent<Text>();
            SetupText(t, fontSize, alignment, color, content);
            return t;
        }

        /// <summary>Przycisk: Image + BoxCollider (trigger, wysunięty przed panel) + MenuButton + napis.</summary>
        static MenuButton Btn(RectTransform parent, string name, Vector2 centerFromTop, Vector2 size, string label, int fontSize, bool richText = true)
        {
            var rt = Rect(parent, name, centerFromTop, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = false;
            var bc = rt.gameObject.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = new Vector3(size.x, size.y, 20f);
            bc.center = new Vector3(0f, 0f, -10f); // przed panelem (panel patrzy w +Z, gracz stoi po stronie -Z)
            var b = rt.gameObject.AddComponent<MenuButton>();
            b.background = img;
            img.color = b.normalColor;

            var text = Txt(rt, "Text", Vector2.zero, Vector2.one, fontSize, TextAnchor.MiddleCenter, Color.white, label, 2f);
            text.supportRichText = richText; // "<" i ">" bez rich textu - inaczej parser znaczników mógłby je „zjeść”
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 16;
            text.resizeTextMaxSize = fontSize;
            return b;
        }

        static void SetupText(Text t, int fontSize, TextAnchor alignment, Color color, string content)
        {
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.text = content;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
        }
    }
}
