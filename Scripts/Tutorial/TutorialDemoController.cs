using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

public enum ToolDemoType
{
    Sponge,
    Broom,
    Mop,
    MopRinse,
    Hand,
    Duster,   // 먼지털이
    Flapper,  // 파리채
    Spray,    // 업그레이드 스펀지
    Vacuum,   // 업그레이드 빗자루
    Mop2,      // 업그레이드 대걸레
    TileBrush,  // 화장실솔 (Stage 3)
    Scraper,    // 스크래퍼 (Stage 3)
    ToiletBrush // 변기솔 (Stage 3)
}

public class TutorialDemoController : MonoBehaviour
{
    [Header("도구 오브젝트")]
    public GameObject spongeObject;
    public GameObject broomObject;
    public GameObject mopObject;
    [Tooltip("헹구기 데모에서 표시할 양동이 오브젝트")]
    public GameObject bucketObject;

    [Header("신규 도구 오브젝트 (Stage 1 첫 사용 데모)")]
    public GameObject dusterObject;        // 먼지털이
    public GameObject flapperObject;       // 파리채
    public GameObject spraySpongeObject;   // 업그레이드 스펀지 — 스펀지 파트
    public GameObject sprayBottleObject;   // 업그레이드 스펀지 — 스프레이 파트
    public GameObject vacuumObject;        // 업그레이드 빗자루
    public GameObject mop2Object;          // 업그레이드 대걸레

    [Header("신규 도구 오브젝트 (Stage 3 첫 사용 데모)")]
    public GameObject tileBrushObject;    // 화장실솔
    public GameObject scraperObject;      // 스크래퍼
    public GameObject toiletBrushObject;  // 변기솔

    [Header("신규 도구 앵커")]
    public Transform dusterAnchor;
    public Transform flapperAnchor;
    public Transform sprayAnchor;
    public Transform vacuumAnchor;
    public Transform mop2Anchor;

    [Header("Stage 3 도구 앵커")]
    public Transform tileBrushAnchor;
    public Transform scraperAnchor;
    public Transform toiletBrushAnchor;

    [Header("신규 도구 위치 오프셋")]
    [Tooltip("데모 전체 위치를 카메라 기준으로 이동 (Y를 내리면 데모가 아래로 내려감)\n피벗(손잡이) 위치도 함께 이동함")]
    public Vector3 dusterPositionOffset       = Vector3.zero;
    [Tooltip("헤드가 피벗(앵커)으로 쓰이고 있다면 Y를 음수로 설정해 손잡이 끝이 앵커에 오도록 조정\n예) 파리채 전체 길이 0.4m → Y = -0.4")]
    public Vector3 flapperPositionOffset      = Vector3.zero;
    public Vector3 sprayBottlePositionOffset  = Vector3.zero;  // 스프레이 파트
    public Vector3 vacuumPositionOffset       = Vector3.zero;
    public Vector3 mop2PositionOffset         = Vector3.zero;

    [Header("Stage 3 도구 위치 오프셋")]
    public Vector3 tileBrushPositionOffset   = Vector3.zero;
    public Vector3 scraperPositionOffset     = Vector3.zero;
    public Vector3 toiletBrushPositionOffset = Vector3.zero;

    [Header("신규 도구 회전 오프셋 (Euler)")]
    public Vector3 dusterRotationOffset  = Vector3.zero;
    public Vector3 flapperRotationOffset = Vector3.zero;
    public Vector3 sprayRotationOffset   = Vector3.zero;
    public Vector3 vacuumRotationOffset  = Vector3.zero;
    public Vector3 mop2RotationOffset    = Vector3.zero;

    [Header("Stage 3 도구 회전 오프셋 (Euler)")]
    public Vector3 tileBrushRotationOffset   = Vector3.zero;
    public Vector3 scraperRotationOffset     = Vector3.zero;
    public Vector3 toiletBrushRotationOffset = Vector3.zero;

    [Header("먼지털이 스윙 설정 (손잡이 기준 회전)")]
    [Tooltip("스윙 회전축 (앵커 로컬). (1,0,1) = 대각선 앞뒤 흔들기, (1,0,0) = 좌우, (0,0,1) = 앞뒤")]
    public Vector3 dusterSwingAxis  = new Vector3(1f, 0f, 1f);
    [Tooltip("스윙 최대 각도 (도). 20~35 정도 권장")]
    public float dusterSwingAngle   = 25f;
    [Tooltip("탁탁 느낌 강도. 0=부드러운 사인파, 1에 가까울수록 극값에 머물다 빠르게 전환\n0.2~0.4 권장")]
    [Range(0.05f, 1f)]
    public float dusterSnapSharpness = 0.3f;
    [Tooltip("스윙 방향이 반대로 나올 때 체크")]
    public bool dusterInvertSwing   = false;
    [Tooltip("도구 원점 기준으로 손잡이(피벗) 위치 보정\n원점이 헤드에 있으면 Y를 음수로 설정해 손잡이 끝이 피벗에 오도록 조정")]
    public Vector3 dusterPivotOffset = Vector3.zero;

    [Header("신규 도구 운동 방향 (앵커 로컬, Duster 제외)")]
    [Tooltip("업그레이드 빗자루: 앞뒤 밀기 기본값")]
    public Vector3 vacuumMotionAxis  = new Vector3(0f, 0f, 1f);
    [Tooltip("업그레이드 대걸레: 좌우 기본값")]
    public Vector3 mop2MotionAxis    = new Vector3(1f, 0f, 0f);

    [Header("Stage 3 도구 운동 방향 (앵커 로컬)")]
    [Tooltip("화장실솔: 앞뒤 밀기 (Mop과 동일)")]
    public Vector3 tileBrushMotionAxis   = new Vector3(0f, 0f, 1f);
    [Tooltip("스크래퍼: 좌우 긁기 기본값")]
    public Vector3 scraperMotionAxis     = new Vector3(1f, 0f, 0f);
    [Tooltip("변기솔: 원형 느낌의 대각선 왕복")]
    public Vector3 toiletBrushMotionAxis = new Vector3(1f, 0f, 1f);

    [Header("신규 도구 화살표 앵커")]
    public Transform dusterArrowAnchor;
    public Transform flapperArrowAnchor;
    public Transform sprayArrowAnchor;
    public Transform vacuumArrowAnchor;
    public Transform mop2ArrowAnchor;

    [Header("Stage 3 도구 화살표 앵커")]
    public Transform tileBrushArrowAnchor;
    public Transform scraperArrowAnchor;
    public Transform toiletBrushArrowAnchor;

