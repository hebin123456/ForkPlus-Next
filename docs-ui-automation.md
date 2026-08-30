# UI 冒烟自动化操作手册（agent 实战经验）

> 目的：沉淀「沙盒里如何驱动 ForkPlus UI 做冒烟验证」的完整经验，后续 agent 直接照做，不要重复探索。
> 记录于 2026-08-30，基于本轮实测（首启向导三步全通）。

## 0. 标准环境与分辨率

**统一使用 1920×1280**（用户实际工作分辨率，截图坐标以此为准，勿改回 1080p）。

```bash
# Xvfb：注意 /tmp/.X11-unix 可能不存在导致 SocketCreateListener 失败（报"server already running"是误导）
sudo mkdir -p /tmp/.X11-unix && sudo chmod 1777 /tmp/.X11-unix
(Xvfb :89 -screen 0 1920x1280x24 -nolisten tcp > /tmp/xvfb89.log 2>&1 &)
# 坑：旧 X server 的锁文件 root 属主时 rm 不掉，直接换显示号（:89/:97/:98...）最快
# 验证存活：ps aux | grep Xvfb（xdpyinfo 未装，别用它判断）
```

依赖安装（apt 走代理，沙盒直连不通）：
```bash
echo 'Acquire::http::Proxy "http://127.0.0.1:18080"; Acquire::https::Proxy "http://127.0.0.1:18080";' | sudo tee /etc/apt/apt.conf.d/99proxy
sudo apt-get update && sudo apt-get install -y xvfb xdotool imagemagick
```

## 1. 启动应用

```bash
export DISPLAY=:89
cd src/ForkPlus
(dotnet run --no-build > /tmp/run_log.txt 2>&1 &)
sleep 12   # 首启要过向导，冷启动稍慢
DISPLAY=:89 import -window root /tmp/ui.png    # 全屏截图
```

## 2. 首启向导三步（坐标按 1920×1280 实测，1920×1080 下按钮位置不同！）

| 步骤 | 窗口 | 按钮 | 可靠点法 |
|---|---|---|---|
| 1 | 配置 Git（检测 /usr/bin/git） | 继续 | 焦点默认按钮，`xdotool key Return` |
| 2 | Git 版本过旧（2.34.1 < 2.40 警告） | 确定 | 鼠标点按钮中心（见下节定位法） |
| 3 | 用户信息（用户名/邮箱/默认源码目录 /root） | 完成 | 鼠标点（焦点在输入框，Enter 无效） |

**经验**：
- 第 1 步对话框里 Enter 有效（默认按钮聚焦）；
- 第 2/3 步焦点在别处，**Enter 无效，必须鼠标点击**；
- 点完等 2~3 秒再截图，UI 切换有延迟。

## 3. 按钮精确定位法（颜色连通域分析，实测最有效）

肉眼估坐标经常点空。用 imagemagick 找「主色描边按钮」的连通域：

```bash
DISPLAY=:89 import -window root /tmp/f.png
# 按钮是青/蓝描边矩形。把目标色转白、其余转黑，再做连通域分析
convert /tmp/f.png -fuzz 30% -fill white -opaque 'rgb(0,160,180)' -fill black +opaque white /tmp/m.png
convert /tmp/m.png -define connected-components:verbose=true -connected-components 4 null: 2>/dev/null \
  | grep -E "^\s+[0-9]+:" | awk '$NF>200 && $NF<20000 {print}'
# 输出形如： 46x22+528+112 ... 734
#            宽x高+X+Y          面积
# 按钮中心 = (528+46/2, 112+22/2) = (551,123)
xdotool mousemove 551 123 click 1
```

要点：
- 按钮尺寸特征：40~60px 宽、20~30px 高、面积 700~2000，凭这个过滤掉图标和噪点；
- `-fuzz 30%` 容差必要（描边有抗锯齿渐变）；
- 输出里的 `centroid` 列直接给中心点，可省手算。

备选（粗验）：裁剪可疑区域放大肉眼确认再换算坐标：
```bash
convert /tmp/f.png -crop 480x70+0+95 +repage -resize 150% /tmp/zoom.png
```

## 4. 窗口枚举与键盘

```bash
export DISPLAY=:89
xdotool search --onlyvisible --name "." | while read w; do
  echo "$w | $(xdotool getwindowname $w) | $(xdotool getwindowgeometry --shell $w | tr '\n' ' ')"
done
# ForkPlus 对话框有 WM_NAME（如"配置 Git"），可 search --name 精确找
# Chromium about:blank 窗口是浏览器沙箱预览，与被测应用无关，勿点
```

## 4b. 主菜单操作（2026-08-30 菜单复活后实测）

主窗口 1000x600 位于 (100,100)，标题栏含菜单栏：

- **菜单项定位**：文字块扫描（标题栏 y=105-138 区间，深色像素列聚类）：
  文件 x≈127-152（中心139）、视图 x≈189-207、仓库 x≈239-267、窗口 x≈306-384 区域。点击 y=122。
- **打开菜单**：`xdotool mousemove 139 122 click 1` → 等 1.2s 截图。
- **菜单项行定位**：下拉面板出现后（x=0-270，y=148-490），扫描深色文字行得 y 中心，
  行距约 22-28px（分隔线不计）。11 项菜单的行中心实测：151/173/195/218/264/287/309/356/378/约420/约445。
- **点击菜单项**：直接 `xdotool mousemove 150 <行y> click 1`——注意菜单已开时**不要再点菜单标题**（会切回关闭）。
- **判别菜单是否真打开**：菜单面板是纯色 (240,240,240)，主界面背景更杂；同时看进程/对话框变化。
  菜单项命令执行的终极判据：点"退出"后 `pgrep -f ForkPlus` 应无进程。
- **坑**：菜单弹出位置在屏幕左缘 x=0 而非菜单项正下方（Placement 对齐待修），定位菜单面板时从 x=0 开始扫。

## 5. 稳定截图流程

```bash
shot() { DISPLAY=:89 import -window root "/tmp/$1"; }
# 关键节点：启动向导每步、主窗口出现、打开仓库、点提交行、切页签
# 验收标准截图放 verification/，命名 20-xxx.png 顺延编号
```

## 6. 判定标准

- 进程存活：`pgrep -af "net10.0/ForkPlus"`
- 无未处理异常：`grep -c Unhandled /tmp/run_log.txt` 应为 0
- 截图内容与预期 UI 一致（读图确认，注意黑边是 1920×1280 内 Xvfb 画布外的部分，正常）

## 7. 已知坑汇总

1. `xdpyinfo`/`xset` 未安装，别用来探测 X 存活，用 `ps aux | grep Xvfb`。
2. Xvfb 报 "server already running" 但 `ps` 无进程 → 检查 `/tmp/.X11-unix` 目录是否存在、锁文件属主，最省事是换显示号。
3. `import` 必须带 `DISPLAY=:89` 前缀（脚本内环境不继承）。
4. 点"确定"没反应时先重截一张确认对话框还在，再校准坐标（UI 可能有窗口位移）。
5. 测试仓库用 `/tmp/repos/.nvm`（nvm-sh/nvm 完整克隆：2392 提交/106 tag/21 远程分支，比浅克隆能测出更多分支树/性能问题）；检出 v0.40.2 模拟用户场景。
