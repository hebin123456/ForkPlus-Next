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
# ⚠️ 教训（2026-09-03 实证）：克隆后直接 dotnet build，一行都不要改！
# 官方版 oxyplot 用 Avalonia 11.0.0 + netstandard2.0，与主工程（Avalonia 12.1.1 + net10.0）并存完全正常——
# 它自己按 11 编译成 netstandard2.0 程序集，主工程直接 ProjectReference 引用，NuGet 各按各的版本还原，互不冲突。
# 曾错误地把它当"版本不匹配"去改 AvaloniaVersion→12.1.1，结果：netstandard2.0 下大量类型解析失败（786 错），
# 又继续"救火"改 TFM→net10.0、剪贴板 API SetTextAsync→SetDataAsync、关编译绑定开关……越改越多、全部是白干。
# 正确姿势：官方原版直接编译即可通过（Build succeeded, 0 Error, ~21s），三方件保持零改动。

# ── git 身份（沙盒重置后需重新设置）──
git config user.name "Test User" && git config user.email "test@example.com"

# ── 环境就绪验证口径（2026-09-02 全部实证）：dotnet build 0 错误 + ForkPlus.Tests 全绿 ──
# 注意：曾偶发 "Test Run Aborted"（根因：Dispatcher.UIThread 是进程级单例、首触线程拥有，
# 并行测试集先触碰后 headless 启动线程初始化 Compositor 即崩；各测试类 SpinUntil 超时
# 后继续也会让 worker 抢先触碰）。已根治（2026-09-02）：HeadlessAppBootstrap 用
# [ModuleInitializer] 在程序集加载期启动真实 App 并同步等待就绪，归属恒为 UI 线程。

# 编译主工程（在 /data/user/work/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 编译整个解决方案（在 /data/user/work/ForkPlus-Next/src 下）
dotnet build ForkPlus.sln -clp:ErrorsOnly -nologo 2>&1 | tail -3

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/
```

## GUI 调试与冒烟

**首选：headless 控件级自动化（快、准、带堆栈）**——in-process 驱动真实 App 资源，
异常堆栈直接进测试输出（截图+xdotool 坐标点击复现一次崩溃要几分钟，headless 秒级）。
原 Windows-only FlaUI/UIA3 套件 ForkPlus.AutomationTests 已删除（2026-09-02），
UI 冒烟测试全部归一到此处：

```bash
# 在 /data/user/work/ForkPlus-Next/src 下
dotnet test ForkPlus.Tests --filter "FullyQualifiedName~MenuWindowSmokeTests" -v q --nologo
```

启动基建已统一收拢到 `src/ForkPlus.Tests/HeadlessAppBootstrap.cs`：[ModuleInitializer]
在程序集加载期启动继承真实 `App` 的 headless 单例（全套 App.axaml 资源，只 override
掉启动副作用；`ShutdownMode=OnExplicitShutdown` 防 Dispatcher 连锁关闭），任何测试线程
不再与 Compositor 初始化竞争。新窗口/菜单冒烟直接 `[Collection("HeadlessAvalonia")]` +
`HeadlessAppBootstrap.Run(delegate { ... })`，参照 `UiSmokeHeadlessTests.cs` /
`MenuWindowSmokeTests.cs` 加测试即可。

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
