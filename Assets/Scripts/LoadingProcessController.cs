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
        ArmBaseToBendingMold, ArmApproachBendingMold,
        TurnSuctionUp, PositionBendEdge, CloseBendingDie,
        OpenBendingDie, BendingComplete, ArmToHClamps,
        PlaceBentPartInHClamps, ArmReturnToRailEnd,
        ReadyForPunching
    }

    [Header("工件与设备")]
    public GameObject rawSheetPrefab;
    public GameObject finishedPartPrefab;
    public GameObject[] bendingPiecePrefabs;
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
    [Range(0f, 120f)] public float armThirdLinkBackAngle = 35f;
    [Range(0f, 120f)] public float armFourthLinkForwardAngle = 55f;
    [Min(0.01f)] public float bendingPressSpeed = 0.25f;
    [Min(0f)] public float bendingDieClearance = 0.004f;
    [Min(0f)] public float bendingPressHold = 0.25f;
    [Min(0.01f)] public float hClampApproachClearance = 0.18f;

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
    private Transform bendingUpper, bendingUpperMold, bendingLowerMold;
    private Transform[] bendingUpperVisuals;
    private Vector3[] bendingUpperHomePositions;
    private Transform[] armSegments, armMovingVisuals, armSliders;
    private Transform[] armOriginalParents;
    private Transform armRig;
    private Transform armThirdJoint, armFourthJoint, armSixthJoint;
    private Quaternion armThirdHomeRotation, armFourthHomeRotation,
        armSixthHomeRotation;
    private Transform armWristRig;
    private Transform[] armWristVisuals, armWristOriginalParents;
    private Quaternion armWristHomeLocalRotation;
    private Transform armContactAnchor;
    private Transform armWristStem;
    private Transform[] bentFlaps;
    private Bounds bentPanelBoundsLocal;
    private struct HClampGap
    {
        public Vector3 center;
        public Vector3 direction;
        public float near, far;
        public Vector3 tangent;
        public float tangentMin, tangentMax;
    }
    private Vector3[] cupTipLocalPoints;
    private Vector3 finishedTopLocalNormal = Vector3.up;
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
        bendingUpper = FindModelNode(121);
        bendingUpperMold = FindModelNode(1565);
        bendingLowerMold = FindModelNode(1547);
        bendingUpperVisuals = new[] { bendingUpper, bendingUpperMold };
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
#if UNITY_EDITOR
        EnsureBendingPieceReferencesForPlay();