    [Header("파리채 스윙 설정 (손잡이 기준 회전)")]
    [Tooltip("스윙 회전축 (앵커 로컬). (1,0,0) = 좌우, (0,0,1) = 앞뒤, (1,0,1) = 대각선")]
    public Vector3 flapperSwingAxis   = new Vector3(1f, 0f, 0f);
    [Tooltip("탁 치는 최대 각도 (도). 25~35 권장 — 너무 크면 야구 스윙처럼 보임")]
    public float flapperSwingAngle    = 30f;
    [Tooltip("스윙 방향이 반대로 나올 때 체크")]
    public bool flapperInvertSwing    = false;
    [Tooltip("치는 횟수")]
    public int flapperSwingCount      = 3;
    [Tooltip("내려치는 시간 (초) — 짧을수록 탁 치는 느낌. 0.05~0.08 권장")]
    public float flapperStrikeDuration = 0.06f;
    [Tooltip("치고 난 뒤 잠깐 멈추는 시간 (초)")]
    public float flapperHoldDuration   = 0.08f;
    [Tooltip("복귀 시간 (초)")]
    public float flapperReturnDuration = 0.25f;
    [Tooltip("스윙 사이 대기 시간 (초)")]
    public float flapperSwingPause     = 0.5f;

    [Header("스프레이 데모 — 스펀지 파트 설정")]
    [Tooltip("스펀지가 앵커 로컬 기준 위치할 오프셋")]
    public Vector3 spraySpongePositionOffset = Vector3.zero;
    [Tooltip("스펀지의 앵커 로컬 기준 회전 오프셋 (Euler)")]
    public Vector3 spraySpongeRotationOffset = Vector3.zero;
    [Tooltip("스펀지 닦기 운동 방향 (앵커 로컬). 기본값은 위아래")]
    public Vector3 sprayMotionAxis = new Vector3(0f, 1f, 0f);

    [Header("스프레이 분사 설정")]
    [Tooltip("FX를 지속하는 시간 (초) — 길수록 분사가 오래 지속됨")]
    public float sprayBurstHoldDuration = 0.8f;
    [Tooltip("세트 사이 대기 시간 (초)")]
    public float sprayBurstPause = 0.4f;
    [Tooltip("분사 → 닦기 세트 반복 횟수")]
    public int sprayDemoSets = 2;
    [Tooltip("한 세트당 닦기 지속 시간 (초). 0이면 demoDuration * 0.4 자동 사용")]
    public float sprayWipeDuration = 0f;

    [Header("데모 앵커 (도구가 위치할 Transform)")]
    [Tooltip("플레이어가 잘 볼 수 있는 위치에 배치")]
    public Transform spongeAnchor;
    public Transform broomAnchor;
    public Transform mopAnchor;
    public Transform mopBucketAnchor;

    [Header("도구 위치 오프셋 (앵커 로컬 좌표)")]
    [Tooltip("앵커 기준 위치 오프셋. 예) Y=-0.3 → 아래로 이동")]
    public Vector3 spongePositionOffset = Vector3.zero;
    [Tooltip("앵커 기준 위치 오프셋")]
    public Vector3 broomPositionOffset  = Vector3.zero;
    [Tooltip("앵커 기준 위치 오프셋. 예) Y=0.3 → 위로 올려 헤드가 땅 위로 올라옴")]
    public Vector3 mopPositionOffset    = Vector3.zero;
    [Tooltip("양동이 앵커 기준 위치 오프셋")]
    public Vector3 bucketPositionOffset = Vector3.zero;

    [Header("양동이 회전 오프셋 (Euler)")]
    [Tooltip("양동이 앵커 기본 회전에 더해질 오프셋")]
    public Vector3 bucketRotationOffset = Vector3.zero;

    [Header("방향 화살표 UI")]
    [Tooltip("화살표를 담은 World Space Canvas 또는 오브젝트 루트")]
    public GameObject arrowUIRoot;
    [Tooltip("양의 방향 화살표 Image (위 / 오른쪽 / 앞)")]
    public Image arrowPositiveImage;
    [Tooltip("음의 방향 화살표 Image (아래 / 왼쪽 / 뒤)")]
    public Image arrowNegativeImage;
    [Tooltip("현재 이동 방향 화살표 색상")]
    public Color arrowActiveColor = Color.white;
    [Tooltip("반대 방향 화살표 색상 (흐리게)")]
    public Color arrowInactiveColor = new Color(1f, 1f, 1f, 0.25f);

    [Header("화살표 앵커 (도구별 위치·회전을 씬에서 직접 조정)")]
    [Tooltip("스펀지 데모 화살표가 붙을 빈 오브젝트. Play 중 기즈모로 이동·회전 가능")]
    public Transform spongeArrowAnchor;
    [Tooltip("빗자루 데모 화살표 앵커")]
    public Transform broomArrowAnchor;
    [Tooltip("대걸레 데모 화살표 앵커")]
    public Transform mopArrowAnchor;
    [Tooltip("대걸레 헹구기 데모 화살표 앵커")]
    public Transform mopRinseArrowAnchor;

    [Header("대걸레 헹구기 포인트 설정")]
    [Tooltip("헹구기 데모 시작 시 대걸레 앵커 기준 초기 위치 오프셋. 대걸레 데모(mopPositionOffset)와 별도로 조정 가능")]
    public Vector3 mopRinseStartPositionOffset = Vector3.zero;
    [Tooltip("버킷 앵커 기준, 대걸레가 담그기 직전에 위치할 오프셋 (대걸레 머리가 양동이 입구에 맞게 조정)")]
    public Vector3 mopRinseArrivalOffset = Vector3.zero;
    [Tooltip("담그기 시 내려가는 방향과 거리. 기본값 (0, -0.18, 0) = 아래로 18cm")]
    public Vector3 mopRinseDunkOffset = new Vector3(0f, -0.18f, 0f);

    [Header("도구 앵커 회전 오프셋 (Euler) — 도구 모델 방향")]
    [Tooltip("앵커 기본 회전(플레이어 바라봄)에 더해질 오프셋. 도구 모델의 시각적 기울기를 조정")]
    public Vector3 spongeRotationOffset = Vector3.zero;
    [Tooltip("앵커 기본 회전에 더해질 오프셋")]
    public Vector3 broomRotationOffset  = Vector3.zero;
    [Tooltip("앵커 기본 회전에 더해질 오프셋")]
    public Vector3 mopRotationOffset    = Vector3.zero;

