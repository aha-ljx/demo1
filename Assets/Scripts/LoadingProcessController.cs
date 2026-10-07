using System.Collections;
using UnityEngine;

/// <summary>原料台 -> 吸盘 -> 拾取机床夹爪 -> 冲床夹爪的单块板材上料演示。</summary>
public sealed class LoadingProcessController : MonoBehaviour
{
    public enum LoadingState
    {
        Idle, MoveSuctionToMaterial, LowerSuction, AttachMaterial,
        RaiseMaterial, MoveToTransferPoint, LowerToTransferPoint,
        ClampMaterial, ReleaseSuction, RaiseSuctionClearance,
        ClearClampFromSuction, RaiseSuction, ReturnSuctionHome,
        PickupFeed, PickupRailEndReached, PlaceSheetOnPunchTable,
        SlideSheetToPunchClamp, PunchClampMoveToAcquire,
        PunchClampAcquire, PickupClampRelease, TransferToPunchClamp,
        PickupClampReturn, PunchFeed, PunchStartPosition,
        PunchClampRelease, SlideSheetIntoPunchGuard, PunchClampReturn,
        Punching, ReplaceWithFinishedPart, RetrieveFinishedPart,
        PunchClampPullBack, PunchClampMoveToRailEnd, FinishedPartDelivered,
        SlideFinishedPartToTableEdge, ArmBaseTravel, ArmRotateToPart,
        ArmSuctionPickup, ArmLiftPart, ArmRestorePosture,
        ArmBaseToBendingProbe, ArmBendToProbe,
        PlacePartOnBendingProbe, ArmClearBendingProbe,
        ReadyForPunching
    }

    [Header("工件与设备")]
    public GameObject rawSheetPrefab;
    public GameObject finishedPartPrefab;
    public Transform workpieceRoot;
    public Renderer materialTable;
    public Renderer controlConsole;
    public Transform suctionMover;
    public Transform clampMover;
    public Transform[] suctionVisuals;
    public Transform suctionHousing;
    public Transform suctionVerticalGuide;
    public Transform suctionRack;
    public Transform suctionGear;
    public Transform[] clampVisuals;
    public Transform pickupClampBeam;
    public Transform clampJawA;
    public Transform clampJawB;
    [Header("冲床内部送料夹爪")]
    public Transform punchClampMover;
    public Transform[] punchClampVisuals;
    public Transform[] punchClampBrackets;
    public Transform[] punchJawsA;
    public Transform[] punchJawsB;
    public Renderer punchTable;

    [Header("静止设备，仅用于检查场景绑定")]
    public Transform suctionRail;
    public Transform clampRail;
    public Transform punchClampRail;
    public Transform punchRailFrame;
    public Transform connector;
    public Transform punchMachine;

    [Header("LoadingPoints：在场景中调整这些空节点")]
    public Transform suctionHome;
    public Transform materialPickupPoint;
    public Transform materialLiftPoint;
    public Transform transferPoint;
    public Transform clampPickupPoint;
    public Transform connectorPassPoint;
    public Transform punchEntryPoint;
    public Transform punchStartPoint;
    public Transform pickupClampHomePoint;
    public Transform pickupRailStartPoint;
    public Transform pickupRailEndPoint;
    public Transform punchClampHomePoint;
    public Transform punchClampAcquirePoint;
    public Transform punchRailStartPoint;
    public Transform punchRailEndPoint;
    public Transform transferToPunchPoint;

    [Header("运动速度，单位：Unity 世界单位/秒")]
    [Min(0.01f)] public float suctionMoveSpeed = 1.2f;
    [Min(0.01f)] public float suctionVerticalSpeed = 0.7f;
    [Min(0.01f)] public float clampMoveSpeed = 0.8f;
    [Min(0.01f)] public float feedSpeed = 0.55f;
    [Min(0.01f)] public float punchClampMoveSpeed = 0.8f;
    [Min(0.01f)] public float punchFeedSpeed = 0.55f;
    [Min(0.05f)] public float suctionReleaseClearance = 0.08f;
    [Min(0.1f)] public float clampClearanceDistance = 0.95f;
    [Min(0.001f)] public float jawCloseSpeed = 0.08f;
    [Min(0f)] public float jawCloseDistance = 0.035f;
    [Min(0.0001f)] public float positionTolerance = 0.002f;
    [Min(0.1f)] public float rawSheetWorldWidth = 0.8f;
    [ColorUsage(false)] public Color rawSheetColor = new Color(59f / 255f, 66f / 255f, 74f / 255f, 1f);
    [Min(0f)] public float punchTableGrooveInset = 0.01f;
    [Min(0.001f)] public float punchRailOutsideClearance = 0.01f;
    [Min(0.1f)] public float consoleInteractionDistance = 2.5f;
    [Min(0.01f)] public float armBaseMoveSpeed = 0.7f;
    [Min(1f)] public float armRotationSpeed = 45f;
    [Min(0.01f)] public float armPickupSpeed = 0.35f;
    [Min(0.01f)] public float armLiftHeight = 0.15f;
    [Min(0f)] public float armPickupRailInset = 0.15f;

    public LoadingState CurrentState { get; private set; } = LoadingState.Idle;
    public bool SuctionActive { get; private set; }
    public bool PickupClampActive { get; private set; }
    public bool PunchClampActive { get; private set; }
    public bool ClampActive => PickupClampActive; // 兼容已有读取端
    public Transform Workpiece => workpieceRoot;

    private Coroutine sequence;
    private Transform sheet;
    private Transform rawSheetInstance;
    private Quaternion workpieceHomeLocalRotation, rawSheetHomeLocalRotation;
    private Vector3 workpieceHomeLocalScale, rawSheetHomeLocalPosition;
    private Vector3 rawSheetHomeLocalScale;
    private Material rawSheetMaterial;
    private Transform workpieceHomeParent;
    private Vector3[] suctionVisualHome;
    private Vector3[] clampVisualHome;
    private Vector3[] punchVisualHome;
    private Vector3[] punchJawAHome, punchJawBHome;
    private Vector3 jawAHome, jawBHome;
    private Vector3 suctionMoverHome, clampMoverHome;
    private Vector3 suctionGuideHome;
    private Vector3 suctionRailAxis;
    private float suctionMinTravel, suctionMaxTravel, suctionGearRadius;
    private float suctionGearRollSign = 1f;
    private float suctionCarryHeight;
    private Vector3 suctionGearLocalAxis;
    private Quaternion suctionGearHomeRotation;
    private Vector3 punchMoverHome;
    private Transform punchRightGuard;
    private Transform punchRailShell;
    private Transform armLongSlot, armBaseAssembly, armBaseJoint;
    private Transform armSuctionAssembly, bendingProbe;
    private Transform bendingBody, bendingRail;
    private Transform[] armSegments, armMovingVisuals, armSliders;
    private Transform[] armOriginalParents;
    private Transform armRig;
    private Transform armContactAnchor;
    private Vector3[] armVisualHomePositions;
    private Quaternion[] armVisualHomeRotations;
    private Transform armBaseMover;
    private Vector3 armBaseHome;
    private bool initialized;
    private string message = "准备上料";
    private Font uiFont;

    private void Start()
    {
        uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
        if (controlConsole == null) controlConsole = FindControlConsole();
        if (suctionHousing == null && suctionVisuals != null && suctionVisuals.Length > 1)
            suctionHousing = suctionVisuals[1];
        if (suctionVerticalGuide == null)
            suctionVerticalGuide = FindModelNode(1207);
        if (suctionRack == null) suctionRack = FindModelNode(1120);
        if (suctionGear == null) suctionGear = FindModelNode(1138);
        if (pickupClampBeam == null) pickupClampBeam = FindModelNode(1017);
        if (punchTable == null) punchTable = FindModelNode(1414)?.GetComponent<Renderer>();
        if (punchRailFrame == null) punchRailFrame = FindModelNode(889);
        punchRightGuard = FindModelNode(1472);
        punchRailShell = FindModelNode(1490);
        BindTransferArm();
        bendingBody = FindModelNode(268);
        bendingRail = FindModelNode(411);
        if (punchClampBrackets == null || punchClampBrackets.Length != 2)
            punchClampBrackets = new[] { FindModelNode(1513), FindModelNode(1499) };
        if (punchClampVisuals != null && punchClampVisuals.Length == 2
            && (punchJawsA == null || punchJawsA.Length != 4
                || punchJawsB == null || punchJawsB.Length != 4))
            BindPunchJawPairs();
        if (pickupRailStartPoint == null && clampRail != null
            && pickupClampHomePoint != null && pickupRailEndPoint != null)
            pickupRailStartPoint = CreateRuntimeRailStart(clampRail,
                pickupClampHomePoint, pickupRailEndPoint, "PickupRailStartPoint (runtime)");
        if (punchRailStartPoint == null && punchClampRail != null
            && punchClampHomePoint != null && punchRailEndPoint != null)
            punchRailStartPoint = CreateRuntimeRailStart(punchClampRail,
                punchClampHomePoint, punchRailEndPoint, "PunchRailStartPoint (runtime)");
        if (!ValidateSetup()) return;
        SyncPunchClampPointsToPlacedAssemblies();
        // 控制空节点不属于 FBX。旧场景中它可能留在世界原点，模型本身仍在导轨上。
        suctionMover.position = suctionHome.position;
        clampMover.position = pickupClampHomePoint.position;
        punchClampMover.position = punchClampHomePoint.position;
        if (!CalibratePickupRailFromMesh()) return;
        PlaceTransferOnClampLeft();
        if (!CalibrateSuctionRailFromMesh()) return;
        suctionCarryHeight = CalculateSuctionCarryHeight();
        workpieceHomeParent = workpieceRoot.parent;
        suctionMoverHome = suctionMover.position;
        suctionGuideHome = suctionVerticalGuide.position;
        suctionGearHomeRotation = suctionGear.localRotation;
        clampMoverHome = clampMover.position;
        punchMoverHome = punchClampMover.position;
        suctionVisualHome = SavePositions(suctionVisuals);
        clampVisualHome = SavePositions(clampVisuals);
        punchVisualHome = SavePositions(punchClampVisuals);
        armRig = new GameObject("TransferArmRig (runtime)").transform;
        armRig.position = GetBounds(armBaseJoint).center;
        armOriginalParents = new Transform[armSegments.Length + 1];
        for (int i = 0; i < armSegments.Length; i++)
        {
            armOriginalParents[i] = armSegments[i].parent;
            armSegments[i].SetParent(armRig, true);
        }
        armOriginalParents[armSegments.Length] = armSuctionAssembly.parent;
        armSuctionAssembly.SetParent(armRig, true);
        Vector3 cupContact = FindSuctionCupContactPoint();
        armContactAnchor = new GameObject("SuctionContact (runtime)").transform;
        armContactAnchor.SetParent(armSuctionAssembly, false);
        armContactAnchor.position = cupContact;
        armMovingVisuals = new[] { armBaseAssembly, armRig };
        armBaseMover = new GameObject("ArmBaseMover (runtime)").transform;
        armBaseHome = GetBounds(armBaseAssembly).center;
        armBaseMover.position = armBaseHome;
        armVisualHomePositions = SavePositions(armMovingVisuals);
        armVisualHomeRotations = new Quaternion[armMovingVisuals.Length];
        for (int i = 0; i < armMovingVisuals.Length; i++)
            armVisualHomeRotations[i] = armMovingVisuals[i].rotation;
        punchJawAHome = SaveLocalPositions(punchJawsA);
        punchJawBHome = SaveLocalPositions(punchJawsB);
        if (clampJawA != null) jawAHome = clampJawA.localPosition;
        if (clampJawB != null) jawBHome = clampJawB.localPosition;

        if (workpieceRoot.childCount == 0)
        {
            GameObject raw = Instantiate(rawSheetPrefab, workpieceRoot);
            raw.name = "RawSheet";
            OrientSheetFlat(raw.transform);
            Bounds b = GetBounds(raw.transform);
            float width = Mathf.Max(b.size.x, b.size.z);
            if (width <= 0.0001f)
            {
                Debug.LogError("LoadingProcessController: 原料板没有有效的 Renderer bounds。", this);
                Destroy(raw);
                return;
            }
            raw.transform.localScale *= rawSheetWorldWidth / width;
        }
        sheet = workpieceRoot.GetChild(0);
        rawSheetInstance = sheet;
        workpieceHomeLocalRotation = workpieceRoot.localRotation;
        workpieceHomeLocalScale = workpieceRoot.localScale;
        rawSheetHomeLocalPosition = rawSheetInstance.localPosition;
        rawSheetHomeLocalRotation = rawSheetInstance.localRotation;
        rawSheetHomeLocalScale = rawSheetInstance.localScale;
        ApplyWorkpieceColor(sheet);
        initialized = true;
        ResetLoadingProcess();
    }