#endif
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
        armThirdJoint = CreateArmJoint("Arm3 r848 pivot at Arm2 r851",
            armSegments[3].position, armRig);
        armFourthJoint = CreateArmJoint("Arm4 r845 lower pivot",
            armSegments[2].position, armThirdJoint);
        armSixthJoint = CreateArmJoint("Arm6 r839 pivot",
            armSegments[0].position, armFourthJoint);
        armSegments[3].SetParent(armThirdJoint, true);
        armSegments[2].SetParent(armFourthJoint, true);
        armSegments[1].SetParent(armFourthJoint, true);
        armSegments[0].SetParent(armSixthJoint, true);
        armSuctionAssembly.SetParent(armSixthJoint, true);
        armThirdHomeRotation = armThirdJoint.localRotation;
        armFourthHomeRotation = armFourthJoint.localRotation;
        armSixthHomeRotation = armSixthJoint.localRotation;
        armWristStem = FindModelNode(armSuctionAssembly, 857);
        armWristVisuals = new[] { armWristStem,
            FindModelNode(armSuctionAssembly, 863),
            FindModelNode(armSuctionAssembly, 860), FindModelNode(armSuctionAssembly, 866),
            FindModelNode(armSuctionAssembly, 867), FindModelNode(armSuctionAssembly, 868) };
        armWristRig = new GameObject("BendingWrist (runtime)").transform;
        armWristRig.position = armWristStem.position;
        armWristRig.SetParent(armSuctionAssembly, true);
        armWristOriginalParents = new Transform[armWristVisuals.Length];
        for (int i = 0; i < armWristVisuals.Length; i++)
        {
            armWristOriginalParents[i] = armWristVisuals[i].parent;
            armWristVisuals[i].SetParent(armWristRig, true);
        }
        armWristHomeLocalRotation = armWristRig.localRotation;
        if (!CacheCupTipPlane()) return;
        Vector3 cupContact, cupNormal;
        GetCupTipPlane(out cupContact, out cupNormal);
        armContactAnchor = new GameObject("SuctionContact (runtime)").transform;
        armContactAnchor.SetParent(armWristRig, false);
        armContactAnchor.position = cupContact;
        armMovingVisuals = new[] { armBaseAssembly, armRig };
        armBaseMover = new GameObject("ArmBaseMover (runtime)").transform;
        armBaseHome = GetBounds(armBaseAssembly).center;
        armBaseMover.position = armBaseHome;
        armVisualHomePositions = SavePositions(armMovingVisuals);
        bendingUpperHomePositions = SavePositions(bendingUpperVisuals);
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
        if (bendingUpperHomePositions != null)
            RestorePositions(bendingUpperVisuals, bendingUpperHomePositions);
        if (armBaseMover != null) Destroy(armBaseMover.gameObject);
        if (armRig != null)
        {
            if (armContactAnchor != null) Destroy(armContactAnchor.gameObject);
            if (armWristRig != null)
            {
                for (int i = 0; i < armWristVisuals.Length; i++)
                    if (armWristVisuals[i] != null)
                        armWristVisuals[i].SetParent(armWristOriginalParents[i], true);
                Destroy(armWristRig.gameObject);
            }
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
        bentFlaps = null;
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
        armThirdJoint.localRotation = armThirdHomeRotation;
        armFourthJoint.localRotation = armFourthHomeRotation;
        armSixthJoint.localRotation = armSixthHomeRotation;
        armWristRig.localRotation = armWristHomeLocalRotation;
        RestorePositions(bendingUpperVisuals, bendingUpperHomePositions);
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
        ReparentWithoutJump(workpieceRoot, armWristRig);
        if (!AlignFinishedPartWithCupTips()) yield break;
        SetState(LoadingState.ArmLiftPart, "机械臂吸盘抬起冲压成品");
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * armLiftHeight);

        SetState(LoadingState.ArmRestorePosture, "机械臂携成品恢复取件前姿态");
        yield return RestoreArmPosture(pickupPosturePositions, pickupPostureRotations,
            pickupPostureScale);

        Vector3 probeRailDestination;
        if (!TryGetArmRailPositionForBendingMold(out probeRailDestination)) yield break;
        SetState(LoadingState.ArmBaseToBendingMold,
            "滑槽基座 r872 沿 r775 移到下模 r1547 方位");
        yield return MoveCarrier(armBaseMover, armMovingVisuals,
            probeRailDestination, armBaseMoveSpeed);
        yield return RunBendingSequence();
        if (CurrentState == LoadingState.BendingComplete)
        {
            yield return DeliverBentPartToHClamps();
            if (CurrentState == LoadingState.ArmReturnToRailEnd)
                SetState(LoadingState.ReadyForPunching,
                    "成品已扣入 H 夹头，机械臂在长滑槽右端等待下一件；按 R 可重置");
        }
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

    private static Transform CreateArmJoint(string name, Vector3 pivot,
        Transform parent)
    {
        Transform joint = new GameObject(name + " (runtime)").transform;
        joint.position = pivot;
        joint.SetParent(parent, true);
        return joint;
    }

    private IEnumerator RunBendingSequence()
    {
        Bounds lower = GetBounds(bendingLowerMold);
        Bounds upper = GetBounds(bendingUpperMold);
        float openGap = upper.min.y - lower.max.y;
        if (openGap <= 0.02f)
        {
            Fail("折弯机上下模没有足够的初始开口");
            yield break;
        }
        Vector3 railNear, railFar;
        if (!TryRailMeshEnds(bendingRail, out railNear, out railFar))
        {
            Fail("无法计算折弯机的进料方向");
            yield break;
        }
        Vector3 outside = railFar - railNear;
        outside.y = 0f;
        if (outside.sqrMagnitude < 0.0001f)
        {
            Fail("折弯机导轨没有水平进料方向");
            yield break;
        }
        outside.Normalize();
        if (Vector3.Dot(armBaseMover.position - lower.center, outside) < 0f)
            outside = -outside;

        SetState(LoadingState.ArmApproachBendingMold,
            "机械臂在折弯机外侧抬起成品并朝模具弯曲");
        Bounds body = GetBounds(bendingBody);
        float outsideLimit = Vector3.Dot(body.center, outside)
            + HorizontalRadius(body, outside)
            + HorizontalRadius(GetBounds(sheet), outside) + 0.12f;
        float outsideGap = outsideLimit
            - Vector3.Dot(GetBounds(sheet).center, outside);
        if (outsideGap > 0f)
            yield return MoveArmSuctionTo(SuctionContactPoint() + outside * outsideGap);
        float lift = lower.max.y + Mathf.Min(openGap * 0.5f, armLiftHeight)
            - GetBounds(sheet).min.y;
        if (lift > 0f)
            yield return MoveArmSuctionTo(SuctionContactPoint() + Vector3.up * lift);

        Vector3[] straightPositions = SavePositions(armMovingVisuals);
        Quaternion[] straightRotations = SaveRotations(armMovingVisuals);
        float straightScale = armRig.localScale.x;
        Quaternion straightThird = armThirdJoint.localRotation;
        Quaternion straightFourth = armFourthJoint.localRotation;
        Quaternion straightSixth = armSixthJoint.localRotation;
        Quaternion straightWrist = armWristRig.localRotation;
        ReparentWithoutJump(workpieceRoot, armWristRig);
        Vector3 bendAxis = Vector3.Cross(Vector3.up, outside);
        if (bendAxis.sqrMagnitude < 0.0001f)
        {
            Fail("折弯机进料方向无法确定机械臂关节转轴");
            yield break;
        }
        bendAxis.Normalize();
        SetState(LoadingState.TurnSuctionUp,
            "r848 后转、r845 前转，r839 带 r857 与吸盘向上旋转");
        yield return ArticulateArmForBending(bendAxis, outside);
        if (sequence == null) yield break;
        if (!ReplaceWithBendingParts()) yield break;

        Bounds localPart;
        if (!TryGetLocalMeshBounds(sheet, out localPart))
        {
            Fail("冲压成品缺少可识别四边的网格");
            yield break;
        }
        Vector3 extents = localPart.extents;
        int thinAxis = extents.x < extents.y
            ? (extents.x < extents.z ? 0 : 2)
            : (extents.y < extents.z ? 1 : 2);
        Vector3 firstAxis = thinAxis == 0 ? Vector3.up : Vector3.right;
        Vector3 secondAxis = thinAxis == 2 ? Vector3.up : Vector3.forward;
        float firstExtent = Vector3.Scale(firstAxis, extents).magnitude;
        float secondExtent = Vector3.Scale(secondAxis, extents).magnitude;
        Vector3[] edgeOffsets = { firstAxis * firstExtent,
            secondAxis * secondExtent, -firstAxis * firstExtent,
            -secondAxis * secondExtent };
        Transform[] bendFlaps;
        Bounds centerPanel;
        if (!TryFindBendingFlaps(edgeOffsets, out bendFlaps, out centerPanel))
        {
            Fail("折弯分件模型缺少中间板或四块独立边缘");
            yield break;
        }
        bentFlaps = bendFlaps;
        bentPanelBoundsLocal = centerPanel;
        float partRadius = Mathf.Max(GetBounds(sheet).size.x,
            GetBounds(sheet).size.z);
        float retractDistance = partRadius + 0.15f;
        float dieMinX = Mathf.Max(lower.min.x, upper.min.x);
        float dieMaxX = Mathf.Min(lower.max.x, upper.max.x);
        float dieMinZ = Mathf.Max(lower.min.z, upper.min.z);
        float dieMaxZ = Mathf.Min(lower.max.z, upper.max.z);
        if (dieMinX >= dieMaxX || dieMinZ >= dieMaxZ)
        {
            Fail("折弯机上下模在水平面没有重叠的压制区域");
            yield break;
        }
        Vector3 diePoint = new Vector3((dieMinX + dieMaxX) * 0.5f,
            lower.center.y, (dieMinZ + dieMaxZ) * 0.5f);
        diePoint.y = lower.max.y + bendingDieClearance;
        float approachY = lower.max.y + Mathf.Min(openGap * 0.5f, 0.1f);
        float sheetHeight = Mathf.Min(GetBounds(sheet).size.y, 0.03f);
        Vector3 finalHingeLocal = Vector3.zero;
        for (int edge = 0; edge < edgeOffsets.Length; edge++)
        {
            Vector3 localOutward = edgeOffsets[edge].normalized;
            MeshFilter flapMesh = bendFlaps[edge].GetComponent<MeshFilter>();
            if (flapMesh == null)
            {
                Fail("成品第 " + (edge + 1) + " 边缺少折弯网格");
                yield break;
            }
            Bounds flapLocalBounds = FilterBoundsInRoot(sheet, flapMesh);
            Vector3 hingeLocal = flapLocalBounds.center
                - Vector3.Scale(localOutward, flapLocalBounds.extents);
            finalHingeLocal = hingeLocal;
            SetState(LoadingState.PositionBendEdge,
                "吸盘头旋转，将成品第 " + (edge + 1) + "/4 边对准上下模");
            if (edge == 0)
            {
                Vector3 edgeDirection = sheet.TransformDirection(edgeOffsets[edge]);
                edgeDirection.y = 0f;
                if (edgeDirection.sqrMagnitude < 0.0001f)
                {
                    Fail("成品边缘方向异常，无法定位第 1 边");
                    yield break;
                }
                float yaw = Vector3.SignedAngle(edgeDirection, -outside,
                    Vector3.up);
                yield return RotateWristTo(Quaternion.AngleAxis(yaw, Vector3.up)
                    * armWristRig.rotation);
            }
            else
            {
                Vector3 fromHinge = sheet.TransformPoint(hingeLocal)
                    - armWristRig.position;
                Vector3 toDie = diePoint - armWristRig.position;
                fromHinge.y = toDie.y = 0f;
                if (fromHinge.sqrMagnitude < 0.0001f
                    || toDie.sqrMagnitude < 0.0001f)
                {
                    Fail("吸盘旋转中心与第 " + (edge + 1) + " 边铰线重合");
                    yield break;
                }
                float yaw = Vector3.SignedAngle(fromHinge, toDie, Vector3.up);
                yield return RotateWristTo(Quaternion.AngleAxis(yaw, Vector3.up)
                    * armWristRig.rotation);
            }
            Quaternion wristOrientation = armWristRig.rotation;
            if (edge == 0)
            {
                Vector3 approach = diePoint;
                approach.y = approachY;
                yield return MoveArmEdgeTo(hingeLocal, approach, wristOrientation);
                if (sequence == null) yield break;
                yield return MoveArmEdgeTo(hingeLocal, diePoint, wristOrientation);
                if (sequence == null) yield break;
            }
            if (Vector3.Distance(sheet.TransformPoint(hingeLocal), diePoint)
                > positionTolerance * 5f)
            {
                Fail("成品第 " + (edge + 1) + " 边的铰线未对准折弯模具");
                yield break;
            }

            float stroke = upper.min.y
                - (lower.max.y + sheetHeight + bendingDieClearance);
            if (stroke <= positionTolerance)
            {
                Fail("上模无法对第 " + (edge + 1) + " 边完成闭合");
                yield break;
            }
            SetState(LoadingState.CloseBendingDie,
                "r121 带上模 r1565 下压第 " + (edge + 1) + "/4 边");
            Vector3 hinge = sheet.TransformPoint(hingeLocal);
            Vector3 foldAxis = Vector3.Cross(
                sheet.TransformDirection(localOutward), Vector3.up).normalized;
            Vector3 outerLocal = flapLocalBounds.center
                + Vector3.Scale(localOutward, flapLocalBounds.extents);
            Vector3 outerPointOnFlap = bendFlaps[edge].InverseTransformPoint(
                sheet.TransformPoint(outerLocal));
            yield return MoveBendingUpper(stroke, bendFlaps[edge], hinge, foldAxis);
            if (bendFlaps[edge].TransformPoint(outerPointOnFlap).y
                <= hinge.y + 0.01f)
            {
                Fail("成品第 " + (edge + 1) + " 边没有向上折起");
                yield break;
            }
            if (bendingPressHold > 0f)
                yield return new WaitForSeconds(bendingPressHold);
            SetState(LoadingState.OpenBendingDie,
                "折弯机上部抬起，上下模打开");
            yield return MoveBendingUpper(0f);
        }
        Vector3 retract = sheet.TransformPoint(finalHingeLocal)
            + outside * retractDistance;
        retract.y = approachY;
        yield return MoveArmEdgeTo(finalHingeLocal, retract,
            armWristRig.rotation);
        if (sequence == null) yield break;
        yield return RestoreArmPosture(straightPositions, straightRotations,
            straightScale);
        yield return RestoreBendingJoints(straightThird, straightFourth,
            straightSixth, straightWrist);
        SetState(LoadingState.BendingComplete, "四个突出边缘已依次完成上下模闭合");
    }

    private IEnumerator DeliverBentPartToHClamps()
    {
        if (bentFlaps == null || bentFlaps.Length != 4)
        {
            Fail("四边折弯成品缺少立边分件，无法放入 H 夹头");
            yield break;
        }
        Vector3 turntablePoint, turntableNormal;
        if (!TryGetHTurntablePlane(out turntablePoint, out turntableNormal))
            yield break;
        Vector3[] gaps;
        HClampGap[] clampGaps;
        float clampTop;
        if (!TryGetHClampGaps(turntableNormal, out gaps,
            out clampGaps, out clampTop)) yield break;
        Vector3 flapCenter = Vector3.zero;
        foreach (Transform flap in bentFlaps)
            flapCenter += GetBounds(flap).center;
        flapCenter *= 0.25f;
        float flapSide = Vector3.Dot(
            sheet.InverseTransformPoint(flapCenter) - bentPanelBoundsLocal.center,
            finishedTopLocalNormal);
        if (Mathf.Abs(flapSide) < 0.001f)
        {
            Fail("四条折弯立边没有离开中心板平面，无法确定朝下方向");
            yield break;
        }
        Vector3 downwardLocalNormal = finishedTopLocalNormal
            * Mathf.Sign(flapSide);
        Vector3 panelUnderLocal = bentPanelBoundsLocal.center
            + Vector3.Scale(downwardLocalNormal,
                bentPanelBoundsLocal.extents);
        Vector3 centerLocal = sheet.InverseTransformPoint(flapCenter);
        float clearance = Mathf.Max(0.01f, hClampApproachClearance);
        float safeCenterY = Mathf.Max(clampTop, turntablePoint.y)
            + GetBounds(sheet).extents.magnitude + clearance;
        if (flapCenter.y < safeCenterY)
        {
            Vector3 raised = flapCenter;
            raised.y = safeCenterY;
            yield return MoveArmEdgeTo(centerLocal, raised, armWristRig.rotation);
            if (sequence == null) yield break;
        }
        Vector3 productNormal = sheet.TransformDirection(downwardLocalNormal);
        yield return RotateWristTo(Quaternion.FromToRotation(productNormal,
            -turntableNormal) * armWristRig.rotation);
        if (Vector3.Dot(sheet.TransformDirection(downwardLocalNormal),
            -turntableNormal) < 0.999f)
        {
            Fail("四条折弯立边未能朝下指向 H 转台 r726 台面");
            yield break;
        }

        Vector3 targetCenter = Vector3.zero;
        foreach (Vector3 gap in gaps) targetCenter += gap;
        targetCenter *= 0.25f;
        Vector3 railAxis;
        float minimumTravel, maximumTravel;
        if (!TryGetArmRailLimits(out railAxis, out minimumTravel,
            out maximumTravel)) yield break;
        float projectedTravel = Vector3.Dot(targetCenter
            - sheet.TransformPoint(centerLocal), railAxis);
        float travel = Mathf.Clamp(projectedTravel, minimumTravel, maximumTravel);
        SetState(LoadingState.ArmToHClamps,
            "折弯成品抬高，滑槽基座移向四组 H 夹头");
        yield return MoveCarrier(armBaseMover, armMovingVisuals,
            armBaseMover.position + railAxis * travel, armBaseMoveSpeed);

        float yaw;
        float fitError;
        if (!TryFitBentFlapsToGaps(gaps, turntableNormal,
            out yaw, out fitError)) yield break;
        yield return RotateWristTo(Quaternion.AngleAxis(yaw, turntableNormal)
            * armWristRig.rotation);

        Vector3[] parkPositions = SavePositions(armMovingVisuals);
        Quaternion[] parkRotations = SaveRotations(armMovingVisuals);
        float parkScale = armRig.localScale.x;
        Vector3 approach;
        if (!TryBentFlapPlacementTarget(targetCenter, turntablePoint,
                turntableNormal, centerLocal, out approach)) yield break;
        approach += turntableNormal * clearance;
        approach.y = Mathf.Max(approach.y,
            clampTop + sheet.TransformPoint(centerLocal).y
            - GetBounds(sheet).min.y + clearance);
        Quaternion orientation = armWristRig.rotation;
        yield return MoveArmEdgeTo(centerLocal, approach, orientation);
        if (sequence == null) yield break;
        if (!TryBentFlapPlacementTarget(targetCenter, turntablePoint,
                turntableNormal, centerLocal, out targetCenter)) yield break;
        SetState(LoadingState.PlaceBentPartInHClamps,
            "四条立边朝下落在 H 转台台面并进入夹头间隙");
        yield return MoveArmEdgeTo(centerLocal, targetCenter, orientation);
        if (sequence == null) yield break;
        for (int correction = 0; correction < 3; correction++)
        {
            float lowest;
            if (!TryGetLowestBentFlapProjection(turntableNormal,
                    out lowest)) yield break;
            float gap = lowest - Vector3.Dot(turntablePoint,
                turntableNormal);
            if (Mathf.Abs(gap - 0.002f) <= 0.004f) break;
            targetCenter = sheet.TransformPoint(centerLocal)
                + turntableNormal * (0.002f - gap);
            yield return MoveArmEdgeTo(centerLocal, targetCenter, orientation);
            if (sequence == null) yield break;
        }
        float lowestFlap;
        if (!TryGetLowestBentFlapProjection(turntableNormal,
                out lowestFlap)) yield break;
        float bottomGap = lowestFlap - Vector3.Dot(turntablePoint,
            turntableNormal);
        float protrusion;
        if (!AreBentFlapsInsideGaps(clampGaps, out protrusion))
        {
            Fail("成品立边未全部进入四组 H 夹头间隙（最大越界 "
                + protrusion.ToString("F3") + "m），已保留在吸盘上");
            yield break;
        }
        if (Mathf.Abs(bottomGap - 0.002f) > 0.01f
            || Vector3.Dot(sheet.TransformDirection(downwardLocalNormal),
                -turntableNormal) < 0.999f)
        {
            Fail("折弯立边末端未落在 H 转台 r726 台面，已保留在吸盘上");
            yield break;
        }
        float panelUnderHeight = Vector3.Dot(
            sheet.TransformPoint(panelUnderLocal) - turntablePoint,
            turntableNormal);
        if (panelUnderHeight < 0.01f)
        {
            Fail("中心板没有被四条向下的立边撑在台面上方，已保留在吸盘上");
            yield break;
        }
        for (int i = 0; i < bentFlaps.Length; i++)
        {
            float lowest, highest;
            if (!TryGetProjectedMeshInterval(bentFlaps[i], turntableNormal,
                    out lowest, out highest)
                || Mathf.Abs(lowest - Vector3.Dot(turntablePoint,
                    turntableNormal) - 0.002f) > 0.015f
                || highest < lowest + 0.01f)
            {
                Fail("第 " + (i + 1)
                    + " 条折弯立边没有向下立在 H 转台台面，已保留在吸盘上");
                yield break;
            }
        }
        ReparentWithoutJump(workpieceRoot, workpieceHomeParent);
        bentFlaps = null;
        yield return MoveArmSuctionTo(SuctionContactPoint()
            + Vector3.up * (GetBounds(sheet).size.y + clearance));
        yield return RestoreArmPosture(parkPositions, parkRotations, parkScale);

        Vector3 rightEnd;
        if (!TryGetArmRailRightEnd(out rightEnd)) yield break;
        SetState(LoadingState.ArmReturnToRailEnd,
            "成品留在 H 夹头内，机械臂沿长滑槽返回右端等待");
        yield return MoveCarrier(armBaseMover, armMovingVisuals,
            rightEnd, armBaseMoveSpeed);
    }

    private bool TryGetHClampGaps(Vector3 surfaceNormal,
        out Vector3[] gaps, out HClampGap[] spaces, out float top)
    {
        int[,] ids = { { 1563, 1564 }, { 1560, 1553 },
            { 1556, 1562 }, { 1559, 1561 } };
        gaps = new Vector3[4];
        spaces = new HClampGap[4];
        top = float.NegativeInfinity;
        for (int i = 0; i < 4; i++)
        {
            Transform a = FindModelNode(ids[i, 0]);
            Transform b = FindModelNode(ids[i, 1]);
            if (a == null || b == null)
                return Fail("缺少第 " + (i + 1) + " 组 H 夹头模型");
            if (a.GetComponentInChildren<Renderer>() == null
                || b.GetComponentInChildren<Renderer>() == null)
                return Fail("第 " + (i + 1) + " 组 H 夹头缺少可测量的网格");
            Bounds first = GetBounds(a);
            Bounds second = GetBounds(b);
            Vector3 direction = second.center - first.center;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.000001f)
                return Fail("第 " + (i + 1) + " 组 H 夹头没有水平间隙");
            direction.Normalize();
            Vector3 faceA = first.center
                + direction * HorizontalRadius(first, direction);
            Vector3 faceB = second.center
                - direction * HorizontalRadius(second, direction);
            if (Vector3.Dot(faceB - faceA, direction) <= 0f)
                return Fail("第 " + (i + 1) + " 组 H 夹头相向表面重叠");
            gaps[i] = (faceA + faceB) * 0.5f;
            Vector3 tangent = Vector3.Cross(surfaceNormal, direction).normalized;
            spaces[i].direction = direction;
            spaces[i].near = Vector3.Dot(faceA, direction);
            spaces[i].far = Vector3.Dot(faceB, direction);
            spaces[i].tangent = tangent;
            spaces[i].tangentMin = Mathf.Max(
                Vector3.Dot(first.center, tangent)
                    - HorizontalRadius(first, tangent),
                Vector3.Dot(second.center, tangent)
                    - HorizontalRadius(second, tangent));
            spaces[i].tangentMax = Mathf.Min(
                Vector3.Dot(first.center, tangent)
                    + HorizontalRadius(first, tangent),
                Vector3.Dot(second.center, tangent)
                    + HorizontalRadius(second, tangent));
            float sharedBottom = Mathf.Max(first.min.y, second.min.y);
            float sharedTop = Mathf.Min(first.max.y, second.max.y);
            gaps[i].y = sharedBottom <= sharedTop
                ? (sharedBottom + sharedTop) * 0.5f
                : (first.center.y + second.center.y) * 0.5f;
            spaces[i].center = gaps[i];
            top = Mathf.Max(top, first.max.y, second.max.y);
        }
        return true;
    }

    private bool AreBentFlapsInsideGaps(HClampGap[] spaces,
        out float protrusion)
    {
        protrusion = float.PositiveInfinity;
        if (bentFlaps == null || bentFlaps.Length != 4
            || spaces == null || spaces.Length != 4) return false;
        for (int a = 0; a < 4; a++)
        for (int b = 0; b < 4; b++)
        for (int c = 0; c < 4; c++)
        for (int d = 0; d < 4; d++)
        {
            if (a == b || a == c || a == d || b == c || b == d || c == d)
                continue;
            int[] assignment = { a, b, c, d };
            float worst = 0f;
            for (int i = 0; i < 4; i++)
            {
                HClampGap space = spaces[assignment[i]];
                float low, high, sideLow, sideHigh;
                if (!TryGetProjectedMeshInterval(bentFlaps[i],
                        space.direction, out low, out high)
                    || !TryGetProjectedMeshInterval(bentFlaps[i],
                        space.tangent, out sideLow, out sideHigh))
                    return false;
                worst = Mathf.Max(worst, space.near - low, high - space.far);
                if (space.tangentMin <= space.tangentMax)
                    worst = Mathf.Max(worst,
                        space.tangentMin - sideHigh,
                        sideLow - space.tangentMax);
            }
            protrusion = Mathf.Min(protrusion, worst);
        }
        return protrusion <= 0.005f;
    }

    private static bool TryGetProjectedMeshInterval(Transform root,
        Vector3 axis, out float minimum, out float maximum)
    {
        minimum = float.PositiveInfinity;
        maximum = float.NegativeInfinity;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3(x, y, z));
                float position = Vector3.Dot(
                    filter.transform.TransformPoint(corner), axis);
                minimum = Mathf.Min(minimum, position);
                maximum = Mathf.Max(maximum, position);
            }
        }
        return !float.IsInfinity(minimum);
    }

    private bool TryGetHTurntablePlane(out Vector3 point, out Vector3 normal)
    {
        point = Vector3.zero;
        normal = Vector3.up;
        Transform turntable = FindModelNode(726);
        if (turntable == null)
            return Fail("缺少 H 转台 r726，无法确定成品底面姿态");
        MeshFilter filter = turntable.GetComponent<MeshFilter>();
        if (filter == null)
            filter = turntable.GetComponentInChildren<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return Fail("H 转台 r726 缺少可测量的台面网格");
        Bounds mesh = filter.sharedMesh.bounds;
        Vector3 localNormal = mesh.size.x < mesh.size.y
            ? (mesh.size.x < mesh.size.z ? Vector3.right : Vector3.forward)
            : (mesh.size.y < mesh.size.z ? Vector3.up : Vector3.forward);
        normal = filter.transform.TransformDirection(localNormal).normalized;
        if (Vector3.Dot(normal, Vector3.up) < 0f)
        {
            normal = -normal;
            localNormal = -localNormal;
        }
        if (Vector3.Dot(normal, Vector3.up) < 0.5f)
            return Fail("H 转台 r726 的台面法线不是朝上的平面");
        point = filter.transform.TransformPoint(mesh.center
            + Vector3.Scale(localNormal, mesh.extents));
        return true;
    }

    private bool TryBentFlapPlacementTarget(Vector3 gapCenter,
        Vector3 surfacePoint, Vector3 surfaceNormal, Vector3 centerLocal,
        out Vector3 target)
    {
        target = gapCenter;
        float lowest;
        if (!TryGetLowestBentFlapProjection(surfaceNormal, out lowest))
            return Fail("无法读取四条折弯立边的最低点");
        Vector3 currentCenter = sheet.TransformPoint(centerLocal);
        float correction = Vector3.Dot(surfacePoint, surfaceNormal)
            + 0.002f - lowest
            - Vector3.Dot(gapCenter - currentCenter, surfaceNormal);
        target = gapCenter + surfaceNormal * correction;
        return true;
    }

    private bool TryGetLowestBentFlapProjection(Vector3 axis,
        out float lowest)
    {
        lowest = float.PositiveInfinity;
        foreach (Transform flap in bentFlaps)
        {
            float minimum, maximum;
            if (!TryGetProjectedMeshInterval(flap, axis,
                out minimum, out maximum)) return false;
            lowest = Mathf.Min(lowest, minimum);
        }
        return !float.IsInfinity(lowest);
    }

    private bool TryFitBentFlapsToGaps(Vector3[] gaps, Vector3 planeNormal,
        out float yaw,
        out float error, bool allowRotation = true)
    {
        yaw = 0f;
        error = float.PositiveInfinity;
        if (bentFlaps == null || bentFlaps.Length != 4) return false;
        Vector3[] source = new Vector3[4];
        Vector3 sourceCenter = Vector3.zero;
        Vector3 gapCenter = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            source[i] = GetBounds(bentFlaps[i]).center;
            sourceCenter += source[i];
            gapCenter += gaps[i];
        }
        sourceCenter *= 0.25f;
        gapCenter *= 0.25f;
        for (int a = 0; a < 4; a++)
        for (int b = 0; b < 4; b++)
        for (int c = 0; c < 4; c++)
        for (int d = 0; d < 4; d++)
        {
            if (a == b || a == c || a == d || b == c || b == d || c == d)
                continue;
            int[] assignment = { a, b, c, d };
            float dot = 0f, cross = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 from = Vector3.ProjectOnPlane(
                    source[i] - sourceCenter, planeNormal);
                Vector3 to = Vector3.ProjectOnPlane(
                    gaps[assignment[i]] - gapCenter, planeNormal);
                dot += Vector3.Dot(from, to);
                cross += Vector3.Dot(Vector3.Cross(from, to), planeNormal);
            }
            float angle = allowRotation
                ? Mathf.Atan2(cross, dot) * Mathf.Rad2Deg : 0f;
            Quaternion rotation = Quaternion.AngleAxis(angle, planeNormal);
            float squaredError = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 difference = Vector3.ProjectOnPlane(
                    rotation * (source[i] - sourceCenter)
                    - (gaps[assignment[i]] - gapCenter), planeNormal);
                squaredError += difference.sqrMagnitude;
            }
            float candidate = Mathf.Sqrt(squaredError * 0.25f);
            if (candidate >= error) continue;
            error = candidate;
            yaw = angle;
        }
        return !float.IsInfinity(error);
    }

    private bool TryGetArmRailRightEnd(out Vector3 destination)
    {
        destination = armBaseMover.position;
        Vector3 axis;
        float minimumTravel, maximumTravel;
        if (!TryGetArmRailLimits(out axis, out minimumTravel,
            out maximumTravel)) return false;
        Vector3 first = destination + axis * minimumTravel;
        Vector3 second = destination + axis * maximumTravel;
        if (Mathf.Abs(first.x - second.x) > 0.05f)
            destination = first.x < second.x ? first : second;
        else
        {
            Vector3 table = punchTable.bounds.center;
            destination = Vector3.Distance(first, table)
                > Vector3.Distance(second, table) ? first : second;
        }
        return true;
    }

    private IEnumerator ArticulateArmForBending(Vector3 axis, Vector3 outside)
    {
        Quaternion startThird = armThirdJoint.localRotation;
        Quaternion startFourth = armFourthJoint.localRotation;
        Quaternion startSixth = armSixthJoint.localRotation;
        float thirdDirection = Vector3.Dot(Vector3.Cross(axis,
            armFourthJoint.position - armThirdJoint.position), outside);
        float thirdAngle = (thirdDirection >= 0f ? 1f : -1f)
            * armThirdLinkBackAngle;
        armThirdJoint.RotateAround(armThirdJoint.position, axis, thirdAngle);
        float fourthDirection = Vector3.Dot(Vector3.Cross(axis,
            armSixthJoint.position - armFourthJoint.position), -outside);
        float fourthAngle = (fourthDirection >= 0f ? 1f : -1f)
            * armFourthLinkForwardAngle;
        armFourthJoint.RotateAround(armFourthJoint.position, axis, fourthAngle);
        Vector3 cupCenter, cupNormal;
        GetCupTipPlane(out cupCenter, out cupNormal);
        Vector3 projectedNormal = Vector3.ProjectOnPlane(cupNormal, axis);
        if (projectedNormal.sqrMagnitude < 0.000001f)
        {
            armFourthJoint.localRotation = startFourth;
            armThirdJoint.localRotation = startThird;
            Fail("吸盘末端平面与 r839 转轴平行，无法确定翻转角度");
            yield break;
        }
        float sixthAngle = Vector3.SignedAngle(projectedNormal, Vector3.up, axis);
        Vector3 stemOffset = GetBounds(armWristStem).center - armSixthJoint.position;
        float liftDirection = Vector3.Cross(axis, stemOffset).y;
        if (Mathf.Abs(Mathf.Abs(sixthAngle) - 180f) < 0.01f)
            sixthAngle = (liftDirection >= 0f ? 1f : -1f) * 180f;
        armSixthJoint.localRotation = startSixth;
        armFourthJoint.localRotation = startFourth;
        armThirdJoint.localRotation = startThird;
        float duration = Mathf.Max(Mathf.Abs(thirdAngle), Mathf.Abs(fourthAngle),
            Mathf.Abs(sixthAngle)) / Mathf.Max(1f, armRotationSpeed);
        duration = Mathf.Max(duration, 0.01f);
        float fraction = 0f;
        while (fraction < 1f)
        {
            float next = Mathf.Min(1f, fraction + Time.deltaTime / duration);
            float step = next - fraction;
            armThirdJoint.RotateAround(armThirdJoint.position, axis,
                thirdAngle * step);
            armFourthJoint.RotateAround(armFourthJoint.position, axis,
                fourthAngle * step);
            armSixthJoint.RotateAround(armSixthJoint.position, axis,
                sixthAngle * step);
            fraction = next;
            yield return null;
        }
        GetCupTipPlane(out cupCenter, out cupNormal);
        if (Vector3.Dot(cupNormal, Vector3.up) < 0.999f)
        {
            Quaternion wristTarget = Quaternion.FromToRotation(cupNormal,
                Vector3.up) * armWristRig.rotation;
            yield return RotateWristTo(wristTarget);
        }
        GetCupTipPlane(out cupCenter, out cupNormal);
        Vector3 faceNormal = sheet.TransformDirection(finishedTopLocalNormal);
        float lowestTip = float.PositiveInfinity;
        float highestTip = float.NegativeInfinity;
        for (int i = 0; i < cupTipLocalPoints.Length; i++)
        {
            float height = armWristRig.TransformPoint(cupTipLocalPoints[i]).y;
            lowestTip = Mathf.Min(lowestTip, height);
            highestTip = Mathf.Max(highestTip, height);
        }
        if (Vector3.Dot(cupNormal, Vector3.up) < 0.995f
            || Vector3.Dot(faceNormal, cupNormal) > -0.995f
            || highestTip - lowestTip > 0.005f)
            Fail("翻转后四个吸盘末端与成品板面没有共同保持水平");
    }

    private IEnumerator RotateWristTo(Quaternion target)
    {
        while (Quaternion.Angle(armWristRig.rotation, target) > 0.01f)
        {
            armWristRig.rotation = Quaternion.RotateTowards(armWristRig.rotation,
                target, armRotationSpeed * Time.deltaTime);
            yield return null;
        }
        armWristRig.rotation = target;
    }

    private IEnumerator RestoreBendingJoints(Quaternion third, Quaternion fourth,
        Quaternion sixth, Quaternion wrist)
    {
        while (Quaternion.Angle(armThirdJoint.localRotation, third) > 0.01f
            || Quaternion.Angle(armFourthJoint.localRotation, fourth) > 0.01f
            || Quaternion.Angle(armSixthJoint.localRotation, sixth) > 0.01f
            || Quaternion.Angle(armWristRig.localRotation, wrist) > 0.01f)
        {
            float step = armRotationSpeed * Time.deltaTime;
            armThirdJoint.localRotation = Quaternion.RotateTowards(
                armThirdJoint.localRotation, third, step);
            armFourthJoint.localRotation = Quaternion.RotateTowards(
                armFourthJoint.localRotation, fourth, step);
            armSixthJoint.localRotation = Quaternion.RotateTowards(
                armSixthJoint.localRotation, sixth, step);
            armWristRig.localRotation = Quaternion.RotateTowards(
                armWristRig.localRotation, wrist, step);
            yield return null;
        }
        armThirdJoint.localRotation = third;
        armFourthJoint.localRotation = fourth;
        armSixthJoint.localRotation = sixth;
        armWristRig.localRotation = wrist;
    }

    private bool CacheCupTipPlane()
    {
        cupTipLocalPoints = new Vector3[4];
        Vector3 cupCenter = Vector3.zero;
        for (int i = 0; i < 4; i++)
            cupCenter += GetBounds(armWristVisuals[i + 2]).center;
        cupCenter *= 0.25f;
        Vector3 outward = cupCenter - GetBounds(armWristVisuals[1]).center;
        if (outward.sqrMagnitude < 0.000001f)
            outward = cupCenter - GetBounds(armWristStem).center;
        if (outward.sqrMagnitude < 0.000001f)
            return Fail("无法确定四个机械臂吸盘的末端方向");
        outward.Normalize();
        for (int i = 0; i < 4; i++)
        {
            Vector3 tip;
            if (!TryFindCupTip(armWristVisuals[i + 2], outward, out tip))
                return Fail("无法读取机械臂吸盘末端网格 r"
                    + new[] { 860, 866, 867, 868 }[i]);
            cupTipLocalPoints[i] = armWristRig.InverseTransformPoint(tip);
        }
        Vector3 center, normal;
        GetCupTipPlane(out center, out normal);
        return normal.sqrMagnitude > 0.5f
            || Fail("四个机械臂吸盘末端无法确定一个平面");
    }

    private static bool TryFindCupTip(Transform cup, Vector3 outward,
        out Vector3 tip)
    {
        tip = Vector3.zero;
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        MeshFilter[] filters = cup.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            foreach (Vector3 vertex in mesh.vertices)
            {
                float projection = Vector3.Dot(filter.transform.TransformPoint(vertex),
                    outward);
                min = Mathf.Min(min, projection);
                max = Mathf.Max(max, projection);
            }
        }
        if (float.IsInfinity(max)) return false;
        float threshold = max - Mathf.Max((max - min) * 0.01f, 0.0001f);
        int count = 0;
        foreach (MeshFilter filter in filters)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = filter.transform.TransformPoint(vertex);
                if (Vector3.Dot(point, outward) < threshold) continue;
                tip += point;
                count++;
            }
        }
        if (count == 0) return false;
        tip /= count;
        return true;
    }

    private void GetCupTipPlane(out Vector3 center, out Vector3 normal)
    {
        Vector3[] tips = new Vector3[4];
        center = Vector3.zero;
        for (int i = 0; i < 4; i++)
        {
            tips[i] = armWristRig.TransformPoint(cupTipLocalPoints[i]);
            center += tips[i];
        }
        center *= 0.25f;
        normal = Vector3.Cross(tips[2] - tips[3], tips[1] - tips[0]);
        if (normal.sqrMagnitude < 0.000001f)
            normal = Vector3.Cross(tips[2] - tips[0], tips[3] - tips[0]);
        if (normal.sqrMagnitude < 0.000001f) return;
        normal.Normalize();
        Vector3 outward = center - GetBounds(armWristVisuals[1]).center;
        if (Vector3.Dot(normal, outward) < 0f) normal = -normal;
    }

    private bool AlignFinishedPartWithCupTips()
    {
        Bounds local;
        if (!TryGetLocalMeshBounds(sheet, out local))
            return Fail("冲压成品缺少可对齐吸盘的板面网格");
        Vector3 center, cupNormal;
        GetCupTipPlane(out center, out cupNormal);
        if (cupNormal.sqrMagnitude < 0.5f)
            return Fail("四个机械臂吸盘末端平面无效");
        Vector3 faceLocal = local.center
            + Vector3.Scale(finishedTopLocalNormal, local.extents);
        Vector3 faceNormal = sheet.TransformDirection(finishedTopLocalNormal);
        sheet.rotation = Quaternion.FromToRotation(faceNormal, -cupNormal)
            * sheet.rotation;
        sheet.position += center + cupNormal * 0.001f
            - sheet.TransformPoint(faceLocal);
        return true;
    }

    private IEnumerator MoveArmEdgeTo(Vector3 localEdge, Vector3 targetEdge,
        Quaternion wristOrientation)
    {
        Vector3 currentEdge = sheet.TransformPoint(localEdge);
        if (Vector3.Distance(currentEdge, targetEdge) <= positionTolerance * 2f)
            yield break;
        Vector3 pivot = armRig.position;
        Vector3 currentWrist = armWristRig.position - pivot;
        float startScale = armRig.localScale.x;
        if (currentWrist.sqrMagnitude < 0.000001f || startScale < 0.0001f)
        {
            Fail("机械臂无法保持基座连接并对准折弯模具");
            yield break;
        }
        // The wrist keeps a fixed world orientation while the arm rig turns.
        // Solve its uniform scale and rotation from the requested edge point.
        Vector3 edgeOffsetPerScale = (currentEdge - armWristRig.position) / startScale;
        Vector3 wristOffsetPerScale = currentWrist / startScale;
        Vector3 desired = targetEdge - pivot;
        float a = edgeOffsetPerScale.sqrMagnitude - wristOffsetPerScale.sqrMagnitude;
        float b = -2f * Vector3.Dot(desired, edgeOffsetPerScale);
        float c = desired.sqrMagnitude;
        float targetScale;
        if (Mathf.Abs(a) < 0.000001f)
        {
            if (Mathf.Abs(b) < 0.000001f)
            {
                Fail("折弯边缘目标超出机械臂可解范围");
                yield break;
            }
            targetScale = -c / b;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                Fail("折弯边缘目标超出机械臂可解范围");
                yield break;
            }
            float root = Mathf.Sqrt(discriminant);
            float first = (-b + root) / (2f * a);
            float second = (-b - root) / (2f * a);
            targetScale = first > 0f && (second <= 0f
                || Mathf.Abs(first - startScale) < Mathf.Abs(second - startScale))
                ? first : second;
        }
        if (targetScale < 0.05f || targetScale > 4f)
        {
            Fail("折弯边缘目标需要异常的机械臂比例");
            yield break;
        }
        Vector3 targetWrist = desired - edgeOffsetPerScale * targetScale;
        Quaternion startRotation = armRig.rotation;
        Quaternion targetRotation = Quaternion.FromToRotation(currentWrist,
            targetWrist) * startRotation;
        float duration = Mathf.Max(Vector3.Distance(currentEdge, targetEdge)
                / armPickupSpeed,
            Quaternion.Angle(startRotation, targetRotation) / armRotationSpeed,
            0.01f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = elapsed / duration;
            armRig.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
            armRig.localScale = Vector3.one * Mathf.Lerp(startScale, targetScale, t);
            armWristRig.rotation = wristOrientation;
            yield return null;
        }
        armRig.rotation = targetRotation;
        armRig.localScale = Vector3.one * targetScale;
        armWristRig.rotation = wristOrientation;
    }

    private IEnumerator MoveBendingUpper(float travel, Transform flap = null,
        Vector3 hinge = default(Vector3), Vector3 foldAxis = default(Vector3))
    {
        Vector3 destination = bendingUpperHomePositions[0] + Vector3.down * travel;
        float totalTravel = Vector3.Distance(bendingUpper.position, destination);
        float folded = 0f;
        while (Vector3.Distance(bendingUpper.position, destination) > positionTolerance)
        {
            Vector3 next = Vector3.MoveTowards(bendingUpper.position, destination,
                bendingPressSpeed * Time.deltaTime);
            Vector3 delta = next - bendingUpper.position;
            bendingUpper.position = next;
            if (!bendingUpperMold.IsChildOf(bendingUpper))
                bendingUpperMold.position += delta;
            if (flap != null && totalTravel > 0.0001f)
            {
                float nextFold = 90f * (1f - Vector3.Distance(next, destination)
                    / totalTravel);
                flap.RotateAround(hinge, foldAxis, nextFold - folded);
                folded = nextFold;
            }
            yield return null;
        }
        Vector3 remainder = destination - bendingUpper.position;
        bendingUpper.position = destination;
        if (!bendingUpperMold.IsChildOf(bendingUpper))
            bendingUpperMold.position += remainder;
        if (flap != null) flap.RotateAround(hinge, foldAxis, 90f - folded);
    }

    private bool ReplaceWithBendingParts()
    {
        if (bendingPiecePrefabs == null || bendingPiecePrefabs.Length != 5
            || System.Array.Exists(bendingPiecePrefabs, prefab => prefab == null))
            return Fail("缺少加工零件 冲压完毕1–5.fbx 五块折弯分件模型");
        Bounds original = GetBounds(sheet);
        Transform previous = sheet;
        Vector3 previousFaceNormal = previous.TransformDirection(finishedTopLocalNormal);
        GameObject split = new GameObject("BendingPart");
        split.transform.SetParent(workpieceRoot, false);
        Transform[] sections = new Transform[bendingPiecePrefabs.Length];
        for (int i = 0; i < bendingPiecePrefabs.Length; i++)
        {
            GameObject piece = Instantiate(bendingPiecePrefabs[i], split.transform, false);
            piece.name = "FinishedPartSection" + (i + 1);
            sections[i] = piece.transform;
        }
        if (!AlignThirdBendingSection(split.transform, sections[0], sections[2]))
        {
            Destroy(split);
            return Fail("冲压完毕3.fbx 无法与中心板边界对齐");
        }
        split.transform.position = previous.position;
        split.transform.rotation = previous.rotation;
        Bounds splitBounds = GetBounds(split.transform);
        float oldWidth = Mathf.Max(original.size.x, original.size.z);
        float newWidth = Mathf.Max(splitBounds.size.x, splitBounds.size.z);
        if (newWidth < 0.0001f)
        {
            Destroy(split);
            return Fail("折弯分件模型没有有效的网格边界");
        }
        split.transform.localScale *= oldWidth / newWidth;
        splitBounds = GetBounds(split.transform);
        split.transform.position += original.center - splitBounds.center;
        sheet = split.transform;
        Bounds localSplit;
        if (TryGetLocalMeshBounds(sheet, out localSplit))
        {
            Vector3 e = localSplit.extents;
            finishedTopLocalNormal = e.x < e.y
                ? (e.x < e.z ? Vector3.right : Vector3.forward)
                : (e.y < e.z ? Vector3.up : Vector3.forward);
            if (Vector3.Dot(sheet.TransformDirection(finishedTopLocalNormal),
                previousFaceNormal) < 0f)
                finishedTopLocalNormal = -finishedTopLocalNormal;
        }
        if (!AlignFinishedPartWithCupTips())
        {
            sheet = previous;
            Destroy(split);
            return false;
        }
        previous.gameObject.SetActive(false);
        ApplyWorkpieceColor(sheet);
        Destroy(previous.gameObject);
        return true;
    }

    private static bool AlignThirdBendingSection(Transform assembly,
        Transform panel, Transform thirdSection)
    {
        MeshFilter panelMesh = panel.GetComponentInChildren<MeshFilter>(true);
        MeshFilter sectionMesh = thirdSection.GetComponentInChildren<MeshFilter>(true);
        if (panelMesh == null || sectionMesh == null
            || panelMesh.sharedMesh == null || sectionMesh.sharedMesh == null)
            return false;
        Bounds panelBounds = FilterBoundsInRoot(assembly, panelMesh);
        Bounds sectionBounds = FilterBoundsInRoot(assembly, sectionMesh);
        Vector3 separation = sectionBounds.center - panelBounds.center;
        float x = Mathf.Abs(separation.x);
        float y = Mathf.Abs(separation.y);
        float z = Mathf.Abs(separation.z);
        Vector3 outward = x >= y && x >= z ? Vector3.right
            : y >= z ? Vector3.up : Vector3.forward;
        if (Vector3.Dot(separation, outward) < 0f) outward = -outward;
        float panelEdge = Vector3.Dot(panelBounds.center, outward)
            + Vector3.Dot(panelBounds.extents, Abs(outward));
        float sectionInnerEdge = Vector3.Dot(sectionBounds.center, outward)
            - Vector3.Dot(sectionBounds.extents, Abs(outward));
        float correction = panelEdge - sectionInnerEdge;
        if (Mathf.Abs(correction) > panelBounds.size.magnitude * 0.01f)
            thirdSection.localPosition += outward * correction;
        return true;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private bool TryFindBendingFlaps(Vector3[] directions,
        out Transform[] flaps, out Bounds centerPanel)
    {
        flaps = new Transform[directions.Length];
        centerPanel = new Bounds();
        MeshFilter[] filters = sheet.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter central = null;
        float greatestArea = 0f;
        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
            Bounds b = FilterBoundsInRoot(sheet, filter);
            Vector3 size = b.size;
            float[] dimensions = { size.x, size.y, size.z };
            System.Array.Sort(dimensions);
            float area = dimensions[1] * dimensions[2];
            if (area <= greatestArea) continue;
            greatestArea = area;
            central = filter;
            centerPanel = b;
        }
        if (central == null) return false;
        System.Collections.Generic.HashSet<MeshFilter> used =
            new System.Collections.Generic.HashSet<MeshFilter> { central };
        for (int edge = 0; edge < directions.Length; edge++)
        {
            MeshFilter best = null;
            float bestProjection = 0f;
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null || used.Contains(filter)
                    || !filter.gameObject.activeInHierarchy) continue;
                float projection = Vector3.Dot(
                    FilterBoundsInRoot(sheet, filter).center - centerPanel.center,
                    directions[edge].normalized);
                if (projection <= bestProjection) continue;
                bestProjection = projection;
                best = filter;
            }
            if (best == null) return false;
            flaps[edge] = best.transform;
            used.Add(best);
        }
        return true;
    }

    private static Bounds FilterBoundsInRoot(Transform root, MeshFilter filter)
    {
        Bounds mesh = filter.sharedMesh.bounds;
        Bounds result = new Bounds();
        bool found = false;
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 local = root.InverseTransformPoint(
                        filter.transform.TransformPoint(mesh.center
                            + Vector3.Scale(mesh.extents, new Vector3(x, y, z))));
                    if (!found) { result = new Bounds(local, Vector3.zero); found = true; }
                    else result.Encapsulate(local);
                }
        return result;
    }

    private static bool TryGetLocalMeshBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.gameObject.activeInHierarchy) continue;
            Bounds part = FilterBoundsInRoot(root, filter);
            if (!found) { bounds = part; found = true; }
            else bounds.Encapsulate(part);
        }
        return found;
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

    private bool TryGetArmRailPositionForBendingMold(out Vector3 destination)
    {
        destination = armBaseMover.position;
        Vector3 axis;
        float minimumTravel, maximumTravel;
        if (!TryGetArmRailLimits(out axis, out minimumTravel, out maximumTravel))
            return false;
        float projectedTravel = Vector3.Dot(GetBounds(bendingLowerMold).center
            - SuctionContactPoint(), axis);
        float travel = Mathf.Clamp(projectedTravel, minimumTravel, maximumTravel);
        destination += axis * travel;
        if (Mathf.Abs(travel - projectedTravel) > 0.05f)
            Debug.LogWarning("折弯机下模 r1547 的投影超出 r775 行程，基座停在可达端点。", this);
        Debug.Log(string.Format("机械臂对准 r1547：沿 r775 移动 {0:F3}m，下模投影行程 {1:F3}m",
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
        Bounds localFinished;
        if (TryGetLocalMeshBounds(sheet, out localFinished))
        {
            Vector3 e = localFinished.extents;
            finishedTopLocalNormal = e.x < e.y
                ? (e.x < e.z ? Vector3.right : Vector3.forward)
                : (e.y < e.z ? Vector3.up : Vector3.forward);
            if (Vector3.Dot(sheet.TransformDirection(finishedTopLocalNormal),
                Vector3.up) < 0f)
                finishedTopLocalNormal = -finishedTopLocalNormal;
        }
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

#if UNITY_EDITOR
    private void EnsureBendingPieceReferencesForPlay()
    {
        if (bendingPiecePrefabs != null && bendingPiecePrefabs.Length == 5
            && System.Array.TrueForAll(bendingPiecePrefabs, prefab => prefab != null))
            return;
        GameObject[] pieces = new GameObject[5];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = bendingPiecePrefabs != null && i < bendingPiecePrefabs.Length
                ? bendingPiecePrefabs[i] : null;
            if (pieces[i] == null)
                pieces[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Moudles/加工零件 冲压完毕" + (i + 1) + ".fbx");
        }
        bendingPiecePrefabs = pieces;
    }
#endif

    private bool ValidateSetup()
    {
        if (bendingPiecePrefabs == null || bendingPiecePrefabs.Length != 5
            || System.Array.Exists(bendingPiecePrefabs, prefab => prefab == null))
        {
            message = "缺少冲压成品的五个折弯分件引用";
            Debug.LogError("LoadingProcessController: 缺少加工零件 冲压完毕1–5.fbx 引用。", this);
            return false;
        }
        bool valid = rawSheetPrefab != null && finishedPartPrefab != null
            && workpieceRoot != null && materialTable != null
            && armLongSlot != null && armBaseAssembly != null && bendingProbe != null
            && armBaseJoint != null && armSuctionAssembly != null
            && armSegments != null && System.Array.TrueForAll(armSegments, part => part != null)
            && armSliders != null && System.Array.TrueForAll(armSliders, slider => slider != null)
            && bendingBody != null && bendingRail != null
            && bendingUpper != null && bendingUpperMold != null
            && bendingLowerMold != null
            && FindModelNode(armSuctionAssembly, 863) != null
            && FindModelNode(armSuctionAssembly, 857) != null
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
