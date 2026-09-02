# ForkPlus-Next — WPF → Avalonia 迁移

> 本仓库是 [ForkPlus](https://github.com/hebin123456/ForkPlus)（WPF 版）向 Avalonia 12 的迁移目标。
> 历史修复链记录已按用户要求精简（2026-09-02），本文档只保留环境配置。

## 环境与构建（重要）

**📍 路径说明：仓库位于 `/data/user/work/ForkPlus-Next`**（沙盒重置后重新克隆的位置）。

```bash
# dotnet 不在默认 PATH，必须先 export（沙盒环境重置后 SDK 装在 ~/.dotnet）
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# ── SDK 重装实录（2026-09-02 实测）──
# ① 中科大/南大镜像站无 dotnet 目录（404 实证），官方源直连仅 ~147KB/s；
#    正解 = aria2 16 连接切片下载，240MB 约 15 秒（先 apt-get update &&
#    apt-get install -y aria2 xvfb xdotool x11-utils，后三者为截图冒烟必备）：
aria2c -x 16 -s 16 -k 8M --file-allocation=none \
  -o dotnet-sdk-10.0.400-linux-x64.tar.gz \
  https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.400/dotnet-sdk-10.0.400-linux-x64.tar.gz
# ② SHA512 校验（对照官方 .sha512 文件）：
sha512sum dotnet-sdk-10.0.400-linux-x64.tar.gz
# ③ 解压安装：
mkdir -p ~/.dotnet && tar -xzf dotnet-sdk-10.0.400-linux-x64.tar.gz -C ~/.dotnet

# ── git 2.50.1 编译安装（2026-09-02 实测，必装）──
# Ubuntu 22.04 apt 的 git 是 2.34.1 < 推荐的 2.40（GitVersionChecker.RecommendedVersion），
# 应用每次启动都会弹"Git 版本过旧"对话框（账号窗口等交互前还得先点掉它）。
# 正解 = 源码编译 2.50.1（与 App.ForkGitInstancePath 期望的版本一致），装两处：
#   ① /usr/local/bin/git —— 系统级（shell 里也是新版）
#   ② ~/.local/share/ForkPlus/gitInstance/2.50.1/bin/git —— 软链到 ①，
#      即真实 Fork 的"自带 git 实例"，App.GitPath 首选路径（settings 里 GitInstancePath
#      为 null 时生效；若曾被写成 /usr/bin/git 需改回 null，否则仍用旧版 git）
apt-get update && apt-get install -y zlib1g-dev libssl-dev libcurl4-openssl-dev \
  libexpat1-dev gettext gcc make
cd /tmp && aria2c -x 16 -s 16 -k 1M --file-allocation=none \
  -o git-2.50.1.tar.xz https://www.kernel.org/pub/software/scm/git/git-2.50.1.tar.xz
tar -xf git-2.50.1.tar.xz && cd git-2.50.1
./configure --prefix=/usr/local --without-tcltk && make -j$(nproc) all && make install
GI="$HOME/.local/share/ForkPlus/gitInstance/2.50.1" && mkdir -p "$GI/bin" \
  && ln -sf /usr/local/bin/git "$GI/bin/git"
# 验证：/usr/local/bin/git --version → 2.50.1；$GI/bin/git --exec-path → /usr/local/libexec/git-core

# ── oxyplot-avalonia 是仓库外源码引用（csproj 的 ..\..\..\oxyplot-avalonia），沙盒重置即丢，必须重新克隆（与 build.yml 的 Clone 步骤同源）──
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git /data/user/work/oxyplot-avalonia

# ── git 身份（沙盒重置后需重新设置）──
git config user.name "Test User" && git config user.email "test@example.com"

# ── 环境就绪验证口径（2026-09-02 全部实证）：dotnet build 0 错误 + ForkPlus.Tests 全绿 ──
# 注意：全量 dotnet test 偶发 "Test Run Aborted"（headless Compositor 初始化与其他测试
# 集合并行竞争；单跑该类可过、立即重跑全量即绿）。遇到先重跑一次再排查，勿误判环境损坏。

# 编译主工程（在 /data/user/work/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 编译整个解决方案（在 /data/user/work/ForkPlus-Next/src 下）
dotnet build ForkPlus.sln -clp:ErrorsOnly -nologo 2>&1 | tail -3

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/
```

## GUI 调试与冒烟

**首选：headless 控件级自动化（快、准、带堆栈）**——Linux 上等价于 Windows 侧
ForkPlus.AutomationTests 的 FlaUI/UIA3 那套。in-process 驱动真实 App 资源，
异常堆栈直接进测试输出（截图+xdotool 坐标点击复现一次崩溃要几分钟，headless 秒级）：

```bash
# 在 /data/user/work/ForkPlus-Next/src 下
dotnet test ForkPlus.Tests --filter "FullyQualifiedName~MenuWindowSmokeTests" -v q --nologo
```

参考实现 `src/ForkPlus.Tests/MenuWindowSmokeTests.cs`：继承真实 `App`（拿到全套
App.axaml 资源，只 override 掉启动逻辑）、`SetupWithClassicDesktopLifetime` 挂
lifetime（注意必须在 Setup 前赋值，且 `ShutdownMode` 改 `OnExplicitShutdown`，否则
首个测试关窗口会把 Dispatcher 整个关掉，后续测试全部 TaskCanceledException）。
新窗口/菜单冒烟优先照这个模式加测试。

**备选：Xvfb 真机截图冒烟**（最终视觉确认用）：

```bash
Xvfb :99 -screen 0 1920x1080x24 &   # 后台启动虚拟显示
export DISPLAY=:99
cd /data/user/work/ForkPlus-Next/src/ForkPlus && ./bin/Debug/net10.0/ForkPlus
import -window root /tmp/ui.png     # 截图（imagemagick）
xdotool mousemove X Y click 1       # 菜单点击（坐标靠截图测量，效率低，仅最终验证用）
```

# git 推送（凭据已配置在 remote url 中）
git push origin HEAD
```

- 工作目录：`/data/user/work/ForkPlus-Next`（主仓库）、`/data/user/work/oxyplot-avalonia`（图表库源码，仓库外引用）
- 进度截图统一放 `verification/`（仓根），有进展及时提交推送，不攒批
- 构建产物不入库（bin/obj 已在 .gitignore；publish/ 已于 2026-09-02 清除，CI 产物走 release artifact）