    [Header("도구 운동 방향 (앵커 로컬 좌표) — 왕복 방향")]
    [Tooltip("앵커 로컬 기준: X=좌우, Y=위아래, Z=앞뒤(플레이어 방향)\n예) 대각선 위아래: (1,1,0) / 순수 위아래: (0,1,0)")]
    public Vector3 spongeMotionAxis = new Vector3(0f, 1f, 0f);
    [Tooltip("앵커 로컬 기준: X=좌우, Y=위아래, Z=앞뒤(플레이어 방향)")]
    public Vector3 broomMotionAxis  = new Vector3(1f, 0f, 0f);
    [Tooltip("앵커 로컬 기준: X=좌우, Y=위아래, Z=앞뒤(플레이어 방향)")]
    public Vector3 mopMotionAxis    = new Vector3(0f, 0f, 1f);

    [Header("손(Hand) 데모")]
    [Tooltip("가상 컨트롤러 오브젝트 (반투명 머티리얼 적용 권장)")]
    public GameObject ghostController;
    [Tooltip("가상 컨트롤러가 위치할 앵커")]
    public Transform handAnchor;
    [Tooltip("데모용 LineRenderer (ghostController 자식에 부착)")]
    public LineRenderer demoRay;
    [Tooltip("데모 전용 가짜 쓰레기 프롭 (실제 청소 오브젝트와 별개)")]
    public GameObject demoTrashProp;
    [Tooltip("씬에 배치된 쓰레기통 오브젝트 (데모 시작마다 자동 재배치됨)")]
    public Transform demoTrashBin;
    [Tooltip("쓰레기 프롭 위치 오프셋 (카메라 기준 로컬: X=좌우, Y=위아래, Z=앞뒤)")]
    public Vector3 trashPropPositionOffset = new Vector3(0f, -0.3f, 0.5f);
    [Tooltip("쓰레기통 위치 오프셋 (카메라 기준 로컬: X=좌우, Y=위아래, Z=앞뒤)")]
    public Vector3 binPositionOffset = new Vector3(0.5f, -0.3f, 0.3f);

    [Header("손(Hand) 데모 타이밍 (초)")]
    [Tooltip("레이를 뻗어 쓰레기에 닿기까지 걸리는 시간")]
    public float handRayExtendDuration = 0.8f;
    [Tooltip("쓰레기를 컨트롤러 쪽으로 당겨오는 시간")]
    public float handPullDuration      = 0.5f;
    [Tooltip("컨트롤러가 쓰레기통으로 이동하는 시간")]
    public float handMoveToBinDuration = 1.2f;
    [Tooltip("쓰레기가 입구 위에서 수직으로 낙하하는 시간")]
    public float handDropIntoBinDuration = 0.25f;
    [Tooltip("쓰레기가 낙하하기 직전 멈추는 위치 오프셋 (demoTrashBin 기준: X=좌우, Y=위아래, Z=앞뒤)\n수직 낙하를 위해 Y값을 충분히 주는 것을 권장")]
    public Vector3 binApproachOffset = new Vector3(0f, 0.2f, 0f);

    [Header("모션 설정")]
    [Tooltip("왕복 진폭 (미터)")]
    public float motionAmplitude = 0.06f;
    [Tooltip("왕복 속도 (Hz)")]
    public float motionSpeed = 1.5f;
    [Tooltip("데모 재생 시간 (초)")]
    public float demoDuration = 6f;

    [Header("이즈 인/아웃 설정")]
    [Tooltip("모션 시작 시 속도를 서서히 높이는 구간 (초)")]
    public float easeInDuration = 1.0f;
    [Tooltip("모션 종료 시 속도를 서서히 낮추는 구간 (초)")]
    public float easeOutDuration = 0.8f;

    [Header("데모 리플레이 설정")]
    [Tooltip("첫 데모 종료 후 리플레이 시작 전 대기 시간 (초)")]
    public float replayPauseDelay = 0.7f;
    [Tooltip("리플레이 재생 시간 (초, 0이면 리플레이 없음)")]
    public float replayDuration = 3.0f;

    [Header("레이 색상")]
    public Color rayNormalColor = Color.white;
    public Color rayTriggerColor = Color.yellow;

    [Header("참조")]
    public ToolSwitcher toolSwitcher;
    [Tooltip("XR 카메라 (없으면 Camera.main 자동 사용)")]
    public Camera xrCamera;
    [Tooltip("데모 중 비활성화할 컴포넌트 (이동·회전 Provider 등). Inspector에서 드래그로 등록")]
    public MonoBehaviour[] locomotionComponents;

    [Header("나레이션")]
    [Tooltip("없으면 TutorialNarrationManager.Instance 자동 사용")]
    public TutorialNarrationManager narrationManager;

    [Header("데모 앵커 자동 배치 설정")]
    [Tooltip("플레이어 정면 거리 (m)")]
    public float demoDistance = 0.8f;
    [Tooltip("플레이어 눈 높이 기준 수직 오프셋")]
    public float demoHeightOffset = -0.2f;
    [Tooltip("시뮬레이션 시작 전 대기 시간 (초)")]
    public float demoStartDelay = 3f;
    [Tooltip("Hand 데모 고스트 컨트롤러 높이 오프셋")]
    public float ghostControllerHeightOffset = 0.15f;

    // ─────────────────────────────────────────
    // 외부 호출 진입점
    // ─────────────────────────────────────────

    void Awake()
    {
        if (narrationManager == null)
            narrationManager = TutorialNarrationManager.Instance;
    }

    // overrideDelay < 0 이면 demoStartDelay 사용, 0 이상이면 해당 값 사용
    public Coroutine PlayDemo(ToolDemoType type, float overrideDelay = -1f)
    {
        return StartCoroutine(DemoRoutine(type, overrideDelay));
    }

