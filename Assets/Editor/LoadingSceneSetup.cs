using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class LoadingSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    static LoadingSceneSetup()
    {
        EditorApplication.delayCall += ConfigureActiveScene;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Factory/Configure Loading Scene")]
    private static void ReconfigureActiveScene() => ConfigureActiveScene(true);

    private static void ConfigureActiveScene() => ConfigureActiveScene(false);

    private static void ConfigureActiveScene(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath) return;

        LoadingProcessController controller = Object.FindObjectOfType<LoadingProcessController>();
        if (controller == null) return;
        Transform legacyClamp = GameObject.Find("FeedClamp")?.transform;
        if (legacyClamp != null)
        {
            Transform staleClamp = controller.clampMover;
            controller.clampMover = legacyClamp;
            legacyClamp.name = "PickupFeedClamp";
            if (staleClamp != null && staleClamp != legacyClamp
                && staleClamp.childCount == 0 && staleClamp.position.sqrMagnitude < 0.0001f)
                Undo.DestroyObjectImmediate(staleClamp.gameObject);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
        }
        bool alreadyConfigured = controller.workpieceRoot != null && controller.suctionMover != null
            && controller.controlConsole != null
            && controller.suctionHousing != null && controller.suctionVerticalGuide != null
            && controller.suctionRack != null && controller.suctionGear != null
            && controller.pickupClampBeam != null
            && controller.clampMover != null && controller.suctionHome != null
            && controller.materialPickupPoint != null && controller.materialLiftPoint != null
            && controller.transferPoint != null && controller.clampPickupPoint != null
            && controller.connectorPassPoint != null
            && controller.punchEntryPoint != null && controller.punchStartPoint != null
            && controller.punchClampMover != null && controller.punchClampRail != null
            && controller.punchRailFrame != null
            && controller.punchTable != null && controller.punchClampBrackets != null
            && controller.punchClampBrackets.Length == 2
            && controller.punchJawsA != null && controller.punchJawsA.Length == 4
            && controller.punchJawsB != null && controller.punchJawsB.Length == 4
            && controller.pickupClampHomePoint != null && controller.pickupRailStartPoint != null
            && controller.pickupRailEndPoint != null
            && controller.punchClampHomePoint != null && controller.punchClampAcquirePoint != null
            && controller.punchRailStartPoint != null && controller.punchRailEndPoint != null
            && controller.transferToPunchPoint != null;
        // A configured scene may contain hand-adjusted clamp transforms. Only the
        // explicit menu command is allowed to run the model seating routine again.
        if (alreadyConfigured && !force)
        {
            EnsureBendingPieceReferences(scene, controller);
            return;
        }
        Transform modelRoot = FindModelRoot(scene);
        if (modelRoot == null)
        {
            Debug.LogError("LoadingSceneSetup: 未找到总装配体。", controller);
            return;
        }

        Transform table = FindNode(modelRoot, 1393);
        Transform console = FindNode(modelRoot, 646);
        Transform suctionAssembly = FindNode(modelRoot, 1180);
        Transform suctionBox = FindNode(modelRoot, 1126);
        Transform suctionRail = FindNode(modelRoot, 1090);
        Transform suctionRack = FindNode(modelRoot, 1120);
        Transform suctionGear = FindNode(modelRoot, 1138);
        Transform suctionVerticalGuide = FindNode(modelRoot, 1207);
        Transform clampAssembly = FindNode(modelRoot, 1001);
        Transform clampRail = FindNode(modelRoot, 1087);
        Transform clampBeam = FindNode(modelRoot, 1017);
        Transform clampSliderA = FindNode(modelRoot, 1007);
        Transform clampSliderB = FindNode(modelRoot, 1010);
        Transform jawA = FindNode(modelRoot, 1070);
        Transform jawB = FindNode(modelRoot, 1073);
        Transform support = FindNode(modelRoot, 945);
        Transform connector = FindNode(modelRoot, 1402);
        Transform punch = FindNode(modelRoot, 1405);
        Transform punchClampA = FindNode(modelRoot, 1417);
        Transform punchClampB = FindNode(modelRoot, 1515);
        Transform punchBracketA = FindNode(modelRoot, 1513);
        Transform punchBracketB = FindNode(modelRoot, 1499);
        Transform punchTable = FindNode(modelRoot, 1414);
        Transform punchRail = FindNode(modelRoot, 892);
        Transform punchRailFrame = FindNode(modelRoot, 889);
        Transform punchJawA1 = punchClampA != null ? FindNode(punchClampA, 1460) : null;
        Transform punchJawB1 = punchClampA != null ? FindNode(punchClampA, 1463) : null;
        Transform punchJawA1Rear = punchClampA != null ? FindNode(punchClampA, 1467) : null;
        Transform punchJawB1Rear = punchClampA != null ? FindNode(punchClampA, 1466) : null;
        Transform punchJawA2 = punchClampB != null ? FindNode(punchClampB, 1460) : null;
        Transform punchJawB2 = punchClampB != null ? FindNode(punchClampB, 1463) : null;
        Transform punchJawA2Rear = punchClampB != null ? FindNode(punchClampB, 1467) : null;
        Transform punchJawB2Rear = punchClampB != null ? FindNode(punchClampB, 1466) : null;
        if (table == null || console == null || suctionAssembly == null || suctionBox == null
            || suctionRail == null || suctionRack == null || suctionGear == null
            || suctionVerticalGuide == null
            || clampAssembly == null || clampRail == null || clampBeam == null
            || clampSliderA == null || clampSliderB == null
            || jawA == null || jawB == null
            || support == null || connector == null || punch == null
            || punchClampA == null || punchClampB == null || punchRail == null
            || punchBracketA == null || punchBracketB == null || punchTable == null
            || punchRailFrame == null
            || punchJawA1 == null || punchJawB1 == null
            || punchJawA1Rear == null || punchJawB1Rear == null
            || punchJawA2 == null || punchJawB2 == null
            || punchJawA2Rear == null || punchJawB2Rear == null)
        {
            Debug.LogError("LoadingSceneSetup: 原料台、吸盘、夹爪或冲床模型节点缺失；未生成错误的目标点。", controller);
            return;
        }

        Renderer punchTableRenderer = punchTable.GetComponent<Renderer>();
        bool punchAlignedA = LoadingProcessController.SeatPunchAssembly(
            punchClampA, punchBracketA, punchRailFrame, punchRail,
            punchTableRenderer, punchJawA1, punchJawB1,
            punchJawA1Rear, punchJawB1Rear,
            controller.punchTableGrooveInset, controller.punchRailOutsideClearance);
        bool punchAlignedB = LoadingProcessController.SeatPunchAssembly(
            punchClampB, punchBracketB, punchRailFrame, punchRail,
            punchTableRenderer, punchJawA2, punchJawB2,
            punchJawA2Rear, punchJawB2Rear,
            controller.punchTableGrooveInset, controller.punchRailOutsideClearance);
        Bounds tableBounds = BoundsOf(table);
        Bounds suctionBounds = BoundsOf(suctionAssembly);
        Bounds supportBounds = BoundsOf(support);
        Bounds jawBounds = BoundsOf(jawA);
        jawBounds.Encapsulate(BoundsOf(jawB));
        Bounds punchBounds = BoundsOf(punch);
        Bounds connectorBounds = BoundsOf(connector);
        Bounds punchRailBounds = BoundsOf(punchRail);
        Bounds punchClampBounds = BoundsOf(punchClampA);
        punchClampBounds.Encapsulate(BoundsOf(punchClampB));
        Vector3 feedDirection = punchBounds.center - tableBounds.center;
        feedDirection.y = 0f;
        if (feedDirection.sqrMagnitude < 0.01f) feedDirection = Vector3.right;
        feedDirection.Normalize();

        Vector3 clampContact = jawBounds.center;
        Vector3 pickupDirection, pickupStart, pickupEnd;
        if (!TryPickupRailTravel(clampRail, clampSliderA, clampSliderB,
            clampContact, punchBounds.center, out pickupDirection,
            out pickupStart, out pickupEnd))
        {
            Debug.LogError("LoadingSceneSetup: 无法从 r1087 导轨和夹爪架滑块计算安全轨迹。", controller);
            return;
        }
        if (alreadyConfigured && !punchAlignedA && !punchAlignedB
            && Vector3.Distance(controller.pickupRailStartPoint.position, pickupStart) < 0.005f
            && Vector3.Distance(controller.pickupRailEndPoint.position, pickupEnd) < 0.005f
            && Vector3.Distance(controller.clampMover.position, clampContact) < 0.005f)
        {
            Vector3 relative = controller.transferPoint.position - clampContact;
            relative.y = 0f;
            float along = Vector3.Dot(relative, pickupDirection);
            if (along > 0.05f
                && (relative - pickupDirection * along).magnitude < 0.04f)
                return;
        }

        bool wasDirty = scene.isDirty;
        bool hadSuctionMover = controller.suctionMover != null;
        bool hadClampMover = controller.clampMover != null;
        bool hadPunchMover = controller.punchClampMover != null;
        Transform line = Child(null, "ProductionLine");
        Transform rawArea = Child(line, "RawMaterialArea");
        Transform suctionSystem = Child(line, "SuctionSystem");
        Transform feedSystem = Child(line, "FeedSystem");
        Transform points = Child(line, "LoadingPoints");
        controller.workpieceRoot = Child(rawArea, "WorkpieceRoot");
        controller.suctionMover = Child(suctionSystem, "SuctionMover");
        Transform oldClamp = feedSystem.Find("FeedClamp");
        if (oldClamp != null) oldClamp.name = "PickupFeedClamp";
        controller.clampMover = hadClampMover ? controller.clampMover
            : Child(feedSystem, "PickupFeedClamp");
        controller.punchClampMover = Child(feedSystem, "PunchFeedClamp");

        Vector3 suctionContact = new Vector3(suctionBounds.center.x, suctionBounds.min.y,
                                             suctionBounds.center.z);
        Vector3 punchContact = punchClampBounds.center;
        if (!hadSuctionMover) controller.suctionMover.position = suctionContact;
        if (!hadClampMover || controller.clampMover.position.sqrMagnitude < 0.0001f)
            controller.clampMover.position = clampContact;
        controller.punchClampMover.position = punchContact;

        float sheetThickness = controller.rawSheetWorldWidth * 0.25f / 80f;
        Vector3 pickup = new Vector3(tableBounds.center.x,
            tableBounds.max.y + sheetThickness + 0.01f, tableBounds.center.z);
        Vector3 lift = pickup + Vector3.up * 0.55f;
        Vector3 boardAtTransfer = clampContact + pickupDirection *
            (controller.rawSheetWorldWidth * 0.7f);
        boardAtTransfer.y = supportBounds.max.y + 0.002f;
        Vector3 transfer = boardAtTransfer + Vector3.up * (sheetThickness + 0.01f);
        Vector3 clampPickup = boardAtTransfer - pickupDirection *
            (controller.rawSheetWorldWidth * 0.5f);
        clampPickup.y = clampContact.y;
        Vector3 boardOffset = controller.transferPoint != null
            ? controller.transferPoint.position - clampContact : Vector3.zero;
        boardOffset.y = 0f;
        float alongPickupRail = Vector3.Dot(boardOffset, pickupDirection);
        bool movedBoardToClampLeft = controller.transferPoint != null
            && (alongPickupRail < 0.05f
                || (boardOffset - pickupDirection * alongPickupRail).magnitude > 0.04f);
        if (movedBoardToClampLeft)
        {
            controller.transferPoint.position = transfer;
            if (controller.clampPickupPoint != null)
                controller.clampPickupPoint.position = clampPickup;
        }
        Vector3 suctionDirection;
        if (!TryRailDirection(suctionRail, clampContact - tableBounds.center,
            out suctionDirection))
        {
            Debug.LogError("LoadingSceneSetup: 无法读取 r1090 吸盘导轨的真实方向。", controller);
            return;
        }
        pickup = ProjectToRail(suctionContact, pickup, suctionDirection);
        transfer = ProjectToRail(suctionContact, transfer, suctionDirection);
        lift = new Vector3(pickup.x, lift.y, pickup.z);
        if (controller.materialPickupPoint != null)
            controller.materialPickupPoint.position = ProjectToRail(suctionContact,
                controller.materialPickupPoint.position, suctionDirection);
        if (controller.transferPoint != null)
            controller.transferPoint.position = ProjectToRail(suctionContact,
                controller.transferPoint.position, suctionDirection);
        if (controller.materialLiftPoint != null)
        {
            Vector3 alignedPickup = controller.materialPickupPoint != null
                ? controller.materialPickupPoint.position : pickup;
            controller.materialLiftPoint.position = new Vector3(alignedPickup.x,
                controller.materialLiftPoint.position.y, alignedPickup.z);
        }

        float pressHalfExtent = Mathf.Abs(feedDirection.x) * punchBounds.extents.x
            + Mathf.Abs(feedDirection.z) * punchBounds.extents.z;
        Vector3 entry = punchBounds.center - feedDirection *
            (pressHalfExtent + controller.rawSheetWorldWidth * 0.5f);
        entry.y = boardAtTransfer.y;
        Vector3 start = punchBounds.center;
        start.y = boardAtTransfer.y;
        Vector3 connectorPass = connectorBounds.center;
        connectorPass.y = boardAtTransfer.y;

        // 端点沿真实模型导轨的包围盒投影，留出夹爪滑块的安全余量。
        Vector3 pickupPosition = controller.materialPickupPoint != null
            ? controller.materialPickupPoint.position : pickup;
        Vector3 actualBoardAtTransfer = new Vector3(pickupPosition.x,
            tableBounds.max.y + 0.002f, pickupPosition.z)
            + (controller.transferPoint != null ? controller.transferPoint.position : transfer)
            - (controller.materialPickupPoint != null ? controller.materialPickupPoint.position : pickup);
        Vector3 sheetAtHandoff = actualBoardAtTransfer + pickupEnd
            - (controller.clampPickupPoint != null ? controller.clampPickupPoint.position : clampPickup);
        Vector3 punchEnd = RailEndNearPunch(punchRailBounds, punchContact,
            feedDirection, 0.08f);
        Vector3 punchStart = RailEndNearPunch(punchRailBounds, punchContact,
            -feedDirection, 0.08f);
        Vector3 punchAxis = (punchEnd - punchStart).normalized;
        Vector3 punchTarget = sheetAtHandoff
            + feedDirection * (controller.rawSheetWorldWidth * 0.5f);
        float acquireTravel = Mathf.Clamp(Vector3.Dot(punchTarget - punchStart, punchAxis),
            0f, Vector3.Distance(punchStart, punchEnd));
        Vector3 punchAcquire = punchStart + punchAxis * acquireTravel;

        controller.suctionHome = Point(points, "SuctionHome", suctionContact);
        controller.materialPickupPoint = Point(points, "MaterialPickupPoint", pickup);
        controller.materialLiftPoint = Point(points, "MaterialLiftPoint", lift);
        controller.transferPoint = Point(points, "TransferPoint", transfer);
        controller.clampPickupPoint = Point(points, "ClampPickupPoint", clampPickup);
        controller.connectorPassPoint = Point(points, "ConnectorPassPoint", connectorPass);
        controller.punchEntryPoint = Point(points, "PunchEntryPoint", entry);
        controller.punchStartPoint = Point(points, "PunchStartPoint", start);
        controller.pickupClampHomePoint = Point(points, "PickupFeedClampHomePoint", clampContact);
        controller.pickupRailStartPoint = Point(points, "PickupRailStartPoint", pickupStart);
        controller.pickupRailEndPoint = Point(points, "PickupRailEndPoint", pickupEnd);
        controller.pickupRailStartPoint.position = pickupStart;
        controller.pickupRailEndPoint.position = pickupEnd;
        controller.punchClampHomePoint = Point(points, "PunchClampHomePoint", punchContact);
        controller.punchClampAcquirePoint = Point(points, "PunchClampAcquirePoint", punchAcquire);
        controller.punchRailStartPoint = Point(points, "PunchRailStartPoint", punchStart);
        controller.punchRailEndPoint = Point(points, "PunchRailEndPoint", punchEnd);
        controller.punchClampHomePoint.position = punchContact;
        controller.punchClampAcquirePoint.position = punchContact;
        controller.punchRailStartPoint.position = punchStart;
        controller.punchRailEndPoint.position = punchEnd;
        controller.transferToPunchPoint = Point(points, "TransferToPunchPoint", sheetAtHandoff);
        controller.transferToPunchPoint.position = sheetAtHandoff;
        controller.punchClampAcquirePoint.position = punchAcquire;

        controller.rawSheetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Moudles/加工零件.fbx");
        controller.finishedPartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Moudles/加工零件 冲压完毕.fbx");
        controller.bendingPiecePrefabs = new GameObject[5];
        for (int i = 0; i < controller.bendingPiecePrefabs.Length; i++)
            controller.bendingPiecePrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Moudles/加工零件 冲压完毕" + (i + 1) + ".fbx");
        controller.materialTable = table.GetComponent<Renderer>();
        controller.controlConsole = console.GetComponent<Renderer>();
        controller.suctionVisuals = new[] { suctionAssembly, suctionBox };
        controller.suctionHousing = suctionBox;
        controller.suctionVerticalGuide = suctionVerticalGuide;
        controller.suctionRack = suctionRack;
        controller.suctionGear = suctionGear;
        controller.clampVisuals = new[] { clampAssembly };
        controller.pickupClampBeam = clampBeam;
        controller.punchClampVisuals = new[] { punchClampA, punchClampB };
        controller.punchClampBrackets = new[] { punchBracketA, punchBracketB };
        controller.punchTable = punchTable.GetComponent<Renderer>();
        controller.punchJawsA = new[] { punchJawA1, punchJawA1Rear, punchJawA2, punchJawA2Rear };
        controller.punchJawsB = new[] { punchJawB1, punchJawB1Rear, punchJawB2, punchJawB2Rear };
        controller.clampJawA = jawA;
        controller.clampJawB = jawB;
        controller.suctionRail = suctionRail;
        controller.clampRail = clampRail;
        controller.punchClampRail = punchRail;
        controller.punchRailFrame = punchRailFrame;
        controller.connector = connector;
        controller.punchMachine = punch;

        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty) EditorSceneManager.SaveScene(scene);
        Debug.Log("LoadingSceneSetup: 已绑定两套送料夹爪及各自导轨，创建双级送料交接点。请在场景中核对并校准点位。", controller);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode) => ConfigureActiveScene();

    private static void EnsureBendingPieceReferences(Scene scene,
        LoadingProcessController controller)
    {
        GameObject[] current = controller.bendingPiecePrefabs;
        if (current != null && current.Length == 5
            && System.Array.TrueForAll(current, prefab => prefab != null)) return;

        bool wasDirty = scene.isDirty;
        GameObject[] pieces = new GameObject[5];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = current != null && i < current.Length ? current[i] : null;
            if (pieces[i] == null)
                pieces[i] = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Moudles/加工零件 冲压完毕" + (i + 1) + ".fbx");
            if (pieces[i] == null)
            {
                Debug.LogError("LoadingSceneSetup: 缺少折弯分件资源 "
                    + (i + 1), controller);
                return;
            }
        }
        controller.bendingPiecePrefabs = pieces;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!wasDirty && !EditorApplication.isPlayingOrWillChangePlaymode)
            EditorSceneManager.SaveScene(scene);
        Debug.Log("LoadingSceneSetup: 已补齐五个折弯分件引用，保留夹爪和点位。", controller);
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (scene.path == ScenePath)
            {
                LoadingProcessController controller =
                    Object.FindObjectOfType<LoadingProcessController>();
                if (controller != null)
                    EnsureBendingPieceReferences(scene, controller);
            }
        }
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += ConfigureActiveScene;
    }

    private static Transform FindModelRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentsInChildren<Transform>(true).Length > 500) return root.transform;
        return null;
    }

    private static Transform FindNode(Transform root, int sourceId)
    {
        string marker = "r" + sourceId;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string name = child.name;
            int index = name.IndexOf(marker, System.StringComparison.Ordinal);
            if (index >= 0 && (index == 0 || name[index - 1] == '_')
                && (index + marker.Length == name.Length || name[index + marker.Length] == '_'))
                return child;
        }
        return null;
    }

    private static Bounds BoundsOf(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static bool TryPickupRailTravel(Transform rail, Transform sliderA,
        Transform sliderB, Vector3 home, Vector3 punchCenter,
        out Vector3 direction, out Vector3 start, out Vector3 end)
    {
        direction = Vector3.zero;
        start = home;
        end = home;
        MeshFilter filter = rail.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;
        Bounds local = filter.sharedMesh.bounds;
        Vector3 localAxis = Vector3.right;
        float halfLength = local.extents.x;
        if (local.extents.y > halfLength)
        {
            localAxis = Vector3.up;
            halfLength = local.extents.y;
        }
        if (local.extents.z > halfLength)
        {
            localAxis = Vector3.forward;
            halfLength = local.extents.z;
        }
        Vector3 near = filter.transform.TransformPoint(local.center - localAxis * halfLength);
        Vector3 far = filter.transform.TransformPoint(local.center + localAxis * halfLength);
        Vector3 toPunch = punchCenter - home;
        toPunch.y = 0f;
        if (Vector3.Dot(far - near, toPunch) < 0f)
        {
            Vector3 swap = near;
            near = far;
            far = swap;
        }
        direction = far - near;
        direction.y = 0f;
        if (direction.magnitude < 0.5f) return false;
        direction.Normalize();
        Bounds sliders = BoundsOf(sliderA);
        sliders.Encapsulate(BoundsOf(sliderB));
        float radius = Mathf.Abs(direction.x) * sliders.extents.x
            + Mathf.Abs(direction.z) * sliders.extents.z;
        float sliderMin = Vector3.Dot(sliders.center, direction) - radius;
        float sliderMax = Vector3.Dot(sliders.center, direction) + radius;
        float startTravel = Vector3.Dot(near, direction) + 0.05f - sliderMin;
        float endTravel = Vector3.Dot(far, direction) - 0.05f - sliderMax;
        if (endTravel < 0.2f) return false;
        start = home + direction * startTravel;
        end = home + direction * endTravel;
        return true;
    }

    private static bool TryRailDirection(Transform rail, Vector3 toward,
        out Vector3 direction)
    {
        direction = Vector3.zero;
        MeshFilter filter = rail.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;
        Bounds bounds = filter.sharedMesh.bounds;
        Vector3 localAxis = Vector3.right;
        float halfLength = bounds.extents.x;
        if (bounds.extents.y > halfLength)
        {
            localAxis = Vector3.up;
            halfLength = bounds.extents.y;
        }
        if (bounds.extents.z > halfLength)
        {
            localAxis = Vector3.forward;
            halfLength = bounds.extents.z;
        }
        direction = filter.transform.TransformVector(localAxis * halfLength);
        direction.y = 0f;
        if (direction.magnitude < 0.5f) return false;
        direction.Normalize();
        toward.y = 0f;
        if (Vector3.Dot(direction, toward) < 0f) direction = -direction;
        return true;
    }

    private static Vector3 ProjectToRail(Vector3 home, Vector3 point, Vector3 axis)
    {
        Vector3 offset = point - home;
        offset.y = 0f;
        Vector3 aligned = home + axis * Vector3.Dot(offset, axis);
        return new Vector3(aligned.x, point.y, aligned.z);
    }

    private static Vector3 RailEndNearPunch(Bounds rail, Vector3 home,
        Vector3 direction, float inset)
    {
        float radius = Mathf.Abs(direction.x) * rail.extents.x
            + Mathf.Abs(direction.z) * rail.extents.z;
        float farProjection = Vector3.Dot(rail.center, direction) + radius - inset;
        float travel = Mathf.Max(0.01f, farProjection - Vector3.Dot(home, direction));
        return home + direction * travel;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform existing = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
        if (existing != null) return existing;
        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "Create loading controls");
        if (parent != null) created.transform.SetParent(parent, false);
        return created.transform;
    }

    private static Transform Point(Transform parent, string name, Vector3 position)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;
        Transform point = Child(parent, name);
        point.position = position;
        return point;
    }
}
