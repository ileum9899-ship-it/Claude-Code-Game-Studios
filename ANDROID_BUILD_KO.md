# Life RP — Android 빌드 & 설치 가이드

Life RP를 Android 폰에 설치해서 플레이하는 방법입니다. APK는 **Unreal Engine 5.8이 설치된 Windows PC**에서 만듭니다.

> ⚠️ 이 프로젝트는 Unreal이 없는 환경에서 작성되어 아직 실제 기기에서 빌드·실행해 보지 않았습니다.
> 처음 빌드할 때 컴파일 오류나 설정 문제가 나올 수 있습니다. 오류 로그를 보내 주시면 고쳐 드립니다.

## 1. 준비물

| 항목 | 내용 |
|---|---|
| PC | Windows 10/11, 여유 공간 100GB 이상 권장 |
| Unreal Engine 5.8 | Epic Games Launcher > Unreal Engine > 라이브러리 > 5.8 설치. **옵션 > 대상 플랫폼에서 Android를 체크**하세요 |
| Visual Studio 2022 (17.14 이상) | "C++를 사용한 게임 개발" 워크로드 |
| Android Studio | SDK 관리용 |
| 휴대폰 | **Android 8.0 이상**, Vulkan 지원 기기 (대략 2019년 이후 중급 이상 기기 권장) |

UE 5.8 요구 사항: 설치 최소 SDK 26(Android 8.0), NDK r27c, 컴파일 SDK 34 이상(35 권장).

## 2. Android SDK / NDK 설정 (한 번만)

1. Android Studio를 설치하고 한 번 실행합니다.
2. **SDK Manager**에서 다음을 설치합니다.
   - SDK Platforms: **Android 15 (API 35)**
   - SDK Tools: **Android SDK Build-Tools**, **Android SDK Command-line Tools**, **Android SDK Platform-Tools**
3. 엔진 폴더의 `Engine\Extras\Android\SetupAndroid.bat`를 실행합니다. 맞는 NDK 버전이 설치되고 환경 변수가 설정됩니다.
   예: `C:\Program Files\Epic Games\UE_5.8\Engine\Extras\Android\SetupAndroid.bat`
4. **PC를 재시작**합니다. 환경 변수를 적용하기 위해서입니다.

## 3. 휴대폰 준비

1. **설정 > 휴대전화 정보 > 소프트웨어 정보 > 빌드 번호**를 7번 누르면 개발자 모드가 켜집니다.
2. **설정 > 개발자 옵션 > USB 디버깅**을 켭니다.
3. USB로 PC에 연결하고, 폰에 뜨는 "USB 디버깅 허용"에서 **허용**을 누릅니다.

## 4. 프로젝트 열기

1. `LifeRP.uproject`를 더블클릭합니다. "모듈을 다시 빌드하시겠습니까?"가 나오면 **예**를 누릅니다. C++ 컴파일에 몇 분 걸립니다.
2. **편집 > 프로젝트 설정 > 플랫폼 > Android**에서:
   - 상단의 **Configure Now**를 누릅니다(프로젝트를 Android용으로 구성).
   - **Accept SDK License**를 누릅니다.
3. **프로젝트 설정 > 플랫폼 > Android SDK**에서 SDK/NDK 경로가 채워져 있는지 확인합니다. 비어 있으면 2단계를 다시 확인하세요.

패키지 이름(`com.liferp.game`), 가로 화면, arm64, Vulkan 같은 Android 설정은 `Config/DefaultEngine.ini`에 미리 들어 있습니다.

## 5-A. 바로 폰에서 실행 (가장 빠름)

에디터 상단 툴바에서 **Platforms > Android > (연결된 폰 이름)**을 선택합니다.
빌드, 쿡, 설치, 실행이 자동으로 진행됩니다. 처음에는 셰이더 컴파일 때문에 오래 걸립니다(10~40분).

## 5-B. APK 파일 만들기 (설치 파일로 공유)