    IEnumerator DemoRoutine(ToolDemoType type, float overrideDelay = -1f)
    {
        SetPlayerInputEnabled(false);

        float delay = overrideDelay >= 0f ? overrideDelay : demoStartDelay;
        RepositionAnchorToPlayerView(type);
        PlayDemoNarration(type);
        yield return new WaitForSeconds(delay);

        switch (type)
        {
            case ToolDemoType.Sponge:   yield return OscillationDemo(spongeObject,  spongeAnchor,  spongeMotionAxis,  spongePositionOffset,  spongeArrowAnchor);  break;
            case ToolDemoType.Broom:    yield return OscillationDemo(broomObject,   broomAnchor,   broomMotionAxis,   broomPositionOffset,   broomArrowAnchor);   break;
            case ToolDemoType.Mop:      yield return OscillationDemo(mopObject,     mopAnchor,     mopMotionAxis,     mopPositionOffset,     mopArrowAnchor);     break;
            case ToolDemoType.MopRinse: yield return MopRinseDemo();   break;
            case ToolDemoType.Hand:     yield return HandDemo();        break;
            case ToolDemoType.Duster:   yield return DusterSwingDemo(); break;
            case ToolDemoType.Vacuum:   yield return OscillationDemo(vacuumObject,  vacuumAnchor,  vacuumMotionAxis,  vacuumPositionOffset,  vacuumArrowAnchor);  break;
            case ToolDemoType.Mop2:     yield return OscillationDemo(mop2Object,    mop2Anchor,    mop2MotionAxis,    mop2PositionOffset,    mop2ArrowAnchor);    break;
            case ToolDemoType.Flapper:     yield return FlapperSwatDemo(); break;
            case ToolDemoType.Spray:       yield return SprayDemo();       break;
            case ToolDemoType.TileBrush:   yield return OscillationDemo(tileBrushObject,   tileBrushAnchor,   tileBrushMotionAxis,   tileBrushPositionOffset,   tileBrushArrowAnchor);   break;
            case ToolDemoType.Scraper:     yield return OscillationDemo(scraperObject,      scraperAnchor,     scraperMotionAxis,     scraperPositionOffset,     scraperArrowAnchor);     break;
            case ToolDemoType.ToiletBrush: yield return OscillationDemo(toiletBrushObject,  toiletBrushAnchor, toiletBrushMotionAxis,  toiletBrushPositionOffset, toiletBrushArrowAnchor); break;
        }

        SetPlayerInputEnabled(true);
    }

    [Header("데모 나레이션 인덱스")]
    [Tooltip("TutorialSequencer의 narSpongeDemo와 일치해야 함")]
    public int narSpongeDemo  = 5;
    [Tooltip("TutorialSequencer의 narBroomDemo와 일치해야 함")]
    public int narBroomDemo   = 6;
    [Tooltip("TutorialSequencer의 narMopDemo와 일치해야 함")]
    public int narMopDemo     = 7;

    void PlayDemoNarration(ToolDemoType type)
    {
        if (narrationManager == null) return;

        int idx = -1;
        switch (type)
        {
            case ToolDemoType.Sponge:   idx = narSpongeDemo;  break;
            case ToolDemoType.Broom:    idx = narBroomDemo;   break;
            case ToolDemoType.Mop:      idx = narMopDemo;     break;
            // 신규 도구(먼지털이/파리채/업그레이드 도구/Stage3 도구)는 첫 사용 나레이션을 사용하지 않음
            // MopRinse / Hand 나레이션은 TutorialSequencer에서 제어
        }
        if (idx >= 0) narrationManager.Play(idx);
    }

    // 플레이어가 현재 보는 방향 정면에 해당 앵커를 재배치
    void RepositionAnchorToPlayerView(ToolDemoType type)
    {
        Camera cam = xrCamera != null ? xrCamera : Camera.main;
        if (cam == null) return;

        // 수평 forward (고개 숙임 무시)
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 basePos = cam.transform.position
                        + forward * demoDistance
                        + Vector3.up * demoHeightOffset;

        Quaternion facePlayer = Quaternion.LookRotation(-forward); // 플레이어 쪽을 바라봄

        switch (type)
        {
            case ToolDemoType.Sponge:
                if (spongeAnchor) { spongeAnchor.position = basePos; spongeAnchor.rotation = facePlayer * Quaternion.Euler(spongeRotationOffset); }
                break;
            case ToolDemoType.Broom:
                if (broomAnchor) { broomAnchor.position = basePos; broomAnchor.rotation = facePlayer * Quaternion.Euler(broomRotationOffset); }
                break;
            case ToolDemoType.Mop:
                if (mopAnchor) { mopAnchor.position = basePos; mopAnchor.rotation = facePlayer * Quaternion.Euler(mopRotationOffset); }
                break;
            case ToolDemoType.MopRinse:
                if (mopAnchor) { mopAnchor.position = basePos; mopAnchor.rotation = facePlayer * Quaternion.Euler(mopRotationOffset); }
                // 버킷 앵커는 정면 옆에 배치
                if (mopBucketAnchor) mopBucketAnchor.position = basePos + cam.transform.right * 0.4f;
                break;
            case ToolDemoType.Hand:
                if (handAnchor)
                {
                    handAnchor.position = basePos + cam.transform.right * -0.2f + Vector3.up * ghostControllerHeightOffset;
                    handAnchor.rotation = facePlayer;
                }
                if (demoTrashProp) demoTrashProp.transform.position = basePos
                    + cam.transform.right * trashPropPositionOffset.x
                    + Vector3.up          * trashPropPositionOffset.y
                    + forward             * trashPropPositionOffset.z;
                if (demoTrashBin)  demoTrashBin.position = basePos
                    + cam.transform.right * binPositionOffset.x
                    + Vector3.up          * binPositionOffset.y
                    + forward             * binPositionOffset.z;
                break;
            case ToolDemoType.Duster:
                if (dusterAnchor)
                {
                    // dusterPositionOffset을 카메라 기준 월드 오프셋으로 적용 → 앵커(피벗) 자체를 이동
                    Vector3 dusterWorldOffset = cam.transform.right * dusterPositionOffset.x
                                             + Vector3.up           * dusterPositionOffset.y
                                             + forward              * dusterPositionOffset.z;
                    dusterAnchor.position = basePos + dusterWorldOffset;
                    dusterAnchor.rotation = facePlayer * Quaternion.Euler(dusterRotationOffset);
                } break;
            case ToolDemoType.Flapper:
                if (flapperAnchor) { flapperAnchor.position = basePos; flapperAnchor.rotation = facePlayer * Quaternion.Euler(flapperRotationOffset); } break;
            case ToolDemoType.Spray:
                if (sprayAnchor) { sprayAnchor.position = basePos; sprayAnchor.rotation = facePlayer * Quaternion.Euler(sprayRotationOffset); } break;
            case ToolDemoType.Vacuum:
                if (vacuumAnchor)  { vacuumAnchor.position  = basePos; vacuumAnchor.rotation  = facePlayer * Quaternion.Euler(vacuumRotationOffset);  } break;
            case ToolDemoType.Mop2:
                if (mop2Anchor)    { mop2Anchor.position    = basePos; mop2Anchor.rotation    = facePlayer * Quaternion.Euler(mop2RotationOffset);    } break;
            case ToolDemoType.TileBrush:
                if (tileBrushAnchor)   { tileBrushAnchor.position   = basePos; tileBrushAnchor.rotation   = facePlayer * Quaternion.Euler(tileBrushRotationOffset);   } break;
            case ToolDemoType.Scraper:
                if (scraperAnchor)     { scraperAnchor.position     = basePos; scraperAnchor.rotation     = facePlayer * Quaternion.Euler(scraperRotationOffset);     } break;
            case ToolDemoType.ToiletBrush:
                if (toiletBrushAnchor) { toiletBrushAnchor.position = basePos; toiletBrushAnchor.rotation = facePlayer * Quaternion.Euler(toiletBrushRotationOffset); } break;
        }
    }

