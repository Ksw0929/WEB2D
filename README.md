# [프로젝트명] — 웹게임(2D) 기반 그래픽 실습

> 컴퓨터그래픽스(2) | 2026학년도 2학기 | 담당교수: 최도현
> 작성자: (학번) / (이름)
> 최종 수정일: (YYYY-MM-DD)

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [개발 환경](#2-개발-환경)
3. [x 주차 별 세부 내용]
4. [실행 방법](#5-실행-방법)
5. [트러블슈팅 기록](#6-트러블슈팅-기록)
6. [참고 자료](#7-참고-자료)

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|---|---|
| 프로젝트명 | (예: Survivor 2D) |
| 장르 | 2D 탑뷰 서바이벌 액션 (Vampire Survivors 스타일) |
| 플랫폼 | Web (WebGL2) |
| 조작 | WASD / 방향키 이동, 자동 공격 |
| 한 줄 소개 | (자신의 게임을 한 문장으로 설명) |

---

## 2. 개발 환경

| 항목 | 값 |
|---|---|
| Unity 버전 | 6000.5.1f1 |
| Render Pipeline | Universal RP (2D Renderer) |
| Input System | New Input System (Input System Package) |
| UI | TextMesh Pro |
| 웹 빌드 | WebGL 2 (WebGPU 실험적 지원 별도) |

---

## 3. 2주차 — 프로젝트 준비 & 웹 빌드

### 3.1 프로젝트 초기 세팅

- [x] Unity 6000.5.1f1로 2D URP 템플릿 프로젝트 생성
- [x] `2주차_StarterProject.unitypackage` 임포트
- [x] `CG2 Starter → Step 1 → Step 2 → Step 3` 순서로 씬 빌드
- [x] ▶ Play 버튼으로 정상 동작 확인 (WASD 이동, 자동 공격, 적 스폰)

### 3.2 씬 구성 요약

```
Player      SpriteRenderer(Order 2) · Rigidbody2D · CircleCollider2D
            PlayerController · HitReceiver · Health · WeaponController

Enemy       SpriteRenderer(Order 1) · Rigidbody2D · CircleCollider2D
            EnemyAI · HitReceiver · Health

Projectile  SpriteRenderer(Order 3) · Rigidbody2D · CircleCollider2D(Trigger)
            Projectile

Canvas      Screen Space - Overlay · HUD(Script)
            HpBar / ExpBar / ScoreText / LevelText / TimeText / GameOverPanel
```

### 3.3 웹 빌드 확인

- [x] `File → Build Profiles → Web → Build And Run`으로 브라우저 실행 확인
- [x] 빌드 소요 시간: 약 ( )분
- [x] 브라우저 정상 실행 여부: (예/아니오)

**웹 빌드 vs 에디터 Play 차이**

| 항목 | 에디터 Play | 웹 빌드 |
|---|---|---|
| 실행 속도 | 즉시 | 1~3분 |
| 확인 목적 | 로직 테스트 | 실제 브라우저 동작 확인 |

### 3.4 GitHub 업로드 체크리스트

- [x] `.gitignore`에 `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` 포함 확인
- [x] `Assets/`, `ProjectSettings/`, `Packages/`는 반드시 포함
- [x] 개별 파일 100MB 초과 여부 확인 (대용량은 Git LFS 고려)

### 3.5 2주차 문제 
| PPU 32 | Scale 2 |
| :---: | :---: |
| ![PPU 32 실행 화면](Images/ppu%2032.png) | ![Scale 2 실행 화면](Images/scale%202.png) |

PPU 32와 Scale 2 비교

두 실행 화면에서 파란색 플레이어 원은 거의 같은 크기로 보였다. PPU를 64에서 32로 낮추거나 Transform Scale을 1에서 2로 높이면, 두 경우 모두 스프라이트의 가로와 세로가 월드에서 각각 2배가 된다.

PPU 32는 스프라이트 이미지의 픽셀을 월드 크기로 환산하는 기준을 바꾼다. 같은 스프라이트 에셋을 사용하는 다른 오브젝트에도 적용된다.

Scale 2는 선택한 오브젝트의 Transform 크기를 2배로 늘린다. 해당 오브젝트에 붙은 콜라이더와 자식 오브젝트도 함께 스케일된다.

## 3주차 — Batching과 Sprite Atlas

### 실습 내용 정리

SRP Batcher와 Sprite Atlas를 각각 켜고 끄면서 Draw Calls, SetPass, 텍스처 수, 텍스처 메모리, FPS를 비교했다.

- **SRP Batcher**는 렌더링 상태를 준비하는 CPU 비용을 줄인다. Draw Calls가 그대로여도 SetPass가 줄어들 수 있다.
- **Sprite Atlas**는 여러 스프라이트를 하나의 텍스처로 묶는다. 텍스처 전환과 Draw Calls를 줄일 수 있고, 아틀라스 크기에 따라 텍스처 메모리는 늘어날 수 있다.
- **Frame Debugger**에서는 SRP Batcher ON/OFF에 따라 렌더링 트리에 표시되는 항목과 드로우콜 줄 수를 비교했다.

### 성능 측정 기록표

![성능 측정 기록표](Images/3-table1.png)

### Frame Debugger 관찰 기록표

![Frame Debugger 관찰 기록표](Images/3-table2.png)

### Frame Debugger 캡처 비교

| SRP ON | SRP OFF |
| :---: | :---: |
| ![SRP ON](Images/3-on.png) | ![SRP OFF](Images/3-off.png) |

## 4주차 — Tilemap 렌더링 최적화

### 실습 내용 정리

TilemapLab에서 Chunk와 Individual 렌더링을 비교하고, 텍스처 구성과 청크 컬링 설정이 Draw Calls, SetPass, Tris, FPS에 어떤 영향을 주는지 확인했다.

- **Chunk 모드**는 타일을 청크 단위로 묶어 그린다. #1 Chunk는 Draw Calls 2회, #2 Individual은 1,401회였다. Individual에서 Tris는 줄었지만, FPS도 101에서 18.2로 낮아졌다.
- **청크 컬링**은 카메라에 보이지 않는 청크를 렌더링에서 제외한다. #4에서 컬링 범위를 수동으로 넓히자 Draw Calls는 2회로 같았지만, Tris는 8,193에서 10,241로 늘었다.
- 바닥처럼 캐릭터와 앞뒤 정렬이 필요 없는 타일은 Chunk를 사용하고, 캐릭터와의 정렬이 필요한 타일은 별도 타일맵에 두고 Individual을 적용할 수 있다.

### 측정 기록표

![4주차 Tilemap 성능 측정 기록표](Images/4-table.png)

### Game 뷰 Stats 비교

| #1 Chunk | #2 Individual | #6 Mixed + Atlas |
| :---: | :---: | :---: |
| ![#1 Stats](Images/4-1.png) | ![#2 Stats](Images/4-2.png) | ![#6 Stats](Images/4-6.png) |

### 추가 분석 — Mixed와 Mixed + Atlas 비교

#3 Mixed와 #6 Mixed + Atlas는 Draw Calls 5회, SetPass 5회, Tris 8,193으로 같았다. 기록된 FPS는 #3에서 144.7, #6에서 36.4였다. 이번 측정에서 Atlas 적용 후 Draw Calls나 SetPass의 감소는 관찰되지 않았다. FPS 차이의 원인은 이 기록만으로 확인하기 어려우므로, 같은 조건에서 여러 번 측정해 비교할 필요가 있다.

## 5주차 — 카메라·UI 구성과 AI 에셋 적용

### 학습 내용 요약

- Cinemachine의 Follow와 Confiner 기능을 활용하여 플레이어를 따라가면서 맵 밖을 비추지 않는 카메라 구성을 학습하였다.
- 직교 카메라의 Size, 화면 해상도, PPU가 스프라이트의 화면 표시 크기에 미치는 영향을 확인하였다.
- UI Canvas를 합치거나 분리할 때 드로우콜과 UI 재구성 범위 사이에 차이가 발생한다는 점을 학습하였다.
- AI로 생성한 이미지를 Art Studio에서 배경 제거, 크기 조정, 외곽선 처리 등을 거쳐 게임용 스프라이트로 가공하였다.
- 에셋 교체 후 Sprite Atlas를 재적용하고, 에디터와 웹 브라우저에서 기존 에셋과 AI 에셋의 성능을 비교하였다.


#### AI 이미지 가공

![AI 이미지 가공 화면](Captures/week05/ai.png)

Art Studio에서 기사 이미지를 Player 용도로 설정하여 가공하였다. 가공 로그에서 원본 이미지가 배경 제거와 크기 조정을 거쳐 64×64px 캔버스로 저장된 것을 확인하였다.

#### 게임 적용 결과

![AI 에셋 적용 화면](Captures/week05/play.png)

플레이 화면에서 기사 플레이어와 초록색 슬라임이 표시되는 것을 확인하였다. 기사 캐릭터는 밝은 갑옷과 붉은 장식으로 어두운 바닥과 구분되며, 슬라임도 배경과 대비되는 색상으로 식별할 수 있었다.

### 측정 기록표

![AI 에셋 전후 측정 기록표](Captures/week05/result.png)


### 추가 분석

#### 1. 에디터에서는 AI 에셋 적용 후 렌더링 수치가 증가하였다.

2회 측정의 평균을 비교하면 DrawCalls는 13에서 15.5로, SetPass는 15에서 17.5로 증가하였다. Tris는 8,600에서 9,350으로 증가했으며, FPS는 147.5에서 138로 약 6.4% 감소하였다.

다만 AI 에셋 1회에서는 SetPass와 Tris가 기존 에셋과 같았고, 2회에서 함께 증가하였다. 따라서 이번 결과만으로 AI 이미지 자체가 성능 저하의 원인이라고 단정하기는 어렵다. 적과 투사체 수, UI 창 표시 여부, 아틀라스 적용 상태 등의 조건을 확인하고 같은 상태에서 반복 측정할 필요가 있다.

#### 2. 브라우저에서는 전후 성능 차이가 작았다.

브라우저 FPS는 기존 60.1, AI 60.0으로 거의 같았고, p95 프레임 시간도 16.9ms로 동일하였다. 측정된 프레임 지표에서는 에셋 교체 후 뚜렷한 차이가 나타나지 않았다.

다만 약 60FPS로 동작하는 환경에서는 화면 주사율이나 VSync의 영향으로 성능 여유의 차이가 FPS에 드러나지 않을 수 있다. 따라서 FPS와 프레임 시간을 함께 확인하는 것이 필요하다.

#### 3. 전송량과 로딩 시간의 변화는 작았다.

전송량은 69.3MB에서 69.4MB로 약 0.14% 증가하였다. 로딩 시간은 424ms에서 407ms로 17ms 짧아졌다.

전송량이 증가했는데도 로딩 시간이 감소했으므로, 이 결과를 AI 에셋의 로딩 성능 개선으로 해석하기는 어렵다. 캐시, 로컬 서버 상태, 측정 시점 등의 영향을 확인하기 위해 동일한 캐시 조건에서 반복 측정해야 한다.

#### 4. 캐릭터 외형과 배경의 구분이 명확해졌다.

적용 화면에서 기사의 밝은 갑옷과 붉은 장식, 슬라임의 초록색이 어두운 바닥과 대비된다. 캐릭터를 작은 크기로 표시해도 위치와 종류를 구분하기 쉬운 외형을 확인할 수 있었다.

AI 이미지는 생성 결과를 그대로 사용하는 것보다 배경과 외곽선을 정리하고, 크기와 PPU 등 프로젝트 규격을 맞추는 과정이 중요하다.

## 4. 실행 방법

### 에디터에서 실행

```
1. Unity 6000.5.1f1로 프로젝트 열기
2. Assets/StarterProject/Scenes/SampleScene 열기
3. ▶ Play 버튼 클릭
```

### 웹 빌드 실행

```
1. File → Build Profiles → Web 선택
2. Build And Run 클릭
3. 브라우저에서 자동 실행 확인
```

---

## 5. 트러블슈팅 기록

> 진행 중 겪은 문제와 해결 과정을 기록하세요 (선택 사항이지만 권장).

| 문제 | 원인 | 해결 방법 |
|---|---|---|
| TilemapLab 스크립트는 있었지만 Hierarchy에 TilemapLab 오브젝트가 보이지 않았다. | 열어둔 씬과 TilemapLab 오브젝트가 배치된 실습 씬이 달랐다. | TilemapLab이 배치된 실습 씬을 열고 Hierarchy에서 오브젝트를 확인했다. |

| | | |

---

## 6. 참고 자료
- 수업 자료: 2주차 PPT
