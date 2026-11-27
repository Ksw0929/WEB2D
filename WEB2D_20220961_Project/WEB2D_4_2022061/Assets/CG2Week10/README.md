# 10주차 패키지 — Shader Graph 기초 (피격 효과)

8주차까지 완료한 프로젝트(Linear 색 공간 · 노멀 맵 · 밤 장면 · 조명과 그림자)에 임포트합니다.
모든 메뉴는 `CG2 Lab → 10주차`에 **수업 순서대로** 있고, Console에 `[실습]` `[측정]` `[검증]` 로그를 남깁니다.

## 메뉴 (위에서 아래로 누르면 수업 순서)

| 메뉴 | 어디서 | 하는 일 |
|---|---|---|
| `1-1. 측정 #1` | 게임 씬 · **Play 중** | 지금의 피격 플래시(SpriteRenderer.color = 흰색)가 화면을 얼마나 바꾸는지 잰다 |
| `1-2. 그래프 검사` | 어디서나 | `Shaders/SG_HitFlash` 파일 · 대상(Sprite Lit) · 컴파일 |
| `2-1. 속성 검사` | 어디서나 | 셰이더가 실제로 가진 속성 이름(`_MainTex` · `_FlashAmount` · `_FlashColor`) |
| `2-2. 측정 #2` | **Play 중** | FlashAmount 0~1에서 Lerp 계산이 맞는지 · 그림 바깥이 칠해지는지 |
| `3-1. 측정 #3` | **Play 중** | 밝은 곳 · 그늘 · 어두운 곳에서 플래시 밝기 (흰색 · HDR Intensity 1 · 2) |
| `4-1. 머티리얼 검사` | 어디서나 | `Materials/M_HitFlash`의 셰이더 · Flash Color Intensity |
| `4-2. 적 · 플레이어에 적용` | 게임 씬 | Enemy 프리팹 · 씬의 Player · Enemy 머티리얼을 M_HitFlash로 |
| `4-3. 측정 #4` | 게임 씬 · **Play 중** | 바꾸기 전후 DrawCalls · SetPass · 노멀 맵 반응 |
| `5-1. 측정 #5` | 게임 씬 · **Play 중** | HitReceiver.OnHit() → _FlashAmount → 화면 변화 · README 캡처 |
| `5-2. 측정 #6` | 게임 씬 · **Play 중** | 적 20마리 — 공유 · MaterialPropertyBlock · .material 복제 비용 |
| `5-3. 체크포인트` | 게임 씬 | 그래프 · 머티리얼 · 적용 · 스크립트 · 측정 점검 |
| `되돌리기 — …` | | 머티리얼을 Sprite-Lit-Default로 · HitReceiver 원본 |
| `(막혔을 때) …` | | 정답 그래프 · 머티리얼 만들기 / HitReceiver 정답으로 바꾸기 |

## 학생이 직접 만드는 것

| 파일 | 만드는 곳 |
|---|---|
| `Assets/CG2Week10/Shaders/SG_HitFlash.shadergraph` | Project 창 우클릭 → `Create → Shader Graph → URP → Sprite Lit Shader Graph` |
| `Assets/CG2Week10/Materials/M_HitFlash.mat` | SG_HitFlash 우클릭 → `Create → Material` |
| `Assets/StarterProject/Scripts/HitReceiver.cs` 수정 | 수업 내용 5단계 코드 |

## 실행하면 생기는 파일

`Captures/` 피격 전후 그림 · `Backup/` HitReceiver 원본(정답 메뉴를 쓴 경우) — 측정 기록은 `Library/CG2Week10/`(깃에 올라가지 않음)

## 주의

- 그래프를 고친 뒤에는 그래프 창 왼쪽 위 **Save Asset**을 눌러야 셰이더에 반영됩니다.
- 속성의 **Reference**(참조 이름)가 스크립트와 SpriteRenderer가 부르는 이름입니다. 틀려도 오류는 나지 않습니다. `2-1`로 확인합니다.
- `(막혔을 때)` 정답 그래프는 Shader Graph 내부 API를 리플렉션으로 호출해 만듭니다(Unity 6.6 · Shader Graph 17.6에서 확인).