    // ─────────────────────────────────────────
    // 공통 왕복 모션 (스펀지 / 빗자루 / 대걸레)
    // ─────────────────────────────────────────

    IEnumerator OscillationDemo(GameObject tool, Transform anchor, Vector3 localAxis, Vector3 positionOffset, Transform arrowAnchor)
    {
        if (tool == null || anchor == null) yield break;

        PrepareToolForDemo(tool, anchor, positionOffset);
        ShowArrowUI(arrowAnchor);

        yield return OscillationPass(tool, localAxis, positionOffset, demoDuration);

        if (replayDuration > 0f)
        {
            tool.transform.localPosition = positionOffset;
            yield return new WaitForSeconds(replayPauseDelay);
            yield return OscillationPass(tool, localAxis, positionOffset, replayDuration);
        }

        tool.transform.localPosition = positionOffset;
        HideArrowUI();
        ReturnToolAfterDemo(tool);
    }

    IEnumerator OscillationPass(GameObject tool, Vector3 localAxis, Vector3 positionOffset, float duration)
    {
        Vector3 axis = localAxis.sqrMagnitude > 0f ? localAxis.normalized : Vector3.up;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float easeIn  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / easeInDuration));
            float easeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - elapsed) / easeOutDuration));
            float sinValue = Mathf.Sin(elapsed * motionSpeed * Mathf.PI * 2f);
            float offset   = sinValue * motionAmplitude * (easeIn * easeOut);
            tool.transform.localPosition = positionOffset + axis * offset;

            UpdateArrowColors(sinValue);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    void ShowArrowUI(Transform arrowAnchor)
    {
        if (arrowUIRoot == null || arrowAnchor == null) return;
        // 부모 변경 없이 위치·회전만 복사 → 스케일 상속 문제 없음
        arrowUIRoot.transform.position = arrowAnchor.position;
        arrowUIRoot.transform.rotation = arrowAnchor.rotation;
        arrowUIRoot.SetActive(true);
        if (arrowPositiveImage) arrowPositiveImage.color = arrowInactiveColor;
        if (arrowNegativeImage) arrowNegativeImage.color = arrowInactiveColor;
    }

    void HideArrowUI()
    {
        if (arrowUIRoot == null) return;
        arrowUIRoot.SetActive(false);
    }

    void UpdateArrowColors(float sinValue)
    {
        if (arrowPositiveImage != null)
            arrowPositiveImage.color = Color.Lerp(arrowInactiveColor, arrowActiveColor, Mathf.Clamp01(sinValue));
        if (arrowNegativeImage != null)
            arrowNegativeImage.color = Color.Lerp(arrowInactiveColor, arrowActiveColor, Mathf.Clamp01(-sinValue));
    }

    // ─────────────────────────────────────────
    // 대걸레 헹구기 모션
    // ─────────────────────────────────────────

    IEnumerator MopRinseDemo()
    {
        if (mopObject == null || mopAnchor == null || mopBucketAnchor == null) yield break;

        // 양동이 배치
        if (bucketObject != null)
        {
            bucketObject.SetActive(true);
            Rigidbody bucketRb = bucketObject.GetComponent<Rigidbody>();
            if (bucketRb != null) bucketRb.isKinematic = true;
            bucketObject.transform.SetParent(mopBucketAnchor, false);
            bucketObject.transform.localPosition = bucketPositionOffset;
            bucketObject.transform.localRotation = Quaternion.Euler(bucketRotationOffset);
        }

        PrepareToolForDemo(mopObject, mopAnchor, mopRinseStartPositionOffset);
        ShowArrowUI(mopRinseArrowAnchor);

        // mopRinseArrivalOffset은 앵커 로컬 좌표 → 플레이어 방향과 무관하게 일정한 상대 위치 유지
        Vector3 dunkTop    = mopBucketAnchor.TransformPoint(mopRinseArrivalOffset);
        Vector3 dunkBottom = dunkTop + mopBucketAnchor.TransformDirection(mopRinseDunkOffset);

        // 앵커에서 버킷으로 이동 (mopRinseStartPositionOffset이 적용된 월드 좌표 기준)
        Vector3 mopRinseStart = mopAnchor.TransformPoint(mopRinseStartPositionOffset);
        yield return MoveTool(mopObject.transform, mopRinseStart, dunkTop, 0.8f);

        // 버킷 담그기 3회씩 2세트
        for (int set = 0; set < 2; set++)
        {
            for (int i = 0; i < 3; i++)
            {
                yield return MoveToolWithArrows(mopObject.transform, dunkTop, dunkBottom, 0.35f, -1f);
                yield return MoveToolWithArrows(mopObject.transform, dunkBottom, dunkTop,  0.35f, +1f);
                yield return new WaitForSeconds(0.1f);
            }
            if (set < 1) yield return new WaitForSeconds(0.4f); // 세트 사이 짧은 정지
        }

        // 원래 위치로 복귀
        yield return MoveTool(mopObject.transform, dunkTop, mopRinseStart, 0.6f);

        HideArrowUI();
        ReturnToolAfterDemo(mopObject);

        // 양동이 정리
        if (bucketObject != null)
        {
            bucketObject.transform.SetParent(null);
            bucketObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────
    // 손(Hand) — 가상 컨트롤러 + 레이 → 트리거 효과
    // ─────────────────────────────────────────

    IEnumerator HandDemo()
    {
        if (ghostController == null || handAnchor == null || demoTrashProp == null || demoTrashBin == null)
        {
            Debug.LogError("[HandDemo] ghostController / handAnchor / demoTrashProp / demoTrashBin 확인");
            yield break;
        }

        // ── 초기화 ────────────────────────────────────────────────────
        var trashInteractable = demoTrashProp.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (trashInteractable != null) trashInteractable.enabled = false;
        foreach (var col in demoTrashProp.GetComponentsInChildren<Collider>())
            col.enabled = false;
        demoTrashProp.SetActive(true);
        if (demoTrashBin != null) demoTrashBin.gameObject.SetActive(true);
        Vector3 trashStartPos = demoTrashProp.transform.position;

        ghostController.SetActive(true);
        ghostController.transform.SetParent(handAnchor, false);
        ghostController.transform.localPosition = Vector3.zero;
        ghostController.transform.localRotation = Quaternion.identity;

        bool demoRayIsControllerVisual = IsControllerChild(demoRay != null ? demoRay.gameObject : null);
        if (demoRay != null)
        {
            if (!demoRayIsControllerVisual) demoRay.gameObject.SetActive(true);
            demoRay.positionCount = 2;
        }

        // ── Phase 1: 레이를 쓰레기까지 뻗기 ──────────────────────────
        float elapsed = 0f;
        while (elapsed < handRayExtendDuration)
        {
            float t = elapsed / handRayExtendDuration;

            Vector3 dir = (trashStartPos - ghostController.transform.position).normalized;
            ghostController.transform.rotation = Quaternion.Slerp(
                ghostController.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 8f);

            if (demoRay != null)
            {
                demoRay.SetPosition(0, ghostController.transform.position);
                demoRay.SetPosition(1, Vector3.Lerp(ghostController.transform.position, trashStartPos, t));
                demoRay.startColor = rayNormalColor;
                demoRay.endColor   = rayNormalColor;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 레이가 닿은 순간 색상 변경
        if (demoRay != null)
        {
            demoRay.SetPosition(1, trashStartPos);
            demoRay.startColor = rayTriggerColor;
            demoRay.endColor   = rayTriggerColor;
        }
        yield return new WaitForSeconds(0.15f);

        // ── Phase 2: 쓰레기를 컨트롤러 쪽으로 당겨옴 ────────────────
        Vector3 pullTarget = ghostController.transform.position;
        elapsed = 0f;
        while (elapsed < handPullDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / handPullDuration);
            demoTrashProp.transform.position = Vector3.Lerp(trashStartPos, pullTarget, t);

            if (demoRay != null)
            {
                demoRay.SetPosition(0, ghostController.transform.position);
                demoRay.SetPosition(1, demoTrashProp.transform.position);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 레이 숨기고 쓰레기를 컨트롤러에 붙임
        if (demoRay != null)
        {
            if (!demoRayIsControllerVisual) demoRay.gameObject.SetActive(false);
            else { demoRay.SetPosition(0, Vector3.zero); demoRay.SetPosition(1, Vector3.zero); }
        }
        demoTrashProp.transform.SetParent(ghostController.transform, true);

        // ── Phase 3: 컨트롤러 + 쓰레기를 쓰레기통 방향으로 이동 ─────
        ghostController.transform.SetParent(null, true); // 월드 공간에서 자유롭게 이동
        Vector3 controllerStart = ghostController.transform.position;
        Vector3 binApproach     = demoTrashBin.position + binApproachOffset;
        elapsed = 0f;
        while (elapsed < handMoveToBinDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / handMoveToBinDuration);
            ghostController.transform.position = Vector3.Lerp(controllerStart, binApproach, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ── Phase 4: 컨트롤러 멈춘 위치(binApproach)에서 수직 낙하 ──
        demoTrashProp.transform.SetParent(null, true);
        Vector3 trashDropStart = demoTrashProp.transform.position;
        elapsed = 0f;
        while (elapsed < handDropIntoBinDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / handDropIntoBinDuration);
            demoTrashProp.transform.position = Vector3.Lerp(trashDropStart, demoTrashBin.position, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        demoTrashProp.SetActive(false);
        if (demoTrashBin != null) demoTrashBin.gameObject.SetActive(false);

        // ── 정리 ──────────────────────────────────────────────────────
        ghostController.transform.SetParent(null);
        ghostController.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 먼지털이 — 손잡이 기준 회전 스윙
    // ─────────────────────────────────────────

    IEnumerator DusterSwingDemo()
    {
        if (dusterObject == null || dusterAnchor == null) yield break;

        // 피벗 오프셋이 있으면 앵커 아래에 빈 피벗 오브젝트를 만들어서 도구를 자식으로 붙임
        Transform pivot;
        GameObject pivotGO = null;
        // dusterPositionOffset은 앵커 위치에 이미 적용됨 (RepositionAnchorToPlayerView)
        // dusterPivotOffset만 도구의 손잡이-피벗 정렬에 사용
        if (dusterPivotOffset != Vector3.zero)
        {
            pivotGO = new GameObject("_DusterPivot");
            pivotGO.transform.SetParent(dusterAnchor, false);
            pivotGO.transform.localPosition = Vector3.zero;
            pivotGO.transform.localRotation = Quaternion.identity;
            pivot = pivotGO.transform;
            PrepareToolForDemo(dusterObject, pivot, dusterPivotOffset);
        }
        else
        {
            pivot = dusterAnchor;
            PrepareToolForDemo(dusterObject, pivot, Vector3.zero);
        }

        Quaternion dusterBaseRot = pivot.rotation;
        ShowArrowUI(dusterArrowAnchor);

        yield return DusterSwingPass(pivot, dusterBaseRot, demoDuration);

        if (replayDuration > 0f)
        {
            pivot.rotation = dusterBaseRot;
            yield return new WaitForSeconds(replayPauseDelay);
            yield return DusterSwingPass(pivot, dusterBaseRot, replayDuration);
        }

        pivot.rotation = dusterBaseRot;
        HideArrowUI();
        ReturnToolAfterDemo(dusterObject);

        if (pivotGO != null) Destroy(pivotGO);
    }

    IEnumerator DusterSwingPass(Transform pivot, Quaternion baseRot, float duration)
    {
        Vector3 localAxis = dusterSwingAxis.sqrMagnitude > 0f ? dusterSwingAxis.normalized : Vector3.right;
        if (dusterInvertSwing) localAxis = -localAxis;
        // 로컬 축을 월드 축으로 변환하여 기준 회전 위에 정확히 더함
        Vector3 worldAxis = baseRot * localAxis;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float easeIn  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / easeInDuration));
            float easeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((duration - elapsed) / easeOutDuration));
            float sinRaw   = Mathf.Sin(elapsed * motionSpeed * Mathf.PI * 2f);
            // sharpness < 1 → 극값에 오래 머물다 빠르게 전환 (탁탁 느낌)
            float sinValue = Mathf.Sign(sinRaw) * Mathf.Pow(Mathf.Abs(sinRaw), dusterSnapSharpness);
            float angle    = sinValue * dusterSwingAngle * (easeIn * easeOut);

            pivot.rotation = Quaternion.AngleAxis(angle, worldAxis) * baseRot;

            UpdateArrowColors(sinValue);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ─────────────────────────────────────────
    // 파리채 — 손잡이 기준 회전 타격 (탁 치는 느낌)
    // ─────────────────────────────────────────

    IEnumerator FlapperSwatDemo()
    {
        if (flapperObject == null || flapperAnchor == null) yield break;

        Transform pivot = flapperAnchor;
        PrepareToolForDemo(flapperObject, pivot, flapperPositionOffset);

        Quaternion flapperBaseRot = pivot.rotation;
        ShowArrowUI(flapperArrowAnchor);

        for (int i = 0; i < flapperSwingCount; i++)
        {
            yield return FlapperStrike(pivot, flapperBaseRot);
            if (i < flapperSwingCount - 1)
                yield return new WaitForSeconds(flapperSwingPause);
        }

        pivot.rotation = flapperBaseRot;
        HideArrowUI();
        ReturnToolAfterDemo(flapperObject);
    }

    IEnumerator FlapperStrike(Transform pivot, Quaternion baseRot)
    {
        Vector3 localAxis = flapperSwingAxis.sqrMagnitude > 0f ? flapperSwingAxis.normalized : Vector3.right;
        if (flapperInvertSwing) localAxis = -localAxis;
        Vector3 worldAxis = baseRot * localAxis;

        void SetAngle(float angle) =>
            pivot.rotation = Quaternion.AngleAxis(angle, worldAxis) * baseRot;

        // 예비 동작: 반대로 살짝 젖히기
        float windUpAngle = flapperSwingAngle * 0.25f;
        float windUpTime  = flapperStrikeDuration * 2f;
        float elapsed = 0f;
        while (elapsed < windUpTime)
        {
            elapsed += Time.deltaTime;
            SetAngle(-windUpAngle * (elapsed / windUpTime));
            yield return null;
        }

        // 빠르게 내려치기
        elapsed = 0f;
        while (elapsed < flapperStrikeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Pow(elapsed / flapperStrikeDuration, 0.4f);
            SetAngle(Mathf.Lerp(-windUpAngle, flapperSwingAngle, t));
            UpdateArrowColors(1f);
            yield return null;
        }
        SetAngle(flapperSwingAngle);

        // 타격 후 잠깐 멈춤
        yield return new WaitForSeconds(flapperHoldDuration);
        UpdateArrowColors(0f);

        // 천천히 원위치 복귀
        elapsed = 0f;
        while (elapsed < flapperReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / flapperReturnDuration);
            SetAngle(Mathf.Lerp(flapperSwingAngle, 0f, t));
            yield return null;
        }
        pivot.rotation = baseRot;
    }

    // ─────────────────────────────────────────
    // 업그레이드 스펀지 — 분사(스프레이) + 닦기(스펀지)
    // ─────────────────────────────────────────

    IEnumerator SprayDemo()
    {
        if (sprayAnchor == null) yield break;
        if (spraySpongeObject == null && sprayBottleObject == null) yield break;

        // 두 오브젝트 모두 실제 게임 도구이므로, 데모 후 원래 상태로 복귀해야 함
        Transform bottleOriginalParent = null;
        Vector3   bottleOriginalLocalPos = Vector3.zero;
        Quaternion bottleOriginalLocalRot = Quaternion.identity;
        bool bottleWasActive = false;
        if (sprayBottleObject != null)
        {
            bottleOriginalParent   = sprayBottleObject.transform.parent;
            bottleOriginalLocalPos = sprayBottleObject.transform.localPosition;
            bottleOriginalLocalRot = sprayBottleObject.transform.localRotation;
            bottleWasActive        = sprayBottleObject.activeSelf;
        }

        Transform spongeOriginalParent = null;
        Vector3   spongeOriginalLocalPos = Vector3.zero;
        Quaternion spongeOriginalLocalRot = Quaternion.identity;
        bool spongeWasActive = false;
        if (spraySpongeObject != null)
        {
            spongeOriginalParent   = spraySpongeObject.transform.parent;
            spongeOriginalLocalPos = spraySpongeObject.transform.localPosition;
            spongeOriginalLocalRot = spraySpongeObject.transform.localRotation;
            spongeWasActive        = spraySpongeObject.activeSelf;
        }

        // SprayCleaner.Update()가 triggerValue=0으로 판단해 sprayEffect.Stop()을 호출하므로
        // yield 전에 미리 비활성화해 첫 번째 세트부터 간섭을 완전히 차단한다
        SprayCleaner sprayCleaner = null;
        {
            SprayCleaner[] all = Resources.FindObjectsOfTypeAll<SprayCleaner>();
            foreach (var sc in all) { if (sc.gameObject.scene.isLoaded) { sprayCleaner = sc; break; } }
        }
        bool sprayCleanerWasEnabled = sprayCleaner != null && sprayCleaner.enabled;
        if (sprayCleaner != null) sprayCleaner.enabled = false;

        // 두 오브젝트 모두 앵커에 배치 (동시 표시)
        if (spraySpongeObject != null)
        {
            PrepareToolForDemo(spraySpongeObject, sprayAnchor, spraySpongePositionOffset);
            spraySpongeObject.transform.localRotation = Quaternion.Euler(spraySpongeRotationOffset);
        }
        if (sprayBottleObject != null) PrepareToolForDemo(sprayBottleObject, sprayAnchor, sprayBottlePositionOffset);

        // sprayBottleObject가 처음 활성화되는 경우, SprayCleaner.Start()가
        // 한 프레임 뒤에 실행되어 SetParent(leftHandController)로 되돌려버림.
        // 한 프레임 대기 후 Start()가 끝난 다음 위치를 재설정한다.
        yield return null;
        if (sprayBottleObject != null) PrepareToolForDemo(sprayBottleObject, sprayAnchor, sprayBottlePositionOffset);

        ShowArrowUI(sprayArrowAnchor);

        ParticleSystem sprayFx = sprayBottleObject != null
            ? sprayBottleObject.GetComponentInChildren<ParticleSystem>()
            : null;
        // 이전 상태가 남아 있을 수 있으므로 루프 시작 전 깨끗하게 리셋
        if (sprayFx != null)
            sprayFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float wipeTime = sprayWipeDuration > 0f ? sprayWipeDuration : demoDuration * 0.4f;

        // 분사 → 닦기 세트 반복
        for (int set = 0; set < sprayDemoSets; set++)
        {
            // 분사 (길게 한 번)
            if (sprayBottleObject != null)
            {
                if (sprayFx != null) sprayFx.Play();
                yield return SprayBurst();
                if (sprayFx != null) sprayFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            yield return new WaitForSeconds(0.3f);

            // 닦기
            if (spraySpongeObject != null)
            {
                yield return OscillationPass(spraySpongeObject, sprayMotionAxis, spraySpongePositionOffset, wipeTime);
                spraySpongeObject.transform.localPosition = spraySpongePositionOffset;
            }

            // 세트 사이 대기 (마지막 세트 제외)
            if (set < sprayDemoSets - 1)
                yield return new WaitForSeconds(sprayBurstPause);
        }

        HideArrowUI();

        // SprayCleaner 복원
        if (sprayCleaner != null) sprayCleaner.enabled = sprayCleanerWasEnabled;

        // 두 도구 모두 원래 부모/위치/회전/활성 상태로 복귀
        if (spraySpongeObject != null)
        {
            var interactable = spraySpongeObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (interactable != null) interactable.enabled = true;
            foreach (var col in spraySpongeObject.GetComponentsInChildren<Collider>())
                col.enabled = true;
            spraySpongeObject.transform.SetParent(spongeOriginalParent);
            spraySpongeObject.transform.localPosition = spongeOriginalLocalPos;
            spraySpongeObject.transform.localRotation = spongeOriginalLocalRot;
            spraySpongeObject.SetActive(spongeWasActive);
        }
        if (sprayBottleObject != null)
        {
            var interactable = sprayBottleObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (interactable != null) interactable.enabled = true;
            foreach (var col in sprayBottleObject.GetComponentsInChildren<Collider>())
                col.enabled = true;
            sprayBottleObject.transform.SetParent(bottleOriginalParent);
            sprayBottleObject.transform.localPosition = bottleOriginalLocalPos;
            sprayBottleObject.transform.localRotation = bottleOriginalLocalRot;
            sprayBottleObject.SetActive(bottleWasActive);
        }
    }

    IEnumerator SprayBurst()
    {
        yield return new WaitForSeconds(sprayBurstHoldDuration);
    }

    // 트리거 당기는 느낌 — 컨트롤러 살짝 앞으로 튕김
    IEnumerator TriggerPulse()
    {
        Vector3 origin = ghostController.transform.localPosition;
        Vector3 forward = ghostController.transform.forward * 0.03f;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.08f;
            ghostController.transform.localPosition = origin + forward * Mathf.Sin(t * Mathf.PI);
            yield return null;
        }
        ghostController.transform.localPosition = origin;
    }

    IEnumerator FlashThenRemoveProp()
    {
        if (demoTrashProp == null) yield break;
        Renderer r = demoTrashProp.GetComponent<Renderer>();

        if (r != null)
        {
            Color original = r.material.color;
            r.material.color = Color.yellow;
            yield return new WaitForSeconds(0.2f);
            r.material.color = original;
        }

        yield return new WaitForSeconds(0.1f);
        demoTrashProp.SetActive(false);
    }

    // ─────────────────────────────────────────
    // 헬퍼
    // ─────────────────────────────────────────

    void PrepareToolForDemo(GameObject tool, Transform anchor, Vector3 positionOffset = default)
    {
        tool.SetActive(true);

        Rigidbody rb = tool.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        // XRBaseInteractable + 모든 Collider 비활성화
        // Interactable만 끄면 Collider가 살아있어 NearFarInteractor가 Near 모드로 전환되어 Far Ray가 사라짐
        var interactable = tool.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable != null) interactable.enabled = false;
        foreach (var col in tool.GetComponentsInChildren<Collider>())
            col.enabled = false;

        tool.transform.SetParent(anchor, false);
        tool.transform.localPosition = positionOffset;
        tool.transform.localRotation = Quaternion.identity;
    }

    void ReturnToolAfterDemo(GameObject tool)
    {
        var interactable = tool.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();

        // FirstUseDemoTrigger가 실제로 손에 장착된 도구로 데모를 돌린 경우(스테이지 실플레이 중 첫 사용) —
        // 데모 끝나고 숨기지 말고 원래 있던 손 위치로 다시 붙여서 계속 들려있게 한다.
        if (toolSwitcher != null && toolSwitcher.GetCurrentToolObject() == tool)
        {
            if (interactable != null) interactable.enabled = false; // 장착 중엔 ToolSwitcher와 동일하게 꺼둠
            foreach (var col in tool.GetComponentsInChildren<Collider>())
                col.enabled = false;

            tool.SetActive(true);
            toolSwitcher.AttachToolToController();
            return;
        }

        // 튜토리얼 자체 시범(아직 장착되지 않은 도구) — 기존대로 숨긴다.
        if (interactable != null) interactable.enabled = true;
        foreach (var col in tool.GetComponentsInChildren<Collider>())
            col.enabled = true;

        tool.transform.SetParent(null);
        tool.SetActive(false);
    }

    IEnumerator MoveTool(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            t.position = Vector3.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.position = to;
    }

    // 이동하는 동안 화살표를 방향에 맞춰 업데이트 (arrowSign: +1=위, -1=아래)
    IEnumerator MoveToolWithArrows(Transform t, Vector3 from, Vector3 to, float duration, float arrowSign)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            t.position = Vector3.Lerp(from, to, elapsed / duration);
            float pulse = Mathf.Sin(elapsed / duration * Mathf.PI); // 이동 구간 중앙에서 최대
            UpdateArrowColors(arrowSign * pulse);
            elapsed += Time.deltaTime;
            yield return null;
        }
        t.position = to;
        UpdateArrowColors(0f); // 이동 끝나면 양쪽 모두 흐리게
    }

    private bool _locomotionLockedBySequencer = false;

    // TutorialSequencer에서 씬 전체 구간 로코모션 제어용
    public void SetLocomotionEnabled(bool enabled)
    {
        _locomotionLockedBySequencer = !enabled;
        ApplyLocomotion(enabled);
    }

    void ApplyLocomotion(bool enabled)
    {
        Transform rightHand = toolSwitcher != null ? toolSwitcher.rightHandController : null;

        if (locomotionComponents != null)
            foreach (var comp in locomotionComponents)
            {
                if (comp == null) continue;
                if (rightHand != null && comp.transform.IsChildOf(rightHand)) continue;
                comp.enabled = enabled;
            }
    }

    void SetPlayerInputEnabled(bool enabled)
    {
        if (toolSwitcher != null)
        {
            toolSwitcher.demoMode = !enabled;
            // 패드 X버튼 차단은 여기서 걸기만 한다. 해제는 TutorialSequencer가 따라하기(practice) 진입 시에만
            // 하므로, 시범이 끝났다고 여기서 같이 풀어버리면 시범 사이/나레이션 중에 패드를 열 수 있게 된다.
            if (!enabled)
            {
                toolSwitcher.padPanelToggle?.SetInputBlocked(true);
                toolSwitcher.CloseAllUI();
                toolSwitcher.LockAllPadButtons();
            }
        }

        // 시퀀서가 로코모션을 잠근 상태면 데모 종료 시에도 재활성화하지 않음
        if (enabled && _locomotionLockedBySequencer) return;
        ApplyLocomotion(enabled);
    }

    bool IsControllerChild(GameObject obj)
    {
        if (obj == null || toolSwitcher == null || toolSwitcher.rightHandController == null)
            return false;
        return obj.transform.IsChildOf(toolSwitcher.rightHandController);
    }
}
