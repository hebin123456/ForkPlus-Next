# ForkPlus-Next

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml/badge.svg)](https://github.com/hebin123456/ForkPlus-Next/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/hebin123456/ForkPlus-Next)](https://github.com/hebin123456/ForkPlus-Next/releases)

[ForkPlus](https://github.com/hebin123456/ForkPlus)(WPF 버전)의 크로스 플랫폼 이식판: UI 레이어를 .NET 10 + Avalonia 12로 다시 작성하여 단일 코드베이스가 Windows / Linux / macOS에서 실행됩니다. 기반 Rust 엔진(biturbo native), AI 지원 개발, 8개 언어, 12종 테마 스킨, git mm 워크플로우, 기여 히트맵·저장소 트리맵 등 시각화 기능은 원본과 동일합니다.

> 마이그레이션 환경 구축, 과거 수정 이력 및 교훈은 [MIGRATION.md](MIGRATION.md)에 기록되어 있습니다.

[English](README.en.md) | [简体中文](README.md) | [繁體中文](README.zh-Hant.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

## 주요 기능

- **크로스 플랫폼**: Avalonia 12 기반 크로스 플랫폼 UI 레이어. CI에서 Windows x64 / Linux x64 / macOS arm64 3개 플랫폼 빌드를 병렬 생성
- **다국어 지원**: 영어, 간체 중국어, 번체 중국어, 일본어, 한국어, 프랑스어, 독일어, 스페인어 8개 언어 내장, JSON 파일로 추가 언어 확장 지원
- **다중 테마 스킨**: 12개 내장 스킨(Light/Dark, Solarized, GitHub, Dracula, Monokai, 보라/초록 라이트&다크)과 사용자 정의 색상 덮어쓰기를 즉시 적용
- **git mm 워크플로우**: `git mm` 하위 명령을 내장하여 린 브랜칭(Lean Branching) 워크플로우로 여러 서브 저장소의 변경과 동기를 통합 관리
- **AI 보조 개발**: AI 코드 리뷰, 자동 커밋 메시지 생성, AI 보조 코드 수정 통합
- **기여 히트맵**: GitHub 스타일 53주 × 7일 커밋 히트맵. 색상 범례와 통계 요약(총 커밋 수 / 최장 연속 커밋 일수 / 가장 활발한 날) 표시, 마우스 호버 시 해당 일의 커밋 수와 Top 3 작성자 표시
- **저장소 트리맵**: biturbo native treemap 알고리즘 기반 저장소 파일 크기 시각화, 클릭으로 단계별 드릴다운 지원
- **원격 브랜치 추적**: 우클릭 "추적"을 원격별로 그룹화한 2단계 메뉴로 변경. 메뉴 내 상단 고정 검색 상자로 수많은 원격 브랜치를 빠르게 검색
- **성능 최적화**: 대형 저장소의 새로고침, diff 렌더링, 서브모듈 관리에 대한 맞춤형 최적화
- **코드 통계**: tokei(Rust, 200+ 언어 지원) 통합으로 언어별 코드 라인 수·파일 수·주석 행·빈 행을 집계하고 파이 차트로 시각화, Workspace/브랜치/tag ref 전환 지원

## 프로젝트 구조

```
ForkPlus-Next/
├── src/
│   ├── ForkPlus/              # 메인 애플리케이션 소스(Avalonia 12 크로스 플랫폼 UI), XAML, 에셋
│   │   ├── Biturbo/           # biturbo native 라이브러리의 P/Invoke 바인딩
│   │   ├── Languages/         # 다국어 번역 파일(JSON)
│   │   │   ├── zh-Hans.json   # 간체 중국어
│   │   │   ├── zh-Hant.json   # 번체 중국어
│   │   │   ├── ja-JP.json     # 일본어
│   │   │   ├── ko-KR.json     # 한국어
│   │   │   ├── fr-FR.json     # 프랑스어
│   │   │   ├── de-DE.json     # 독일어
│   │   │   ├── es-ES.json     # 스페인어
│   │   │   └── README.md      # 언어 파일 형식 설명
│   │   └── ...
│   ├── ForkPlus.AskPass/      # Git/SSH 비밀번호 입력 도우미
│   ├── ForkPlus.RI/           # 대화형 rebase 편집기 도우미
│   ├── ForkPlus.Tests/        # xUnit 단위 테스트 (Avalonia.Headless UI 스모크 테스트 포함)
│   ├── ForkPlus.AskPass.Tests/# AskPass 도우미 단위 테스트
│   └── ForkPlus.RI.Tests/     # RI 도우미 단위 테스트
├── third_party/               # 빌드 시 가져오는 네이티브 바이너리(아래 "biturbo native 라이브러리 출처" 참조)
├── gitmm/                     # git mm 워크플로우 참조 문서
└── .github/workflows/         # GitHub Actions CI 설정
```

## 빌드

### 사전 요구 사항

- Windows 10 이상 / Linux / macOS(크로스 플랫폼 지원)
- .NET 10 SDK
- IDE(선택): Visual Studio 2026(Windows. 저장소 루트의 `OpenForkPlusInVS2026.cmd`로 한 번에 솔루션을 열 수 있습니다), Rider 또는 VS Code
- Git 2.40 이상(권장. 권장 버전 미만이면 시작 시 경고가 표시되며 일부 기능이 정상 동작하지 않을 수 있습니다. 앱은 내장 git 2.50.1을 우선 사용하며 없으면 시스템 git으로 대체합니다)
- git-mm 3.0 이상(git mm 워크플로 사용 시 필수. 미지원 버전이나 미설치 시 시작 시 경고 표시, 환경설정에서 git-mm 경로 구성 가능)

### 빌드 단계

```bash
# ① 차트 라이브러리 OxyPlot.Avalonia는 "저장소 외부 소스 참조" 방식으로 통합되어 있습니다
#    (csproj의 상대 경로가 이 저장소의 형제 디렉터리를 가리킴). 첫 빌드 전에 이 저장소
#    옆에 클론하세요. 공식 원본을 무수정으로 유지하고 Avalonia 버전을 변경하지 마세요:
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git ../oxyplot-avalonia

# ② 빌드(저장소 루트에서):
dotnet build ForkPlus.sln -c Release
```

저장소 루트의 `ForkPlus.sln`을 Visual Studio 2026으로 직접 열어 빌드할 수도 있습니다.

### biturbo native 라이브러리 출처

biturbo native(Rust)는 저장소 트리맵 레이아웃, 커밋 그래프 캐시, revision header 파싱 등의 기능을 제공합니다. **이 바이너리는 이 저장소에 커밋되지 않으며**, 빌드 시 [Biturbo 저장소](https://github.com/hebin123456/Biturbo)의 최신 Release에서 자동으로 가져옵니다. 플랫폼별 파일:

| 플랫폼 | 파일 |
|------|------|
| Windows x64 | `third_party/biturbo.dll` |
| Linux x64 | `third_party/libbiturbo.so` |
| macOS arm64 | `third_party/libbiturbo.dylib` |

구체적인 메커니즘([ForkPlus.csproj](src/ForkPlus/ForkPlus.csproj) 참조):

- `RestoreBiturbo` 타깃(`BeforeTargets=Build`): 현재 플랫폼의 네이티브 라이브러리가 없으면 자동 다운로드(Windows는 PowerShell, Linux/macOS는 bash + curl로 `uname -s`에 따라 `.so` / `.dylib` 선택, 모두 재시도 포함)
- `CopyHelperExecutables` / `PublishHelperExecutables` 타깃(`AfterTargets=Build` / `Publish`): 네이티브 라이브러리와 AskPass/RI 하위 프로세스 산출물을 Build / Publish 출력 디렉터리로 복사
- `.gitignore`가 `third_party/`의 이 파일들을 이미 제외함

따라서 첫 빌드에는 GitHub 네트워크 접근이 필요합니다. CI에서는 workflow가 명시적으로 다운로드하고 검증하며(비어 있지 않고 1MB 초과) csproj의 `RestoreBiturbo`가 대체 수단으로 작동합니다.

### tokei 출처

[tokei](https://github.com/XAMPPRocky/tokei)(MIT 라이선스)는 통계 패널의 "코드 라인 수" 기능에 사용됩니다. 빌드 시 [hebin123456/tokei](https://github.com/hebin123456/tokei) 저장소의 최신 Release에서 **사전 컴파일된 바이너리**를 가져오므로 로컬 Rust 툴체인이 필요 없습니다:

- Windows x64 → 단일 exe, `third_party/tokei.exe`로 저장
- Linux x64 / macOS → tar.gz(단일 `tokei` 바이너리 포함), 압축 해제 후 `third_party/tokei`로 저장
- macOS 에셋은 x86_64이며 Apple Silicon에서는 Rosetta 2를 통해 실행

메커니즘은 biturbo와 동일: `RestoreTokei` 타깃(`BeforeTargets=Build`)이 자동으로 가져오고, CI가 명시적으로 다운로드·검증하며, `.gitignore`가 산출물을 제외합니다.

### 지속적 통합

프로젝트는 GitHub Actions([`.github/workflows/build.yml`](.github/workflows/build.yml))로 구성되어 있습니다. `master` 브랜치에 push / PR하거나 수동 트리거하면 3개 플랫폼에서 병렬 빌드 후 산출물을 업로드합니다:

| 매트릭스 | Runner | RID |
|------|--------|-----|
| windows-x64 | windows-latest | win-x64 |
| linux-x64 | ubuntu-latest | linux-x64 |
| macos-arm64 | macos-latest | osx-arm64 |

산출물은 **프레임워크 의존 게시**(대상 머신에 .NET 10 런타임 필요)로, 메인 앱, AskPass/RI 하위 프로세스 3종, 해당 플랫폼의 biturbo native 라이브러리, tokei, 언어 파일을 포함합니다. 저장소의 [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 페이지에서 해당 실행의 Artifacts(14일 보관)로 다운로드할 수 있습니다.

## 테스트

- 단위 테스트: `dotnet test src/ForkPlus.Tests/ForkPlus.Tests.csproj`(크로스 플랫폼 Avalonia.Headless UI 스모크·E2E 테스트 포함, 단위 테스트와 함께 실행)
- 전체 4000+ 케이스. 마이그레이션 과정의 주요 수정에는 모두 회귀 방어 테스트가 있습니다(자세한 내용은 [MIGRATION.md](MIGRATION.md))

## 다국어 지원

### 내장 언어

| 언어 코드 | 표시 이름 | 상태 |
|-----------|-----------|------|
| `en` | English | 소스 언어 |
| `zh-Hans` | 简体中文 | 완전 |
| `zh-Hant` | 繁體中文 | 완전 |
| `ja-JP` | 日本語 | 완전 |
| `ko-KR` | 한국어 | 완전 |
| `fr-FR` | Français | 완전 |
| `de-DE` | Deutsch | 완전 |
| `es-ES` | Español | 완전 |

### 새 언어 추가

`src/ForkPlus/Languages/` 디렉터리에 새 `<언어-코드>.json` 파일을 생성하면 됩니다. 코드 변경이 필요 없습니다. 파일 형식:

```json
{
  "code": "ko",
  "name": "한국어",
  "translations": {
    "Preferences": "환경설정",
    "General": "일반",
    "Commit": "커밋"
  }
}
```

자세한 내용은 [src/ForkPlus/Languages/README.md](src/ForkPlus/Languages/README.md)를 참조하세요.

### 국제화 API

코드베이스는 다음 API를 사용하여 국제화를 구현합니다:

- `PreferencesLocalization.Current("English text")` — 단순 문자열 번역
- `PreferencesLocalization.FormatCurrent("...{0}...", args)` — 매개변수가 있는 문자열 번역
- `PreferencesLocalization.Translate(text, language)` — 특정 언어에 대한 번역

## 다운로드

- CI 빌드 산출물: [Actions](https://github.com/hebin123456/ForkPlus-Next/actions) 페이지 → 해당 build 실행 → Artifacts(프레임워크 의존 방식, .NET 10 런타임 필요)
- 정식 릴리스: [Releases 페이지](https://github.com/hebin123456/ForkPlus-Next/releases)
- 각 버전의 변경 사항은 [Release Notes](RELEASE_NOTE.md)를 참조하세요(원본 WPF 버전의 역사 포함)

## 개발 규칙

- 애플리케이션 자체를 수정할 때는 `src/ForkPlus` 디렉터리 내에 머무르세요. `third_party/`의 런타임 바이너리(biturbo native 라이브러리, tokei)는 빌드 시 자동으로 가져오므로 바이너리를 수동으로 커밋하지 마세요
- biturbo / tokei 버전을 업그레이드하려면 해당 저장소에 새 Release를 게시하면 이 저장소의 다음 빌드가 자동으로 가져옵니다
- `../oxyplot-avalonia`는 저장소 외부 소스 참조이므로 공식 원본을 무수정으로 유지하세요(버전을 잘못 수정한 교훈은 MIGRATION.md 참조)
- 마이그레이션(WPF → Avalonia)의 환경 구성, 진행 중인 작업, 과거 수정 이력은 [MIGRATION.md](MIGRATION.md)에 기록되어 있습니다

## 라이선스

이 프로젝트는 [MIT License](LICENSE)로 오픈소스입니다.

Copyright (c) 2026 hebin123456