1. **Platforms > Android > Package Project**를 누르고 저장 폴더를 고릅니다. 텍스처 형식은 **ASTC**를 권장합니다.
2. 완료되면 그 폴더에 `Android_ASTC` 폴더가 생깁니다.
3. 폰을 연결한 상태로 `Install_LifeRP-arm64.bat`를 실행하면 설치됩니다.
   APK는 빌드 설정상 데이터를 내부에 포함(`bPackageDataInsideApk`)하므로, `.apk` 파일 하나만 폰에 복사해서 설치할 수도 있습니다.

명령줄로 만들 수도 있습니다.
```bat
"C:\Program Files\Epic Games\UE_5.8\Engine\Build\BatchFiles\RunUAT.bat" BuildCookRun ^
  -project="%CD%\LifeRP.uproject" -platform=Android -cookflavor=ASTC -clientconfig=Development ^
  -build -cook -stage -package -archive -archivedirectory="%CD%\Build"
```

## 6. 터치 조작

| 화면 위치 | 걸을 때 | 운전할 때 |
|---|---|---|
| 왼쪽 아래 조이스틱 | 이동 | 핸들(좌우) |
| 오른쪽 아래 조이스틱 | 시점 | 시점 |
| 오른쪽 큰 버튼 | **행동** (상호작용 / 차 타기 / 활동 중단) | **가속** (누르고 있기) |
| 오른쪽 작은 버튼 | 점프, 달리기(켜기/끄기) | 브레이크·후진, 핸드브레이크, 내리기 |
| 위쪽 가운데 | 상태 화면, 저장 | 상태 화면, 저장 |

앱을 홈으로 내리거나 잠에서 깨면 자동 저장되고, 다시 켜면 이어서 할 수 있습니다.

## 7. PC에서 모바일 화면 미리 테스트하기

폰 없이도 에디터에서 확인할 수 있습니다.
1. 툴바의 **Play 옆 ▼ > Mobile Preview ES3.1 (PIE)**를 선택합니다. 모바일 그래픽으로 실행됩니다.
2. **프로젝트 설정 > 엔진 > 입력 > Use Mouse for Touch**를 켜면 마우스를 손가락처럼 쓸 수 있습니다.
3. **프로젝트 설정 > 게임 > Life RP > UI > Force Touch UI**를 켜면 PC에서도 터치 버튼이 보입니다.

## 8. 모바일 그래픽

PC 버전의 Lumen, 가상 섀도 맵, 볼류메트릭 구름·안개는 모바일에서 지원되지 않거나 너무 무겁습니다. 그래서 모바일 렌더러에서는 다음처럼 동작합니다.
- 하늘(Sky Atmosphere), 해·달 그림자, 낮밤 순환, 가로등과 실내 조명은 그대로 유지합니다.
- 구름, 볼류메트릭 안개, 필름 그레인은 끕니다.
- 주민은 14명, 차량은 4대, 외곽 나무는 50그루로 줄입니다(**프로젝트 설정 > Life RP > Mobile**에서 조절).
- 해상도는 85%로 렌더링합니다(`Config/Android/AndroidEngine.ini`).

## 9. 문제 해결

| 증상 | 해결 |
|---|---|
| Platforms 메뉴에 Android가 없음 | 런처에서 5.8 **옵션 > 대상 플랫폼 > Android** 설치 |
| "SDK not found" / NDK 오류 | 2단계 `SetupAndroid.bat` 다시 실행, PC 재시작, 프로젝트 설정의 경로 확인 |
| 폰이 목록에 안 보임 | USB 디버깅 허용 확인, 케이블 교체(충전 전용 케이블 X), `adb devices`로 확인 |
| 설치 실패 | 폰의 기존 Life RP 삭제 후 재설치, 저장 공간 확인 |
| 한글이 네모(□)로 보임 | Noto Sans KR 등 한글 폰트를 Font 에셋으로 가져와 **Life RP > UI > HUD Font**에 지정 |
| 느리거나 발열이 심함 | Mobile 설정의 주민·차량 수 줄이기, `AndroidEngine.ini`의 `r.ScreenPercentage`를 70으로 |
| 실행 직후 꺼짐 | `adb logcat -s UE` 로그를 보내 주세요 |