    private void ApplyWorkpieceColor(Transform target)
    {
        if (rawSheetMaterial == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("LoadingProcessController: Standard shader not found for workpiece.", this);
                return;
            }

            rawSheetMaterial = new Material(shader)
            {
                name = "Workpiece Dark Gray (runtime)",
                color = rawSheetColor
            };
            rawSheetMaterial.SetFloat("_Metallic", 0.15f);
            rawSheetMaterial.SetFloat("_Glossiness", 0.35f);
        }

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0)
            {
                renderer.sharedMaterial = rawSheetMaterial;
                continue;
            }

            for (int i = 0; i < materials.Length; i++) materials[i] = rawSheetMaterial;
            renderer.sharedMaterials = materials;
        }
    }

    private void OnDestroy()
    {
        if (rawSheetMaterial != null) Destroy(rawSheetMaterial);
        if (armBaseMover != null) Destroy(armBaseMover.gameObject);
        if (armRig != null)
        {
            if (armContactAnchor != null) Destroy(armContactAnchor.gameObject);
            for (int i = 0; i < armSegments.Length; i++)
                if (armSegments[i] != null)
                    armSegments[i].SetParent(armOriginalParents[i], true);
            if (armSuctionAssembly != null)
                armSuctionAssembly.SetParent(armOriginalParents[armSegments.Length], true);
            Destroy(armRig.gameObject);
        }
    }

    private void Update()
    {
        if (!initialized) return;
        if (Input.GetKeyDown(KeyCode.R)) ResetLoadingProcess();
        if (!Input.GetKeyDown(KeyCode.E) || !IsNearConsole()) return;
        if (CurrentState == LoadingState.ReadyForPunching) ResetLoadingProcess();
        if (CurrentState == LoadingState.Idle) StartLoadingProcess();
    }

    /// <summary>供 UI Button、控制台交互或其他脚本调用。</summary>
    [ContextMenu("Start Loading Process")]
    public void StartLoadingProcess()
    {
        if (!initialized || sequence != null || CurrentState != LoadingState.Idle) return;
        sequence = StartCoroutine(RunLoadingProcess());
    }

    /// <summary>恢复同一块板材、吸盘、夹爪和全部状态，允许再次测试。</summary>
    [ContextMenu("Reset Loading Process")]
    public void ResetLoadingProcess()
    {
        if (!initialized) return;
        if (sequence != null) StopCoroutine(sequence);
        sequence = null;
        if (sheet != rawSheetInstance)
        {
            sheet.SetParent(null, true);
            Destroy(sheet.gameObject);
            sheet = rawSheetInstance;
            rawSheetInstance.gameObject.SetActive(true);
        }
        workpieceRoot.SetParent(workpieceHomeParent, true);
        workpieceRoot.localRotation = workpieceHomeLocalRotation;
        workpieceRoot.localScale = workpieceHomeLocalScale;
        rawSheetInstance.localPosition = rawSheetHomeLocalPosition;
        rawSheetInstance.localRotation = rawSheetHomeLocalRotation;
        rawSheetInstance.localScale = rawSheetHomeLocalScale;
        suctionMover.position = suctionMoverHome;
        clampMover.position = clampMoverHome;
        punchClampMover.position = punchMoverHome;
        RestorePositions(suctionVisuals, suctionVisualHome);
        suctionVerticalGuide.position = suctionGuideHome;
        suctionGear.localRotation = suctionGearHomeRotation;
        RestorePositions(clampVisuals, clampVisualHome);
        RestorePositions(punchClampVisuals, punchVisualHome);
        armBaseMover.position = armBaseHome;
        RestorePositions(armMovingVisuals, armVisualHomePositions);
        for (int i = 0; i < armMovingVisuals.Length; i++)
            armMovingVisuals[i].rotation = armVisualHomeRotations[i];
        armRig.localScale = Vector3.one;
        RestoreLocalPositions(punchJawsA, punchJawAHome);
        RestoreLocalPositions(punchJawsB, punchJawBHome);
        if (clampJawA != null) clampJawA.localPosition = jawAHome;
        if (clampJawB != null) clampJawB.localPosition = jawBHome;
        SuctionActive = false;
        PickupClampActive = false;
        PunchClampActive = false;
        PlaceSheetOnTable();
        SetState(LoadingState.Idle, "原料板位于原料台，等待开始上料");
    }

    private IEnumerator RunLoadingProcess()
    {
        SetState(LoadingState.MoveSuctionToMaterial, "吸盘沿导轨移动到原料上方");
        Vector3 aboveMaterial = new Vector3(materialPickupPoint.position.x,
            suctionMover.position.y, materialPickupPoint.position.z);
        yield return MoveSuctionHorizontally(aboveMaterial, suctionMoveSpeed);
        if (sequence == null) yield break;

        SetState(LoadingState.LowerSuction, "吸盘下降到板材表面");
        yield return MoveSuctionVertically(materialPickupPoint.position,
            suctionVerticalSpeed);

        SetState(LoadingState.AttachMaterial, "吸盘吸附板材");
        SuctionActive = true;
        ReparentWithoutJump(workpieceRoot, suctionMover);

        SetState(LoadingState.RaiseMaterial, "吸盘提升板材");
        yield return MoveSuctionVertically(new Vector3(materialLiftPoint.position.x,
                suctionCarryHeight, materialLiftPoint.position.z),
            suctionVerticalSpeed);

        SetState(LoadingState.MoveToTransferPoint, "吸盘沿导轨搬运到交接区");
        Vector3 aboveTransfer = new Vector3(transferPoint.position.x,
            suctionCarryHeight, transferPoint.position.z);
        yield return MoveSuctionHorizontally(aboveTransfer, suctionMoveSpeed);
        if (sequence == null) yield break;

        SetState(LoadingState.LowerToTransferPoint, "吸盘将板材降至送料支撑位置");
        yield return MoveSuctionVertically(transferPoint.position,
            suctionVerticalSpeed);

        SetState(LoadingState.ClampMaterial, "夹爪靠近并夹紧板材边缘");
        Vector3 rightEdgePickup = PickupTargetAtSheetRightEdge();
        if (!RailTargetValid(clampMover.position, rightEdgePickup,
            pickupClampHomePoint, pickupRailEndPoint, "拾取机床夹爪")) yield break;
        yield return MoveCarrier(clampMover, clampVisuals, rightEdgePickup,
            clampMoveSpeed);
        yield return CloseJaws();
        PickupClampActive = true;
        ReparentWithoutJump(workpieceRoot, clampMover);

        SetState(LoadingState.ReleaseSuction, "夹爪已接管，吸盘释放板材");
        SuctionActive = false;

        SetState(LoadingState.RaiseSuctionClearance, "吸盘架升到夹爪梁上方");
        Vector3 initialClearance = new Vector3(transferPoint.position.x,
            Mathf.Max(suctionMover.position.y, suctionCarryHeight), transferPoint.position.z);
        yield return MoveSuctionVertically(initialClearance, suctionVerticalSpeed);

        SetState(LoadingState.ClearClampFromSuction, "夹爪带板材退出吸盘架升降范围");
        Vector3 feedAxis = pickupRailEndPoint.position - pickupRailStartPoint.position;
        feedAxis.y = 0f;
        feedAxis.Normalize();
        Bounds suctionEnvelope = GetBounds(suctionVisuals[0]);
        Bounds jawEnvelope = GetBounds(clampJawA);
        jawEnvelope.Encapsulate(GetBounds(clampJawB));
        float suctionFront = Vector3.Dot(suctionEnvelope.center, feedAxis)
            + HorizontalRadius(suctionEnvelope, feedAxis);
        float jawRear = Vector3.Dot(jawEnvelope.center, feedAxis)
            - HorizontalRadius(jawEnvelope, feedAxis);
        float clearanceTravel = Mathf.Max(clampClearanceDistance,
            suctionFront - jawRear + 0.08f);
        Vector3 clampClearTarget = clampMover.position + feedAxis * clearanceTravel;
        if (!RailTargetValid(clampMover.position, clampClearTarget,
            pickupClampHomePoint, pickupRailEndPoint, "拾取机床夹爪")) yield break;
        yield return MoveCarrier(clampMover, clampVisuals, clampClearTarget, feedSpeed);

        SetState(LoadingState.RaiseSuction, "吸盘架保持避让高度");
        Vector3 transferLift = new Vector3(transferPoint.position.x,
            Mathf.Max(suctionMover.position.y, suctionCarryHeight), transferPoint.position.z);
        yield return MoveSuctionVertically(transferLift,
            suctionVerticalSpeed);

        SetState(LoadingState.ReturnSuctionHome, "吸盘返回待机位置");
        Vector3 homeAbove = new Vector3(suctionHome.position.x, transferLift.y,
            suctionHome.position.z);
        yield return MoveSuctionHorizontally(homeAbove,
            suctionMoveSpeed);
        if (sequence == null) yield break;
        yield return MoveSuctionVertically(suctionHome.position,
            suctionVerticalSpeed);

        SetState(LoadingState.PickupFeed, "拾取机床夹爪沿 3.6m 导轨送料");
        if (!RailTargetValid(clampMover.position, pickupRailEndPoint.position,
            pickupClampHomePoint, pickupRailEndPoint, "拾取机床夹爪")) yield break;
        yield return MoveCarrier(clampMover, clampVisuals, pickupRailEndPoint.position, feedSpeed);
        SetState(LoadingState.PickupRailEndReached, "拾取机床夹爪在自身导轨末端停止");
        // 交接标记跟随板材的实际到位位置；只移动标记，不移动工件。
        transferToPunchPoint.position = SheetBottomCenter();

        SetState(LoadingState.PickupClampRelease, "拾取机床夹爪松开板材");
        yield return MoveJawPair(clampJawA, clampJawB, false, jawAHome, jawBHome);
        PickupClampActive = false;
        ReparentWithoutJump(workpieceRoot, workpieceHomeParent);

        SetState(LoadingState.PlaceSheetOnPunchTable, "板材落到冲床左侧桌面");
        Vector3 tablePosition = workpieceRoot.position;
        tablePosition.y += punchTable.bounds.max.y + 0.002f - SheetBottomCenter().y;
        yield return MoveWorkpiece(tablePosition, feedSpeed);

        SetState(LoadingState.SlideSheetToPunchClamp, "板材沿桌面平移到夹爪位置");
        Vector3 sheetTarget = SheetTargetAtPunchJaws();
        yield return MoveWorkpiece(sheetTarget, feedSpeed);
        transferToPunchPoint.position = SheetBottomCenter();

        SetState(LoadingState.PickupClampReturn, "拾取机床夹爪沿原导轨返回待机位");
        yield return MoveCarrier(clampMover, clampVisuals, pickupClampHomePoint.position,
            clampMoveSpeed);

        SetState(LoadingState.PunchClampAcquire, "冲床两套夹爪闭合并接管板材");
        yield return MoveJawPairs(punchJawsA, punchJawsB, true);
        PunchClampActive = true;
        SetState(LoadingState.TransferToPunchClamp, "板材控制权切换到冲床夹爪");
        ReparentWithoutJump(workpieceRoot, punchClampMover);

        SetState(LoadingState.PunchFeed, "两套冲床夹爪沿模型 Y 轴将板材送入右侧保护罩");
        if (punchRightGuard == null)
        {
            Fail("未找到转塔冲床右侧保护罩 r1472");
            yield break;
        }
        Bounds guardBounds = GetBounds(punchRightGuard);
        // CAD/model Y is the horizontal Unity Z direction for this imported FBX.
        // Keep world Y (height) unchanged while both assemblies carry the sheet.
        float feedDistanceZ = guardBounds.center.z - SheetBottomCenter().z;
        if (Mathf.Abs(feedDistanceZ) < rawSheetWorldWidth * 0.5f)
        {
            Fail(string.Format("模型 Y 轴送料距离仅 {0:F3}m；请检查 r1472 与板材位置",
                Mathf.Abs(feedDistanceZ)));
            yield break;
        }
        Vector3 punchDestination = punchClampMover.position
            + Vector3.forward * SafePunchClampTravel(feedDistanceZ, guardBounds);
        float clampTravelZ = punchDestination.z - punchClampMover.position.z;
        punchRailStartPoint.position = punchClampMover.position;
        punchRailEndPoint.position = punchDestination;
        if (Mathf.Abs(clampTravelZ) > positionTolerance
            && !RailTargetValid(punchClampMover.position, punchDestination,
            punchRailStartPoint, punchRailEndPoint, "冲床夹爪")) yield break;
        Vector3 deliveryPoint = SheetBottomCenter() + Vector3.forward * feedDistanceZ;
        punchStartPoint.position = deliveryPoint;
        Debug.Log(string.Format("冲床送料：模型 Y/Unity Z，夹爪 Z {0:F3} -> {1:F3}，板材目标 Z {2:F3}，r1472 Z 范围 {3:F3}..{4:F3}，夹爪安全行程 {5:F3}m",
            punchClampMover.position.z, punchDestination.z,
            deliveryPoint.z, guardBounds.min.z, guardBounds.max.z,
            Mathf.Abs(clampTravelZ)), this);
        if (Mathf.Abs(clampTravelZ) > positionTolerance)
            yield return MoveCarrier(punchClampMover, punchClampVisuals,
                punchDestination, punchFeedSpeed);

        SetState(LoadingState.PunchClampRelease, "冲床夹爪在保护罩前松开板材");
        yield return MoveJawPairs(punchJawsA, punchJawsB, false);
        PunchClampActive = false;
        ReparentWithoutJump(workpieceRoot, workpieceHomeParent);

        float remainingZ = deliveryPoint.z - SheetBottomCenter().z;
        if (Mathf.Abs(remainingZ) > positionTolerance)
        {
            SetState(LoadingState.SlideSheetIntoPunchGuard,
                "板材沿桌面继续移动到保护罩中间，电线支板保持在罩外");
            yield return MoveWorkpiece(workpieceRoot.position
                + Vector3.forward * remainingZ, punchFeedSpeed);
        }
        SetState(LoadingState.PunchStartPosition, "板材到达转塔冲床右侧保护罩内");
        if (PickupClampActive || workpieceRoot.parent != workpieceHomeParent
            || Vector3.Distance(SheetBottomCenter(), punchStartPoint.position) > positionTolerance * 2f)
        {
            Fail("板材未到达 r1472 中间或工件控制权异常");
            yield break;
        }

        SetState(LoadingState.Punching, "板材在保护罩内冲压，等待 2 秒");
        yield return new WaitForSeconds(2f);
        if (!ReplaceSheetWithFinishedPart()) yield break;

        SetState(LoadingState.RetrieveFinishedPart, "成品沿模型 Y 轴移出保护罩，交给冲床夹爪");
        Vector3 retrievalPoint = SheetBottomCenter();
        retrievalPoint.z -= remainingZ;
        yield return MoveWorkpiece(workpieceRoot.position
            + Vector3.forward * (retrievalPoint.z - SheetBottomCenter().z), punchFeedSpeed);
        yield return MoveJawPairs(punchJawsA, punchJawsB, true);
        PunchClampActive = true;
        ReparentWithoutJump(workpieceRoot, punchClampMover);

        SetState(LoadingState.PunchClampPullBack, "两套冲床夹爪沿模型 Y 轴拉回成品");
        if (Mathf.Abs(clampTravelZ) > positionTolerance
            && !RailTargetValid(punchClampMover.position, punchMoverHome,
            punchRailStartPoint, punchRailEndPoint, "冲床夹爪拉回")) yield break;
        if (Mathf.Abs(clampTravelZ) > positionTolerance)
            yield return MoveCarrier(punchClampMover, punchClampVisuals,
                punchMoverHome, punchClampMoveSpeed);

        Vector3 xRailEnd;
        if (!TryGetPunchXRailEnd(out xRailEnd)) yield break;
        SetState(LoadingState.PunchClampMoveToRailEnd, "两套冲床夹爪沿模型 X 轴导轨送出成品");
        yield return MoveCarrier(punchClampMover, punchClampVisuals,
            xRailEnd, punchClampMoveSpeed);
        SetState(LoadingState.FinishedPartDelivered, "成品已到达模型 X 轴导轨尽头");
        yield return MoveJawPairs(punchJawsA, punchJawsB, false);
        PunchClampActive = false;
        ReparentWithoutJump(workpieceRoot, workpieceHomeParent);
        SetState(LoadingState.PunchClampReturn, "成品留在 r1490 尽头，两套冲床夹爪沿 X 轴返回原位");
        yield return MoveCarrier(punchClampMover, punchClampVisuals,
            punchMoverHome, punchClampMoveSpeed);

        Vector3 tableEdgeBottom;
        if (!TryGetFinishedPartTableEdge(out tableEdgeBottom)) yield break;
        SetState(LoadingState.SlideFinishedPartToTableEdge,
            "冲压成品平移到桌子左 r1414 边缘");
        yield return MoveWorkpiece(workpieceRoot.position
            + Vector3.up * (tableEdgeBottom.y - SheetBottomCenter().y), feedSpeed);
        yield return MoveWorkpiece(workpieceRoot.position
            + tableEdgeBottom - SheetBottomCenter(), feedSpeed);

        Vector3 armDestination;
        if (!TryGetArmRailEnd(out armDestination)) yield break;
        SetState(LoadingState.ArmBaseTravel, "滑槽基座 r872 沿 r775 移至桌面侧端点前安全位置");
        yield return MoveCarrier(armBaseMover, armMovingVisuals,
            armDestination, armBaseMoveSpeed);
        Vector3[] pickupPosturePositions = SavePositions(armMovingVisuals);
        Quaternion[] pickupPostureRotations = SaveRotations(armMovingVisuals);
        float pickupPostureScale = armRig.localScale.x;

        Vector3 pivot = GetBounds(armBaseJoint).center;
        Vector3 from = SuctionContactPoint() - pivot;
        Vector3 to = SheetTopCenter() - pivot;
        from.y = to.y = 0f;
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
        {
            Fail("机械臂吸盘或成品与旋转轴重合，无法计算旋转角度");
            yield break;
        }
        SetState(LoadingState.ArmRotateToPart, "机械臂绕基座关节旋转，吸盘朝向成品");
        yield return RotateArmAroundBase(pivot,
            Vector3.SignedAngle(from, to, Vector3.up));

        SetState(LoadingState.ArmSuctionPickup, "机械臂吸盘接触并吸附冲压成品");
        Vector3 productTop = SheetTopCenter();
        float approachHeight = productTop.y
            + Mathf.Max(armLiftHeight, 0.25f) + 0.02f;
        Vector3 contact = SuctionContactPoint();
        yield return MoveArmSuctionTo(new Vector3(contact.x, approachHeight, contact.z));
        yield return MoveArmSuctionTo(new Vector3(productTop.x, approachHeight, productTop.z));
        yield return MoveArmSuctionTo(productTop + Vector3.up * 0.005f);
        ReparentWithoutJump(workpieceRoot, armSuctionAssembly);
        SetState(LoadingState.ArmLiftPart, "机械臂吸盘抬起冲压成品");
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * armLiftHeight);

        SetState(LoadingState.ArmRestorePosture, "机械臂携成品恢复取件前姿态");
        yield return RestoreArmPosture(pickupPosturePositions, pickupPostureRotations,
            pickupPostureScale);

        Vector3 probeRailDestination;
        if (!TryGetArmRailPositionForProbe(out probeRailDestination)) yield break;
        SetState(LoadingState.ArmBaseToBendingProbe,
            "滑槽基座 r872 沿 r775 移到折弯机探头 r43 方位");
        yield return MoveCarrier(armBaseMover, armMovingVisuals,
            probeRailDestination, armBaseMoveSpeed);

        Vector3[] transferPosturePositions = SavePositions(armMovingVisuals);
        Quaternion[] transferPostureRotations = SaveRotations(armMovingVisuals);
        float transferPostureScale = armRig.localScale.x;
        Bounds probeBounds = GetBounds(bendingProbe);
        Vector3 railNear, railFar;
        if (!TryRailMeshEnds(bendingRail, out railNear, out railFar))
        {
            Fail("无法读取折弯机导轨 r411 的外伸方向");
            yield break;
        }
        Vector3 outsideDirection = railFar - railNear;
        outsideDirection.y = 0f;
        if (outsideDirection.magnitude < 0.01f)
        {
            Fail("折弯机导轨 r411 没有有效的水平外伸方向");
            yield break;
        }
        outsideDirection.Normalize();
        if (Vector3.Dot(SheetBottomCenter() - probeBounds.center,
            outsideDirection) < 0f)
            outsideDirection = -outsideDirection;
        Bounds bodyBounds = GetBounds(bendingBody);
        float frontOfMachine = Mathf.Max(
            Vector3.Dot(probeBounds.center, outsideDirection)
                + HorizontalRadius(probeBounds, outsideDirection),
            Vector3.Dot(bodyBounds.center, outsideDirection)
                + HorizontalRadius(bodyBounds, outsideDirection));
        float outsideLimit = frontOfMachine
            + HorizontalRadius(GetBounds(sheet), outsideDirection) + 0.03f;
        float outsideGap = outsideLimit + 0.15f
            - Vector3.Dot(GetBounds(sheet).center, outsideDirection);
        if (outsideGap > 0f)
            yield return MoveArmSuctionTo(SuctionContactPoint()
                + outsideDirection * outsideGap);
        Vector3[] safePosturePositions = SavePositions(armMovingVisuals);
        Quaternion[] safePostureRotations = SaveRotations(armMovingVisuals);
        float safePostureScale = armRig.localScale.x;

        pivot = GetBounds(armBaseJoint).center;
        from = SuctionContactPoint() - pivot;
        to = probeBounds.center - pivot;
        from.y = to.y = 0f;
        if (from.sqrMagnitude < 0.0001f || to.sqrMagnitude < 0.0001f)
        {
            Fail("机械臂无法计算折弯机探头方位");
            yield break;
        }
        SetState(LoadingState.ArmBendToProbe, "机械臂在折弯机外侧朝探头弯曲");
        yield return RotateArmOutsideProbe(pivot, Vector3.up,
            Vector3.SignedAngle(from, to, Vector3.up),
            outsideDirection, outsideLimit);

        Vector3 horizontal = to.normalized;
        Vector3 bendAxis = Vector3.Cross(Vector3.up, horizontal).normalized;
        Vector3 targetCup = SuctionContactPoint()
            + probeBounds.center - SheetBottomCenter();
        float bendAngle = Vector3.SignedAngle(SuctionContactPoint() - pivot,
            targetCup - pivot, bendAxis);
        yield return RotateArmOutsideProbe(pivot, bendAxis,
            Mathf.Clamp(bendAngle, -25f, 25f),
            outsideDirection, outsideLimit);

        probeBounds = GetBounds(bendingProbe);
        Vector3 probeTip = probeBounds.center + outsideDirection
            * HorizontalRadius(probeBounds, outsideDirection);
        Vector3 probeBottom = new Vector3(probeTip.x,
            probeBounds.max.y + 0.005f, probeTip.z);
        if (Vector3.Dot(probeTip, outsideDirection) < outsideLimit - 0.02f)
        {
            SetState(LoadingState.ArmClearBendingProbe,
                "固定探头位于机身内，机械臂携成品退回外侧，避免穿模");
            yield return RestoreArmPosture(safePosturePositions,
                safePostureRotations, safePostureScale);
            SetState(LoadingState.ReadyForPunching,
                "成品保持在机械臂吸盘上；固定探头无法在机身外安全接料");
            sequence = null;
            yield break;
        }
        SetState(LoadingState.PlacePartOnBendingProbe,
            "吸盘在机器外侧将成品放到固定探头 r43 上");
        float safePartHeight = Mathf.Max(SheetBottomCenter().y,
            probeBottom.y + armLiftHeight);
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * (safePartHeight - SheetBottomCenter().y));
        Vector3 slide = probeBottom - SheetBottomCenter();
        slide.y = 0f;
        yield return MoveArmSuctionTo(SuctionContactPoint() + slide);
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * (probeBottom.y - SheetBottomCenter().y));
        if (Vector3.Distance(SheetBottomCenter(), probeBottom) > positionTolerance * 3f)
        {
            Fail("冲压成品未到达折弯机探头 r43 顶部");
            yield break;
        }
        ReparentWithoutJump(workpieceRoot, workpieceHomeParent);
        SetState(LoadingState.ArmClearBendingProbe,
            "吸盘释放成品，机械臂恢复交接前姿态");
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * armLiftHeight);
        float suctionClearance = outsideLimit + 0.1f
            - Vector3.Dot(SuctionContactPoint(), outsideDirection);
        if (suctionClearance > 0f)
            yield return MoveArmSuctionTo(SuctionContactPoint()
                + outsideDirection * suctionClearance);
        yield return RestoreArmPosture(transferPosturePositions, transferPostureRotations,
            transferPostureScale);
        SetState(LoadingState.ReadyForPunching, "成品已交给固定探头；按 R 可重置");
        sequence = null;
    }

    private void BindTransferArm()
    {
        armLongSlot = FindModelNode(775);
        Transform bendingBeam = FindModelNode(37);
        bendingProbe = bendingBeam != null ? FindModelNode(bendingBeam, 43) : null;
        armBaseAssembly = FindModelNode(869);
        armSuctionAssembly = FindModelNode(854);
        armBaseJoint = armBaseAssembly != null ? FindModelNode(armBaseAssembly, 878) : null;
        armSegments = new[] { FindModelNode(839), FindModelNode(842),
            FindModelNode(845), FindModelNode(848), FindModelNode(851) };
        armMovingVisuals = new[] { armBaseAssembly, armSegments[0], armSegments[1],
            armSegments[2], armSegments[3], armSegments[4], armSuctionAssembly };
        armSliders = armBaseAssembly != null
            ? new[] { FindModelNode(armBaseAssembly, 875),
                FindModelNode(armBaseAssembly, 879),
                FindModelNode(armBaseAssembly, 880),
                FindModelNode(armBaseAssembly, 881) }
            : null;
    }

    private bool TryGetArmRailEnd(out Vector3 destination)
    {
        destination = armBaseMover.position;
        Vector3 axis;
        float minimumTravel, maximumTravel;
        if (!TryGetArmRailLimits(out axis, out minimumTravel, out maximumTravel))
            return false;
        Vector3 lowerEnd = destination + axis * minimumTravel;
        Vector3 upperEnd = destination + axis * maximumTravel;
        Vector3 product = SheetTopCenter();
        product.y = lowerEnd.y = upperEnd.y = 0f;
        bool upperNearTable = (upperEnd - product).sqrMagnitude
            < (lowerEnd - product).sqrMagnitude;
        float inset = Mathf.Min(armPickupRailInset,
            (maximumTravel - minimumTravel) * 0.25f);
        float travel = upperNearTable
            ? maximumTravel - inset : minimumTravel + inset;
        if (Mathf.Abs(travel) <= positionTolerance)
        {
            Debug.Log("滑槽基座已位于靠近成品的 r775 导轨尽头。", this);
            return true;
        }
        destination = armBaseMover.position + axis * travel;
        Debug.Log(string.Format("机械臂滑槽送料：r872 沿 r775 移动 {0:F3}m，距桌面侧端点内收 {1:F3}m，终点 {2}",
            travel, inset, destination), this);
        return true;
    }

    private bool TryGetArmRailPositionForProbe(out Vector3 destination)
    {
        destination = armBaseMover.position;
        Vector3 axis;
        float minimumTravel, maximumTravel;
        if (!TryGetArmRailLimits(out axis, out minimumTravel, out maximumTravel))
            return false;
        float projectedTravel = Vector3.Dot(GetBounds(bendingProbe).center
            - SuctionContactPoint(), axis);
        float travel = Mathf.Clamp(projectedTravel, minimumTravel, maximumTravel);
        destination += axis * travel;
        if (Mathf.Abs(travel - projectedTravel) > 0.05f)
            Debug.LogWarning("折弯机探头 r43 的投影超出 r775 行程，基座停在可达端点，机械臂继续伸向探头。", this);
        Debug.Log(string.Format("机械臂对准 r43：沿 r775 移动 {0:F3}m，探头投影行程 {1:F3}m",
            travel, projectedTravel), this);
        return true;
    }

    private bool TryGetArmRailLimits(out Vector3 axis,
        out float minimumTravel, out float maximumTravel)
    {
        axis = Vector3.zero;
        minimumTravel = float.NegativeInfinity;
        maximumTravel = float.PositiveInfinity;
        Vector3 near, far;
        if (!TryRailMeshEnds(armLongSlot, out near, out far))
            return Fail("无法读取长滑槽 r775 的网格长轴");
        axis = far - near;
        axis.y = 0f;
        if (axis.magnitude < 0.5f)
            return Fail("长滑槽 r775 的水平导轨长度异常");
        axis.Normalize();
        Bounds rail = GetBounds(armLongSlot);
        float railCenter = Vector3.Dot(rail.center, axis);
        float railRadius = HorizontalRadius(rail, axis);
        const float clearance = 0.03f;
        foreach (Transform slider in armSliders)
        {
            Bounds bounds = GetBounds(slider);
            float center = Vector3.Dot(bounds.center, axis);
            float radius = HorizontalRadius(bounds, axis);
            minimumTravel = Mathf.Max(minimumTravel,
                railCenter - railRadius + clearance - (center - radius));
            maximumTravel = Mathf.Min(maximumTravel,
                railCenter + railRadius - clearance - (center + radius));
        }
        if (minimumTravel > maximumTravel)
            return Fail("滑槽基座的四个滑块无法同时处于 r775 导轨范围内");
        return true;
    }

    private bool TryGetFinishedPartTableEdge(out Vector3 bottomCenter)
    {
        bottomCenter = SheetBottomCenter();
        Bounds table = punchTable.bounds;
        Bounds part = GetBounds(sheet);
        const float inset = 0.02f;
        float minimumZ = table.min.z + part.extents.z + inset;
        float maximumZ = table.max.z - part.extents.z - inset;
        if (table.size.x < part.size.x + 2f * inset || minimumZ > maximumZ)
            return Fail("成品尺寸超出桌子左 r1414 的可放置范围");
        bottomCenter = new Vector3(table.max.x - part.extents.x - inset,
            table.max.y + 0.002f,
            Mathf.Clamp(part.center.z, minimumZ, maximumZ));
        return true;
    }

    private IEnumerator RotateArmAroundBase(Vector3 pivot, float degrees)
    {
        yield return RotateArmAroundAxis(pivot, Vector3.up, degrees);
    }

    private IEnumerator RotateArmAroundAxis(Vector3 pivot, Vector3 axis,
        float degrees)
    {
        float rotated = 0f;
        while (Mathf.Abs(degrees - rotated) > 0.01f)
        {
            float next = Mathf.MoveTowards(rotated, degrees,
                armRotationSpeed * Time.deltaTime);
            float step = next - rotated;
            armRig.RotateAround(pivot, axis, step);
            rotated = next;
            yield return null;
        }
    }

    private IEnumerator RotateArmOutsideProbe(Vector3 pivot, Vector3 axis,
        float degrees, Vector3 outsideDirection, float outsideLimit)
    {
        float rotated = 0f;
        while (Mathf.Abs(degrees - rotated) > 0.01f)
        {
            float next = Mathf.MoveTowards(rotated, degrees,
                armRotationSpeed * Time.deltaTime);
            float step = next - rotated;
            Quaternion rigRotation = armRig.rotation;
            Vector3 rigPosition = armRig.position;
            armRig.RotateAround(pivot, axis, step);
            if (Vector3.Dot(GetBounds(sheet).center, outsideDirection) < outsideLimit)
            {
                armRig.position = rigPosition;
                armRig.rotation = rigRotation;
                Debug.LogWarning("机械臂已在折弯机外侧停止弯曲，避免成品穿过探头或机身。", this);
                yield break;
            }
            rotated = next;
            yield return null;
        }
    }

    private IEnumerator RestoreArmPosture(Vector3[] positions, Quaternion[] rotations,
        float rigScale)
    {
        bool moving;
        do
        {
            moving = false;
            for (int i = 1; i < armMovingVisuals.Length; i++)
            {
                Transform visual = armMovingVisuals[i];
                visual.position = Vector3.MoveTowards(visual.position, positions[i],
                    armPickupSpeed * Time.deltaTime);
                visual.rotation = Quaternion.RotateTowards(visual.rotation, rotations[i],
                    armRotationSpeed * Time.deltaTime);
                moving |= Vector3.Distance(visual.position, positions[i]) > positionTolerance
                    || Quaternion.Angle(visual.rotation, rotations[i]) > 0.01f;
            }
            float nextScale = Mathf.MoveTowards(armRig.localScale.x, rigScale,
                armPickupSpeed * Time.deltaTime);
            armRig.localScale = Vector3.one * nextScale;
            moving |= Mathf.Abs(nextScale - rigScale) > 0.0001f;
            if (moving) yield return null;
        } while (moving);
        for (int i = 1; i < armMovingVisuals.Length; i++)
        {
            armMovingVisuals[i].position = positions[i];
            armMovingVisuals[i].rotation = rotations[i];
        }
        armRig.localScale = Vector3.one * rigScale;
    }

    private Vector3 SuctionContactPoint()
    {
        return armContactAnchor.position;
    }

    private Vector3 FindSuctionCupContactPoint()
    {
        Bounds cups = GetBounds(FindModelNode(armSuctionAssembly, 860));
        foreach (int id in new[] { 866, 867, 868 })
            cups.Encapsulate(GetBounds(FindModelNode(armSuctionAssembly, id)));
        return new Vector3(cups.center.x, cups.min.y, cups.center.z);
    }

    private Vector3 SheetTopCenter()
    {
        Bounds bounds = GetBounds(sheet);
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    private IEnumerator MoveArmSuctionTo(Vector3 contactDestination)
    {
        Vector3 pivot = armRig.position;
        Vector3 current = SuctionContactPoint() - pivot;
        Vector3 target = contactDestination - pivot;
        if (current.sqrMagnitude < 0.000001f || target.sqrMagnitude < 0.000001f)
        {
            Fail("机械臂吸盘目标与基座关节重合，无法保持模型连接");
            yield break;
        }

        // Imported links are siblings. Drive their shared rig around the fixed
        // base joint so no individual link can pull away from its neighbours.
        Quaternion startRotation = armRig.rotation;
        Quaternion targetRotation = Quaternion.FromToRotation(current, target)
            * startRotation;
        float startScale = armRig.localScale.x;
        float targetScale = startScale * target.magnitude / current.magnitude;
        float duration = Mathf.Max(
            Vector3.Distance(SuctionContactPoint(), contactDestination)
                / Mathf.Max(0.01f, armPickupSpeed),
            Quaternion.Angle(startRotation, targetRotation)
                / Mathf.Max(0.01f, armRotationSpeed), 0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float fraction = elapsed / duration;
            armRig.rotation = Quaternion.Slerp(startRotation, targetRotation, fraction);
            armRig.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale,
                fraction);
            yield return null;
        }
        armRig.rotation = targetRotation;
        armRig.localScale = Vector3.one * targetScale;
    }

    private bool ReplaceSheetWithFinishedPart()
    {
        if (finishedPartPrefab == null) return Fail("缺少加工零件 冲压完毕.fbx 引用");
        Vector3 oldBottom = SheetBottomCenter();
        GameObject finished = Instantiate(finishedPartPrefab, workpieceRoot);
        finished.name = "FinishedPart";
        OrientSheetFlat(finished.transform);
        Bounds bounds = GetBounds(finished.transform);
        float width = Mathf.Max(bounds.size.x, bounds.size.z);
        if (width <= 0.0001f)
        {
            Destroy(finished);
            return Fail("成品模型没有有效的 Renderer bounds");
        }
        finished.transform.localScale *= rawSheetWorldWidth / width;
        Bounds resized = GetBounds(finished.transform);
        finished.transform.position += oldBottom
            - new Vector3(resized.center.x, resized.min.y, resized.center.z);
        rawSheetInstance.gameObject.SetActive(false);
        sheet = finished.transform;
        ApplyWorkpieceColor(sheet);
        SetState(LoadingState.ReplaceWithFinishedPart, "原料已替换为冲压完毕成品");
        return true;
    }

    private bool TryGetPunchXRailEnd(out Vector3 destination)
    {
        destination = punchClampMover.position;
        if (punchRailShell == null)
            return Fail("未找到转塔冲床导轨壳 r1490，无法确定出料终点");
        Bounds shell = GetBounds(punchRailShell);
        if (shell.size.x <= positionTolerance)
            return Fail("转塔冲床导轨壳 r1490 没有有效的 X 轴范围");
        const float clearance = 0.03f;
        float minimumTravel = float.NegativeInfinity;
        float maximumTravel = float.PositiveInfinity;
        foreach (Transform assembly in punchClampVisuals)
        {
            Transform slider = FindModelNode(assembly, 1420);
            if (slider == null) return Fail("冲床夹爪缺少 r1420 滑块，无法计算 X 轴行程");
            Bounds bounds = GetBounds(slider);
            minimumTravel = Mathf.Max(minimumTravel, shell.min.x + clearance - bounds.min.x);
            maximumTravel = Mathf.Min(maximumTravel, shell.max.x - clearance - bounds.max.x);
        }
        if (minimumTravel > maximumTravel)
            return Fail("两套冲床夹爪的滑块无法同时处于 r1490 导轨壳范围内");
        // 场景镜头中向左对应 Unity 世界 X 正方向。
        float travel = maximumTravel;
        if (travel <= positionTolerance)
            return Fail("冲床夹爪无法沿 X 轴正方向移动到 r1490 尽头");
        destination.x += travel;
        Debug.Log(string.Format("冲床成品出料：夹爪沿 Unity 世界 X 正方向移动 {0:F3}m，到达 r1490 尽头 X={1:F3}",
            travel, shell.max.x), this);
        return true;
    }

    private IEnumerator MoveCarrier(Transform carrier, Transform[] visuals,
        Vector3 destination, float speed, Transform[] verticallyFixed = null,
        bool rollSuctionGear = false)
    {
        while (Vector3.Distance(carrier.position, destination) > positionTolerance)
        {
            Vector3 next = Vector3.MoveTowards(carrier.position, destination,
                Mathf.Max(0.01f, speed) * Time.deltaTime);
            Vector3 delta = next - carrier.position;
            carrier.position = next;
            if (visuals != null)
                foreach (Transform visual in visuals)
                    if (visual != null && !visual.IsChildOf(carrier)) visual.position += delta;
            if (verticallyFixed != null)
                foreach (Transform visual in verticallyFixed)
                    if (visual != null) visual.position -= Vector3.up * delta.y;
            if (rollSuctionGear) UpdateSuctionGearRotation();
            yield return null;
        }
        Vector3 remainder = destination - carrier.position;
        carrier.position = destination;
        if (visuals != null)
            foreach (Transform visual in visuals)
                if (visual != null && !visual.IsChildOf(carrier)) visual.position += remainder;
        if (verticallyFixed != null)
            foreach (Transform visual in verticallyFixed)
                if (visual != null) visual.position -= Vector3.up * remainder.y;
        if (rollSuctionGear) UpdateSuctionGearRotation();
    }

    private IEnumerator MoveWorkpiece(Vector3 destination, float speed)
    {
        while (Vector3.Distance(workpieceRoot.position, destination) > positionTolerance)
        {
            workpieceRoot.position = Vector3.MoveTowards(workpieceRoot.position,
                destination, Mathf.Max(0.01f, speed) * Time.deltaTime);
            yield return null;
        }
        workpieceRoot.position = destination;
    }

    private Vector3 SheetTargetAtPunchJaws()
    {
        Vector3 jawCenter = Vector3.zero;
        for (int i = 0; i < punchJawsA.Length; i++)
            jawCenter += (GetBounds(punchJawsA[i]).center
                + GetBounds(punchJawsB[i]).center) * 0.5f;
        jawCenter /= punchJawsA.Length;

        Vector3 towardTable = punchTable.bounds.center - jawCenter;
        towardTable.y = 0f;
        if (towardTable.sqrMagnitude < 0.0001f)
            towardTable = punchTable.bounds.center - SheetBottomCenter();
        towardTable.y = 0f;
        towardTable.Normalize();
        Bounds board = GetBounds(sheet);
        float halfWidth = HorizontalRadius(board, towardTable);
        Vector3 targetCenter = jawCenter + towardTable * (halfWidth - 0.005f);
        Vector3 move = targetCenter - board.center;
        move.y = 0f;
        return workpieceRoot.position + move;
    }

    private IEnumerator MoveSuctionHorizontally(Vector3 destination, float speed)
    {
        Vector3 movement = destination - suctionMover.position;
        movement.y = 0f;
        float along = Vector3.Dot(movement, suctionRailAxis);
        float crossTrack = (movement - suctionRailAxis * along).magnitude;
        float travel = Vector3.Dot(destination - suctionMoverHome, suctionRailAxis);
        if (crossTrack > 0.01f || travel < suctionMinTravel - 0.01f
            || travel > suctionMaxTravel + 0.01f)
        {
            Fail(string.Format("吸盘小车偏离 r1090 导轨：横向 {0:F3}m，行程 {1:F3}m，允许 {2:F3}..{3:F3}m",
                crossTrack, travel, suctionMinTravel, suctionMaxTravel));
            yield break;
        }
        yield return MoveCarrier(suctionMover, suctionVisuals, destination, speed,
            null, true);
    }

    private void UpdateSuctionGearRotation()
    {
        float travel = Vector3.Dot(suctionMover.position - suctionMoverHome, suctionRailAxis);
        float angle = suctionGearRollSign * travel / suctionGearRadius * Mathf.Rad2Deg;
        suctionGear.localRotation = suctionGearHomeRotation
            * Quaternion.AngleAxis(angle, suctionGearLocalAxis);
    }

    private IEnumerator MoveSuctionVertically(Vector3 destination, float speed)
    {
        // 机箱保护壳与竖向导轨随横移小车走，但不随长杆和吸盘架升降。
        yield return MoveCarrier(suctionMover, suctionVisuals, destination, speed,
            new[] { suctionHousing, suctionVerticalGuide });
    }

    private IEnumerator CloseJaws()
    {
        yield return MoveJawPair(clampJawA, clampJawB, true, jawAHome, jawBHome);
    }

    private IEnumerator MoveJawPairs(Transform[] jawsA, Transform[] jawsB, bool close)
    {
        Vector3[] targetsA = new Vector3[jawsA.Length];
        Vector3[] targetsB = new Vector3[jawsB.Length];
        for (int i = 0; i < jawsA.Length; i++)
        {
            Vector3 direction = (jawsB[i].position - jawsA[i].position).normalized;
            targetsA[i] = close ? jawsA[i].position + direction * (jawCloseDistance * 0.5f)
                : jawsA[i].parent.TransformPoint(punchJawAHome[i]);
            targetsB[i] = close ? jawsB[i].position - direction * (jawCloseDistance * 0.5f)
                : jawsB[i].parent.TransformPoint(punchJawBHome[i]);
        }
        bool moving;
        do
        {
            moving = false;
            for (int i = 0; i < jawsA.Length; i++)
            {
                jawsA[i].position = Vector3.MoveTowards(jawsA[i].position,
                    targetsA[i], jawCloseSpeed * Time.deltaTime);
                jawsB[i].position = Vector3.MoveTowards(jawsB[i].position,
                    targetsB[i], jawCloseSpeed * Time.deltaTime);
                moving |= Vector3.Distance(jawsA[i].position, targetsA[i]) > positionTolerance
                    || Vector3.Distance(jawsB[i].position, targetsB[i]) > positionTolerance;
            }
            if (moving) yield return null;
        } while (moving);
        for (int i = 0; i < jawsA.Length; i++)
        {
            jawsA[i].position = targetsA[i];
            jawsB[i].position = targetsB[i];
        }
    }

    private void BindPunchJawPairs()
    {
        Transform first = punchClampVisuals[0];
        Transform second = punchClampVisuals[1];
        punchJawsA = new[] { FindModelNode(first, 1460), FindModelNode(first, 1467),
            FindModelNode(second, 1460), FindModelNode(second, 1467) };
        punchJawsB = new[] { FindModelNode(first, 1463), FindModelNode(first, 1466),
            FindModelNode(second, 1463), FindModelNode(second, 1466) };
    }

    private void SyncPunchClampPointsToPlacedAssemblies()
    {
        // Use the positions saved in the scene; entering Play must not seat the
        // assemblies or shift their individual jaw meshes again.
        Bounds combined = GetBounds(punchClampVisuals[0]);
        combined.Encapsulate(GetBounds(punchClampVisuals[1]));
        Vector3 home = combined.center;
        punchClampHomePoint.position = home;
        punchClampAcquirePoint.position = home;
        punchRailStartPoint.position = home;
        if (punchRightGuard != null)
            punchRailEndPoint.position = new Vector3(home.x, home.y,
                GetBounds(punchRightGuard).center.z);
    }

    private float SafePunchClampTravel(float requestedZ, Bounds guard)
    {
        float direction = Mathf.Sign(requestedZ);
        float distance = Mathf.Abs(requestedZ);
        const float clearance = 0.03f;
        foreach (Transform assembly in punchClampVisuals)
        {
            foreach (int sourceId in new[] { 1427, 1430 })
            {
                Transform support = FindModelNode(assembly, sourceId);
                if (support == null)
                {
                    Debug.LogWarning("LoadingProcessController: 未找到夹爪电线支板 r"
                        + sourceId + "，冲床夹爪停止在原位以避免穿模。", assembly);
                    return 0f;
                }
                Bounds bounds = GetBounds(support);
                // The guard and support can meet only when their X/Y ranges overlap.
                if (bounds.max.x <= guard.min.x || bounds.min.x >= guard.max.x
                    || bounds.max.y <= guard.min.y || bounds.min.y >= guard.max.y)
                    continue;
                float available = direction > 0f
                    ? guard.min.z - clearance - bounds.max.z
                    : bounds.min.z - guard.max.z - clearance;
                if (available < distance)
                    Debug.Log(string.Format("冲床夹爪避让：{0} 限制模型 Y 行程为 {1:F3}m，保护罩前保留 {2:F3}m 间隙。",
                        support.name, Mathf.Max(0f, available), clearance), support);
                distance = Mathf.Min(distance, Mathf.Max(0f, available));
            }
        }
        return direction * distance;
    }

    public static bool SeatPunchAssembly(Transform assembly, Transform bracket,
        Transform frame, Transform rail, Renderer table, Transform jawA,
        Transform fingerA, Transform jawB, Transform fingerB,
        float grooveInset, float outsideClearance)
    {
        Transform slider = FindModelNode(assembly, 1420);
        Vector3 near, far;
        if (slider == null || !TryRailMeshEnds(rail, out near, out far)) return false;
        Vector3 axis = far - near;
        axis.y = 0f;
        if (axis.sqrMagnitude < 0.01f) return false;
        axis.Normalize();
        Vector3 side = table.bounds.center - GetBounds(frame).center;
        side.y = 0f;
        side -= axis * Vector3.Dot(side, axis);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(Vector3.up, axis);
        side.Normalize();

        Vector3 before = assembly.position;
        Vector3 sliderCenter = GetBounds(slider).center;
        Vector3 bracketCenter = GetBounds(bracket).center;
        assembly.position += axis * Vector3.Dot(bracketCenter - sliderCenter, axis);
        float frameOuter = MeshProjectionExtreme(frame, side, true);
        float sliderInner = MeshProjectionExtreme(slider, side, false);
        float bracketSide = Vector3.Dot(bracketCenter, side);
        float sliderSide = Vector3.Dot(GetBounds(slider).center, side);
        assembly.position += side * Mathf.Max(frameOuter + outsideClearance - sliderInner,
            bracketSide - sliderSide);

        float minimumOffset = frameOuter + outsideClearance
            - MeshProjectionExtreme(slider, side, false);
        bool foundA, foundB;
        float offsetA = FindNearestGrooveOffset(table, fingerA, side, grooveInset,
            out foundA);
        float offsetB = FindNearestGrooveOffset(table, fingerB, side, grooveInset,
            out foundB);
        if (!foundA || !foundB)
            Debug.LogWarning("LoadingProcessController: 冲床夹指未找到匹配的桌面窄槽："
                + (!foundA ? fingerA.name : fingerB.name), assembly);
        float assemblyOffset = Mathf.Max(minimumOffset, (offsetA + offsetB) * 0.5f);
        assembly.position += side * assemblyOffset;
        // Each jaw pair has its own slot. Move both halves together so closing
        // still acts across the sheet and the slider stays on the rail exterior.
        Vector3 correctionA = side * (offsetA - assemblyOffset);
        Vector3 correctionB = side * (offsetB - assemblyOffset);
        jawA.position += correctionA;
        fingerA.position += correctionA;
        jawB.position += correctionB;
        fingerB.position += correctionB;
        Bounds fingers = GetBounds(fingerA);
        fingers.Encapsulate(GetBounds(fingerB));
        float grooveFloor = (SampleTableHeight(table, FingerContactCenter(fingerA), grooveInset)
            + SampleTableHeight(table, FingerContactCenter(fingerB), grooveInset)) * 0.5f;
        assembly.position += Vector3.up * (grooveFloor - fingers.min.y);
        return Vector3.Distance(before, assembly.position) > 0.0001f
            || correctionA.sqrMagnitude > 0.00000001f
            || correctionB.sqrMagnitude > 0.00000001f;
    }

    private static float MeshProjectionExtreme(Transform part, Vector3 axis, bool maximum)
    {
        MeshFilter filter = part.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            try
            {
                float extreme = maximum ? float.NegativeInfinity : float.PositiveInfinity;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    float projection = Vector3.Dot(filter.transform.TransformPoint(vertex), axis);
                    extreme = maximum ? Mathf.Max(extreme, projection)
                        : Mathf.Min(extreme, projection);
                }
                if (!float.IsInfinity(extreme)) return extreme;
            }
            catch (UnityException) { }
        }
        Bounds bounds = GetBounds(part);
        float center = Vector3.Dot(bounds.center, axis);
        return center + (maximum ? 1f : -1f) * HorizontalRadius(bounds, axis);
    }

    private static Vector3 FingerContactCenter(Transform finger)
    {
        MeshFilter filter = finger.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return GetBounds(finger).center;
        try
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            float bottom = GetBounds(finger).min.y;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = finger.TransformPoint(vertex);
                if (point.y > bottom + 0.004f) continue;
                sum += point;
                count++;
            }
            return count > 0 ? sum / count : GetBounds(finger).center;
        }
        catch (UnityException) { return GetBounds(finger).center; }
    }

    private static float FindNearestGrooveOffset(Renderer table, Transform finger,
        Vector3 side, float fallbackInset, out bool found)
    {
        found = false;
        const int count = 121;
        const float step = 0.005f;
        float[] heights = new float[count];
        bool[] inside = new bool[count];
        float highest = float.NegativeInfinity;
        Vector3 contact = FingerContactCenter(finger);
        Bounds tableBounds = table.bounds;
        for (int i = 0; i < count; i++)
        {
            float offset = (i - count / 2) * step;
            Vector3 point = contact + side * offset;
            inside[i] = tableBounds.Contains(new Vector3(point.x,
                tableBounds.center.y, point.z));
            if (!inside[i]) continue;
            heights[i] = SampleTableHeight(table, point, fallbackInset);
            highest = Mathf.Max(highest, heights[i]);
        }
        float best = 0f;
        float bestDistance = float.PositiveInfinity;
        for (int start = 0; start < count;)
        {
            if (!inside[start] || heights[start] > highest - 0.004f)
            {
                start++;
                continue;
            }
            int end = start;
            while (end + 1 < count && inside[end + 1]
                && heights[end + 1] <= highest - 0.004f) end++;
            float width = (end - start + 1) * step;
            if (width >= 0.01f && width <= 0.12f)
            {
                float center = ((start + end) * 0.5f - count / 2) * step;
                if (Mathf.Abs(center) < bestDistance)
                {
                    best = center;
                    bestDistance = Mathf.Abs(center);
                    found = true;
                }
            }
            start = end + 1;
        }
        return best;
    }

    private IEnumerator MoveJawPair(Transform jawA, Transform jawB, bool close,
        Vector3 homeA, Vector3 homeB)
    {
        if (jawA == null || jawB == null) yield break;
        Vector3 direction = jawB.position - jawA.position;
        if (direction.sqrMagnitude < 0.000001f) yield break;
        direction.Normalize();
        Vector3 targetA = close ? jawA.position + direction * (jawCloseDistance * 0.5f)
            : jawA.parent.TransformPoint(homeA);
        Vector3 targetB = close ? jawB.position - direction * (jawCloseDistance * 0.5f)
            : jawB.parent.TransformPoint(homeB);
        while (Vector3.Distance(jawA.position, targetA) > positionTolerance
            || Vector3.Distance(jawB.position, targetB) > positionTolerance)
        {
            jawA.position = Vector3.MoveTowards(jawA.position, targetA,
                jawCloseSpeed * Time.deltaTime);
            jawB.position = Vector3.MoveTowards(jawB.position, targetB,
                jawCloseSpeed * Time.deltaTime);
            yield return null;
        }
        jawA.position = targetA;
        jawB.position = targetB;
    }

    private bool RailTargetValid(Vector3 origin, Vector3 target, Transform home,
        Transform end, string mechanism)
    {
        Vector3 axis = end.position - home.position;
        axis.y = 0f;
        float length = axis.magnitude;
        if (length < 0.01f) return Fail(mechanism + "导轨端点重合");
        axis /= length;
        Vector3 relative = target - home.position;
        relative.y = 0f;
        float travel = Vector3.Dot(relative, axis);
        float crossTrack = (relative - axis * travel).magnitude;
        Vector3 move = target - origin;
        move.y = 0f;
        float moveCrossTrack = (move - axis * Vector3.Dot(move, axis)).magnitude;
        if (travel < -0.015f || travel > length + 0.015f
            || crossTrack > 0.12f || moveCrossTrack > 0.12f
            || Mathf.Abs(target.y - home.position.y) > 0.05f)
            return Fail(string.Format(
                "{0}目标不在导轨内：行程 {1:F3}/{2:F3}m，横向偏差 {3:F3}m，移动横向偏差 {4:F3}m，高差 {5:F3}m；请校准 LoadingPoints",
                mechanism, travel, length, crossTrack, moveCrossTrack,
                target.y - home.position.y));
        return true;
    }

    private void PlaceTransferOnClampLeft()
    {
        Vector3 axis = pickupRailEndPoint.position - pickupRailStartPoint.position;
        axis.y = 0f;
        axis.Normalize();
        Vector3 relative = transferPoint.position - pickupClampHomePoint.position;
        relative.y = 0f;
        float along = Vector3.Dot(relative, axis);
        float lateral = (relative - axis * along).magnitude;
        if (along < 0.05f || lateral > 0.04f)
        {
            Vector3 target = pickupClampHomePoint.position
                + axis * (rawSheetWorldWidth * 0.7f);
            transferPoint.position = new Vector3(target.x, transferPoint.position.y, target.z);
        }
    }

    private bool CalibratePickupRailFromMesh()
    {
        MeshFilter railFilter = clampRail.GetComponent<MeshFilter>();
        Transform sliderA = FindModelNode(1007);
        Transform sliderB = FindModelNode(1010);
        if (railFilter == null || railFilter.sharedMesh == null
            || sliderA == null || sliderB == null)
            return Fail("找不到 r1087 导轨网格或 r1007/r1010 夹爪架滑块，无法计算真实行程");

        Bounds local = railFilter.sharedMesh.bounds;
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
        Vector3 near = railFilter.transform.TransformPoint(local.center - localAxis * halfLength);
        Vector3 far = railFilter.transform.TransformPoint(local.center + localAxis * halfLength);
        Vector3 towardPunch = GetBounds(punchMachine).center - pickupClampHomePoint.position;
        towardPunch.y = 0f;
        if (Vector3.Dot(far - near, towardPunch) < 0f)
        {
            Vector3 swap = near;
            near = far;
            far = swap;
        }
        Vector3 axis = far - near;
        axis.y = 0f;
        float railLength = axis.magnitude;
        if (railLength < 0.5f) return Fail("r1087 导轨网格长轴异常");
        axis /= railLength;

        Bounds sliders = GetBounds(sliderA);
        sliders.Encapsulate(GetBounds(sliderB));
        float sliderMin = Vector3.Dot(sliders.center, axis) - HorizontalRadius(sliders, axis);
        float sliderMax = Vector3.Dot(sliders.center, axis) + HorizontalRadius(sliders, axis);
        float startTravel = Vector3.Dot(near, axis) + 0.05f - sliderMin;
        float endTravel = Vector3.Dot(far, axis) - 0.05f - sliderMax;
        if (endTravel < 0.2f)
            return Fail(string.Format("夹爪架滑块与 r1087 导轨不对齐：后限 {0:F3}m，前限 {1:F3}m",
                startTravel, endTravel));
        pickupRailStartPoint.position = pickupClampHomePoint.position + axis * startTravel;
        pickupRailEndPoint.position = pickupClampHomePoint.position + axis * endTravel;
        Debug.Log(string.Format("LoadingProcessController: r1087 实际导轨方向 {0}，夹爪架安全行程 0..{1:F3}m。",
            axis, endTravel), this);
        return true;
    }

    private bool CalibrateSuctionRailFromMesh()
    {
        Vector3 near, far, rackNear, rackFar;
        if (!TryRailMeshEnds(suctionRail, out near, out far)
            || !TryRailMeshEnds(suctionRack, out rackNear, out rackFar))
            return Fail("找不到 r1090 导轨或 r1120 齿条的网格长轴");
        Vector3 towardTransfer = pickupClampHomePoint.position - materialTable.bounds.center;
        towardTransfer.y = 0f;
        if (Vector3.Dot(far - near, towardTransfer) < 0f)
        {
            Vector3 swap = near;
            near = far;
            far = swap;
        }
        suctionRailAxis = far - near;
        suctionRailAxis.y = 0f;
        if (suctionRailAxis.magnitude < 1f) return Fail("r1090 吸盘导轨长度异常");
        suctionRailAxis.Normalize();
        Vector3 rackAxis = rackFar - rackNear;
        rackAxis.y = 0f;
        if (rackAxis.magnitude < 1f
            || Mathf.Abs(Vector3.Dot(suctionRailAxis, rackAxis.normalized)) < 0.98f)
            return Fail("r1120 齿条与 r1090 导轨不平行");

        int[] sliderIds = { 1139, 1142, 1143, 1144 };
        Bounds sliders = new Bounds();
        for (int i = 0; i < sliderIds.Length; i++)
        {
            Transform slider = FindModelNode(sliderIds[i]);
            if (slider == null) return Fail("吸盘机箱滑块模型缺失：r" + sliderIds[i]);
            if (i == 0) sliders = GetBounds(slider);
            else sliders.Encapsulate(GetBounds(slider));
        }
        float sliderMin = Vector3.Dot(sliders.center, suctionRailAxis)
            - HorizontalRadius(sliders, suctionRailAxis);
        float sliderMax = Vector3.Dot(sliders.center, suctionRailAxis)
            + HorizontalRadius(sliders, suctionRailAxis);
        float measuredMinTravel = Vector3.Dot(near, suctionRailAxis) + 0.05f - sliderMin;
        suctionMinTravel = Mathf.Min(0f, measuredMinTravel);
        suctionMaxTravel = Vector3.Dot(far, suctionRailAxis) - 0.05f - sliderMax;
        if (measuredMinTravel > 0.1f || suctionMaxTravel < 0.1f)
            return Fail(string.Format("吸盘滑块未落在 r1090 导轨内：允许行程 {0:F3}..{1:F3}m",
                suctionMinTravel, suctionMaxTravel));

        MeshFilter gearFilter = suctionGear.GetComponent<MeshFilter>();
        if (gearFilter == null || gearFilter.sharedMesh == null)
            return Fail("找不到 r1138 齿轮网格");
        Vector3 extents = gearFilter.sharedMesh.bounds.extents;
        suctionGearLocalAxis = Vector3.right;
        if (extents.y < extents.x && extents.y <= extents.z)
            suctionGearLocalAxis = Vector3.up;
        else if (extents.z < extents.x && extents.z < extents.y)
            suctionGearLocalAxis = Vector3.forward;
        Vector3 radialA = suctionGearLocalAxis == Vector3.right ? Vector3.up : Vector3.right;
        Vector3 radialB = suctionGearLocalAxis == Vector3.forward ? Vector3.up : Vector3.forward;
        float extentA = Vector3.Scale(extents, radialA).magnitude;
        float extentB = Vector3.Scale(extents, radialB).magnitude;
        suctionGearRadius = Mathf.Max(0.01f,
            Mathf.Max(gearFilter.transform.TransformVector(radialA * extentA).magnitude,
                gearFilter.transform.TransformVector(radialB * extentB).magnitude));
        Vector3 gearCenter = GetBounds(suctionGear).center;
        float rackA = Vector3.Dot(rackNear, suctionRailAxis);
        float rackB = Vector3.Dot(rackFar, suctionRailAxis);
        float gearAtHome = Vector3.Dot(gearCenter, suctionRailAxis);
        suctionMinTravel = Mathf.Max(suctionMinTravel,
            Mathf.Min(rackA, rackB) + 0.02f - gearAtHome);
        suctionMaxTravel = Mathf.Min(suctionMaxTravel,
            Mathf.Max(rackA, rackB) - 0.02f - gearAtHome);
        if (suctionMinTravel > 0.03f || suctionMaxTravel < 0.1f)
            return Fail("r1138 齿轮与 r1120 齿条的可用行程不足");
        suctionMinTravel = Mathf.Min(0f, suctionMinTravel);
        Vector3 towardRack = GetBounds(suctionRack).center - gearCenter;
        towardRack -= suctionRailAxis * Vector3.Dot(towardRack, suctionRailAxis);
        Vector3 worldAxle = suctionGear.TransformDirection(suctionGearLocalAxis);
        float tangent = Vector3.Dot(Vector3.Cross(worldAxle, towardRack), suctionRailAxis);
        suctionGearRollSign = tangent > 0f ? -1f : 1f;

        if (!AlignSuctionPoint(materialPickupPoint) || !AlignSuctionPoint(transferPoint))
            return false;
        Bounds table = materialTable.bounds;
        if (Mathf.Abs(materialPickupPoint.position.x - table.center.x) > table.extents.x + 0.02f
            || Mathf.Abs(materialPickupPoint.position.z - table.center.z) > table.extents.z + 0.02f)
            return Fail("吸盘导轨投影后的取料点不在原料台上");
        Vector3 pickup = materialPickupPoint.position;
        materialLiftPoint.position = new Vector3(pickup.x, materialLiftPoint.position.y, pickup.z);
        Vector3 relative = transferPoint.position - pickupClampHomePoint.position;
        relative.y = 0f;
        Vector3 clampAxis = pickupRailEndPoint.position - pickupClampHomePoint.position;
        clampAxis.y = 0f;
        clampAxis.Normalize();
        float sideOffset = (relative - clampAxis * Vector3.Dot(relative, clampAxis)).magnitude;
        if (sideOffset > rawSheetWorldWidth * 0.5f)
            return Fail(string.Format("吸盘导轨与夹爪交接线相距 {0:F3}m，板材宽度不足以交接", sideOffset));
        Debug.Log(string.Format("LoadingProcessController: r1090 实际导轨方向 {0}，吸盘滑块行程 {1:F3}..{2:F3}m。",
            suctionRailAxis, suctionMinTravel, suctionMaxTravel), this);
        return true;
    }

    private bool AlignSuctionPoint(Transform point)
    {
        Vector3 offset = point.position - suctionHome.position;
        offset.y = 0f;
        float travel = Vector3.Dot(offset, suctionRailAxis);
        if (travel < suctionMinTravel - 0.02f || travel > suctionMaxTravel + 0.02f)
            return Fail(string.Format("{0} 超出吸盘导轨行程：{1:F3}m，允许 {2:F3}..{3:F3}m",
                point.name, travel, suctionMinTravel, suctionMaxTravel));
        Vector3 projected = suctionHome.position + suctionRailAxis * travel;
        point.position = new Vector3(projected.x, point.position.y, projected.z);
        return true;
    }

    private static bool TryRailMeshEnds(Transform rail, out Vector3 near, out Vector3 far)
    {
        near = far = Vector3.zero;
        MeshFilter filter = rail.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return false;
        Bounds local = filter.sharedMesh.bounds;
        Vector3 axis = Vector3.right;
        float halfLength = local.extents.x;
        if (local.extents.y > halfLength)
        {
            axis = Vector3.up;
            halfLength = local.extents.y;
        }
        if (local.extents.z > halfLength)
        {
            axis = Vector3.forward;
            halfLength = local.extents.z;
        }
        near = filter.transform.TransformPoint(local.center - axis * halfLength);
        far = filter.transform.TransformPoint(local.center + axis * halfLength);
        return true;
    }

    private Vector3 PickupTargetAtSheetRightEdge()
    {
        Vector3 axis = pickupRailEndPoint.position - pickupRailStartPoint.position;
        axis.y = 0f;
        axis.Normalize();
        Bounds board = GetBounds(sheet);
        float halfWidth = Mathf.Abs(axis.x) * board.extents.x
            + Mathf.Abs(axis.z) * board.extents.z;
        // 图中板材应在黄色夹爪左侧；夹爪抓取它靠近自身的右边缘。
        Vector3 rightEdge = board.center - axis * halfWidth;
        Vector3 jaws = (clampJawA.position + clampJawB.position) * 0.5f;
        float travel = Vector3.Dot(rightEdge - jaws, axis);
        Vector3 target = clampMover.position + axis * travel;
        target.y = clampPickupPoint.position.y;
        return target;
    }

    private static float HorizontalRadius(Bounds bounds, Vector3 axis)
    {
        return Mathf.Abs(axis.x) * bounds.extents.x
            + Mathf.Abs(axis.z) * bounds.extents.z;
    }

    private static Transform CreateRuntimeRailStart(Transform rail, Transform home,
        Transform end, string name)
    {
        Vector3 direction = end.position - home.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return null;
        direction.Normalize();
        Bounds bounds = GetBounds(rail);
        float radius = Mathf.Abs(direction.x) * bounds.extents.x
            + Mathf.Abs(direction.z) * bounds.extents.z;
        float minimum = Vector3.Dot(bounds.center, direction) - radius + 0.08f;
        float travel = Vector3.Dot(home.position, direction) - minimum;
        GameObject point = new GameObject(name);
        point.transform.position = home.position - direction * Mathf.Max(0f, travel);
        return point.transform;
    }

    private bool Fail(string reason)
    {
        Debug.LogError("LoadingProcessController: " + reason, this);
        message = reason;
        sequence = null;
        return false;
    }

    private void PlaceSheetOnTable()
    {
        Bounds table = materialTable.bounds;
        Vector3 target = new Vector3(materialPickupPoint.position.x,
            table.max.y + 0.002f, materialPickupPoint.position.z);
        workpieceRoot.position += target - SheetBottomCenter();
    }

    private Vector3 SheetBottomCenter()
    {
        Bounds b = GetBounds(sheet);
        return new Vector3(b.center.x, b.min.y, b.center.z);
    }

    private static Bounds GetBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    public static float SampleTableHeight(Renderer table, Vector3 position, float fallbackInset)
    {
        float fallback = table.bounds.max.y - fallbackInset;
        MeshFilter filter = table.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return fallback;
        try
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            Transform t = filter.transform;
            float surface = float.NegativeInfinity;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = t.TransformPoint(vertices[triangles[i]]);
                Vector3 b = t.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = t.TransformPoint(vertices[triangles[i + 2]]);
                float denominator = (b.z - c.z) * (a.x - c.x)
                    + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 0.0000001f) continue;
                float u = ((b.z - c.z) * (position.x - c.x)
                    + (c.x - b.x) * (position.z - c.z)) / denominator;
                float v = ((c.z - a.z) * (position.x - c.x)
                    + (a.x - c.x) * (position.z - c.z)) / denominator;
                if (u < -0.0001f || v < -0.0001f || u + v > 1.0001f) continue;
                surface = Mathf.Max(surface, u * a.y + v * b.y + (1f - u - v) * c.y);
            }
            return float.IsNegativeInfinity(surface) ? fallback : surface;
        }
        catch (UnityException)
        {
            return fallback;
        }
    }

    private float CalculateSuctionCarryHeight()
    {
        // The housing and vertical guide stay at a fixed height while the frame rises.
        Renderer[] renderers = suctionVisuals[0].GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds frame = new Bounds(suctionMover.position, Vector3.zero);
        foreach (Renderer renderer in renderers)
        {
            Transform part = renderer.transform;
            if (part == suctionVerticalGuide || part.IsChildOf(suctionVerticalGuide)
                || part == suctionHousing || part.IsChildOf(suctionHousing)) continue;
            if (!found) { frame = renderer.bounds; found = true; }
            else frame.Encapsulate(renderer.bounds);
        }
        if (!found) frame = GetBounds(suctionVisuals[0]);
        float beamTop = GetBounds(pickupClampBeam).max.y;
        float frameBelowMover = suctionMover.position.y - frame.min.y;
        float safeHeight = Mathf.Max(materialLiftPoint.position.y,
            transferPoint.position.y + suctionReleaseClearance,
            beamTop + suctionReleaseClearance + frameBelowMover);
        Debug.Log(string.Format(
            "LoadingProcessController: suction clearance Y={0:F3}m, clamp beam top={1:F3}m, frame bottom offset={2:F3}m.",
            safeHeight, beamTop, -frameBelowMover), this);
        return safeHeight;
    }

    private static void OrientSheetFlat(Transform raw)
    {
        Bounds b = GetBounds(raw);
        float faceSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (b.size.y <= faceSize * 0.1f) return;
        Vector3 thinAxis = b.size.x < b.size.z ? Vector3.right : Vector3.forward;
        raw.rotation = Quaternion.FromToRotation(thinAxis, Vector3.up) * raw.rotation;
    }

    private bool IsNearConsole()
    {
        Camera camera = Camera.main;
        if (camera == null || controlConsole == null) return false;
        Bounds bounds = controlConsole.bounds;
        Vector3 eye = camera.transform.position;
        Vector3 closest = bounds.ClosestPoint(new Vector3(eye.x, bounds.center.y, eye.z));
        Vector3 delta = eye - closest;
        delta.y = 0f;
        return delta.magnitude <= consoleInteractionDistance;
    }

    private static Renderer FindControlConsole()
    {
        foreach (Renderer renderer in FindObjectsOfType<Renderer>())
        {
            string name = renderer.name;
            int index = name.IndexOf("r646", System.StringComparison.Ordinal);
            if (index >= 0 && (index == 0 || name[index - 1] == '_')
                && (index + 4 == name.Length || name[index + 4] == '_'))
                return renderer;
        }
        return null;
    }

    private static Transform FindModelNode(int sourceId)
    {
        string marker = "r" + sourceId;
        foreach (Transform candidate in FindObjectsOfType<Transform>())
        {
            string name = candidate.name;
            int index = name.IndexOf(marker, System.StringComparison.Ordinal);
            if (index >= 0 && (index == 0 || name[index - 1] == '_')
                && (index + marker.Length == name.Length || name[index + marker.Length] == '_'))
                return candidate;
        }
        return null;
    }

    private static Transform FindModelNode(Transform root, int sourceId)
    {
        string marker = "r" + sourceId;
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            string name = candidate.name;
            int index = name.IndexOf(marker, System.StringComparison.Ordinal);
            if (index >= 0 && (index == 0 || name[index - 1] == '_')
                && (index + marker.Length == name.Length || name[index + marker.Length] == '_'))
                return candidate;
        }
        return null;
    }

    private static void ReparentWithoutJump(Transform child, Transform parent)
    {
        Vector3 before = child.position;
        Quaternion rotation = child.rotation;
        child.SetParent(parent, true);
        if (Vector3.Distance(before, child.position) > 0.0001f)
            Debug.LogError("LoadingProcessController: 工件控制权切换时发生位置跳变。");
        child.position = before;
        child.rotation = rotation;
    }

    private static Vector3[] SavePositions(Transform[] visuals)
    {
        if (visuals == null) return new Vector3[0];
        Vector3[] positions = new Vector3[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) positions[i] = visuals[i].position;
        return positions;
    }

    private static Quaternion[] SaveRotations(Transform[] visuals)
    {
        Quaternion[] rotations = new Quaternion[visuals.Length];
        for (int i = 0; i < visuals.Length; i++)
            rotations[i] = visuals[i].rotation;
        return rotations;
    }

    private static Vector3[] SaveLocalPositions(Transform[] parts)
    {
        if (parts == null) return new Vector3[0];
        Vector3[] positions = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (parts[i] != null) positions[i] = parts[i].localPosition;
        return positions;
    }

    private static void RestoreLocalPositions(Transform[] parts, Vector3[] positions)
    {
        if (parts == null || positions == null) return;
        for (int i = 0; i < Mathf.Min(parts.Length, positions.Length); i++)
            if (parts[i] != null) parts[i].localPosition = positions[i];
    }

    private static void RestorePositions(Transform[] visuals, Vector3[] positions)
    {
        if (visuals == null || positions == null) return;
        for (int i = 0; i < Mathf.Min(visuals.Length, positions.Length); i++)
            if (visuals[i] != null) visuals[i].position = positions[i];
    }

    private bool ValidateSetup()
    {
        bool valid = rawSheetPrefab != null && finishedPartPrefab != null
            && workpieceRoot != null && materialTable != null
            && armLongSlot != null && armBaseAssembly != null && bendingProbe != null
            && armBaseJoint != null && armSuctionAssembly != null
            && armSegments != null && System.Array.TrueForAll(armSegments, part => part != null)
            && armSliders != null && System.Array.TrueForAll(armSliders, slider => slider != null)
            && bendingBody != null && bendingRail != null
            && FindModelNode(armSuctionAssembly, 860) != null
            && FindModelNode(armSuctionAssembly, 866) != null
            && FindModelNode(armSuctionAssembly, 867) != null
            && FindModelNode(armSuctionAssembly, 868) != null
            && controlConsole != null
            && suctionMover != null && clampMover != null && suctionHome != null
            && suctionRail != null && suctionRack != null && suctionGear != null
            && suctionHousing != null && suctionVerticalGuide != null
            && pickupClampBeam != null
            && punchClampMover != null && punchClampRail != null
            && punchRailFrame != null && punchRailShell != null
            && punchTable != null && punchClampBrackets != null
            && punchClampBrackets.Length == 2 && punchClampBrackets[0] != null
            && punchClampBrackets[1] != null
            && punchClampVisuals != null && punchClampVisuals.Length == 2
            && punchClampVisuals[0] != null && punchClampVisuals[1] != null
            && punchJawsA != null && punchJawsB != null
            && punchJawsA.Length == 4 && punchJawsB.Length == 4
            && System.Array.TrueForAll(punchJawsA, jaw => jaw != null)
            && System.Array.TrueForAll(punchJawsB, jaw => jaw != null)
            && materialPickupPoint != null && materialLiftPoint != null
            && transferPoint != null && clampPickupPoint != null
            && pickupClampHomePoint != null && pickupRailStartPoint != null
            && pickupRailEndPoint != null
            && punchClampHomePoint != null && punchClampAcquirePoint != null
            && punchRailStartPoint != null && punchRailEndPoint != null
            && transferToPunchPoint != null
            && punchEntryPoint != null && punchStartPoint != null;
        if (!valid)
        {
            message = "上料配置不完整，请检查 LoadingProcessController Inspector";
            Debug.LogError("LoadingProcessController: 缺少工件、设备或 LoadingPoints 引用。", this);
        }
        return valid;
    }

    private void SetState(LoadingState state, string description)
    {
        CurrentState = state;
        message = description;
        Debug.Log("上料状态: " + state + " — " + description, this);
    }

    private void OnGUI()
    {
        if (uiFont != null) GUI.skin.font = uiFont;
        GUI.Box(new Rect(12, 12, 390, 105), "上料流程");
        GUI.Label(new Rect(24, 37, 370, 24), message);
        GUI.Label(new Rect(24, 63, 370, 22), "状态: " + CurrentState);
        GUI.Label(new Rect(24, 88, 370, 22), "R 重置上料");
        if (initialized && (CurrentState == LoadingState.Idle || CurrentState == LoadingState.ReadyForPunching)
            && IsNearConsole())
            GUI.Box(new Rect(Screen.width * 0.5f - 125f, Screen.height * 0.6f, 250f, 38f),
                "按 E 操作控制台：开始上料");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        DrawSegment(suctionHome, materialPickupPoint);
        DrawSegment(materialPickupPoint, materialLiftPoint);
        DrawSegment(materialLiftPoint, transferPoint);
        Gizmos.color = Color.yellow;
        DrawSegment(pickupRailStartPoint, pickupClampHomePoint);
        DrawSegment(pickupClampHomePoint, pickupRailEndPoint);
        Gizmos.color = Color.green;
        DrawSegment(punchClampHomePoint, punchClampAcquirePoint);
        DrawSegment(transferToPunchPoint, punchStartPoint);
        DrawSegment(punchRailStartPoint, punchRailEndPoint);
    }

    private static void DrawSegment(Transform from, Transform to)
    {
        if (from == null || to == null) return;
        Gizmos.DrawSphere(from.position, 0.035f);
        Gizmos.DrawLine(from.position, to.position);
        Gizmos.DrawSphere(to.position, 0.035f);
    }
}
