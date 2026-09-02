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

# ── oxyplot-avalonia 是仓库外源码引用（csproj 的 ..\..\..\oxyplot-avalonia），沙盒重置即丢，必须重新克隆（与 build.yml 的 Clone 步骤同源）──
git clone --depth 1 https://github.com/oxyplot/oxyplot-avalonia.git /data/user/work/oxyplot-avalonia

# ── git 身份（沙盒重置后需重新设置）──
git config user.name "Test User" && git config user.email "test@example.com"

# ── 环境就绪验证口径（2026-09-02 全部实证）：dotnet build 0 错误 + ForkPlus.Tests 3895 全绿 ──
# 注意：全量 dotnet test 偶发 "Test Run Aborted"（headless Compositor 初始化与其他测试
# 集合并行竞争；单跑该类可过、立即重跑全量即绿）。遇到先重跑一次再排查，勿误判环境损坏。

# 编译主工程（在 /data/user/work/ForkPlus-Next/src/ForkPlus 下）
dotnet build --no-restore -v q -nologo 2>&1 | grep -E "error CS" | sed -E 's/ \[.*//' | sort -u

# 编译整个解决方案（在 /data/user/work/ForkPlus-Next/src 下）
dotnet build ForkPlus.sln -clp:ErrorsOnly -nologo 2>&1 | tail -3

# 查看源生成器产物（调试 x:Name 字段问题时极其有用）
dotnet build --no-restore -v q -nologo -p:EmitCompilerGeneratedFiles=true
# 产物位于 obj/Debug/net10.0/generated/Avalonia.Generators/Avalonia.Generators.NameGenerator.AvaloniaNameIncrementalGenerator/

# ── GUI 冒烟（Xvfb 虚拟显示 + 截图）──
Xvfb :99 -screen 0 1920x1080x24 &   # 后台启动虚拟显示
export DISPLAY=:99
cd /data/user/work/ForkPlus-Next/src/ForkPlus && dotnet run --no-build   # 或直接跑 bin/Debug/net10.0/ForkPlus
import -window root /tmp/ui.png                                        # 截图（imagemagick）
xdotool key alt+f                                                       # 键盘驱动菜单等交互

# git 推送（凭据已配置在 remote url 中）
git push origin HEAD
```

- 工作目录：`/data/user/work/ForkPlus-Next`（主仓库）、`/data/user/work/oxyplot-avalonia`（图表库源码，仓库外引用）
- 进度截图统一放 `verification/`（仓根），有进展及时提交推送，不攒批
- 构建产物不入库（bin/obj 已在 .gitignore；publish/ 已于 2026-09-02 清除，CI 产物走 release artifact）
