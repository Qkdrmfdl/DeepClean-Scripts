# Deep Clean — 게임 로직 코드

VR 청소 시뮬레이션 「Deep Clean」(남서울대학교 가상현실학과 졸업작품, 3인 팀)의 C# 스크립트입니다.
팀에서 개발을 전담한 윤채원이 맡은 게임 로직 전체를 열람용으로 공개합니다.

- 트레일러 : https://youtu.be/JXn4lkIHW5g
- 30초 시연 : https://youtu.be/dTiRBFAPysA

> 씬 · 3D 모델 · 사운드는 팀 저장소(비공개)에 있어서, 이 저장소만으로는 실행되지 않습니다.

## 개발 환경

| 항목 | 내용 |
| --- | --- |
| 엔진 | Unity 6000.3.10f1 |
| XR | XR Interaction Toolkit 3.3.1, OpenXR · Oculus XR Plugin |
| 렌더링 | URP |
| 기기 | Meta Quest 3 (빌드 · Quest Link) |
| 기간 | 2025.09 – 2026.09 |

## 무엇이 어디에 있나

| 시스템 | 파일 |
| --- | --- |
| 도구 11종의 손동작 판정 | `BroomCleaner` `MopCleaner` `VacuumCleaner` `SpongeCleaner` `SprayCleaner` `ScraperCleaner` `DusterCleaner` `FlapperCleaner` `ToiletBrushCleaner` `HandCleaner` `WallPaint` |
| 쓸수록 더러워지는 도구와 물통 | `WaterSystem` `BucketAttacher` `BucketDropZone` |
| 들고 있는 도구로 치울 것만 강조 | `CleanTargetHighlighter` `ToolHighlighter` |
| 튜토리얼 (단계 목록 · 손 시범 · 나레이션) | `Tutorial/` |
| 청소율 · 랭크 · 보상 · 상점 | `ScoreManager` `MoneyManager` `StageTimer` `TotalResultSequencer` `ShopManager` |
| 트렁크에서 도구 고르기 | `TrunkManager` `TrunkToolGrabber` `ToolPickup` `ToolMagnetAttract` `ToolSwitcher` |
| 현장 이동 · 씬 전환 | `AutoDrive` `StageFlowManager` `SceneTransitionManager` `PlayerSeater` |
| 인트로 · 아웃트로 · 가구 배치 | `Intro/` `Outro/` `Interial/` |

## 빗자루 판정 — 손이 아니라 바닥 기준으로

쥔 손의 X축으로 좌우를 읽으면, 손목을 돌릴 때 좌우도 같이 돌아서 사람마다 인식률이 달랐습니다.
바닥 위 수평 속도와 방향이 꺾인 각도로 왕복을 세도록 바꿔, 어떻게 쥐어도 같은 값이 나오게 했습니다. (`BroomCleaner.cs`)

```csharp
// 간략화한 핵심 (실제 코드는 BroomCleaner.cs 321행 부근, 임계값 sweepReverseDot)
// 고치기 전: 쥔 손 기준
float h = hand.InverseTransformDirection(v).x;

// 고친 뒤: 바닥(월드 XZ) 기준, 약 107° 넘게 꺾이면 왕복 1회
Vector3 h = new Vector3(v.x, 0, v.z);
if (Vector3.Dot(last, h) < -0.3f) count++;
```

## 만든 방식

코드는 AI 코딩 도구(Claude)와 함께 작성했습니다.
문제 발견과 팀원 테스트, 재현 조건 좁히기, Unity 에디터 세팅, 실기기 검증, AI의 답이 틀렸는지 판단하는 일은 직접 했습니다.

## 팀

- 윤채원 — 개발 전담 (게임 로직 · 빌드), 협업 툴 관리
- 팀원 2명 — 3D 모델 · 얼룩 텍스처 · 사운드, 맵 배치 · 라이팅 · UI (나레이션 위치 · 볼륨 등 일부 스크립트 수정 포함)
