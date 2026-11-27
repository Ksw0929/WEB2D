# 11주차 패키지 — Shader Graph 응용 (Dissolve 소멸 연출)

10주차까지 완료한 프로젝트(SG_HitFlash · M_HitFlash · HitReceiver 수정)에 임포트합니다.
모든 메뉴는 `CG2 Lab → 11주차`에 **수업 순서대로** 있고, Console에 `[실습]` `[비교]` `[측정]` `[검증]` 로그를 남깁니다.

## 메뉴 (위에서 아래로 누르면 수업 순서)

| 메뉴 | 어디서 | 하는 일 |
|---|---|---|
| `1-1. 측정 #1` | 게임 씬 · **Play 중** | 지금 적이 죽을 때 몇 프레임 만에 사라지는지 잰다 |
| `1-2. 비교` | 게임 씬 | 같은 진행도 0.5에서 일반 Alpha · Alpha Clipping만 · 디졸브를 나란히 그린다 |
| `2-1. 디졸브 그래프 검사` | 어디서나 | 속성 이름 · Alpha Clipping · 진행도 0과 1로 실제로 그려 본다 |
| `2-2. 측정 #2` | 게임 씬 | 진행도 0~1에서 그림이 얼마나 남는지 · Remap 권장값 |
| `3-1. 측정 #3` | 게임 씬 · **Play 중** | 경계 두께 · 경계가 움직이는지 · 어두운 곳의 경계 밝기 |
| `4-1. 적에 M_Dissolve 적용` | 게임 씬 | Enemy 프리팹 · 씬의 적 머티리얼을 M_Dissolve로 |
| `4-2. 측정 #4` | 게임 씬 · **Play 중** | 사망 → 진행도 0 → 1 → 풀 반환을 프레임마다 기록 · README 그림 |
| `5-1. 측정 #5` | 게임 씬 · **Play 중** | 여러 마리의 진행도가 섞이지 않는지 · 중복 사망 · 공유 머티리얼 함정 |
| `5-2. 측정 #6` | 게임 씬 · **Play 중** | 60마리를 잡은 뒤 풀 장부 · 적이 계속 나오는지 |
| `6-1. 스타일 비교` | 게임 씬 | 화염 · 마법 · 재 — 값만 바꾼 세 가지 소멸 |
| `6-2. 체크포인트` | 게임 씬 | 그래프 · 머티리얼 · 적용 · 코드 · 측정 · Web 빌드 준비 점검 |
| `되돌리기 — …` | | 적 머티리얼을 M_HitFlash로 · EnemyAI · Health 원본 |
| `(막혔을 때) …` | | 정답 그래프 SG_Dissolve / EnemyAI · Health 정답으로 바꾸기 |

## 학생이 직접 만드는 것

| 파일 | 만드는 곳 |
|---|---|
| `Assets/CG2Week11/Shaders/SG_Dissolve.shadergraph` | 10주차 `SG_HitFlash` 선택 → `Ctrl+D` → 이름 바꾸고 이 폴더로 옮긴다 |
| `Assets/CG2Week11/Materials/M_Dissolve.mat` | 10주차 `M_HitFlash` 선택 → `Ctrl+D` → 이 폴더로 → Inspector `Shader` = `Shader Graphs/SG_Dissolve` |
| `Assets/StarterProject/Scripts/EnemyAI.cs` · `Health.cs` 수정 | 수업 내용 4 · 5단계 코드 |

## 실행하면 생기는 파일

`Captures/` 비교 · 소멸 단계 · 사망 프레임 그림 · `Compare/` 1-2 비교용 그래프 4개 · `Backup/` 원본 코드(정답 메뉴를 쓴 경우) — 측정 기록은 `Library/CG2Week11/`(깃에 올라가지 않음)

## 주의

- 그래프를 고친 뒤에는 그래프 창 왼쪽 위 **Save Asset**을 눌러야 셰이더에 반영됩니다.
- Play 중 측정은 플레이어 체력을 채우고, 레벨업 창이 떠도 측정이 끝날 때까지 시간을 흐르게 합니다. 끝나면 레벨업 창 상태로 돌아갑니다.
- `(막혔을 때)` 정답 그래프는 Shader Graph 내부 API를 리플렉션으로 호출해 만듭니다(Unity 6.6 · Shader Graph 17.6에서 확인).
